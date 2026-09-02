using Playnite.SDK;
using Playnite.SDK.Models;
using Playnite.SDK.Plugins;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Controls;

namespace RobloxIntegration
{
    public class RobloxIntegration : LibraryPlugin
    {
        private static readonly ILogger logger = LogManager.GetLogger();

        private RobloxIntegrationSettingsViewModel settingsViewModel { get; set; }

        public override Guid Id { get; } = Guid.Parse("0c2dcf0f-511b-43e3-b6db-9c4b331c0dd4");
        public override string Name => "Roblox Integration";
        public override string LibraryIcon => System.IO.Path.Combine(
            System.IO.Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location),
            "Resources", "icon.png");

        public RobloxIntegration(IPlayniteAPI api) : base(api)
        {
            settingsViewModel = new RobloxIntegrationSettingsViewModel(this);
            Properties = new LibraryPluginProperties
            {
                HasSettings = true
            };
        }

        public override ISettings GetSettings(bool firstRunSettings)
        {
            return settingsViewModel;
        }

        public override UserControl GetSettingsView(bool firstRunSettings)
        {
            return new RobloxIntegrationSettingsView();
        }

        public override IEnumerable<PlayController> GetPlayActions(GetPlayActionsArgs args)
        {
            if (args.Game.PluginId != Id)
            {
                yield break;
            }

            yield return new RobloxPlayController(args.Game);
        }

        public override IEnumerable<GameMetadata> GetGames(LibraryGetGamesArgs args)
        {
            var games = new List<GameMetadata>();
            var settings = settingsViewModel.Settings;
            if (settings == null) return games;

            // Run legacy migration on first sync after update
            if (settings.MigrateLegacyIfNeeded())
            {
                logger.Info("Roblox: Legacy settings migrated to multi-account format.");
                SavePluginSettings(settings);
            }

            var enabledAccounts = settings.Accounts?.Where(a => a.IsEnabled).ToList();
            if (enabledAccounts == null || enabledAccounts.Count == 0)
            {
                logger.Info("Roblox: No enabled accounts configured.");
                PlayniteApi.Notifications.Add(new NotificationMessage(
                    "roblox-no-accounts",
                    "Roblox Integration: No accounts configured. Please add an account in plugin settings.",
                    NotificationType.Info));
                return games;
            }

            // Track seen GameIds to de-duplicate across accounts (first-seen wins)
            var seenGameIds = new HashSet<string>();
            bool anyAccountSucceeded = false;

            foreach (var account in enabledAccounts)
            {
                try
                {
                    logger.Info($"Roblox: Processing account '{account.DisplayLabel}' (Mode: {account.ModeLabel})...");
                    var accountGames = GetGamesForAccount(account, seenGameIds);

                    if (accountGames != null)
                    {
                        games.AddRange(accountGames);
                        anyAccountSucceeded = true;
                    }
                }
                catch (Exception ex)
                {
                    logger.Error(ex, $"Roblox: Unexpected error processing account '{account.DisplayLabel}'.");
                    PlayniteApi.Notifications.Add(new NotificationMessage(
                        $"roblox-account-error-{account.Id}",
                        $"Roblox Integration: Error syncing account '{account.DisplayLabel}' — {ex.Message}",
                        NotificationType.Error));
                }
            }

            if (anyAccountSucceeded)
            {
                logger.Info($"Roblox: Successfully imported {games.Count} total game(s) across {enabledAccounts.Count} account(s).");
            }

            // Set Roblox platform icon (once, regardless of account count)
            SetPlatformIcon();

            return games;
        }

        /// <summary>
        /// Fetches games for a single account, validates its session, and returns metadata.
        /// Skips games whose GameId is already in seenGameIds (de-duplication).
        /// </summary>
        private List<GameMetadata> GetGamesForAccount(RobloxAccount account, HashSet<string> seenGameIds)
        {
            var games = new List<GameMetadata>();
            string cookie = account.IsPublicMode ? null : account.RobloSecurityCookie;

            using (var apiClient = new RobloxApiClient(cookie))
            {
                // Step 1: Validate session
                var validation = apiClient.ValidateSession(account);
                account.IsSessionValid = validation.IsValid;
                account.LastValidated = DateTime.Now;

                if (!validation.IsValid)
                {
                    logger.Warn($"Roblox: Account '{account.DisplayLabel}' session invalid — {validation.Message}");
                    PlayniteApi.Notifications.Add(new NotificationMessage(
                        $"roblox-session-{account.Id}",
                        $"Roblox Integration: Account '{account.DisplayLabel}' — {validation.Message}. Please re-authenticate in plugin settings.",
                        NotificationType.Error));
                    return null;
                }

                // Update cached user info from validation
                long userId = validation.ResolvedUserId;
                if (userId > 0)
                {
                    account.RobloxUserId = userId;
                }

                if (!string.IsNullOrEmpty(validation.ResolvedUsername) && string.IsNullOrEmpty(account.DisplayLabel))
                {
                    account.DisplayLabel = validation.ResolvedUsername;
                }

                logger.Info($"Roblox: Account '{account.DisplayLabel}' validated — {validation.Message}");

                // Step 2: Get favorited games
                logger.Info($"Roblox: Fetching favorite games for account '{account.DisplayLabel}'...");
                var favorites = apiClient.GetFavoriteGames(userId);
                if (favorites == null || favorites.Count == 0)
                {
                    logger.Info($"Roblox: No favorite games found for account '{account.DisplayLabel}'.");
                    return games;
                }

                logger.Info($"Roblox: Found {favorites.Count} favorite game(s) for account '{account.DisplayLabel}'.");

                // Step 3: Get thumbnails
                var universeIds = favorites.Select(f => f.UniverseId).Distinct().ToList();
                Dictionary<long, string> thumbnails = new Dictionary<long, string>();
                try
                {
                    logger.Info($"Roblox: Fetching game thumbnails for account '{account.DisplayLabel}'...");
                    thumbnails = apiClient.GetGameThumbnails(universeIds);
                }
                catch (Exception ex)
                {
                    logger.Error(ex, $"Roblox: Failed to fetch thumbnails for account '{account.DisplayLabel}'.");
                }

                // Step 4: Build GameMetadata objects (de-duplicating by GameId)
                foreach (var fav in favorites)
                {
                    var gameId = fav.RootPlaceId.ToString();

                    // De-duplicate: skip if we already have this game from another account
                    if (seenGameIds.Contains(gameId))
                    {
                        continue;
                    }

                    seenGameIds.Add(gameId);

                    var metadata = new GameMetadata
                    {
                        Name = fav.Name,
                        GameId = gameId,
                        IsInstalled = true,
                        Platforms = new HashSet<MetadataProperty>
                        {
                            new MetadataNameProperty("Roblox")
                        },
                        Source = new MetadataNameProperty("Roblox"),
                        Description = fav.Description,
                        InstallDirectory = ""
                    };

                    // Add thumbnail as icon if available
                    if (thumbnails.ContainsKey(fav.UniverseId))
                    {
                        metadata.Icon = new MetadataFile(thumbnails[fav.UniverseId]);
                    }

                    games.Add(metadata);
                }

                logger.Info($"Roblox: Imported {games.Count} new game(s) from account '{account.DisplayLabel}'.");
            }

            return games;
        }

        /// <summary>
        /// Sets the Roblox platform icon in Playnite's database.
        /// </summary>
        private void SetPlatformIcon()
        {
            try
            {
                var platform = PlayniteApi.Database.Platforms.FirstOrDefault(
                    p => p.Name.Equals("Roblox", System.StringComparison.OrdinalIgnoreCase));
                if (platform != null)
                {
                    var localIconPath = System.IO.Path.Combine(
                        System.IO.Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location),
                        "Resources", "platform_icon.png");
                    if (System.IO.File.Exists(localIconPath))
                    {
                        logger.Info("Roblox: Setting platform icon for Roblox...");
                        var dbIconPath = PlayniteApi.Database.AddFile(localIconPath, platform.Id);
                        platform.Icon = dbIconPath;

                        // Reset Cover and Background to null to remove the massive blue banner effect
                        platform.Cover = null;
                        platform.Background = null;

                        PlayniteApi.Database.Platforms.Update(platform);
                    }
                }
            }
            catch (Exception ex)
            {
                logger.Error(ex, "Roblox: Failed to set platform icon programmatically.");
            }
        }
    }
}
