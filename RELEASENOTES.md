# Release Notes

## KSP Launch Countdown Mod

---

## v0.2.1 - Unreleased

### New Voice Presets

- **Soyuz** (`Soyuz/`)
  - Single-segment countdown audio with `singleStageDelay = 3.0` seconds.
- **Space Shuttle** (`Space shuttle/`)
  - Multi-segment (p1/p2) countdown audio with `multiStageDelay = 2` seconds.
- **SpaceX** (`SpaceX/`)
  - Single-segment countdown audio with `singleStageDelay = 1.0` second.
- **Wenchang** (`Wenchang/`)
  - Single-segment countdown audio with `singleStageDelay = 1.0` second.
- **Xichang** (`Xichang/`)
  - Single-segment countdown audio with `singleStageDelay = 0.5` second.

### Voice Preset Renames

- Renamed `DFH-1` → `LM-1(70s Jiuquan)` for clearer identification.
- Renamed `Shenzhou Series` → `LM-2F(Jiuquan)` for clearer identification.

### Improvements

#### Improved Engine-Ignition Detection
- `LaunchSafetyChecker.IsAnyEngineRunning()` now detects engines that are ignited even when throttle is 0.
- Detection condition changed from `finalThrust > 0.01f` to `EngineIgnited || finalThrust > 0.01f`.
- This correctly identifies a manually ignited engine with zero throttle, preventing premature throttle-up during countdown.

#### Refined Engine-Already-Running Strategy
- When the core-stage engine is already running:
  - Throttle is held at 0% during the countdown audio.
  - At the end of the audio, throttle is set to 100%.
  - If the **"Start engine before separation"** checkbox is enabled, the mod will perform staging after the configured delay.
  - If the checkbox is **not** enabled, no automatic staging is performed; separation remains under player control.

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

- **Core Countdown System**
  - Flight-scene addon entry point via `[KSPAddon(Startup.Flight, ...)]`.
  - Automated launch sequence: hide UI → enable SAS → full throttle → play countdown audio → stage → wait 3 seconds → restore UI.
  - Support for single-segment and multi-segment (p1/p2) audio presets.
  - "Start engine before separation" option for staged rockets that need ignition before decoupling.

- **Preset Voice Pack System**
  - `PresetManager` auto-scans subdirectories under `Lauch Voice/`.
  - Each preset includes its own `preset.cfg` and `.ogg` audio files.

- **Audio Player**
  - Loads `.ogg` files via KSP `GameDatabase`.
  - Uses Unity `AudioSource` for playback with completion callbacks.

- **Launch Sequence Executor**
  - Enables SAS, sets throttle, and triggers staging.
  - Uses `OnFlyByWire` callback to maintain throttle state against other inputs.

- **Countdown Menu UI**
  - IMGUI window for preset selection and launch control.
  - Toolbar button integration with `ApplicationLauncher`.
  - `Ctrl+L` shortcut to toggle the menu.

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
