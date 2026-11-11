using System;
using System.Collections.Generic;
using UnityEngine;

namespace Threads.TouchController.Testing
{
    /// <summary>
    /// Runs a whole <see cref="TouchSystem"/> (a platform's full gesture lineup) on scripted input, without
    /// the Input System or a device. For testing how gestures combine, and for testing context code.
    /// </summary>
    /// <example>
    /// <code>
    /// using var h = new TouchSystemHarness(TouchPlatform.Android);
    /// var drag = h.Log&lt;DragGesture&gt;();
    /// h.Input.Down(1, new Vector2(200, 400)); h.Input.Step();
    /// h.Input.Drag(1, new Vector2(300, 400), durationMs: 100);
    /// h.Input.Up(1); h.Input.Step();
    /// CollectionAssert.AreEqual(new[] { "Possible#..", ... }, drag.WithoutStays());
    /// </code>
    /// </example>
    /// <remarks>
    /// Every <see cref="ScriptedTouchInput.Step"/> runs one <see cref="TouchSystem.Update"/>. The system's
    /// pixels per dp equals the <c>pixelsPerDp</c> given here, and no OS values are read, so the
    /// thresholds are exactly the settings asset's.
    /// </remarks>
    public sealed class TouchSystemHarness : IDisposable
    {
        private readonly Dictionary<IGesture, GestureEventLog> _logs = new Dictionary<IGesture, GestureEventLog>();

        /// <param name="platform">Platform whose conventions (and default settings) apply.</param>
        /// <param name="settings">Settings asset to use (thresholds and lineup). Null: the platform's shipped default.</param>
        /// <param name="pixelsPerDp">Scale of the frames.</param>
        /// <param name="screenSizeDp">Screen size. Default 400 × 800 dp.</param>
        public TouchSystemHarness(TouchPlatform platform = TouchPlatform.Android, TouchSettings settings = null,
            float pixelsPerDp = 1f, Vector2? screenSizeDp = null)
        {
            var asset = settings != null ? settings : DefaultTouchSettings.Load(platform);
            Input = new ScriptedTouchInput(pixelsPerDp, screenSizeDp);
            var host = new ScriptedHost(platform, pixelsPerDp * asset.Data.Density.referenceDpi, Input);

            System = new TouchSystem(new TouchSystemOptions
            {
                Settings = asset,
                Platform = platform,
                Host = host,
                Input = Input,
                OsProvider = new NullOsTouchConfigProvider(),
            });
            System.Startup();
            Input.FrameProduced += OnFrame;
        }

        /// <summary>The running system.</summary>
        public TouchSystem System { get; }

        /// <summary>Scripted touches. Every step updates the whole system.</summary>
        public ScriptedTouchInput Input { get; }

        /// <summary>The event log of the first running gesture of type <typeparamref name="T"/> (created on first use).</summary>
        public GestureEventLog Log<T>() where T : class, IGesture => Log(System.Get<T>());

        /// <summary>The event log of a running gesture (created on first use).</summary>
        public GestureEventLog Log(IGesture gesture)
        {
            if (!_logs.TryGetValue(gesture, out var log))
            {
                log = new GestureEventLog(gesture);
                _logs.Add(gesture, log);
            }
            return log;
        }

        /// <summary>All broken event rules across every logged gesture.</summary>
        public List<string> Violations()
        {
            var all = new List<string>();
            foreach (var log in _logs.Values)
                all.AddRange(log.Violations);
            return all;
        }

        public void Dispose()
        {
            Input.FrameProduced -= OnFrame;
            foreach (var log in _logs.Values)
                log.Dispose();
            _logs.Clear();
            System.Dispose();
        }

        private void OnFrame(TouchFrame frame) => System.Update();

        private sealed class ScriptedHost : ITouchHost
        {
            private readonly ScriptedTouchInput _input;

            public ScriptedHost(TouchPlatform platform, float screenDpi, ScriptedTouchInput input)
            {
                Platform = platform;
                ScreenDpi = screenDpi;
                _input = input;
            }

            public TouchPlatform Platform { get; }
            public float ScreenDpi { get; }
            public Vector2 ScreenSizePx => _input.Frame.ScreenSizeDp; // unused: scripted input keeps its own screen size
            public double Time => _input.Time;
        }
    }
}
