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
}
