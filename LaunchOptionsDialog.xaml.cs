using Playnite.SDK;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;

namespace RobloxIntegration
{
    /// <summary>
    /// Modal prompt shown when launching one of the custom Roblox play actions.
    /// Collects the parameters needed for the launch and exposes them to the play controller.
    /// </summary>
    public partial class LaunchOptionsDialog : Window
    {
        private static readonly ILogger logger = LogManager.GetLogger();

        private readonly RobloxLaunchMode mode;
        private readonly string defaultPlaceId;
        private readonly RobloxIntegration plugin;

        /// <summary>Place ID to launch into (defaults to the game's own place).</summary>
        public string ResultPlaceId { get; private set; }

        /// <summary>Extra deep link parameter name (linkCode / accessCode / gameInstanceId / userId), or null.</summary>
        public string ResultParamName { get; private set; }

        /// <summary>Value for <see cref="ResultParamName"/>.</summary>
        public string ResultParamValue { get; private set; }

        private List<FriendEntry> friendEntries = new List<FriendEntry>();

        public LaunchOptionsDialog(RobloxLaunchMode mode, string defaultPlaceId, RobloxIntegration plugin)
        {
            InitializeComponent();

            this.mode = mode;
            this.defaultPlaceId = defaultPlaceId;
            this.plugin = plugin;
            ResultPlaceId = defaultPlaceId;

            switch (mode)
            {
                case RobloxLaunchMode.PrivateServer:
                    Title = "Join Private Server";
                    PromptText.Text = "Join a private server";
                    DetailText.Text = "Paste a private server invite link (roblox.com/share?...), a link code, " +
                                      "or a private server access code.";
                    TextInputPanel.Visibility = Visibility.Visible;
                    CodeTypeLabel.Visibility = Visibility.Visible;
                    CodeTypeBox.Visibility = Visibility.Visible;
                    InputBox.Focus();
                    break;

                case RobloxLaunchMode.ServerInstance:
                    Title = "Join Specific Server";
                    PromptText.Text = "Join a specific server instance";
                    DetailText.Text = "Paste the server instance ID (Job ID) of the server you want to join. " +
                                      "It looks like: 6a62d499-b69c-4300-ac3f-ad2f395f4097";
                    TextInputPanel.Visibility = Visibility.Visible;
                    InputBox.Focus();
                    break;

                case RobloxLaunchMode.FollowFriend:
                    Title = "Join Friend's Activity";
                    PromptText.Text = "Join a friend's current activity";
                    DetailText.Text = "Pick a friend and jump straight into the experience and server they are " +
                                      "playing right now.";
                    FriendPanel.Visibility = Visibility.Visible;
                    Loaded += (s, e) => LoadFriends();
                    RefreshButton.Click += (s, e) => LoadFriends();
                    break;
            }
        }

        // ──────────────────────────────────────────────
        //  OK / validation
        // ──────────────────────────────────────────────

        private void PlayButton_Click(object sender, RoutedEventArgs e)
        {
            StatusText.Text = string.Empty;

            bool valid;
            switch (mode)
            {
                case RobloxLaunchMode.PrivateServer:
                    valid = ValidatePrivateServer();
                    break;
                case RobloxLaunchMode.ServerInstance:
                    valid = ValidateServerInstance();
                    break;
                case RobloxLaunchMode.FollowFriend:
                    valid = ValidateFollowFriend();
                    break;
                default:
                    valid = false;
                    break;
            }

            if (valid)
            {
                DialogResult = true;
            }
        }

        private bool ValidatePrivateServer()
        {
            var input = InputBox.Text.Trim();
            if (input.Length == 0)
            {
                StatusText.Text = "Enter an invite link, link code, or access code.";
                return false;
            }

            if (IsLikelyUrl(input))
            {
                Dictionary<string, string> query;
                if (!TryGetQueryParams(input, out query))
                {
                    StatusText.Text = "That doesn't look like a link containing a private server code.";
                    return false;
                }

                // Share links use ?code=..., explicit links use ?linkCode=...
                string code;
                if (TryGet(query, "code", out code) || TryGet(query, "linkcode", out code))
                {
                    ResultParamName = "linkCode";
                    ResultParamValue = code;
                }
                else if (TryGet(query, "accesscode", out code))
                {
                    ResultParamName = "accessCode";
                    ResultParamValue = code;
                }
                else if (TryGet(query, "reservedserveraccesscode", out code))
                {
                    ResultParamName = "reservedServerAccessCode";
                    ResultParamValue = code;
                }
                else
                {
                    StatusText.Text = "That link doesn't contain a private server code.";
                    return false;
                }

                string linkPlaceId;
                if (TryGet(query, "placeid", out linkPlaceId) && linkPlaceId.Length > 0)
                {
                    ResultPlaceId = linkPlaceId;
                }
                return true;
            }

            bool isAccessCode = CodeTypeBox.SelectedIndex == 1;
            ResultPlaceId = defaultPlaceId;
            ResultParamName = isAccessCode ? "accessCode" : "linkCode";
            ResultParamValue = input;
            return true;
        }

        private bool ValidateServerInstance()
        {
            var input = InputBox.Text.Trim();
            if (input.Length == 0)
            {
                StatusText.Text = "Enter the server instance ID (Job ID).";
                return false;
            }

            if (IsLikelyUrl(input))
            {
                Dictionary<string, string> query;
                string instanceId;
                if (!TryGetQueryParams(input, out query) ||
                    !TryGet(query, "gameinstanceid", out instanceId) ||
                    instanceId.Length == 0)
                {
                    StatusText.Text = "That link doesn't contain a server instance ID (gameInstanceId).";
                    return false;
                }

                ResultParamName = "gameInstanceId";
                ResultParamValue = instanceId;

                string linkPlaceId;
                if (TryGet(query, "placeid", out linkPlaceId) && linkPlaceId.Length > 0)
                {
                    ResultPlaceId = linkPlaceId;
                }
                return true;
            }

            Guid guid;
            if (!Guid.TryParse(input, out guid))
            {
                StatusText.Text = "That doesn't look like a valid server instance ID " +
                                  "(expected a GUID like 6a62d499-b69c-4300-ac3f-ad2f395f4097).";
                return false;
            }

            ResultPlaceId = defaultPlaceId;
            ResultParamName = "gameInstanceId";
            ResultParamValue = input;
            return true;
        }

        private bool ValidateFollowFriend()
        {
            var manual = ManualIdBox.Text.Trim();
            if (manual.Length > 0)
            {
                long userId;
                if (!long.TryParse(manual, out userId) || userId <= 0)
                {
                    StatusText.Text = "Enter a numeric Roblox user ID (found on the user's profile page).";
                    return false;
                }

                ResultPlaceId = defaultPlaceId;
                ResultParamName = "userId";
                ResultParamValue = userId.ToString();
                return true;
            }

            var selected = FriendBox.SelectedItem as FriendEntry;
            if (selected == null)
            {
                StatusText.Text = "Pick a friend from the list, or enter a user ID manually.";
                return false;
            }

            var presence = selected.Presence;
            if (presence == null || !presence.IsInGame || presence.PlaceId <= 0)
            {
                StatusText.Text = selected.DisplayName + " isn't in an experience right now. " +
                                  "Use the manual user ID field if you want to follow them instead.";
                return false;
            }

            // Join their exact server when we know it, otherwise follow the user into the experience.
            ResultPlaceId = presence.PlaceId.ToString();
            if (!string.IsNullOrEmpty(presence.GameId))
            {
                ResultParamName = "gameInstanceId";
                ResultParamValue = presence.GameId;
            }
            else
            {
                ResultParamName = "userId";
                ResultParamValue = selected.Id.ToString();
            }
            return true;
        }

        // ──────────────────────────────────────────────
        //  Friends loading
        // ──────────────────────────────────────────────

        private void LoadFriends()
        {
            var account = plugin?.GetAuthenticatedAccount();
            if (account == null)
            {
                FriendBox.ItemsSource = null;
                FriendHint.Text = "No authenticated (cookie) account is configured — add one in the extension " +
                                  "settings to see your friends list.";
                StatusText.Text = "You can still join by entering a user ID manually.";
                ManualIdBox.Focus();
                return;
            }

            FriendHint.Text = "Loading friends…";
            StatusText.Text = string.Empty;
            RefreshButton.IsEnabled = false;

            Task.Run(() =>
            {
                using (var api = new RobloxApiClient(account.RobloSecurityCookie))
                {
                    long userId = account.RobloxUserId;
                    if (userId <= 0)
                    {
                        var auth = api.GetAuthenticatedUser();
                        userId = auth?.UserId ?? 0;
                    }
                    if (userId <= 0)
                    {
                        throw new InvalidOperationException(
                            "Could not resolve the authenticated account's user ID. Re-authenticate in settings.");
                    }

                    var friends = api.GetFriends(userId);
                    if (friends.Count == 0)
                    {
                        return FriendLoadResult.Empty;
                    }

                    var presence = api.GetPresences(friends.Select(f => f.Id));
                    return new FriendLoadResult
                    {
                        Entries = friends
                            .Select(f => new FriendEntry
                            {
                                Id = f.Id,
                                Name = f.Name,
                                DisplayName = string.IsNullOrEmpty(f.DisplayName) ? f.Name : f.DisplayName,
                                Presence = presence.ContainsKey(f.Id) ? presence[f.Id] : null
                            })
                            .ToList(),
                        // Presence returns an entry for every requested user, so an empty response
                        // means the request failed (rate limit, auth, etc.) rather than "all offline".
                        PresenceUnavailable = presence.Count == 0
                    };
                }
            }).ContinueWith(t =>
            {
                try
                {
                    Dispatcher.Invoke(() => CompleteFriendLoad(t));
                }
                catch (Exception ex)
                {
                    logger.Warn(ex, "Roblox: Failed to update the friend list UI.");
                }
            });
        }

        private void CompleteFriendLoad(Task<FriendLoadResult> task)
        {
            RefreshButton.IsEnabled = true;

            if (task.IsFaulted)
            {
                var message = task.Exception?.GetBaseException()?.Message ?? "unknown error";
                FriendHint.Text = "Failed to load friends: " + message;
                logger.Warn("Roblox: Friend list load failed — " + message);
                return;
            }

            var result = task.Result ?? FriendLoadResult.Empty;
            friendEntries = result.Entries ?? new List<FriendEntry>();
            friendEntries = friendEntries
                .OrderBy(f => PresenceRank(f.Presence))
                .ThenBy(f => f.DisplayName, StringComparer.OrdinalIgnoreCase)
                .ToList();

            FriendBox.ItemsSource = friendEntries;

            if (friendEntries.Count == 0)
            {
                FriendBox.SelectedIndex = -1;
                FriendHint.Text = "No friends found on this account.";
                return;
            }

            if (result.PresenceUnavailable)
            {
                FriendBox.SelectedIndex = -1;
                FriendHint.Text = $"{friendEntries.Count} friends — live activity status is unavailable right now " +
                                  "(rate limited or session issue). Use the manual user ID field below, or hit Refresh.";
                return;
            }

            var inGame = friendEntries.Count(f => f.Presence != null && f.Presence.IsInGame);
            FriendHint.Text = $"{friendEntries.Count} friends — {inGame} currently in an experience.";
            FriendBox.SelectedIndex = 0;
        }

        private static int PresenceRank(UserPresenceInfo presence)
        {
            if (presence == null) return 3;
            if (presence.IsInGame) return 0;
            if (presence.IsOnline) return 1;
            return 2;
        }

        // ──────────────────────────────────────────────
        //  Input helpers
        // ──────────────────────────────────────────────

        private static bool IsLikelyUrl(string input)
        {
            return input.IndexOf("://", StringComparison.Ordinal) >= 0 ||
                   input.StartsWith("http", StringComparison.OrdinalIgnoreCase) ||
                   input.IndexOf("roblox.com", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static bool TryGetQueryParams(string input, out Dictionary<string, string> query)
        {
            query = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            var questionMark = input.IndexOf('?');
            if (questionMark < 0 || questionMark >= input.Length - 1)
            {
                return false;
            }

            var queryString = input.Substring(questionMark + 1);
            var hashIndex = queryString.IndexOf('#');
            if (hashIndex >= 0)
            {
                queryString = queryString.Substring(0, hashIndex);
            }

            foreach (var pair in queryString.Split('&'))
            {
                if (pair.Length == 0) continue;
                var separator = pair.IndexOf('=');
                if (separator <= 0) continue;

                var key = pair.Substring(0, separator).Trim();
                var value = pair.Substring(separator + 1).Trim();
                if (key.Length == 0 || query.ContainsKey(key)) continue;

                try
                {
                    value = Uri.UnescapeDataString(value.Replace("+", " "));
                }
                catch
                {
                    // Keep the raw value if it isn't valid percent-encoding.
                }

                query[key] = value;
            }

            return query.Count > 0;
        }

        private static bool TryGet(Dictionary<string, string> query, string key, out string value)
        {
            return query.TryGetValue(key, out value) && !string.IsNullOrEmpty(value);
        }
    }

    /// <summary>
    /// Outcome of a background friend list load.
    /// </summary>
    public class FriendLoadResult
    {
        public static readonly FriendLoadResult Empty = new FriendLoadResult
        {
            Entries = new List<FriendEntry>()
        };

        public List<FriendEntry> Entries { get; set; }
        public bool PresenceUnavailable { get; set; }
    }

    /// <summary>
    /// A friend plus their current presence, used to populate the friend picker.
    /// </summary>
    public class FriendEntry
    {
        public long Id { get; set; }
        public string Name { get; set; }
        public string DisplayName { get; set; }
        public UserPresenceInfo Presence { get; set; }

        public string Status =>
            Presence == null ? "Offline"
            : Presence.IsInGame
                ? "Playing " + (string.IsNullOrEmpty(Presence.LastLocation) ? "an experience" : Presence.LastLocation)
                : Presence.IsOnline ? "Online"
                : "Offline";

        public override string ToString()
        {
            return $"{DisplayName} — {Status}";
        }
    }
}
