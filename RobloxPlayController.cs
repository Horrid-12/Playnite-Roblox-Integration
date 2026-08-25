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
    public class RobloxPlayController : PlayController
    {
        private CancellationTokenSource watcherToken;
        private Stopwatch stopwatch;
        private static readonly ILogger logger = LogManager.GetLogger();

        public RobloxPlayController(Game game) : base(game)
        {
            Name = "Play Roblox Experience";
        }

        public override void Dispose()
        {
            watcherToken?.Cancel();
            base.Dispose();
        }

        public override void Play(PlayActionArgs args)
        {
            InvokeOnStarted(new GameStartedEventArgs());

            // Launch Roblox
            Process.Start(new ProcessStartInfo($"roblox://experiences/start?placeId={Game.GameId}") { UseShellExecute = true });

            stopwatch = Stopwatch.StartNew();
            watcherToken = new CancellationTokenSource();

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
