# Flow.Plugin.KiroWorkspace

A [Flow Launcher](https://www.flowlauncher.com/) plugin that lists the workspaces and folders you recently opened in
[Kiro](https://kiro.dev/) and opens them with a few keystrokes.

Type the action keyword `k`, then part of a folder name, and press Enter.

## Credits and relationship to the original project

This is a fork of [taooceros/Flow.Plugin.VSCodeWorkspace](https://github.com/taooceros/Flow.Plugin.VSCodeWorkspace),
the VS Code Workspaces plugin for Flow Launcher, adapted for Kiro. The original code in turn comes from
[Microsoft PowerToys](https://github.com/microsoft/PowerToys/tree/main/src/modules/launcher/Plugins/Community.PowerToys.Run.Plugin.VSCodeWorkspaces).
Thanks to the original authors (ricardosantos9521, MaskedRPGFan, taooceros and contributors).

This project is independent. It is **not** affiliated with or endorsed by the original author, Microsoft, Kiro or Flow Launcher.
The original Git history is preserved; see `git log`. Licensed under MIT, see [LICENSE](LICENSE) and
[THIRD_PARTY_NOTICES.md](THIRD_PARTY_NOTICES.md).

## What changed from the VS Code plugin

| Area | VS Code plugin | This plugin |
| --- | --- | --- |
| Executable | `code`, `code-insiders`, `codium` from `PATH` | `Kiro.exe` from `PATH` (`...\Kiro\bin`), then `%LOCALAPPDATA%\Programs\Kiro`, then `%ProgramFiles%\Kiro` |
| User data | `%APPDATA%\Code`, `~\.vscode-shared` | `%APPDATA%\Kiro`, `~\.kiro-shared` |
| Recent list | `history.recentlyOpenedPathsList` in `state.vscdb` (and legacy `storage.json`) | `history.recentlyOpenedPathsList` in `state.vscdb` only |
| Action keyword | `{` | `k` (so both plugins can be installed together; changeable in Flow Launcher settings) |
| SSH host list | Read from `remote.SSH.configFile` and opened with the Microsoft Remote - SSH extension | **Removed** (see below) |
| Launching | `code --folder-uri` / `--file-uri` | `Kiro.exe --folder-uri` / `--file-uri` |
| Icons | VS Code logo | Kiro's own executable icon is read at runtime; the plugin icon is the Kiro app icon (see Trademarks) |
| Translations | 17 locales | English only (the old strings described VS Code and SSH features and were not re-reviewed) |

## Behaviour

- Local folders and `.code-workspace` files from Kiro's recent list are shown. Matching uses Flow Launcher's fuzzy search.
- Enter opens the workspace in Kiro. Ctrl+Enter, or the context menu, opens a local folder in the file manager.
- You can add extra folders or workspace URIs in the plugin's settings.
- If Kiro cannot be found, the plugin says so instead of failing silently.
- If the Kiro executable was moved or a local folder was deleted, or the process cannot start, a notification is shown.

## Requirements

- Windows x64 with Flow Launcher installed.
- Kiro installed. Per-user installs in `%LOCALAPPDATA%\Programs\Kiro` and installs whose `bin` folder is on `PATH` are detected.
- No .NET SDK is needed to *use* the plugin. It is built with `net7.0-windows` and relies on the .NET runtime that Flow Launcher provides.

## Install

1. Open the latest successful run of the **build** workflow under the repository's *Actions* tab.
2. Download the `Flow.Plugin.KiroWorkspace-<version>.zip` artifact.
3. Close Flow Launcher.
4. Extract the ZIP into a new folder named `Flow.Plugin.KiroWorkspace` in Flow Launcher's user plugin directory:
   `%APPDATA%\FlowLauncher\Plugins\` (for the portable edition use `UserData\Plugins\` next to the executable).
   `plugin.json` must sit directly inside that folder.
5. Start Flow Launcher and type `k`.

## Remote workspaces (WSL, SSH, Dev Containers)

Recent entries that use the `vscode-remote://` scheme (SSH, WSL, Dev Containers) are still parsed and passed to Kiro with
`--folder-uri`. **This is unverified.** Kiro does not ship Microsoft's Remote extensions, so whether those entries exist
and whether Kiro can reopen them depends on the remote extension you have installed. Nothing here has been tested against a remote setup.

Discovery of hosts from an SSH config file was removed, because it relied on Microsoft's `ms-vscode-remote.remote-ssh`
extension id and flags that cannot be assumed for Kiro.

Portable layouts (a `data` folder next to `Kiro.exe`) are carried over from VS Code's layout and are also unverified.

## Verification status

What is verified:
- On a Windows machine with Kiro 1.1.70 installed: `Kiro.exe`, `bin\kiro.cmd`, `%APPDATA%\Kiro`, and a `history.recentlyOpenedPathsList` entry
  in `~\.kiro-shared\sharedStorage\state.vscdb` containing `folderUri` items exist, and Kiro's CLI code contains the `--folder-uri` and `--file-uri` options.
- Restore, build, unit tests and packaging run in GitHub Actions (see the workflow).

What is not verified by the automated build:
- Loading the plugin inside Flow Launcher, and actually launching Kiro from it. A green build does not prove either.

## Build

Builds run in GitHub Actions ([`.github/workflows/build.yml`](.github/workflows/build.yml)): restore, Release build, tests, then
`dotnet publish -r win-x64` zipped as the plugin package. Triggered on pushes to `main` and `feat/**`, on pull requests and manually.
If you do have the .NET 7 SDK locally, `dotnet build -c Release -p:Platform=x64` works the same way.

## Trademarks

The Kiro name and icon belong to their owner and are used here only to identify the application this plugin works with. Images/kiro.png is not covered by the MIT license.
