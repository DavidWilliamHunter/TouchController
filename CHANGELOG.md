# Changelog

All notable changes to this package are documented here. The format follows [Keep a Changelog](https://keepachangelog.com/en/1.1.0/) and the package uses [Semantic Versioning](https://semver.org/).

## [0.4.1] - 2026-11-13

A preview build for device testing: gestures as Input System actions. It may still change before it is released as tested.

### Added

- **`TouchGestures`**, an Input System device (layout `TouchGestures`) for binding actions to gestures. The `Touch Gestures` device in the actions editor's binding picker has:
  - pulse buttons: `tap`, `doubleTap`, `twoFingerTap`, `swipeLeft` / `Right` / `Up` / `Down`;
  - held buttons: `longPress`, `edgeSwipeLeft` / `Right` / `Bottom` / `Top`, `dragging`, `pinching`, `rotating`, `twoFingerPanning`;
  - per-update deltas: `drag`, `twoFingerPan` (pixels), `pinch` (scale factor − 1), `rotate` (degrees);
  - `position` and `swipe`.
- `TouchGesturesDevice`: adds and removes the device. It collects a frame's gestures and sends them at the next Input System update, so each pulse is down for exactly one update. It takes an optional usage, to tell several devices apart.
- `GestureInputOutput`: feeds the device from the running gesture lineup through shared intentions. An area and exclusive intentions (joysticks, dragged frames) apply. `GestureInputOptions` sets the gestures, the area, the priority and `TapWaitsForDoubleTap`.
- `IntentionRunner` gains:
  - **Create Touch Gestures**, with **Touch Gesture Kinds**, **Touch Gestures Ignore UI** and **Tap Waits For Double Tap**;
  - the `TouchGestures` and `GestureInput` properties.
- Usage.md §7d.
- Demo Scenes sample: a **Gesture Actions Demo** scene that drives a card through Input System actions only.

## [0.4.0] - 2026-11-12

Touch joysticks, and Unity UI controls inside dragged frames: the 0.3.1 preview, released after testing. No code changes since 0.3.1.

## [0.3.1] - 2026-11-12

A preview build for device testing: touch joysticks, and Unity UI controls inside dragged frames. It may still change before it is released as tested.

### Added

- **Touch joysticks.** `TouchJoystick` comes in two modes: fixed, or floating (it appears under the thumb; optionally `Follow`). It responds at touch-down, has a radial dead zone and a response curve, and follows one finger at a time. Read `Value`, `IsHeld`, `OriginDp` and `ThumbDp`, or subscribe to `Pressed`, `Changed` and `Released`.
- **`TouchSticks`**, an Input System device (layout `TouchSticks`, `leftStick` / `rightStick` with usages `Primary2DMotion` / `Secondary2DMotion`), with `TouchSticksDevice` (adds and removes it) and `JoystickInputOutput` (writes a joystick to a stick). Actions bound to `<TouchSticks>/leftStick` read the joysticks, so input code needs no changes.
- `TouchJoystickBehaviour`: a joystick set up in the inspector, with an optional base and knob, and UnityEvents. `IntentionRunner` gains **Create Touch Sticks**.
- `Intention.StartWhen(condition)`: start only if the condition holds when the gesture enters. If it doesn't, the instance is rejected with the new reason `StartConditionFailed`, before claiming the finger. Use it for axis-lock.
- `UiRaycastArea.DragOwners(eventSystem, root)`: where a drag starts on a UI control that handles drags itself (Slider, Scrollbar, ScrollRect, input fields). Use `Area.Not(...)` to leave those controls out of a frame drag. `IntentionBehaviour` gains **Exclude Drag Owners** (off by default).
- Usage.md §7b (Unity UI's controls inside a dragged frame) and §7c (touch joysticks).
- Demo Scenes sample: the frame demo now has a slider, a list and a text field. A new **Joystick Demo** scene drives a ship through Input System actions only.

## [0.3.0] - 2026-11-12

The intention system, after device tests on a Surface and on Android. No code changes since 0.2.2.

## [0.2.2] - 2026-11-12

A preview build for device testing. The intention system is new and may still change before 0.3.0.

### Added

- `TouchSystem.TryGetRuntime(asset)`: finds the running copy of a gesture asset.
- Intentions (preview), in the `Threads.TouchController.Intentions` namespace:
  - **`Intention`**: you define it in code. You bind it to gestures and can limit it to an area of the screen. It sends `Possible`, `Rejected`, `Started`, `Performed` and `Ended` events.
  - **`IntentionSystem`**: decides between intentions that want the same finger. It uses each intention's priority and whether it shares its fingers or claims them (`Shared`/`Exclusive`). An intention can also wait for another gesture to fail (`RequireFailure`) or start only while one is active (`RequireActive`).
  - **Areas:** `ScreenRectArea` (in dp, pixels or a fraction of the screen, or anchored like a UI element), `FuncArea`, and ways to combine them (`Area.Not`, `Area.All`, `Area.Any`).
- Testing assembly: `IntentionSystemHarness` and `IntentionEventLog`, for testing intention code without a device.
- Unity helpers for intentions:
  - Areas from a `RectTransform`, a Unity UI raycast (`UiRaycastArea`), and a physics raycast (`PhysicsRaycastArea`). The physics area checks 3D colliders, 2D colliders or both, and with both, the hit nearest the camera wins. Each half compiles only when its physics module is installed.
  - For Unity UI: `EventSystemSync`, which sets uGUI's drag threshold from the touch slop; `IntentionDragRelay`, which lets uGUI cancel a button press when a frame drag starts; and `UiPointerIntention`, which presses UI elements through the intention system.
- Other input for intentions: `IntentionSource`, which you drive by hand (mouse, keyboard, network, tests), and `InputActionSource`, which drives intentions from Input System actions (a pointer press with a position, or a stick).
- Intentions in the inspector: `IntentionRunner`, a bootstrap component that owns the touch and intention systems, and `IntentionBehaviour`, an intention set up in the inspector, with UnityEvents.
- Instances of the same intention never block or preempt each other. One intention bound to two-finger pan, pinch and rotate runs all three together.
- Demo Scenes sample: the card in the Surface and Android demos now runs on intentions.
- Demo Scenes sample: an **Intentions Frame Demo** scene. A frame can be dragged from anywhere, even from its buttons, and a tap on a button still presses it. A switch at the top changes whether uGUI or the intention system presses the buttons.

### Fixed

- Gestures: when an event handler threw an exception, the handlers subscribed after it on the same event were not called. Each handler is now called on its own; an exception is still logged.
- Pinch measured the finger spread as half the fingers' distance apart, but compared it with Android's minimum, which is a full distance. So a pinch needed the fingers about 340 dp apart to start, and ended as soon as they came closer, cutting short a zoom-out. The spread is now the distance apart, as on Android.
- Touch input: when the Input System updated more than once per frame, a touch's Began, Moved or Ended could be missed, and a touch whose Began was missed never started any gesture. `TouchSource` now works out phases from what it has seen: first sight is Began, a position change is Moved, and a touch that disappears is Ended.
- Gestures: when a handler disabled its gesture, the handlers subscribed after it still received the event, after `OnDisabled`, so they saw an instance that had already been discarded. They now get only `OnDisabled`.

### Changed

- The package now depends on Unity UI (`com.unity.ugui` 2.0.0) as well as the Input System. In Unity 6, Unity UI ships with the editor.

## [0.2.1] - 2026-11-11

### Changed

- Demo Scenes sample: the gesture panel has a **Detected** column that counts recognitions and stays lit until **Reset detected**. Short gestures such as taps and swipes no longer flash past unnoticed. The live state text is shorter so the column fits.

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
