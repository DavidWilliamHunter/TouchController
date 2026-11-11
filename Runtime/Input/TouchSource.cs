using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem.EnhancedTouch;
using EnhancedTouch = UnityEngine.InputSystem.EnhancedTouch.Touch;
using InputTouchPhase = UnityEngine.InputSystem.TouchPhase;

namespace Threads.TouchController
{
    /// <summary>
    /// Adapter from the Input System's <c>EnhancedTouch</c> API to <see cref="TouchFrame"/>s in dp.
    /// The only class in the package that reads touch input directly.
    /// </summary>
    /// <remarks>
    /// <para>Contacts get a session-unique <see cref="TouchPoint.touchId"/>. Input System touch IDs are
    /// only unique per device, and a Surface in the Editor has both a real touchscreen and the simulated
    /// one used by <c>TouchSimulation</c>, whose IDs can collide.</para>
    /// <para><c>EnhancedTouchSupport.Enable</c> is reference-counted, so enabling here does not interfere
    /// with other code using it. <c>TouchSimulation</c> is not: it is only disabled again if this source
    /// enabled it, and is enabled again on the next <see cref="Enable"/> if it is off.</para>
    /// </remarks>
    public sealed class TouchSource : ITouchInput, IDisposable
    {
        private readonly bool _simulateTouchInEditor;
        private readonly Dictionary<(int deviceId, int touchId), int> _ids = new Dictionary<(int, int), int>();
        private readonly List<(int deviceId, int touchId)> _ended = new List<(int, int)>();
        private int _nextId = 1;
        private bool _enabled;
        private bool _ownsSimulation;

        /// <param name="simulateTouchInEditor">In the Editor, turn mouse / pen input into touches (Input System <c>TouchSimulation</c>).</param>
        public TouchSource(bool simulateTouchInEditor = true)
        {
            _simulateTouchInEditor = simulateTouchInEditor;
        }

        public bool IsEnabled => _enabled;

        public void Enable()
        {
            if (_enabled) return;
            EnhancedTouchSupport.Enable();
#if UNITY_EDITOR
            // TouchSimulation.Disable() only disables the instance (it is not destroyed), so check
            // "enabled", not just "exists": otherwise a restart would leave simulation off.
            var simulation = TouchSimulation.instance;
            if (_simulateTouchInEditor && (simulation == null || !simulation.enabled))
            {
                TouchSimulation.Enable();
                _ownsSimulation = true;
            }
#endif
            _enabled = true;
        }

        public void Disable()
        {
            if (!_enabled) return;
            if (_ownsSimulation)
            {
                TouchSimulation.Disable();
                _ownsSimulation = false;
            }
            EnhancedTouchSupport.Disable();
            _ids.Clear();
            _ended.Clear();
            _enabled = false;
        }

        public void Dispose() => Disable();

        /// <summary>Fills <paramref name="frame"/> with the current touches.</summary>
        public void Capture(TouchFrame frame, float pixelsPerDp, double time, Vector2 screenSizePx)
        {
            if (frame == null) throw new ArgumentNullException(nameof(frame));
            if (!_enabled) throw new InvalidOperationException("TouchSource is not enabled. Call Enable() first.");

            // Contacts that ended in the previous capture release their IDs now.
            foreach (var key in _ended)
                _ids.Remove(key);
            _ended.Clear();

            frame.Reset(time, pixelsPerDp, screenSizePx / pixelsPerDp);

            foreach (var touch in EnhancedTouch.activeTouches)
            {
                if (!TryMapPhase(touch.phase, out var phase))
                    continue;

                var key = (touch.screen.deviceId, touch.touchId);
                if (!_ids.TryGetValue(key, out var id) || phase == TouchPointPhase.Began)
                {
                    id = _nextId++;
                    _ids[key] = id;
                }
                if (phase == TouchPointPhase.Ended || phase == TouchPointPhase.Canceled)
                    _ended.Add(key);

                frame.Add(new TouchPoint(
                    id,
                    phase,
                    touch.screenPosition / pixelsPerDp,
                    touch.startScreenPosition / pixelsPerDp,
                    touch.startTime,
                    touch.touchId,
                    touch.screen.deviceId));
            }
        }

        internal static bool TryMapPhase(InputTouchPhase phase, out TouchPointPhase mapped)
        {
            switch (phase)
            {
                case InputTouchPhase.Began: mapped = TouchPointPhase.Began; return true;
                case InputTouchPhase.Moved: mapped = TouchPointPhase.Moved; return true;
                case InputTouchPhase.Stationary: mapped = TouchPointPhase.Stationary; return true;
                case InputTouchPhase.Ended: mapped = TouchPointPhase.Ended; return true;
                case InputTouchPhase.Canceled: mapped = TouchPointPhase.Canceled; return true;
                default: mapped = default; return false;
            }
        }
    }
}
