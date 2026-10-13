using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Playnite.SDK;
using Playnite.SDK.Plugins;
using Playnite.SDK.Models;

namespace RobloxIntegration
{
    /// <summary>
    /// The different ways a Roblox experience can be launched from Playnite.
    /// </summary>
    public enum RobloxLaunchMode
    {
        /// <summary>Standard launch of the game's own place.</summary>
        Default,
        /// <summary>Join a private server via invite link / link code / access code.</summary>
        PrivateServer,
        /// <summary>Join a specific server instance via its Job ID.</summary>
        ServerInstance,
        /// <summary>Join the experience and server a friend is currently playing.</summary>
        FollowFriend
    }

    public class RobloxPlayController : PlayController
    {
        private CancellationTokenSource watcherToken;
        private Stopwatch stopwatch;
        private static readonly ILogger logger = LogManager.GetLogger();

        private readonly RobloxIntegration plugin;
        private readonly RobloxLaunchMode mode;

        public RobloxPlayController(Game game, RobloxIntegration plugin, RobloxLaunchMode mode) : base(game)
        {
            this.plugin = plugin;
            this.mode = mode;
            Name = GetActionName(mode);
        }

        /// <summary>
        /// Menu label shown next to the game's Play button for the given mode.
        /// </summary>
        public static string GetActionName(RobloxLaunchMode mode)
        {
            switch (mode)
            {
                case RobloxLaunchMode.PrivateServer:
                    return "Join Private Server";
                case RobloxLaunchMode.ServerInstance:
                    return "Join Specific Server";
                case RobloxLaunchMode.FollowFriend:
                    return "Join Friend's Activity";
                default:
                    return "Play Roblox Experience";
            }
        }

        public override void Dispose()
        {
            watcherToken?.Cancel();
            base.Dispose();
        }

        public override void Play(PlayActionArgs args)
        {
            InvokeOnStarted(new GameStartedEventArgs());

            // Work out where to launch (may prompt the user for details).
            string launchUrl;
            try
            {
                launchUrl = BuildLaunchUrl();
            }
            catch (Exception ex)
            {
                logger.Error(ex, "Roblox: Failed to build launch URL.");
                ShowError("Failed to prepare the Roblox launch: " + ex.Message);
                launchUrl = null;
            }

            // User cancelled the prompt (or preparation failed).
            if (string.IsNullOrEmpty(launchUrl))
            {
                InvokeOnStopped(new GameStoppedEventArgs(0));
                return;
            }

            logger.Info($"Roblox: Launching ({GetActionName(mode)}) → {launchUrl}");
            try
            {
                Process.Start(new ProcessStartInfo(launchUrl) { UseShellExecute = true });
            }
            catch (Exception ex)
            {
                logger.Error(ex, $"Roblox: Failed to start \"{launchUrl}\".");
                ShowError("Failed to launch Roblox: " + ex.Message);
                InvokeOnStopped(new GameStoppedEventArgs(0));
                return;
            }

            stopwatch = Stopwatch.StartNew();
            watcherToken = new CancellationTokenSource();
            TrackGameSession();
        }

        // ──────────────────────────────────────────────
        //  Launch URL construction
        // ──────────────────────────────────────────────

        private string BuildLaunchUrl()
        {
            var placeId = Game.GameId;
            if (string.IsNullOrEmpty(placeId))
            {
                throw new InvalidOperationException("This game has no Roblox place ID.");
            }

            if (mode == RobloxLaunchMode.Default)
            {
                return BuildUrl(placeId, null, null);
            }

            var dialog = new LaunchOptionsDialog(mode, placeId, plugin);
            var confirmed = ShowDialogOnUIThread(dialog);
            if (confirmed != true)
            {
                return null;
            }

            return BuildUrl(dialog.ResultPlaceId, dialog.ResultParamName, dialog.ResultParamValue);
        }

        private static string BuildUrl(string placeId, string paramName, string paramValue)
        {
            var url = $"roblox://experiences/start?placeId={Uri.EscapeDataString(placeId ?? string.Empty)}";
            if (!string.IsNullOrEmpty(paramName) && !string.IsNullOrEmpty(paramValue))
            {
                url += $"&{paramName}={Uri.EscapeDataString(paramValue)}";
            }
            return url;
        }

        private bool? ShowDialogOnUIThread(LaunchOptionsDialog dialog)
        {
            var app = System.Windows.Application.Current;
            if (app == null || app.Dispatcher == null || app.Dispatcher.CheckAccess())
            {
                SetOwner(dialog, app);
                return dialog.ShowDialog();
            }

            return app.Dispatcher.Invoke(() =>
            {
                SetOwner(dialog, app);
                return dialog.ShowDialog();
            });
        }

        private static void SetOwner(LaunchOptionsDialog dialog, System.Windows.Application app)
        {
            try
            {
                var mainWindow = app?.MainWindow;
                if (mainWindow != null && mainWindow.IsLoaded && mainWindow.IsVisible)
                {
                    dialog.Owner = mainWindow;
                }
            }
            catch
            {
                // Ownership is cosmetic — never fail a launch over it.
            }
        }

        private void ShowError(string message)
        {
            try
            {
                plugin?.PlayniteApi?.Dialogs?.ShowErrorMessage(message, "Roblox Integration");
            }
            catch (Exception ex)
            {
                logger.Warn(ex, "Roblox: Failed to show error dialog.");
            }
        }

        // ──────────────────────────────────────────────
        //  Session / playtime tracking
        // ──────────────────────────────────────────────

        private void TrackGameSession()
        {
            Task.Run(async () =>
            {
                try
                {
                    // Wait up to 60 seconds for the Roblox process to appear
                    bool processFound = false;
                    for (int i = 0; i < 30; i++)
                    {
                        if (watcherToken.IsCancellationRequested) return;

                        if (Process.GetProcessesByName("RobloxPlayerBeta").Any())
                        {
                            processFound = true;
                            break;
                        }
                        await Task.Delay(2000);
                    }

                    if (!processFound)
                    {
                        InvokeOnStopped(new GameStoppedEventArgs(0));
                        return;
                    }

                    // Find the latest Roblox Player log file to monitor
                    var logDir = Path.Combine(
                        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                        "Roblox", "logs");

                    // Give Roblox a moment to create/write its log file
                    await Task.Delay(3000);

                    // Monitor for the game leave signal by tailing the log files
                    while (!watcherToken.IsCancellationRequested)
                    {
                        // Check if the process has fully exited (user closed Roblox entirely)
                        if (!Process.GetProcessesByName("RobloxPlayerBeta").Any())
                        {
                            logger.Info("RobloxPlayerBeta process exited.");
                            stopwatch.Stop();
                            InvokeOnStopped(new GameStoppedEventArgs(Convert.ToUInt64(stopwatch.Elapsed.TotalSeconds)));
                            return;
                        }

                        // Check the latest log file for the leave signal
                        if (Directory.Exists(logDir))
                        {
                            var latestLog = new DirectoryInfo(logDir)
                                .GetFiles("*_Player_*.log")
                                .OrderByDescending(f => f.LastWriteTime)
                                .FirstOrDefault();

                            if (latestLog != null)
                            {
                                try
                                {
                                    // Read the log file (shared read access since Roblox is writing to it)
                                    using (var fs = new FileStream(latestLog.FullName, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                                    using (var reader = new StreamReader(fs))
                                    {
                                        var content = reader.ReadToEnd();

                                        // Check if the game session ended
                                        // "leaveUGCGameInternal" is logged when leaving a game experience
                                        if (content.Contains("leaveUGCGameInternal"))
                                        {
                                            // Make sure we also saw a join first (to avoid false positives from old logs)
                                            if (content.Contains("Joining game"))
                                            {
                                                // Find the last join and last leave timestamps
                                                var lastJoinIdx = content.LastIndexOf("Joining game");
                                                var lastLeaveIdx = content.LastIndexOf("leaveUGCGameInternal");

                                                // Only stop if the leave happened AFTER the last join
                                                if (lastLeaveIdx > lastJoinIdx)
                                                {
                                                    logger.Info("Detected game leave via Roblox log (leaveUGCGameInternal).");
                                                    stopwatch.Stop();
                                                    InvokeOnStopped(new GameStoppedEventArgs(Convert.ToUInt64(stopwatch.Elapsed.TotalSeconds)));
                                                    return;
                                                }
                                            }
                                        }
                                    }
                                }
                                catch (Exception ex)
                                {
                                    logger.Warn(ex, "Failed to read Roblox log file.");
                                }
                            }
                        }

                        await Task.Delay(3000); // Check every 3 seconds
                    }
                }
                catch (Exception ex)
                {
                    logger.Error(ex, "Error in Roblox play tracking.");
                    stopwatch?.Stop();
                    InvokeOnStopped(new GameStoppedEventArgs(
                        stopwatch != null ? Convert.ToUInt64(stopwatch.Elapsed.TotalSeconds) : 0));
                }
            });
        }
    }
}
