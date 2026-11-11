using System;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Threads.TouchController.Testing
{
    /// <summary>
    /// Runs one gesture on scripted input, without a TouchSystem or the Input System. For testing
    /// gestures (the package's own or yours) and context code.
    /// </summary>
    /// <example>
    /// <code>
    /// using var harness = new GestureHarness&lt;MyGesture&gt;();
    /// harness.Input.Down(1, new Vector2(100, 100)); harness.Input.Step();
    /// harness.Input.Up(1);                           harness.Input.Step();
    /// CollectionAssert.AreEqual(new[] { "Possible#1", "Enter#1", "Exit(Completed)#1" }, harness.Log.WithoutStays());
    /// Assert.IsEmpty(harness.Log.Violations);
    /// </code>
    /// </example>
    public sealed class GestureHarness<T> : IDisposable where T : GestureBase
    {
        /// <param name="gestureAsset">Asset to copy (its serialized settings are used). Null: a new instance with default settings.</param>
        /// <param name="platform">Whose shipped thresholds to use (copied; change them through <see cref="Settings"/>).</param>
        /// <param name="pixelsPerDp">Scale of the produced frames.</param>
        /// <param name="enabled">Whether the gesture starts enabled.</param>
        public GestureHarness(T gestureAsset = null, TouchPlatform platform = TouchPlatform.Android, float pixelsPerDp = 1f, bool enabled = true)
        {
            Settings = new TouchSettingsData();
            Settings.CopyFrom(DefaultTouchSettings.Load(platform).Data);
            History = new TouchHistory(Settings.Fling);
            Context = new GestureContext(Settings, History);

            Gesture = gestureAsset != null ? Object.Instantiate(gestureAsset) : ScriptableObject.CreateInstance<T>();
            Gesture.name = typeof(T).Name;
            Gesture.hideFlags = HideFlags.DontSave;
            Gesture.Initialise(Context, enabled);

            Log = new GestureEventLog(Gesture);
            Input = new ScriptedTouchInput(pixelsPerDp);
            Input.FrameProduced += OnFrame;
        }

        /// <summary>The running gesture.</summary>
        public T Gesture { get; }

        /// <summary>Scripted touches. Every <see cref="ScriptedTouchInput.Step"/> updates the history and the gesture.</summary>
        public ScriptedTouchInput Input { get; }

        /// <summary>Everything the gesture sent, and any broken event rules.</summary>
        public GestureEventLog Log { get; }

        /// <summary>The thresholds the gesture reads (a private copy; change freely).</summary>
        public TouchSettingsData Settings { get; }

        public TouchHistory History { get; }

        public GestureContext Context { get; }

        public void Dispose()
        {
            Input.FrameProduced -= OnFrame;
            Log.Dispose();
            if (Gesture != null)
            {
                if (Application.isPlaying)
                    Object.Destroy(Gesture);
                else
                    Object.DestroyImmediate(Gesture);
            }
        }

        private void OnFrame(TouchFrame frame)
        {
            History.Update(frame);
            Gesture.UpdateGesture(frame);
        }
    }
}
