# Playnite Roblox Integration

A seamless Library Integration for [Playnite](https://playnite.link/) that imports your favorited Roblox experiences as playable games.

## Features

- **Automatic Import:** Syncs your favorited Roblox experiences directly into your Playnite library, complete with titles, thumbnails, and descriptions.
- **Multi-Account Support:** Add, store, and enable up to 5 Roblox accounts. Sync all enabled accounts at once (games are de-duplicated across accounts).
- **Public & Cookie Modes:** Import a user's public favorites with just a username (no login required), or authenticate with a `.ROBLOSECURITY` cookie to access their favorites.
- **Session Health Checks:** Validates each account during sync and raises a notification when a session has expired or become invalid.
- **Direct Launch:** One-click launch opens the specific experience directly in the Roblox desktop player via the `roblox://` protocol. Requires the Roblox desktop client to be installed.
- **Playtime Tracking:** Tails the Roblox Player logs (`%LOCALAPPDATA%\Roblox\logs`) to detect when you leave an experience, so playtime is recorded correctly even if the Roblox player stays open in the background. Windows only.
- **Custom Platform Icon:** Registers a `Roblox` platform with a bundled icon and clears Playnite's default cover/background for it.

## Requirements

- **Playnite 6.11.0 or newer** (see `RequiredApiVersion` in `installer.yaml`).
- **Roblox desktop client** — launching relies on the `roblox://` protocol handler.
- Only enabled accounts are synced; at least one account must be ticked in the settings list.

## Installation

1. Download the latest `.pext` package from the [Releases page](https://github.com/Horrid-12/Playnite-Roblox-Integration/releases).
2. Drag and drop the `.pext` file into your Playnite window, or open it directly, to install.
3. Once installed, navigate to **Playnite Settings -> Add-ons -> Extension settings -> Libraries -> Roblox Integration**.
4. Click **+ Public** or **+ Cookie** to add an account. The button creates a new row in the account list and selects it.
5. Configure the selected account using the **Selected Account Details** panel:
   - **+ Public**: type the Roblox username into the **Roblox Username** field. No login is required, but that user's favorites must be set to *Everyone* or *Public* in Roblox privacy settings.
   - **+ Cookie**: click **Log in with Roblox** to authenticate in the built-in browser (the `.ROBLOSECURITY` cookie is captured automatically), or paste a cookie manually into the **Manual Cookie Entry** box.
6. Leave the account's checkbox ticked to include it in library syncs, and use **Display Label** to give it a recognizable name.
7. Click **Test Connection** to check the selected account, or **Validate All Accounts** to check every account at once.
8. Click **Update Game Library -> Roblox** to import your favorites! Games shared between accounts are de-duplicated (first enabled account wins).

Use **🗑 Remove** to delete the selected account.

## Cookie Storage & Security

> ⚠ Cookie-authenticated accounts store your `.ROBLOSECURITY` cookie **in plaintext** in Playnite's extension settings file on disk. It is never sent anywhere except Roblox, and public-favorites accounts store no sensitive data — but anyone with access to your Playnite profile can read the cookie. Prefer **+ Public** mode where possible.

Pasted cookies have surrounding whitespace, quotes, and backslashes trimmed before use; no further sanitization or encryption is applied.

## Upgrading

Settings from v1.0.2 and earlier used a single account. On first launch of v1.0.3+, that account is automatically migrated into the account list (one-time, non-destructive to your library). You can safely delete the migrated entry afterwards.

## Manual Build

If you wish to build the extension yourself:
1. Clone this repository.
2. Build the solution using `dotnet build` (targets `net462`; requires the .NET Framework 4.6.2 targeting pack and the WPF build tools).
3. Use the Playnite Toolbox (`Toolbox.exe pack`) to generate the `.pext` package.

> Note: the `.csproj` contains a `DeployToPlaynite` target that hardcodes `D:\Software\Playnite\Extensions\RobloxInt\` as a post-build copy destination. It is set to `ContinueOnError`, so builds on other machines still succeed — edit or remove the target if that path doesn't apply to you.

## Known Limitations

- Windows only (the playtime watcher depends on the `RobloxPlayerBeta` process and Windows-local Roblox log paths).
- Playtime tracking depends on Roblox's internal log strings (`Joining game` / `leaveUGCGameInternal`). If Roblox changes them, session-end detection will silently fall back to watching for the process exiting.

## License

All Roblox brand assets and logos are the property of Roblox Corporation.

<!-- TODO: no LICENSE file exists in this repository yet. Add one (e.g. MIT) before publishing, then replace this section with the standard license text. -->
