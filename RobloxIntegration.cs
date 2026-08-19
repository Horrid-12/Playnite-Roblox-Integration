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

        public override IEnumerable<GameMetadata> GetGames(LibraryGetGamesArgs args)
        {
            var games = new List<GameMetadata>();

            var settings = settingsViewModel.Settings;
            if (settings == null) return games;

            long userId = 0;
            string cookie = null;

            if (settings.UsePublicFavorites)
            {
                if (string.IsNullOrEmpty(settings.RobloxUsername))
                {
                    logger.Warn("Roblox: Public mode enabled but no username set.");
                    return games;
                }
            }
            else
            {
                cookie = settings.RobloSecurityCookie;
                if (string.IsNullOrEmpty(cookie))
                {
                    logger.Warn("Roblox: Private mode enabled but no cookie set.");
                    PlayniteApi.Notifications.Add(new NotificationMessage(
                        "roblox-not-auth",
                        "Roblox Integration: Not authenticated. Please configure your cookie in plugin settings.",
                        NotificationType.Info));
                    return games;
                }
            }

            try
            {
                using (var apiClient = new RobloxApiClient(cookie))
                {
                    if (settings.UsePublicFavorites)
                    {
                        // Check if we already have the resolved user ID
                        if (settings.RobloxUserId > 0)
                        {
                            userId = settings.RobloxUserId;
                        }
                        else
                        {
                            logger.Info($"Roblox: Resolving user ID for '{settings.RobloxUsername}'...");
                            userId = apiClient.GetUserIdFromUsername(settings.RobloxUsername);
                            if (userId > 0)
                            {
                                settings.RobloxUserId = userId;
                                SavePluginSettings(settings);
                            }
                        }

                        if (userId == 0)
                        {
                            logger.Error($"Roblox: Could not resolve username '{settings.RobloxUsername}'");
                            return games;
                        }
                        logger.Info($"Roblox: Using public profile for ID {userId}");
                    }
                    else
                    {
                        // Step 1: Get authenticated user via cookie
                        logger.Info("Roblox: Getting authenticated user...");
                        var user = apiClient.GetAuthenticatedUser();
                        if (user == null)
                        {
                            logger.Error("Roblox: Failed to authenticate. Cookie may be expired.");
                            PlayniteApi.Notifications.Add(new NotificationMessage(
                                "roblox-auth-fail",
                                "Roblox Integration: Authentication failed. Your cookie may be expired. Please update it in plugin settings.",
                                NotificationType.Error));
                            return games;
                        }
                        userId = user.UserId;
                        logger.Info($"Roblox: Authenticated as {user.Username} (ID: {userId})");
                    }

                    // Step 2: Get favorited games
                    logger.Info("Roblox: Fetching favorite games...");
                    var favorites = apiClient.GetFavoriteGames(userId);
                    if (favorites == null || favorites.Count == 0)
                    {
                        logger.Info("Roblox: No favorite games found.");
                        return games;
                    }

                    logger.Info($"Roblox: Found {favorites.Count} favorite game(s).");

                    // Step 3: Get thumbnails
                    var universeIds = favorites.Select(f => f.UniverseId).Distinct().ToList();
                    Dictionary<long, string> thumbnails = new Dictionary<long, string>();
                    try
                    {
                        logger.Info("Roblox: Fetching game thumbnails...");
                        thumbnails = apiClient.GetGameThumbnails(universeIds);
                    }
                    catch (Exception ex)
                    {
                        logger.Error(ex, "Roblox: Failed to fetch thumbnails.");
                    }

                    // Step 4: Build GameMetadata objects
                    foreach (var fav in favorites)
                    {
                        var metadata = new GameMetadata
                        {
                            Name = fav.Name,
                            GameId = fav.RootPlaceId.ToString(),
                            IsInstalled = true,
                            Platforms = new HashSet<MetadataProperty>
                            {
                                new MetadataNameProperty("Roblox")
                            },
                            Source = new MetadataNameProperty("Roblox"),
                            Description = fav.Description,
                            GameActions = new List<GameAction>
                            {
                                new GameAction
                                {
                                    Name = "Launch in Roblox",
                                    Type = GameActionType.URL,
                                    Path = $"roblox://experiences/start?placeId={fav.RootPlaceId}",
                                    IsPlayAction = true
                                }
                            }
                        };

                        // Add thumbnail as icon if available
                        if (thumbnails.ContainsKey(fav.UniverseId))
                        {
                            metadata.Icon = new MetadataFile(thumbnails[fav.UniverseId]);
                        }

                        games.Add(metadata);
                    }

                    logger.Info($"Roblox: Successfully imported {games.Count} game(s).");

                    // Set Roblox platform icon programmatically
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
            catch (Exception ex)
            {
                logger.Error(ex, "Roblox: Unexpected error during game import.");
                PlayniteApi.Notifications.Add(new NotificationMessage(
                    "roblox-import-error",
                    $"Roblox Integration: Error importing games - {ex.Message}",
                    NotificationType.Error));
            }

            return games;
        }
    }
}
