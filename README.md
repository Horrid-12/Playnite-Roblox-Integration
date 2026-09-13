# Playnite Roblox Integration

A seamless Library Integration for [Playnite](https://playnite.link/) that imports your favorited Roblox experiences as playable games.

## Features

- **Automatic Import:** Syncs your favorited Roblox experiences directly into your Playnite library, complete with titles, thumbnails, and descriptions.
- **Multi-Account Support:** Add, store, and enable up to 5 Roblox accounts. Sync all enabled accounts at once (games are de-duplicated across accounts).
- **Public & Cookie Modes:** Import a user's public favorites with just a username (no login required), or authenticate with a `.ROBLOSECURITY` cookie to access their favorites.
- **Session Health Checks:** Validates each account during sync and raises a notification when a session has expired or become invalid.
- **Accurate Playtime Tracking:** Monitors Roblox's session logs so playtime is recorded correctly, even when the Roblox player stays open in the background.
- **Direct Launch:** One-click launch opens the specific experience directly in the Roblox desktop player via the `roblox://` protocol.
- **Theme Support:** Applies a custom platform icon designed to blend cleanly with themes like eMixedNiteMC.
- **Secure Authentication:** Cookies are stored locally and strictly sanitized before use.

## Installation

1. Download the latest `.pext` package from the [Releases page](https://github.com/Horrid-12/Playnite-Roblox-Integration/releases).
2. Drag and drop the `.pext` file into your Playnite window, or open it directly, to install.
3. Once installed, navigate to **Playnite Settings -> Add-ons -> Extension settings -> Libraries -> Roblox Integration**.
4. In the settings tab, add an account:
   - **+ Public**: enter a Roblox username to import that user's public favorites. No login is required, but the account's favorites must be visible publicly in Roblox privacy settings.
   - **+ Cookie**: click **Log in with Roblox** to authenticate in the built-in browser (the `.ROBLOSECURITY` cookie is captured automatically), or paste a cookie manually.
5. Optionally use **Validate All Accounts** to confirm every account's session is healthy.
6. Click **Update Game Library -> Roblox** to import your favorites!

## Manual Build

If you wish to build the extension yourself:
1. Clone this repository.
2. Build the solution using `dotnet build`.
3. Use the Playnite Toolbox (`Toolbox.exe pack`) to generate the `.pext` package.

## License

This project is open-source. All Roblox brand assets and logos are the property of Roblox Corporation.