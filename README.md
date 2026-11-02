# Mod Manager Enhancements

[日本語](README.ja.md)

Adds DLL indicators, file-override badges, and a read-only details panel to Sea Power's built-in mod manager.

## Features

- Shows a small gray `DLL` label without a frame for mods containing DLL files, including disabled local and Workshop mods. Detection uses installed files rather than Workshop dependency metadata.
- Orders row information as native `A`, `DLL` and other status marks, a source icon, then `↑/↓` at the far right. The circular Steam icon identifies Workshop mods; a folder identifies local mods. Hover for the localized source name. Clicking the Steam icon retains the original Workshop action.
- Reserves equal source-icon widths and fixed arrow slots across all rows, including rows without overrides. Each direction shows counts 1–9 and `9+` for 10 or more; tooltips and details keep the exact totals.
- Shows differing files shared by enabled local and Workshop mods, including User Data.
- `↑` counts shared files for which that mod is the highest-priority provider. `↓` counts files whose contents differ from the highest-priority provider.
- Click an override badge to filter the details to that mod. Clicking the selected outlined badge again clears the filter; clicking a different badge switches to that mod. While filtered, the selected badge has a cyan outline and background; related arrows turn cyan. Counts still cover all of each mod's overrides. **Show all overrides** clears the filter and highlighting.
- Lists providers in priority order. Hover over a file to inspect its full source paths.
- Updates after changes to enabled mods, drag order, dependency sorting, or profiles. **Rescan** rereads edited, added, or deleted files.
- Keeps the game's existing mod controls. File details do not have a click-selection state.
- Uses Japanese when the game language is Japanese, and English otherwise.

## Installation

Requires Sea Power 0.8.7 and a working [Anchor Chain](https://steamcommunity.com/sharedfiles/filedetails/?id=3380210757) installation.

1. Download `ModManagerEnhancements-v0.1.13.zip` from [Releases](https://github.com/mebiustos-sea-power/ModManagerEnhancements/releases).
2. Extract the `ModManagerEnhancements` folder into `Sea Power_Data/StreamingAssets`.
3. Enable this mod and Anchor Chain in the game's mod manager, then fully exit and restart Sea Power.

Changes to your mod selection and order are saved with the game's existing **Accept** button.

## What the badges mean

The `DLL` badge identifies DLL file presence in the mod folder and its subfolders; it does not establish whether a plugin has loaded successfully. DLL files are not opened or executed for this check.

The mod compares files with the same relative path in enabled mods. For ordinary single-file lookups, the mod higher in the manager has priority. Identical contents do not produce a badge. Normal replacements of the base game's `original` files and package metadata are excluded.

INI files can be merged by key depending on how they are read. An INI entry therefore describes file-provider priority; it does not establish which individual keys will be used. These entries include a note in the details panel.

This mod does not analyze DLL dependencies, duplicate plugin IDs, Harmony patch interactions, or incompatibilities between differently named files. It does not change files or automatically reorder mods. An override can be an intentional replacement rather than an error.

A missing or unreadable mod file is reported in the panel and the BepInEx log. Linked subdirectories are reported as scan errors rather than silently skipped.

## Build and verification

Requires a .NET 10 SDK, the installed game, and Anchor Chain. Game and third-party dependency binaries are not included; the project references your locally installed copies. No NuGet downloads are needed.

Copy `Directory.Build.local.props.example` to `Directory.Build.local.props` and set the dependency paths, then run:

```powershell
.\build.ps1 -Test
```

The package is written to `dist/ModManagerEnhancements`. The build does not install the mod, start the game, or alter game settings.

The regression suite passes 28 checks under .NET 10 and separately under the game's bundled Mono runtime. It covers DLL presence, disabled mods, nested files, file additions/removals, independent scan recovery, file priority, content comparison, filtering, cancellation, stale results, and errors. Three standalone Noesis checks cover badge order, source-icon and fixed-arrow alignment, source-icon hover, pointer targets, highlighting, and row detachment/recreation. These checks do not render the live game window or verify the full game UI theme.

Source layout:

- `src/Core`: file analysis, results, presentation data, and asynchronous scan management, without Unity or Noesis dependencies.
- `src/UI` and `src/ManagerController.cs`: integration with the existing mod manager and its lifetime.
- `src/Plugin.cs`: activation check and Anchor Chain startup.
- `tests`, `tests/UI`, and `tools/test-mono.ps1`: core behavior and native Noesis checks in separate processes.

Plugin ID: `mebiustos.modmanagerenhancements`. Version: `0.1.13`.

The source indicator uses the official Steam store icon embedded in this mod. Steam and the Steam logo are trademarks and/or registered trademarks of Valve Corporation in the U.S. and/or other countries. The artwork is sourced from the [official Steam store icon](https://store.steampowered.com/favicon.ico).
