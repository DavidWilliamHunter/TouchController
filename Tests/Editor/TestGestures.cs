using System;
using System.Collections.Generic;
using UnityEngine;

// Small gestures used only to test the gesture core (GestureBase, TouchSystem lineup).
// The real gestures (Tap, Drag, ...) arrive in phase 5 with their own tests.

namespace Threads.TouchController.Tests
{
    /// <summary>Long-press-like, one instance per finger: Enter after longPressTimeoutMs (time-driven), Exit on release.</summary>
    public sealed class TestHoldGesture : GestureBase<GestureInstance>
    {
        public int DiscardCount;

        protected override void OnFrame(TouchFrame frame)
        {
            foreach (var touch in EligibleTouches)
                if (touch.phase == TouchPointPhase.Began)
                    BeginPossible(touch.touchId);

            foreach (var instance in LiveInstances())
            {
                frame.TryGet(instance.PrimaryFingerId, out var touch);
                if (instance.State == GestureState.Possible)
                {
                    if ((touch.positionDp - instance.StartPositionDp).magnitude > Settings.Motion.touchSlopDp)
                        FailInstance(instance, GestureReason.Moved);
                    else if (touch.HasEnded)
                        FailInstance(instance, GestureReason.Released);
                    else if (HasElapsed(instance.StartTime, Settings.LongPress.longPressTimeoutMs))
                        EnterInstance(instance);
                }
                else if (touch.HasEnded)
                {
                    ExitInstance(instance);
                }
            }
        }

        protected override void OnDiscard() => DiscardCount++;

        /// <summary>For tests: attempts a transition outside OnFrame.</summary>
        public void ExitOutsideFrame(GestureInstance instance) => ExitInstance(instance);
    }

    /// <summary>Tap-like, discrete: Enter + Exit on release.</summary>
    public sealed class TestTapGesture : GestureBase<GestureInstance>
    {
        protected override void OnFrame(TouchFrame frame)
        {
            foreach (var touch in EligibleTouches)
                if (touch.phase == TouchPointPhase.Began)
                    BeginPossible(touch.touchId);

            foreach (var instance in LiveInstances())
            {
                frame.TryGet(instance.PrimaryFingerId, out var touch);
                if ((touch.positionDp - instance.StartPositionDp).magnitude > Settings.Motion.touchSlopDp)
                    FailInstance(instance, GestureReason.Moved);
                else if (HasElapsed(instance.StartTime, Settings.LongPress.longPressTimeoutMs))
                    FailInstance(instance, GestureReason.Held);
                else if (touch.HasEnded)
                    RecogniseInstance(instance);
            }
        }
    }

    public sealed class PairInstance : GestureInstance
    {
        public float StartSpanDp;
    }

    /// <summary>Pinch-like, one instance for all fingers: Possible at 2 fingers, Enter when the spread changes, scale while active.</summary>
    public sealed class TestPairGesture : GestureBase<PairInstance>
    {
        private readonly List<int> _fingers = new List<int>();

        protected override void OnFrame(TouchFrame frame)
        {
            _fingers.Clear();
            foreach (var touch in EligibleTouches)
                if (touch.IsInContact)
                    _fingers.Add(touch.touchId);

            var live = LiveInstances();
            var instance = live.Count > 0 ? live[0] : null;

            if (instance == null)
            {
                if (_fingers.Count >= 2)
                {
                    instance = BeginPossible(_fingers);
                    instance.StartSpanDp = Span(frame, instance);
                }
                return;
            }

            foreach (var id in _fingers)
                AddFinger(instance, id); // late fingers join

            var inContact = 0;
            foreach (var id in instance.FingerIds)
                if (frame.TryGet(id, out var t) && t.IsInContact) inContact++;

            if (inContact < 2)
            {
                if (instance.State == GestureState.Possible) FailInstance(instance, GestureReason.FingerCount);
                else ExitInstance(instance);
                return;
            }

            var span = Span(frame, instance);
            if (instance.State == GestureState.Possible && Mathf.Abs(span - instance.StartSpanDp) > 2f * Settings.Motion.touchSlopDp)
            {
                SetScale(instance, span / instance.StartSpanDp); // set values before the event that reports them
                EnterInstance(instance);
            }
            else if (instance.State == GestureState.Active)
            {
                SetScale(instance, span / instance.StartSpanDp);
            }
        }

        // Mean distance of the fingers in contact from the instance position (their centroid).
        private static float Span(TouchFrame frame, GestureInstance instance)
        {
            float sum = 0f;
            var count = 0;
            var centre = Vector2.zero;
            foreach (var id in instance.FingerIds)
                if (frame.TryGet(id, out var t) && t.IsInContact) { centre += t.positionDp; count++; }
            centre /= Mathf.Max(1, count);
            foreach (var id in instance.FingerIds)
                if (frame.TryGet(id, out var t) && t.IsInContact) sum += (t.positionDp - centre).magnitude;
            return count > 0 ? sum / count : 0f;
        }
    }

    /// <summary>Breaks the rules on purpose when a touch begins.</summary>
    public sealed class TestMisuseGesture : GestureBase<GestureInstance>
    {
        public enum Misuse { ExitFromPossible, FailWithCompleted, EnterTwice, ExitWithMoved, IneligibleFinger, EmptyFingers }

        public Misuse Kind;

        protected override void OnFrame(TouchFrame frame)
        {
            foreach (var touch in EligibleTouches)
            {
                if (touch.phase != TouchPointPhase.Began) continue;
                switch (Kind)
                {
                    case Misuse.ExitFromPossible:
                        ExitInstance(BeginPossible(touch.touchId));
                        break;
                    case Misuse.FailWithCompleted:
                        FailInstance(BeginPossible(touch.touchId), GestureReason.Completed);
                        break;
                    case Misuse.EnterTwice:
                        var instance = BeginPossible(touch.touchId);
                        EnterInstance(instance);
                        EnterInstance(instance);
                        break;
                    case Misuse.ExitWithMoved:
                        var entered = BeginPossible(touch.touchId);
                        EnterInstance(entered);
                        ExitInstance(entered, GestureReason.Moved);
                        break;
                    case Misuse.IneligibleFinger:
                        BeginPossible(touch.touchId + 1000);
                        break;
                    case Misuse.EmptyFingers:
                        BeginPossible(Array.Empty<int>());
                        break;
                }
            }
        }
    }
}
