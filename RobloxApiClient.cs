using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Playnite.SDK;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;

namespace RobloxIntegration
{
    public class RobloxApiClient : IDisposable
    {
        private static readonly ILogger logger = LogManager.GetLogger();
        private readonly HttpClient client;

        public RobloxApiClient(string robloSecurityCookie)
        {
            var handler = new HttpClientHandler();
            if (!string.IsNullOrEmpty(robloSecurityCookie))
            {
                var cookieContainer = new CookieContainer();
                cookieContainer.Add(new Uri("https://roblox.com"), new Cookie(".ROBLOSECURITY", robloSecurityCookie));
                handler.CookieContainer = cookieContainer;
            }
            client = new HttpClient(handler);
            client.DefaultRequestHeaders.Add("Accept", "application/json");
        }

        /// <summary>
        /// Resolves a Roblox username to a User ID.
        /// </summary>
        public long GetUserIdFromUsername(string username)
        {
            try
            {
                var payload = new JObject
                {
                    ["usernames"] = new JArray(username),
                    ["excludeBannedUsers"] = true
                };

                var content = new StringContent(payload.ToString(), System.Text.Encoding.UTF8, "application/json");
                var response = client.PostAsync("https://users.roblox.com/v1/usernames/users", content).GetAwaiter().GetResult();
                
                if (response.IsSuccessStatusCode)
                {
                    var responseBody = response.Content.ReadAsStringAsync().GetAwaiter().GetResult();
                    var json = JObject.Parse(responseBody);
                    var data = json["data"];
                    if (data != null && data.HasValues)
                    {
                        return data[0]["id"]?.Value<long>() ?? 0;
                    }
                }
            }
            catch (Exception ex)
            {
                logger.Error(ex, $"Failed to resolve username '{username}' to user ID.");
            }
            return 0;
        }

        /// <summary>
        /// Gets the authenticated user's ID and username.
        /// </summary>
        public AuthenticatedUser GetAuthenticatedUser()
        {
            try
            {
                var response = client.GetStringAsync("https://users.roblox.com/v1/users/authenticated").GetAwaiter().GetResult();
                var json = JObject.Parse(response);
                return new AuthenticatedUser
                {
                    UserId = json["id"].Value<long>(),
                    Username = json["name"]?.Value<string>() ?? json["displayName"]?.Value<string>() ?? "Unknown"
                };
            }
            catch (Exception ex)
            {
                logger.Error(ex, "Failed to get authenticated Roblox user.");
                return null;
            }
        }

        /// <summary>
        /// Gets the user's favorited games (experiences) using the V2 Favorite Games API.
        /// Returns detailed game structures including universe IDs, names, and root place IDs.
        /// </summary>
        public List<FavoriteGameV2> GetFavoriteGames(long userId)
        {
            var favorites = new List<FavoriteGameV2>();
            string cursor = null;

            try
            {
                do
                {
                    var url = $"https://games.roblox.com/v2/users/{userId}/favorite/games?limit=50";
                    if (!string.IsNullOrEmpty(cursor))
                    {
                        url += $"&cursor={cursor}";
                    }

                    var response = client.GetStringAsync(url).GetAwaiter().GetResult();
                    var json = JObject.Parse(response);
                    var dataArray = json["data"];

                    if (dataArray != null && dataArray.HasValues)
                    {
                        foreach (var item in dataArray)
                        {
                            var fav = new FavoriteGameV2
                            {
                                UniverseId = item["id"]?.Value<long>() ?? 0,
                                Name = item["name"]?.Value<string>() ?? "Unknown Game",
                                Description = item["description"]?.Value<string>() ?? "",
                                RootPlaceId = item["rootPlace"]?["id"]?.Value<long>() ?? 0
                            };
                            
                            if (fav.UniverseId > 0 && fav.RootPlaceId > 0)
                            {
                                favorites.Add(fav);
                            }
                        }
                    }

                    cursor = json["nextPageCursor"]?.Value<string>();
                }
                while (!string.IsNullOrEmpty(cursor));
            }
            catch (Exception ex)
            {
                logger.Error(ex, $"Failed to fetch favorite games for user ID {userId}");
            }

            return favorites;
        }

        /// <summary>
        /// Gets universe IDs for given place IDs using the multiget-place-details endpoint.
        /// </summary>
        public Dictionary<long, PlaceDetails> GetPlaceDetails(IEnumerable<long> placeIds)
        {
            var result = new Dictionary<long, PlaceDetails>();
            var idList = placeIds.ToList();

            // Process in batches of 50
            for (int i = 0; i < idList.Count; i += 50)
            {
                var batch = idList.Skip(i).Take(50);
                var idsParam = string.Join(",", batch);

                try
                {
                    var url = $"https://games.roblox.com/v1/games/multiget-place-details?placeIds={idsParam}";
                    var response = client.GetStringAsync(url).GetAwaiter().GetResult();
                    var items = JArray.Parse(response);

                    foreach (var item in items)
                    {
                        var placeId = item["placeId"]?.Value<long>() ?? 0;
                        if (placeId > 0)
                        {
                            result[placeId] = new PlaceDetails
                            {
                                PlaceId = placeId,
                                UniverseId = item["universeId"]?.Value<long>() ?? 0,
                                Name = item["name"]?.Value<string>() ?? "Unknown",
                                Description = item["description"]?.Value<string>() ?? "",
                                SourceName = item["sourceName"]?.Value<string>() ?? ""
                            };
                        }
                    }
                }
                catch (Exception ex)
                {
                    logger.Error(ex, $"Failed to get place details for batch starting at index {i}.");
                }
            }

            return result;
        }

        /// <summary>
        /// Gets game details for given universe IDs.
        /// </summary>
        public Dictionary<long, GameDetails> GetGameDetails(IEnumerable<long> universeIds)
        {
            var result = new Dictionary<long, GameDetails>();
            var idList = universeIds.Where(id => id > 0).ToList();

            for (int i = 0; i < idList.Count; i += 50)
            {
                var batch = idList.Skip(i).Take(50);
                var idsParam = string.Join(",", batch);

                try
                {
                    var url = $"https://games.roblox.com/v1/games?universeIds={idsParam}";
                    var response = client.GetStringAsync(url).GetAwaiter().GetResult();
                    var json = JObject.Parse(response);
                    var dataArray = json["data"];

                    if (dataArray != null)
                    {
                        foreach (var item in dataArray)
                        {
                            var universeId = item["id"]?.Value<long>() ?? 0;
                            if (universeId > 0)
                            {
                                result[universeId] = new GameDetails
                                {
                                    UniverseId = universeId,
                                    Name = item["name"]?.Value<string>() ?? "Unknown",
                                    Description = item["description"]?.Value<string>() ?? "",
                                    Creator = item["creator"]?["name"]?.Value<string>() ?? "",
                                    RootPlaceId = item["rootPlaceId"]?.Value<long>() ?? 0
                                };
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    logger.Error(ex, $"Failed to get game details for batch starting at index {i}.");
                }
            }

            return result;
        }

        /// <summary>
        /// Gets game icon thumbnail URLs for given universe IDs.
        /// </summary>
        public Dictionary<long, string> GetGameThumbnails(IEnumerable<long> universeIds)
        {
            var result = new Dictionary<long, string>();
            var idList = universeIds.Where(id => id > 0).ToList();

            for (int i = 0; i < idList.Count; i += 50)
            {
                var batch = idList.Skip(i).Take(50);
                var idsParam = string.Join(",", batch);

                try
                {
                    var url = $"https://thumbnails.roblox.com/v1/games/icons?universeIds={idsParam}&size=512x512&format=Png&isCircular=false";
                    var response = client.GetStringAsync(url).GetAwaiter().GetResult();
                    var json = JObject.Parse(response);
                    var dataArray = json["data"];

                    if (dataArray != null)
                    {
                        foreach (var item in dataArray)
                        {
                            var targetId = item["targetId"]?.Value<long>() ?? 0;
                            var imageUrl = item["imageUrl"]?.Value<string>();
                            var state = item["state"]?.Value<string>();

                            if (targetId > 0 && !string.IsNullOrEmpty(imageUrl) && state == "Completed")
                            {
                                result[targetId] = imageUrl;
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    logger.Error(ex, $"Failed to get thumbnails for batch starting at index {i}.");
                }
            }

            return result;
        }

        /// <summary>
        /// Gets the friend list of the specified user (paginated, up to 500 friends).
        /// Requires a cookie-authenticated client.
        /// </summary>
        public List<RobloxFriend> GetFriends(long userId)
        {
            var friends = new List<RobloxFriend>();
            if (userId <= 0) return friends;

            string cursor = null;
            try
            {
                do
                {
                    var url = $"https://friends.roblox.com/v1/users/{userId}/friends?limit=100";
                    if (!string.IsNullOrEmpty(cursor))
                    {
                        url += $"&cursor={Uri.EscapeDataString(cursor)}";
                    }

                    var response = client.GetStringAsync(url).GetAwaiter().GetResult();
                    var json = JObject.Parse(response);
                    var data = json["data"];

                    if (data != null && data.HasValues)
                    {
                        foreach (var item in data)
                        {
                            // Items are normally flat user objects; tolerate a nested "user" wrapper too.
                            var user = item.Type == JTokenType.Object && item["user"] != null ? item["user"] : item;
                            var id = user["id"]?.Value<long>() ?? 0;
                            if (id <= 0) continue;

                            friends.Add(new RobloxFriend
                            {
                                Id = id,
                                Name = user["name"]?.Value<string>() ?? "",
                                DisplayName = user["displayName"]?.Value<string>() ??
                                              user["name"]?.Value<string>() ?? ""
                            });
                        }
                    }

                    cursor = json["nextPageCursor"]?.Value<string>();
                }
                while (!string.IsNullOrEmpty(cursor));
            }
            catch (Exception ex)
            {
                logger.Error(ex, $"Failed to fetch friends for user ID {userId}");
            }

            return friends;
        }

        /// <summary>
        /// Gets presence (offline / online / in-game + current location) for a batch of users.
        /// Requires a cookie-authenticated client. Requests are batched 100 IDs at a time.
        /// </summary>
        public Dictionary<long, UserPresenceInfo> GetPresences(IEnumerable<long> userIds)
        {
            var result = new Dictionary<long, UserPresenceInfo>();
            var ids = userIds?.Where(id => id > 0).Distinct().ToList() ?? new List<long>();

            for (int i = 0; i < ids.Count; i += 100)
            {
                try
                {
                    var batch = ids.Skip(i).Take(100).ToList();
                    var payload = new JObject { ["userIds"] = new JArray(batch) };
                    var content = new StringContent(payload.ToString(), System.Text.Encoding.UTF8, "application/json");

                    var response = client.PostAsync("https://presence.roblox.com/v1/presence/users", content)
                        .GetAwaiter().GetResult();

                    if (!response.IsSuccessStatusCode)
                    {
                        logger.Warn($"Roblox: Presence request failed with status {(int)response.StatusCode}.");
                        continue;
                    }

                    var body = response.Content.ReadAsStringAsync().GetAwaiter().GetResult();
                    var presences = JObject.Parse(body)["userPresences"];
                    if (presences == null) continue;

                    foreach (var item in presences)
                    {
                        var info = new UserPresenceInfo
                        {
                            UserId = item["userId"]?.Value<long>() ?? 0,
                            // 0 = Offline, 1 = Online, 2 = InGame, 3 = InStudio, 4 = Invisible
                            PresenceType = item["userPresenceType"]?.Value<int>() ?? 0,
                            LastLocation = item["lastLocation"]?.Value<string>() ?? "",
                            PlaceId = item["placeId"]?.Value<long>() ?? 0,
                            RootPlaceId = item["rootPlaceId"]?.Value<long>() ?? 0,
                            GameId = item["gameId"]?.Value<string>() ?? "",
                            UniverseId = item["universeId"]?.Value<long>() ?? 0
                        };

                        if (info.UserId > 0)
                        {
                            result[info.UserId] = info;
                        }
                    }
                }
                catch (Exception ex)
                {
                    logger.Error(ex, "Failed to fetch Roblox presence batch.");
                }
            }

            return result;
        }

        /// <summary>
        /// Validates whether the given account's session/credentials are still valid.
        /// For public mode, resolves the username. For cookie mode, calls the authenticated endpoint.
        /// </summary>
        public SessionValidationResult ValidateSession(RobloxAccount account)
        {
            try
            {
                if (account.IsPublicMode)
                {
                    if (string.IsNullOrEmpty(account.RobloxUsername))
                    {
                        return new SessionValidationResult
                        {
                            IsValid = false,
                            Message = "No username configured."
                        };
                    }

                    var userId = GetUserIdFromUsername(account.RobloxUsername);
                    return new SessionValidationResult
                    {
                        IsValid = userId > 0,
                        ResolvedUserId = userId,
                        ResolvedUsername = account.RobloxUsername,
                        Message = userId > 0
                            ? $"Username verified (ID: {userId})"
                            : "Username not found on Roblox."
                    };
                }
                else
                {
                    if (string.IsNullOrEmpty(account.RobloSecurityCookie))
                    {
                        return new SessionValidationResult
                        {
                            IsValid = false,
                            Message = "No cookie configured."
                        };
                    }

                    var user = GetAuthenticatedUser();
                    return new SessionValidationResult
                    {
                        IsValid = user != null,
                        ResolvedUserId = user?.UserId ?? 0,
                        ResolvedUsername = user?.Username,
                        Message = user != null
                            ? $"Authenticated as {user.Username} (ID: {user.UserId})"
                            : "Cookie expired or invalid."
                    };
                }
            }
            catch (Exception ex)
            {
                logger.Error(ex, $"Session validation failed for account '{account.DisplayLabel}'.");
                return new SessionValidationResult
                {
                    IsValid = false,
                    Message = $"Validation error: {ex.Message}"
                };
            }
        }

        public void Dispose()
        {
            client?.Dispose();
        }
    }

    // Data models
    public class AuthenticatedUser
    {
        public long UserId { get; set; }
        public string Username { get; set; }
    }

    public class FavoriteGameV2
    {
        public long UniverseId { get; set; }
        public string Name { get; set; }
        public string Description { get; set; }
        public long RootPlaceId { get; set; }
    }

    public class PlaceDetails
    {
        public long PlaceId { get; set; }
        public long UniverseId { get; set; }
        public string Name { get; set; }
        public string Description { get; set; }
        public string SourceName { get; set; }
    }

    public class GameDetails
    {
        public long UniverseId { get; set; }
        public string Name { get; set; }
        public string Description { get; set; }
        public string Creator { get; set; }
        public long RootPlaceId { get; set; }
    }

    public class SessionValidationResult
    {
        public bool IsValid { get; set; }
        public long ResolvedUserId { get; set; }
        public string ResolvedUsername { get; set; }
        public string Message { get; set; }
    }

    /// <summary>
    /// A single entry from a user's Roblox friend list.
    /// </summary>
    public class RobloxFriend
    {
        public long Id { get; set; }
        public string Name { get; set; }
        public string DisplayName { get; set; }
    }

    /// <summary>
    /// Roblox presence for a single user (what they are doing right now).
    /// </summary>
    public class UserPresenceInfo
    {
        public long UserId { get; set; }

        /// <summary>
        /// 0 = Offline, 1 = Online, 2 = InGame, 3 = InStudio, 4 = Invisible.
        /// </summary>
        public int PresenceType { get; set; }

        /// <summary>
        /// Human readable location, e.g. the experience name when in-game.
        /// </summary>
        public string LastLocation { get; set; }

        /// <summary>
        /// The place the user is currently in (0 when not in-game).
        /// </summary>
        public long PlaceId { get; set; }

        public long RootPlaceId { get; set; }

        /// <summary>
        /// The server instance (DataModel.JobId) the user is in, if known.
        /// </summary>
        public string GameId { get; set; }

        public long UniverseId { get; set; }

        [Newtonsoft.Json.JsonIgnore]
        public bool IsInGame => PresenceType == 2;

        [Newtonsoft.Json.JsonIgnore]
        public bool IsOnline => PresenceType == 1 || PresenceType == 2 || PresenceType == 3;
    }
}
