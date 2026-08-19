# Playnite Roblox Integration

A seamless Library Integration for [Playnite](https://playnite.link/) that imports your favorited Roblox experiences as playable games.

## Features

- **Automatic Import:** Syncs your favorited Roblox experiences directly into your Playnite library.
- **Direct Launch:** One-click launch opens the specific experience directly in the Roblox desktop player.
- **Theme Support:** Generates sleek custom platform icons designed to blend beautifully with custom themes like eMixedNiteMC.
- **Secure Authentication:** Connects to Roblox API v2 using `.ROBLOSECURITY` cookie authentication (stored locally and strictly sanitized).

## Installation

1. Download the latest `.pext` package from the [Releases page](https://github.com/Horrid-12/Playnite-Roblox-Integration/releases).
2. Drag and drop the `.pext` file into your Playnite window, or open it directly, to install.
3. Once installed, navigate to **Playnite Settings -> Add-ons -> Extension settings -> Libraries -> Roblox Integration**.
4. Follow the prompt to log into Roblox in the built-in browser and save your settings.
5. Click **Update Game Library -> Roblox** to import your favorites!

## Manual Build

If you wish to build the extension yourself:
1. Clone this repository.
2. Build the solution using `dotnet build`.
3. Use the Playnite Toolbox (`Toolbox.exe pack`) to generate the `.pext` package.

## License

This project is open-source. All Roblox brand assets and logos are the property of Roblox Corporation.
