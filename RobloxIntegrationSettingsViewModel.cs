using Playnite.SDK;
using Playnite.SDK.Data;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace RobloxIntegration
{
    public class RobloxIntegrationSettingsViewModel : ObservableObject, ISettings
    {
        private readonly RobloxIntegration plugin;
        private RobloxIntegrationSettings editingClone { get; set; }
        private static readonly ILogger logger = LogManager.GetLogger();

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

        // ── Selected account in the UI list ──

        private RobloxAccount selectedAccount;
        public RobloxAccount SelectedAccount
        {
            get => selectedAccount;
            set
            {
                selectedAccount = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(HasSelectedAccount));
                OnPropertyChanged(nameof(IsSelectedAccountCookieMode));
                OnPropertyChanged(nameof(SelectedAccountConnectionStatus));
            }
        }

        public bool HasSelectedAccount => SelectedAccount != null;

        public bool IsSelectedAccountCookieMode =>
            SelectedAccount != null && !SelectedAccount.IsPublicMode;

        // ── Per-account connection test status ──

        private string selectedAccountConnectionStatus = "";
        public string SelectedAccountConnectionStatus
        {
            get => selectedAccountConnectionStatus;
            set
            {
                selectedAccountConnectionStatus = value;
                OnPropertyChanged();
            }
        }

        // ── Can we add more accounts? ──

        public bool CanAddAccount =>
            Settings?.Accounts == null || Settings.Accounts.Count < RobloxIntegrationSettings.MaxAccounts;

        // ── Commands ──

        public RelayCommand<object> AddPublicAccountCommand
        {
            get => new RelayCommand<object>((a) => AddAccount(isPublic: true));
        }

        public RelayCommand<object> AddCookieAccountCommand
        {
            get => new RelayCommand<object>((a) => AddAccount(isPublic: false));
        }

        public RelayCommand<object> RemoveAccountCommand
        {
            get => new RelayCommand<object>((a) => RemoveSelectedAccount());
        }

        public RelayCommand<object> LoginCommand
        {
            get => new RelayCommand<object>((a) => LoginSelectedAccount());
        }

        public RelayCommand<object> TestConnectionCommand
        {
            get => new RelayCommand<object>((a) => TestSelectedAccountConnection());
        }

        public RelayCommand<object> ValidateAllCommand
        {
            get => new RelayCommand<object>((a) => ValidateAllAccounts());
        }

        // ── Constructor ──

        public RobloxIntegrationSettingsViewModel(RobloxIntegration plugin)
        {
            this.plugin = plugin;
            var savedSettings = plugin.LoadPluginSettings<RobloxIntegrationSettings>();
            Settings = savedSettings ?? new RobloxIntegrationSettings();

            // Ensure Accounts collection exists
            if (Settings.Accounts == null)
            {
                Settings.Accounts = new ObservableCollection<RobloxAccount>();
            }

            // Run legacy migration if needed
            if (Settings.MigrateLegacyIfNeeded())
            {
                logger.Info("Roblox: Migrated legacy settings during ViewModel init.");
                plugin.SavePluginSettings(Settings);
            }
        }

        // ── ISettings implementation ──

        public void BeginEdit()
        {
            editingClone = Serialization.GetClone(Settings);
        }

        public void CancelEdit()
        {
            Settings = editingClone;
            SelectedAccount = null;
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

        // ── Account management ──

        private void AddAccount(bool isPublic)
        {
            if (!CanAddAccount)
            {
                plugin.PlayniteApi.Dialogs.ShowMessage(
                    $"Maximum of {RobloxIntegrationSettings.MaxAccounts} accounts reached. Please remove an existing account first.",
                    "Account Limit Reached");
                return;
            }

            var account = new RobloxAccount
            {
                Id = Guid.NewGuid().ToString(),
                IsPublicMode = isPublic,
                DisplayLabel = isPublic ? "New Public Account" : "New Cookie Account",
                IsEnabled = true,
                IsSessionValid = true
            };

            Settings.Accounts.Add(account);
            SelectedAccount = account;
            OnPropertyChanged(nameof(CanAddAccount));
        }

        private void RemoveSelectedAccount()
        {
            if (SelectedAccount == null) return;

            var result = plugin.PlayniteApi.Dialogs.ShowMessage(
                $"Remove account '{SelectedAccount.DisplayLabel}'? This cannot be undone.",
                "Confirm Removal",
                System.Windows.MessageBoxButton.YesNo);

            if (result == System.Windows.MessageBoxResult.Yes)
            {
                Settings.Accounts.Remove(SelectedAccount);
                SelectedAccount = Settings.Accounts.FirstOrDefault();
                OnPropertyChanged(nameof(CanAddAccount));
            }
        }

        private void LoginSelectedAccount()
        {
            if (SelectedAccount == null || SelectedAccount.IsPublicMode) return;

            try
            {
                using (var webView = plugin.PlayniteApi.WebViews.CreateView(680, 750))
                {
                    var account = SelectedAccount; // Capture for closure

                    webView.LoadingChanged += (s, e) =>
                    {
                        try
                        {
                            var cookies = webView.GetCookies();
                            var robloSecurity = cookies?.FirstOrDefault(
                                c => c.Name == ".ROBLOSECURITY" && c.Domain.Contains("roblox.com"));
                            if (robloSecurity != null && !string.IsNullOrEmpty(robloSecurity.Value))
                            {
                                account.RobloSecurityCookie = robloSecurity.Value;
                                OnPropertyChanged(nameof(IsSelectedAccountCookieMode));
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

        private void TestSelectedAccountConnection()
        {
            if (SelectedAccount == null) return;

            SelectedAccountConnectionStatus = "⏳ Testing connection...";

            try
            {
                string cookie = SelectedAccount.IsPublicMode ? null : SelectedAccount.RobloSecurityCookie;
                using (var apiClient = new RobloxApiClient(cookie))
                {
                    var result = apiClient.ValidateSession(SelectedAccount);

                    SelectedAccount.IsSessionValid = result.IsValid;
                    SelectedAccount.LastValidated = DateTime.Now;

                    if (result.IsValid)
                    {
                        // Update cached info
                        if (result.ResolvedUserId > 0)
                        {
                            SelectedAccount.RobloxUserId = result.ResolvedUserId;
                        }
                        if (!string.IsNullOrEmpty(result.ResolvedUsername))
                        {
                            // Auto-update label if it's still the default
                            if (SelectedAccount.DisplayLabel == "New Public Account" ||
                                SelectedAccount.DisplayLabel == "New Cookie Account" ||
                                SelectedAccount.DisplayLabel == "Migrated Account")
                            {
                                SelectedAccount.DisplayLabel = result.ResolvedUsername;
                            }
                        }

                        SelectedAccountConnectionStatus = $"✅ {result.Message}";
                    }
                    else
                    {
                        SelectedAccountConnectionStatus = $"❌ {result.Message}";
                    }
                }
            }
            catch (Exception ex)
            {
                SelectedAccountConnectionStatus = $"❌ Error: {ex.Message}";
            }
        }

        private void ValidateAllAccounts()
        {
            if (Settings?.Accounts == null || Settings.Accounts.Count == 0) return;

            int valid = 0;
            int invalid = 0;

            foreach (var account in Settings.Accounts)
            {
                try
                {
                    string cookie = account.IsPublicMode ? null : account.RobloSecurityCookie;
                    using (var apiClient = new RobloxApiClient(cookie))
                    {
                        var result = apiClient.ValidateSession(account);
                        account.IsSessionValid = result.IsValid;
                        account.LastValidated = DateTime.Now;

                        if (result.IsValid)
                        {
                            if (result.ResolvedUserId > 0)
                            {
                                account.RobloxUserId = result.ResolvedUserId;
                            }
                            valid++;
                        }
                        else
                        {
                            invalid++;
                        }
                    }
                }
                catch (Exception ex)
                {
                    logger.Error(ex, $"Failed to validate account '{account.DisplayLabel}'.");
                    account.IsSessionValid = false;
                    invalid++;
                }
            }

            SelectedAccountConnectionStatus = $"Validated all: {valid} ✅, {invalid} ❌";
        }
    }
}
