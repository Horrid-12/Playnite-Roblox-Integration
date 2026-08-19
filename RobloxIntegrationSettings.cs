using Playnite.SDK;
using System.Collections.Generic;

namespace RobloxIntegration
{
    public class RobloxIntegrationSettings : ObservableObject
    {
        private string robloSecurityCookie = string.Empty;
        public string RobloSecurityCookie
        {
            get => robloSecurityCookie;
            set
            {
                var cleaned = value ?? string.Empty;
                cleaned = cleaned.Trim().Trim('"').Trim('\\').Trim('"').Trim();
                robloSecurityCookie = cleaned;
                OnPropertyChanged();
            }
        }

        private string robloxUsername = string.Empty;
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
        public long RobloxUserId
        {
            get => robloxUserId;
            set
            {
                robloxUserId = value;
                OnPropertyChanged();
            }
        }

        private bool usePublicFavorites = true;
        public bool UsePublicFavorites
        {
            get => usePublicFavorites;
            set
            {
                usePublicFavorites = value;
                OnPropertyChanged();
            }
        }
    }
}
