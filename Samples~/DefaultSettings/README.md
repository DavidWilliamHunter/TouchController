# Default Settings

Editable copies of the `TouchSettings` assets that ship with TouchController, one for each platform. Each asset holds every threshold (in dp and ms) and the gesture lineup. Its **Notes** field gives the source of each value and marks the ones that are still guesses.

## Using one

Assign the asset you want when creating the system:

```csharp
[SerializeField] private TouchSettings touchSettings;   // drag e.g. TouchSettings_Android here

_touch = new TouchSystem(new TouchSystemOptions { Settings = touchSettings });
```

With no asset assigned, the package's own copy for the running platform is used.

If **Policy → Use OS Values** is on, values the operating system provides still replace the asset's values at runtime. This includes the user's accessibility settings. Turn it off to test exactly the values in the asset.

## Gesture lineup

The lineup refers to the gesture assets inside the package, which hold no values themselves. You can change the order, turn gestures off, or add your own gesture assets (*Create → Threads → TouchController → Gestures*, or your own `GestureBase` subclass). See the package's `Documentation~/Usage.md`.
