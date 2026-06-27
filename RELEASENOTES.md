# Release Notes

## KSP Launch Countdown Mod

---

## v0.2.1 - Unreleased

### New Voice Presets

Added three new countdown voice packs with preset configurations:

- **Ariane 5** (`Ariane 5/`)
  - Single-segment countdown audio (French/European style)
  - Configuration: `singleStageDelay = 0.3` seconds

- **Delta IV** (`Delta IV/`)
  - Multi-segment countdown audio (p1/p2 format, United Launch Alliance style)
  - Configuration: `multiStageDelay = 5.0` seconds

- **Taiyuan** (`Taiyuan/`)
  - Single-segment countdown audio (Chinese, Taiyuan Satellite Launch Center)
  - Configuration: `singleStageDelay = 0.3` seconds

### Other Additions

- Added `preset.cfg.example` at the project root — a global default preset configuration example demonstrating the standard `preset.cfg` format with default values (`singleStageDelay = 2.0`, `multiStageDelay = 0.3`).

### Improvements

#### Improved Engine-Ignition Detection

- `LaunchSafetyChecker.IsAnyEngineRunning()` now correctly detects engines that are ignited even when throttle is 0.
- Detection condition changed from `finalThrust > 0.01f` to `EngineIgnited || finalThrust > 0.01f`.
- This prevents premature throttle-up during countdown when a player has manually ignited the engine but kept throttle at 0%.

#### Refined Engine-Already-Running Strategy

- When the core-stage engine is already running before countdown starts:
  - Throttle is held at 0% during the countdown audio playback.
  - At the end of the audio, throttle is set to 100%.
  - If the **"Start engine before separation"** checkbox is enabled, the mod performs staging after the configured delay.
  - If the checkbox is **not** enabled, no automatic staging is performed — separation remains under player manual control.

---

## v0.2.0 - June 22, 2026

### New Features

#### Volume Control

- Added an in-menu volume slider (0% ~ 100%) for countdown voice audio.
- Volume is automatically saved to the current save-game config: `saves/<save_name>/KSPLaunchCountdown/Settings.cfg`.
- Volume changes apply in real time, even while a countdown is playing.

#### Multi-language i18n Support

- Added localization system with automatic detection of the current KSP language.
- Added language files for:
  - Simplified Chinese (`zh-cn`)
  - English (`en-us`)
  - Russian (`ru-ru`)
- Falls back to English if a translation key is missing for the current language.
- All UI text in the countdown menu is now localized.

#### Pre-Launch Safety Checks

- Added automatic safety checks before starting a countdown:
  - Vessel must be on the launch pad (altitude < 100m and velocity near zero).
  - No other countdown may already be in progress.
  - Vessel electric charge must be at least 5% of total capacity.
  - Detects whether the core-stage engine is already running.
- If checks fail, a yellow warning is shown in the menu with a "Force Launch" option to bypass checks.
- Added `OnSafetyCheckFailed` event so the UI always reflects failed checks regardless of code path.

#### Engine-Already-Running Handling

- When the core-stage engine is already running, the mod uses a "hold-down" strategy:
  - Throttle is held at 0% during the countdown audio.
  - At the end of the audio, throttle is set to 100% only.
  - No automatic staging is performed, leaving separation under player control.

#### SAS Retry & MJ/Power Loss Detection

- Before launch, the mod attempts to enable SAS up to 3 times.
- If SAS still fails after 3 attempts:
  - With normal electric charge: assumed to be controlled by MechJeb or another autopilot mod; staging proceeds normally.
  - With low electric charge: assumed to be a power loss; the launch sequence is aborted.

### Bug Fixes

#### Fixed Intermittent Toolbar Icon Load Failure

- Fixed an issue where the toolbar icon would fail to load after scene changes.
- Root cause: `GameDatabase` may not be fully ready when a new `ToolbarButton` instance immediately tried to load the icon.
- Changes:
  - Added a null check for `GameDatabase.Instance` before loading.
  - Changed the failure log from `Error` to `Warning` and clarified that a retry will happen on the next frame.
  - Removed `Destroy(iconTexture)` from cleanup to avoid breaking GameDatabase's internal reference.

---

## v0.1.1 - June 22, 2026

### Bug Fixes

#### Fixed Toolbar Button Not Displaying

- Fixed an issue where the toolbar button did not appear in the flight scene.
- Root cause: a local stub type `KSPLaunchCountdown.ApplicationLauncher` was shadowing the real `KSP.UI.Screens.ApplicationLauncher`, causing `Ready=false` and `Instance=null` at runtime.
- Removed the local stub and switched to direct calls to `KSP.UI.Screens.ApplicationLauncher`.
- Added `UnityEngine.AnimationModule.dll` reference to resolve indirect compile dependencies.

---

## v0.1.0 - June 21, 2026

### New Features

#### Core Countdown System

- Flight-scene addon entry point via `[KSPAddon(Startup.Flight, ...)]`.
- Automated launch sequence: hide UI → enable SAS → full throttle → play countdown audio → stage → wait 3 seconds → restore UI.
- Support for single-segment and multi-segment (p1/p2) audio presets.
- "Start engine before separation" option for staged rockets that need ignition before decoupling.
- `Ctrl+L` shortcut to toggle the menu.

#### Preset Voice Pack System

- `PresetManager` auto-scans subdirectories under `Lauch Voice/`.
- Each preset includes its own `preset.cfg` and `.ogg` audio files.
- Built-in voice packs:
  - **LM-1(70s Jiuquan)** — Long March 1, Chinese voice (single-segment)
  - **LM-2F(Jiuquan)** — Long March 2F, Chinese voice (single-segment)
  - **Saturn V** — Apollo-era, English voice (single-segment)
  - **Soyuz** — Russian voice (single-segment)
  - **Space shuttle** — NASA Shuttle, English voice (multi-segment p1/p2)
  - **SpaceX** — Modern SpaceX, English voice (single-segment)
  - **Starship** — SpaceX Starship, English voice (multi-segment p1/p2)
  - **Wenchang** — Wenchang Launch Center, Chinese voice (single-segment)
  - **Xichang** — Xichang Launch Center, Chinese voice (single-segment)

#### Audio Player

- Loads `.ogg` files via KSP `GameDatabase`.
- Uses Unity `AudioSource` for playback with completion callbacks.

#### Launch Sequence Executor

- Enables SAS, sets throttle, and triggers staging.
- Uses `OnFlyByWire` callback to maintain throttle state against other inputs.
- Staging via `keybd_event` spacebar simulation for maximum reliability.

#### Countdown Menu UI

- IMGUI window for preset selection and launch control.
- Toolbar button integration with `ApplicationLauncher`.
- Custom mod icon (38x38 pixels).

---

## Known Issues

- Audio preview is not yet implemented; the volume slider adjusts playback volume directly.
- Japanese and Traditional Chinese translations are not yet available.
- HUD countdown display is planned for a future release.

---

## Compatibility

- **KSP Version**: 1.12.x
- **.NET Framework**: 4.7.2
- **Platforms**: Windows, Linux, macOS (via KSP's Mono runtime)

---

## Installation

1. Copy `GameData/KSPLaunchCountdown/` into your KSP `GameData/` folder.
2. Ensure `KSPLaunchCountdown.dll` is inside `GameData/KSPLaunchCountdown/`.
3. Launch KSP and enter a flight scene. The toolbar icon should appear after a few frames.

---

## License

This project is licensed under the GNU General Public License v3.0 (GPL v3).