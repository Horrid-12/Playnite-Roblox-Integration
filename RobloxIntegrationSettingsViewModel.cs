using Playnite.SDK;
using Playnite.SDK.Data;
using System;
using System.Collections.Generic;
using System.Linq;

namespace RobloxIntegration
{
    public class RobloxIntegrationSettingsViewModel : ObservableObject, ISettings
    {
        private readonly RobloxIntegration plugin;
        private RobloxIntegrationSettings editingClone { get; set; }

        private RobloxIntegrationSettings settings;
        public RobloxIntegrationSettings Settings
        {
            get => settings;
            set
            {
                settings = value;
                OnPropertyChanged();
            }
        }

        public bool IsUserLoggedIn => !string.IsNullOrEmpty(Settings?.RobloSecurityCookie);

        private string connectionStatus = "";
        public string ConnectionStatus
        {
            get => connectionStatus;
            set
            {
                connectionStatus = value;
                OnPropertyChanged();
            }
        }

        public RelayCommand<object> LoginCommand
        {
            get => new RelayCommand<object>((a) => Login());
        }

        public RelayCommand<object> TestConnectionCommand
        {
            get => new RelayCommand<object>((a) => TestConnection());
        }

        public RobloxIntegrationSettingsViewModel(RobloxIntegration plugin)
        {
            this.plugin = plugin;
            var savedSettings = plugin.LoadPluginSettings<RobloxIntegrationSettings>();
            Settings = savedSettings ?? new RobloxIntegrationSettings();
        }

        public void BeginEdit()
        {
            editingClone = Serialization.GetClone(Settings);
        }

        public void CancelEdit()
        {
            Settings = editingClone;
        }

        public void EndEdit()
        {
            plugin.SavePluginSettings(Settings);
        }

        public bool VerifySettings(out List<string> errors)
        {
            errors = new List<string>();
            return true;
        }

        private void Login()
        {
            try
            {
                using (var webView = plugin.PlayniteApi.WebViews.CreateView(680, 750))
                {
                    webView.LoadingChanged += (s, e) =>
                    {
                        try
                        {
                            var cookies = webView.GetCookies();
                            var robloSecurity = cookies?.FirstOrDefault(
                                c => c.Name == ".ROBLOSECURITY" && c.Domain.Contains("roblox.com"));
                            if (robloSecurity != null && !string.IsNullOrEmpty(robloSecurity.Value))
                            {
                                Settings.RobloSecurityCookie = robloSecurity.Value;
                                OnPropertyChanged(nameof(IsUserLoggedIn));
                                webView.Close();
                            }
                        }
                        catch { /* Ignore cookie read errors during loading */ }
                    };

                    webView.Navigate("https://www.roblox.com/");
                    webView.OpenDialog();
                }
            }
            catch (Exception e)
            {
                plugin.PlayniteApi.Dialogs.ShowErrorMessage(
                    "Failed to open Roblox login window: " + e.Message, "Login Error");
            }
        }

        private void TestConnection()
        {
            if (Settings == null) return;

            if (Settings.UsePublicFavorites)
            {
                if (string.IsNullOrEmpty(Settings.RobloxUsername))
                {
                    ConnectionStatus = "❌ Please enter a Roblox username first.";
                    return;
                }

                ConnectionStatus = "⏳ Resolving username...";

                try
                {
                    using (var apiClient = new RobloxApiClient(null))
                    {
                        var userId = apiClient.GetUserIdFromUsername(Settings.RobloxUsername);
                        if (userId > 0)
                        {
                            Settings.RobloxUserId = userId;
                            ConnectionStatus = $"✅ Username resolved! User ID: {userId}. Ensure your favorites are set to Public on Roblox.";
                        }
                        else
                        {
                            ConnectionStatus = "❌ Username not found on Roblox.";
                        }
                    }
                }
                catch (Exception ex)
                {
                    ConnectionStatus = $"❌ Error: {ex.Message}";
                }
            }
            else
            {
                if (string.IsNullOrEmpty(Settings.RobloSecurityCookie))
                {
                    ConnectionStatus = "❌ No cookie set. Please authenticate first.";
                    return;
                }

                ConnectionStatus = "⏳ Testing connection...";

                try
                {
                    using (var apiClient = new RobloxApiClient(Settings.RobloSecurityCookie))
                    {
                        var user = apiClient.GetAuthenticatedUser();
                        if (user != null)
                        {
                            ConnectionStatus = $"✅ Connected as: {user.Username} (ID: {user.UserId})";
                        }
                        else
                        {
                            ConnectionStatus = "❌ Authentication failed. Cookie may be expired.";
                        }
                    }
                }
                catch (Exception ex)
                {
                    ConnectionStatus = $"❌ Error: {ex.Message}";
                }
            }
        }
    }
}
