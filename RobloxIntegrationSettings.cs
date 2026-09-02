using Playnite.SDK;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace RobloxIntegration
{
    public class RobloxIntegrationSettings : ObservableObject
    {
        private static readonly ILogger logger = LogManager.GetLogger();

        /// <summary>
        /// Multi-account storage. Each entry is a self-contained account.
        /// </summary>
        public ObservableCollection<RobloxAccount> Accounts { get; set; }
            = new ObservableCollection<RobloxAccount>();

        /// <summary>
        /// Maximum number of accounts that can be added.
        /// </summary>
        public const int MaxAccounts = 5;

        // ──────────────────────────────────────────────
        // Legacy fields — kept for one-time migration only.
        // After migration these are cleared and ignored.
        // ──────────────────────────────────────────────

        public string RobloSecurityCookie { get; set; } = string.Empty;
        public string RobloxUsername { get; set; } = string.Empty;
        public long RobloxUserId { get; set; } = 0;
        public bool UsePublicFavorites { get; set; } = true;

        /// <summary>
        /// Flag that tracks whether legacy migration has already run.
        /// </summary>
        public bool LegacyMigrated { get; set; } = false;

        /// <summary>
        /// Migrates legacy single-account data into the Accounts list.
        /// Called once on first load after the update. Returns true if migration occurred.
        /// </summary>
        public bool MigrateLegacyIfNeeded()
        {
            if (LegacyMigrated)
            {
                return false;
            }

            bool hasLegacyCookie = !string.IsNullOrEmpty(RobloSecurityCookie);
            bool hasLegacyUsername = !string.IsNullOrEmpty(RobloxUsername);

            if (!hasLegacyCookie && !hasLegacyUsername)
            {
                // Nothing to migrate — mark done so we don't check again
                LegacyMigrated = true;
                return false;
            }

            try
            {
                var account = new RobloxAccount
                {
                    Id = Guid.NewGuid().ToString(),
                    IsEnabled = true,
                    IsSessionValid = true
                };

                if (UsePublicFavorites && hasLegacyUsername)
                {
                    account.IsPublicMode = true;
                    account.RobloxUsername = RobloxUsername;
                    account.RobloxUserId = RobloxUserId;
                    account.DisplayLabel = RobloxUsername;
                }
                else if (hasLegacyCookie)
                {
                    account.IsPublicMode = false;
                    account.RobloSecurityCookie = RobloSecurityCookie;
                    account.DisplayLabel = "Migrated Account";

                    // Also carry over username/userId if they were set
                    if (hasLegacyUsername)
                    {
                        account.RobloxUsername = RobloxUsername;
                        account.RobloxUserId = RobloxUserId;
                        account.DisplayLabel = RobloxUsername;
                    }
                }
                else if (hasLegacyUsername)
                {
                    account.IsPublicMode = true;
                    account.RobloxUsername = RobloxUsername;
                    account.RobloxUserId = RobloxUserId;
                    account.DisplayLabel = RobloxUsername;
                }

                if (Accounts == null)
                {
                    Accounts = new ObservableCollection<RobloxAccount>();
                }

                Accounts.Add(account);
                logger.Info($"Roblox: Migrated legacy account '{account.DisplayLabel}' to multi-account system.");

                // Clear legacy fields
                RobloSecurityCookie = string.Empty;
                RobloxUsername = string.Empty;
                RobloxUserId = 0;
                LegacyMigrated = true;

                return true;
            }
            catch (Exception ex)
            {
                logger.Error(ex, "Roblox: Failed to migrate legacy account settings.");
                LegacyMigrated = true; // Don't retry on failure
                return false;
            }
        }
    }
}
