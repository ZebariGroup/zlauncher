# ZLauncher

ZLauncher is a customizable Windows launcher for Windows 11 inspired by the Windows 10 Start menu and PowerToys Run (Alt+Space). It combines a powerful search-driven command palette with structured navigation, tiles, workflows, and macro automation.

## Features

- Global hotkey (Alt+Space) to summon the launcher overlay.
- Modern, resizable WPF UI with search bar, app list, tiles, and pinned actions.
- Start menu indexer that automatically syncs installed shortcuts.
- Custom action workflows with support for launching multiple apps or running shell commands.
- JSON configuration stored in `%AppData%\ZLauncher\launcher.json` for easy customization.
- Extensible command execution system (shell commands, workflows, custom paths).

## Getting Started

1. Install the .NET 8 desktop runtime.
2. Clone the repository: `git clone https://github.com/<your-org>/ZLauncher.git`
3. Restore/build: `dotnet build`
4. Run: `dotnet run`

## Configuration

Configuration is stored in `%AppData%\ZLauncher\launcher.json`. At first launch, ZLauncher creates this file if it does not exist. Customize sections:

- `pinned`: frequently used shortcuts or macros.
- `macroGroups`: named collections of workflow actions.
- `recent`: items to surface under the Recent section.
- `appFilters`: extra filters for the All Apps list (e.g., `"source:Start Menu"`).
- `tileGroups`: groups of tiles for quick launching.

## Roadmap

- Plugin system for custom indexers (files, websites, scripts).
- Theming support and accent color customization.
- Telemetry-free and privacy-first design.
- Integration with Windows settings, Control Panel, and administrative tools.

## License

This project is open source under the MIT License.

