# TouchController

Touch gesture recognition for Unity on Android, iOS and Windows touch devices (such as Microsoft Surface), built on the Input System.

- **Platform conventions:** thresholds such as touch slop and long-press time are set in density-independent units (dp) and follow each platform's own guidelines.
- **Accessibility:** where the operating system exposes its values, it takes priority. On Android this includes the user's "Touch & hold delay"; on Windows, the double-click time.
- **One settings asset:** a single `TouchSettings` ScriptableObject holds every threshold and the lineup of gestures to run.
- **Gestures, not intentions:** gestures go through a state machine (Possible, Enter, Stay, Exit, Fail), and several can recognise the same touch. Your own code decides what a gesture *means* in context.
- **Testable:** a testing assembly lets you script touches and test gestures and your own context code in EditMode, without a device.

> **Preview:** version 0.1.0 has not yet been tested on devices.

## Requirements

- Unity 6000.3 or later.
- Input System 1.20.0 or later, with *Active Input Handling* set to **Input System Package** or **Both**.

## Installation

In the Package Manager choose **+ → Install package from git URL…** and enter:

```
https://github.com/DavidWilliamHunter/TouchController.git
```

To pin a version, add a tag, for example `https://github.com/DavidWilliamHunter/TouchController.git#v0.2.0`.

You can also add it to `Packages/manifest.json` directly:

```json
"com.threads.touchcontroller": "https://github.com/DavidWilliamHunter/TouchController.git#v0.2.0"
```

## Samples

Import these from the Package Manager: select TouchController, open the **Samples** tab and choose **Import**.

- **Demo Scenes**: `SurfaceDemo` and `AndroidDemo`. Each shows finger markers, lets you switch gestures on and off, lists gesture events as they happen, and has a card you can drag, pinch, rotate, fling, double-tap to reset and long-press to highlight. Add a scene to your build profile to try it on a device. *Threads → TouchController* rebuilds the scenes.
- **Default Settings**: editable copies of the shipped `TouchSettings` for Android, iOS and Windows. The package's own copies (in `Runtime/Resources`) are read-only. Assign one to `TouchSystemOptions.Settings`, or to the demo's *Settings* field, to change thresholds or the gesture lineup.

## Quick start

```csharp
using Threads.TouchController;
using UnityEngine;

public class AppBootstrap : MonoBehaviour
{
    private TouchSystem _touch;

    private void Awake() => _touch = new TouchSystem(new TouchSystemOptions());

    private void OnEnable()
    {
        _touch.Startup();
        _touch.Get<DoubleTapGesture>().Enter += e => Debug.Log($"Double tap at {e.PositionDp} dp");
    }

    private void Update() => _touch.Update();

    private void OnApplicationFocus(bool hasFocus)
    {
        if (hasFocus && _touch.IsRunning) _touch.Refresh(); // pick up changed accessibility settings
    }

    private void OnDisable() => _touch.Shutdown();
}
```

With no settings asset assigned, the shipped default for the running platform is used.

## Documentation

See [Documentation~/Usage.md](Documentation~/Usage.md). It covers settings, units, reading raw touches, Unity UI, each built-in gesture, writing your own gestures, an example that turns gestures into intentions, and testing.

## Tests

To run the package's tests in your project, add it to `testables` in `Packages/manifest.json`:

```json
"testables": [ "com.threads.touchcontroller" ]
```

The serialisation tests also need `"com.unity.nuget.newtonsoft-json": "3.2.2"` in your dependencies. The package itself doesn't use Newtonsoft.

## Licence

[MIT](LICENSE.md)
