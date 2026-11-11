using System.Collections.Generic;
using UnityEngine;

namespace Threads.TouchController
{
    /// <summary>
    /// One occurrence of a gesture, from Possible until it fails or exits. Read-only for consumers;
    /// gestures change it through the protected helpers on <see cref="GestureBase"/>.
    /// Gestures that need extra per-instance data derive from this class
    /// (see <see cref="GestureBase{TInstance}"/>).
    /// </summary>
    public class GestureInstance
    {
        private readonly List<int> _fingerIds = new List<int>();

        /// <summary>Unique within the <see cref="TouchSystem"/> (across all gestures).</summary>
        public int Id { get; internal set; }

        public IGesture Gesture { get; internal set; }

        public GestureState State { get; internal set; }

        public bool IsActive => State == GestureState.Active;

        /// <summary>Time of the Possible event (seconds).</summary>
        public double StartTime { get; internal set; }

        /// <summary>Time of the Enter event, or -1 if not entered.</summary>
        public double EnterTime { get; internal set; } = -1.0;

        /// <summary>The first finger of the instance (<see cref="TouchPoint.touchId"/>). Kept after the finger lifts.</summary>
        public int PrimaryFingerId { get; internal set; }

        /// <summary>The primary finger's Input System touch ID, for Unity APIs (0 if not from the Input System).</summary>
        public int PrimaryInputTouchId { get; internal set; }

        /// <summary>The fingers currently part of the instance. Fingers are removed the update after they lift.</summary>
        public IReadOnlyList<int> FingerIds => _fingerIds;

        internal List<int> FingerList => _fingerIds;

        /// <summary>Position when the instance became Possible (dp; centroid for several fingers).</summary>
        public Vector2 StartPositionDp { get; internal set; }

        /// <summary>Current position (dp; centroid of the fingers unless the gesture sets it).</summary>
        public Vector2 PositionDp { get; internal set; }

        /// <summary>Current velocity (dp/s; mean of the fingers unless the gesture sets it).</summary>
        public Vector2 VelocityDps { get; internal set; }

        /// <summary>Scale factor since the start (pinch). 1 when not used.</summary>
        public float Scale { get; internal set; } = 1f;

        /// <summary>Rotation since the start, in degrees (rotate). 0 when not used.</summary>
        public float AngleDeg { get; internal set; }

        /// <summary>Number of taps (multi-tap gestures). 0 when not used.</summary>
        public int TapCount { get; internal set; }

        /// <summary>Direction (unit vector) for directional gestures such as swipes. Zero when not used.</summary>
        public Vector2 Direction { get; internal set; }

        /// <summary>The screen edge for edge gestures.</summary>
        public ScreenEdge Edge { get; internal set; }

        // Values at the previous event, for the deltas in GestureEventData.
        internal Vector2 LastEventPositionDp;
        internal float LastEventScale = 1f;
        internal float LastEventAngleDeg;
        internal bool EnteredThisFrame;

        public override string ToString() => $"{Gesture?.Name} #{Id} {State}";
    }
}
