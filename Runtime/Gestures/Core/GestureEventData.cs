using System.Collections.Generic;
using UnityEngine;

namespace Threads.TouchController
{
    /// <summary>
    /// What a gesture event reports. A snapshot of the instance at the time of the event, plus the
    /// changes since the instance's previous event. Positions are screen space, origin bottom-left.
    /// </summary>
    public readonly struct GestureEventData
    {
        internal GestureEventData(IGesture gesture, GestureInstance instance, GestureEventType type, GestureReason reason,
            double time, float pixelsPerDp, Vector2 deltaDp, float scaleDelta, float angleDeltaDeg)
        {
            Gesture = gesture;
            Instance = instance;
            Type = type;
            Reason = reason;
            Time = time;
            StartTime = instance.StartTime;
            InstanceId = instance.Id;
            FingerId = instance.PrimaryFingerId;
            InputTouchId = instance.PrimaryInputTouchId;
            FingerCount = instance.FingerIds.Count;
            PositionDp = instance.PositionDp;
            PositionPx = instance.PositionDp * pixelsPerDp;
            DeltaDp = deltaDp;
            VelocityDps = instance.VelocityDps;
            Scale = instance.Scale;
            ScaleDelta = scaleDelta;
            AngleDeg = instance.AngleDeg;
            AngleDeltaDeg = angleDeltaDeg;
            TapCount = instance.TapCount;
            Direction = instance.Direction;
            Edge = instance.Edge;
        }

        public IGesture Gesture { get; }

        /// <summary>The instance itself (live: its values keep changing after this event).</summary>
        public GestureInstance Instance { get; }

        public GestureEventType Type { get; }

        /// <summary>Why it failed or exited; <see cref="GestureReason.None"/> for Possible, Enter and Stay.</summary>
        public GestureReason Reason { get; }

        /// <summary>Time of the event (seconds).</summary>
        public double Time { get; }

        /// <summary>Time the instance became Possible (seconds).</summary>
        public double StartTime { get; }

        /// <summary>Unique within the TouchSystem, across all gestures.</summary>
        public int InstanceId { get; }

        /// <summary>The instance's primary (first) finger, as <see cref="TouchPoint.touchId"/>.</summary>
        public int FingerId { get; }

        /// <summary>The primary finger's Input System touch ID, for Unity APIs such as EventSystem.IsPointerOverGameObject. 0 if not from the Input System.</summary>
        public int InputTouchId { get; }

        /// <summary>All fingers in the instance (live list; copy it to keep it).</summary>
        public IReadOnlyList<int> FingerIds => Instance.FingerIds;

        public int FingerCount { get; }

        public Vector2 PositionDp { get; }

        /// <summary>Position in pixels, for raycasts and UI.</summary>
        public Vector2 PositionPx { get; }

        /// <summary>Movement since this instance's previous event (dp). Zero for Possible.</summary>
        public Vector2 DeltaDp { get; }

        public Vector2 VelocityDps { get; }

        /// <summary>Scale since the start (pinch).</summary>
        public float Scale { get; }

        /// <summary>Scale factor since this instance's previous event (multiply to apply). 1 when unchanged.</summary>
        public float ScaleDelta { get; }

        /// <summary>Rotation since the start (degrees).</summary>
        public float AngleDeg { get; }

        /// <summary>Rotation since this instance's previous event (degrees).</summary>
        public float AngleDeltaDeg { get; }

        public int TapCount { get; }

        public Vector2 Direction { get; }

        public ScreenEdge Edge { get; }

        public override string ToString() =>
            Reason == GestureReason.None
                ? $"{Gesture?.Name} #{InstanceId} {Type}"
                : $"{Gesture?.Name} #{InstanceId} {Type}({Reason})";
    }
}
