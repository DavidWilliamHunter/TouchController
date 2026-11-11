# Changelog

All notable changes to this package are documented here. The format follows [Keep a Changelog](https://keepachangelog.com/en/1.1.0/) and the package uses [Semantic Versioning](https://semver.org/).

## [0.2.0] - 2026-11-11

### Added

- Samples, imported from the Package Manager's **Samples** tab:
  - **Demo Scenes**: Surface (Windows) and Android demo scenes, with menu items to rebuild them.
  - **Default Settings**: editable copies of the per-platform `TouchSettings` assets.

## [0.1.0] - 2026-11-11

First preview release. Not yet tested on devices.

### Added

- `TouchSystem`: a single start-up and shut-down point that reads touches through the Input System's EnhancedTouch API, with positions in density-independent units (dp).
- `TouchSettings`: one ScriptableObject that holds every threshold and the gesture lineup. A default asset ships for each platform (Android, iOS, Windows). Android and Windows OS values, including accessibility settings, are applied at start-up.
- Gesture state machine (Possible, Enter, Stay, Exit, Fail) with per-instance events. A gesture that is disabled sends no events.
- Built-in gestures: Press, Tap, LongPress, Drag, Swipe, DoubleTap (Android, iOS and Windows variants), Pinch, Rotate, TwoFingerPan, TwoFingerTap and EdgeSwipe.
- Testing assembly (`Threads.TouchController.Testing`) with scripted touch input and harnesses for gesture and context-code tests.
