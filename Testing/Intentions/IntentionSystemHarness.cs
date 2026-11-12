using System;
using System.Collections.Generic;
using Threads.TouchController.Testing;
using UnityEngine;

namespace Threads.TouchController.Intentions.Testing
{
    /// <summary>
    /// Runs a <see cref="TouchSystem"/> (a platform's full gesture lineup) and an
    /// <see cref="IntentionSystem"/> on scripted input. Every <see cref="ScriptedTouchInput.Step"/> runs
    /// <c>TouchSystem.Update</c> then <c>IntentionSystem.Update</c>, as an app would.
    /// </summary>
    /// <example>
    /// <code>
    /// using var h = new IntentionSystemHarness();
    /// var drag = h.Enable(new Intention("Drag", h.Intentions).Bind&lt;DragGesture&gt;());
    /// var log = h.Log(drag);
    /// h.Input.Down(1, new Vector2(100, 100)); h.Input.Step();
    /// h.Input.Drag(1, new Vector2(200, 100), durationMs: 200);
    /// h.Input.Up(1); h.Input.Step();
    /// CollectionAssert.AreEqual(new[] { "Possible#1", "Started#1", "Ended(Completed)#1" }, log.WithoutPerformed());
    /// </code>
    /// </example>
    public sealed class IntentionSystemHarness : IDisposable
    {
        private readonly Dictionary<Intention, IntentionEventLog> _logs = new Dictionary<Intention, IntentionEventLog>();

        /// <param name="platform">Platform whose conventions (and default settings) apply.</param>
        /// <param name="settings">Settings asset (thresholds and lineup). Null: the platform's shipped default.</param>
        /// <param name="pixelsPerDp">Scale of the frames.</param>
        /// <param name="screenSizeDp">Screen size. Default 400 × 800 dp.</param>
        public IntentionSystemHarness(TouchPlatform platform = TouchPlatform.Android, TouchSettings settings = null,
            float pixelsPerDp = 1f, Vector2? screenSizeDp = null)
        {
            Touch = new TouchSystemHarness(platform, settings, pixelsPerDp, screenSizeDp);
            Intentions = new IntentionSystem(Touch.System);
            Intentions.Startup();
            Input.FrameProduced += OnFrame; // after the TouchSystemHarness's own handler
        }

        /// <summary>The touch side (system, gesture logs).</summary>
        public TouchSystemHarness Touch { get; }

        public IntentionSystem Intentions { get; }

        public ScriptedTouchInput Input => Touch.Input;

        /// <summary>Enables an intention and returns it (for one-line setup).</summary>
        public Intention Enable(Intention intention)
        {
            intention.Enabled = true;
            return intention;
        }

        /// <summary>The event log of an intention (created on first use).</summary>
        public IntentionEventLog Log(Intention intention)
        {
            if (!_logs.TryGetValue(intention, out var log))
            {
                log = new IntentionEventLog(intention);
                _logs.Add(intention, log);
            }
            return log;
        }

        /// <summary>All broken event rules across every logged intention and gesture.</summary>
        public List<string> Violations()
        {
            var all = new List<string>();
            foreach (var log in _logs.Values)
                all.AddRange(log.Violations);
            all.AddRange(Touch.Violations());
            return all;
        }

        public void Dispose()
        {
            Input.FrameProduced -= OnFrame;
            Intentions.Dispose();
            foreach (var log in _logs.Values)
                log.Dispose();
            _logs.Clear();
            Touch.Dispose();
        }

        private void OnFrame(TouchFrame frame)
        {
            if (Intentions.IsRunning)
                Intentions.Update();
        }
    }
}
