using Playnite.SDK;
using System;
using System.Collections.Generic;

namespace RobloxIntegration
{
    public class RobloxAccount : ObservableObject
    {
        /// <summary>
        /// Unique identifier for this account entry.
        /// </summary>
        public string Id { get; set; } = Guid.NewGuid().ToString();

        private string displayLabel = string.Empty;
        /// <summary>
        /// User-facing label for this account (defaults to the resolved username).
        /// </summary>
        public string DisplayLabel
        {
            get => displayLabel;
            set
            {
                displayLabel = value ?? string.Empty;
                OnPropertyChanged();
            }
        }

        private bool isPublicMode = true;
        /// <summary>
        /// True = public favorites via username (no auth needed).
        /// False = authenticated via .ROBLOSECURITY cookie.
        /// </summary>
        public bool IsPublicMode
        {
            get => isPublicMode;
            set
            {
                isPublicMode = value;
                OnPropertyChanged();
            }
        }

        private string robloxUsername = string.Empty;
        /// <summary>
        /// Roblox username (used when IsPublicMode = true).
        /// </summary>
        public string RobloxUsername
        {
            get => robloxUsername;
            set
            {
                robloxUsername = value?.Trim() ?? string.Empty;
                OnPropertyChanged();
            }
        }

        private long robloxUserId = 0;
        /// <summary>
        /// Resolved / cached Roblox user ID.
        /// </summary>
        public long RobloxUserId
        {
            get => robloxUserId;
            set
            {
                robloxUserId = value;
                OnPropertyChanged();
            }
        }

        private string robloSecurityCookie = string.Empty;
        /// <summary>
        /// .ROBLOSECURITY cookie value (used when IsPublicMode = false).
        /// </summary>
        public string RobloSecurityCookie
        {
            get => robloSecurityCookie;
            set
            {
                var cleaned = value ?? string.Empty;
                cleaned = cleaned.Trim().Trim('"').Trim('\\').Trim('"').Trim();
                robloSecurityCookie = cleaned;
                OnPropertyChanged();
                OnPropertyChanged(nameof(HasCookie));
            }
        }

        private bool isEnabled = true;
        /// <summary>
        /// Whether this account is included during library sync.
        /// </summary>
        public bool IsEnabled
        {
            get => isEnabled;
            set
            {
                isEnabled = value;
                OnPropertyChanged();
            }
        }

        private DateTime? lastValidated = null;
        /// <summary>
        /// Timestamp of the last successful session validation.
        /// </summary>
        public DateTime? LastValidated
        {
            get => lastValidated;
            set
            {
                lastValidated = value;
                OnPropertyChanged();
            }
        }

        private bool isSessionValid = true;
        /// <summary>
        /// Result of the most recent session health check.
        /// </summary>
        public bool IsSessionValid
        {
            get => isSessionValid;
            set
            {
                isSessionValid = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(StatusIndicator));
            }
        }

        // --- Computed helpers (not serialized) ---

        /// <summary>
        /// Whether a cookie value is present for this account.
        /// </summary>
        [Newtonsoft.Json.JsonIgnore]
        public bool HasCookie => !string.IsNullOrEmpty(RobloSecurityCookie);

        /// <summary>
        /// A short status indicator string for the UI.
        /// </summary>
        [Newtonsoft.Json.JsonIgnore]
        public string StatusIndicator => IsSessionValid ? "✅ Valid" : "❌ Expired";

        /// <summary>
        /// Mode label for display.
        /// </summary>
        [Newtonsoft.Json.JsonIgnore]
        public string ModeLabel => IsPublicMode ? "Public" : "Cookie Auth";
    }
}
