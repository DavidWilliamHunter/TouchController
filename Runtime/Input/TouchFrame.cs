using System;
using System.Collections.Generic;
using UnityEngine;

namespace Threads.TouchController
{
    /// <summary>
    /// All contacts for one update, in density-independent units. This is the only input gestures
    /// see, which makes them testable with hand-written or recorded frames (no Input System needed).
    /// </summary>
    /// <remarks>
    /// Instances are reused: <see cref="TouchSource"/> refills the same frame every update.
    /// Copy what you need to keep. Every contact appears in every frame from Began to Ended/Canceled.
    /// </remarks>
    public sealed class TouchFrame
    {
        private readonly List<TouchPoint> _touches = new List<TouchPoint>();

        /// <summary>Time of this frame in seconds (Input System clock: <c>InputState.currentTime</c>).</summary>
        public double Time { get; private set; }

        /// <summary>Pixels per density-independent unit used to produce this frame.</summary>
        public float PixelsPerDp { get; private set; } = 1f;

        /// <summary>Screen size in dp.</summary>
        public Vector2 ScreenSizeDp { get; private set; }

        public IReadOnlyList<TouchPoint> Touches => _touches;

        public int Count => _touches.Count;

        /// <summary>True if any finger is on the screen (ignores contacts that ended this frame).</summary>
        public bool AnyInContact
        {
            get
            {
                for (var i = 0; i < _touches.Count; i++)
                    if (_touches[i].IsInContact) return true;
                return false;
            }
        }

        /// <summary>Number of fingers on the screen (ignores contacts that ended this frame).</summary>
        public int InContactCount
        {
            get
            {
                var count = 0;
                for (var i = 0; i < _touches.Count; i++)
                    if (_touches[i].IsInContact) count++;
                return count;
            }
        }

        /// <summary>Clears the frame and sets its time and scale.</summary>
        public void Reset(double time, float pixelsPerDp, Vector2 screenSizeDp)
        {
            if (!(pixelsPerDp > 0f) || float.IsInfinity(pixelsPerDp))
                throw new ArgumentOutOfRangeException(nameof(pixelsPerDp), pixelsPerDp, "Must be greater than 0.");
            _touches.Clear();
            Time = time;
            PixelsPerDp = pixelsPerDp;
            ScreenSizeDp = screenSizeDp;
        }

        /// <summary>Adds a contact. Each touchId may appear only once per frame.</summary>
        public void Add(TouchPoint touch)
        {
            for (var i = 0; i < _touches.Count; i++)
                if (_touches[i].touchId == touch.touchId)
                    throw new ArgumentException($"Touch {touch.touchId} is already in this frame.", nameof(touch));
            _touches.Add(touch);
        }

        public bool TryGet(int touchId, out TouchPoint touch)
        {
            for (var i = 0; i < _touches.Count; i++)
            {
                if (_touches[i].touchId != touchId) continue;
                touch = _touches[i];
                return true;
            }
            touch = default;
            return false;
        }

        public Vector2 DpToPx(Vector2 dp) => dp * PixelsPerDp;

        public float DpToPx(float dp) => dp * PixelsPerDp;
    }
}
