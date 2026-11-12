using System.Collections.Generic;
using UnityEngine;

namespace Threads.TouchController.Intentions
{
    /// <summary>
    /// One occurrence of an <see cref="Intention"/>: created when a bound gesture instance becomes
    /// Possible on fingers the area accepts. Its values are live (they keep changing after an event).
    /// </summary>
    public sealed class IntentionInstance
    {
        internal readonly List<int> FingerList = new List<int>();
        internal readonly HashSet<int> EverFingers = new HashSet<int>();

        // Movement not yet reported by a Performed.
        internal Vector2 PendingDeltaDp;
        internal float PendingScaleDelta = 1f;
        internal float PendingAngleDeltaDeg;

        internal bool EnterRequested;
        internal IntentionReason? PendingEnd;
        internal List<GestureRecord> WaitingOn;
        internal double Time;
        internal float PixelsPerDp = 1f;

        internal IntentionInstance(int id, Intention intention)
        {
            Id = id;
            Intention = intention;
        }

        /// <summary>Unique within the <see cref="IntentionSystem"/>.</summary>
        public int Id { get; }

        public Intention Intention { get; }

        public IntentionState State { get; internal set; }

        public bool IsActive => State == IntentionState.Active;

        public IntentionSourceKind Source { get; internal set; }

        /// <summary>The gesture driving this instance (null for other sources).</summary>
        public IGesture Gesture { get; internal set; }

        /// <summary>The gesture instance driving this instance (0 for other sources).</summary>
        public int GestureInstanceId { get; internal set; }

        /// <summary>Time the instance became Possible (seconds).</summary>
        public double StartTime { get; internal set; }

        /// <summary>The gesture's primary finger.</summary>
        public int PrimaryFingerId { get; internal set; }

        /// <summary>The fingers it currently uses.</summary>
        public IReadOnlyList<int> FingerIds => FingerList;

        /// <summary>What the area reported as hit (e.g. a Button), or null.</summary>
        public object Target { get; internal set; }

        public Vector2 StartPositionDp { get; internal set; }
        public Vector2 PositionDp { get; internal set; }
        public Vector2 VelocityDps { get; internal set; }
        public float Scale { get; internal set; } = 1f;
        public float AngleDeg { get; internal set; }
        public int TapCount { get; internal set; }
        public Vector2 Direction { get; internal set; }
        public ScreenEdge Edge { get; internal set; }

        internal bool SharesFingerWith(IntentionInstance other)
        {
            foreach (var id in FingerList)
                if (other.FingerList.Contains(id)) return true;
            return false;
        }

        internal void ResetPending()
        {
            PendingDeltaDp = Vector2.zero;
            PendingScaleDelta = 1f;
            PendingAngleDeltaDeg = 0f;
        }

        public override string ToString() => $"{Intention?.Name} #{Id} {State}";
    }

    /// <summary>What an intention event reports: a snapshot of the instance at the time of the event.</summary>
    public readonly struct IntentionEventData
    {
        internal IntentionEventData(IntentionInstance instance, IntentionEventType type, IntentionReason reason, bool withDeltas)
        {
            Instance = instance;
            Intention = instance.Intention;
            Type = type;
            Reason = reason;
            Time = instance.Time;
            StartTime = instance.StartTime;
            InstanceId = instance.Id;
            FingerId = instance.PrimaryFingerId;
            FingerCount = instance.FingerList.Count;
            PositionDp = instance.PositionDp;
            PositionPx = instance.PositionDp * instance.PixelsPerDp;
            StartPositionDp = instance.StartPositionDp;
            VelocityDps = instance.VelocityDps;
            Scale = instance.Scale;
            AngleDeg = instance.AngleDeg;
            DeltaDp = withDeltas ? instance.PendingDeltaDp : Vector2.zero;
            DeltaPx = DeltaDp * instance.PixelsPerDp;
            ScaleDelta = withDeltas ? instance.PendingScaleDelta : 1f;
            AngleDeltaDeg = withDeltas ? instance.PendingAngleDeltaDeg : 0f;
            TapCount = instance.TapCount;
            Direction = instance.Direction;
            Edge = instance.Edge;
            Target = instance.Target;
            Source = instance.Source;
            Gesture = instance.Gesture;
        }

        public Intention Intention { get; }

        /// <summary>The live instance (its values keep changing after this event).</summary>
        public IntentionInstance Instance { get; }

        public IntentionEventType Type { get; }

        /// <summary>Why it was rejected or ended; <see cref="IntentionReason.None"/> otherwise.</summary>
        public IntentionReason Reason { get; }

        /// <summary>Time of the event (seconds, the touch clock).</summary>
        public double Time { get; }

        /// <summary>Time the instance became Possible (seconds).</summary>
        public double StartTime { get; }

        public int InstanceId { get; }

        /// <summary>The primary finger (<see cref="TouchPoint.touchId"/>).</summary>
        public int FingerId { get; }

        /// <summary>All fingers (live list; copy it to keep it).</summary>
        public IReadOnlyList<int> FingerIds => Instance.FingerIds;

        public int FingerCount { get; }

        public Vector2 PositionDp { get; }

        public Vector2 PositionPx { get; }

        public Vector2 StartPositionDp { get; }

        /// <summary>Movement since the previous Performed (dp). Only Performed carries movement; zero for every other event.</summary>
        public Vector2 DeltaDp { get; }

        /// <summary><see cref="DeltaDp"/> in pixels, for moving UI or screen-space objects.</summary>
        public Vector2 DeltaPx { get; }

        public Vector2 VelocityDps { get; }

        /// <summary>Scale since the gesture started (pinch).</summary>
        public float Scale { get; }

        /// <summary>Scale factor since the previous Performed (multiply to apply). 1 for every other event.</summary>
        public float ScaleDelta { get; }

        /// <summary>Rotation since the gesture started (degrees, CCW +).</summary>
        public float AngleDeg { get; }

        /// <summary>Rotation since the previous Performed (degrees). 0 for every other event.</summary>
        public float AngleDeltaDeg { get; }

        public int TapCount { get; }

        public Vector2 Direction { get; }

        public ScreenEdge Edge { get; }

        /// <summary>What the area reported as hit (e.g. a Button or Collider), or null.</summary>
        public object Target { get; }

        public IntentionSourceKind Source { get; }

        /// <summary>The gesture driving the instance (null for other sources).</summary>
        public IGesture Gesture { get; }

        public override string ToString() =>
            Reason == IntentionReason.None
                ? $"{Intention?.Name} #{InstanceId} {Type}"
                : $"{Intention?.Name} #{InstanceId} {Type}({Reason})";
    }
}
