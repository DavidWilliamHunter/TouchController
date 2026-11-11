using UnityEngine;

namespace Threads.TouchController
{
    /// <summary>Per-instance data for <see cref="DoubleTapGesture"/>.</summary>
    public sealed class DoubleTapInstance : GestureInstance
    {
        public enum Step { FirstDown, WaitingForSecond, SecondDown }

        /// <summary>Where the double tap has got to.</summary>
        public Step Progress { get; internal set; }

        /// <summary>When the first finger lifted (seconds).</summary>
        public double FirstUpTime { get; internal set; }

        /// <summary>The second touch (<see cref="TouchPoint.touchId"/>), or 0 before it.</summary>
        public int SecondFingerId { get; internal set; }

        /// <summary>When and where the second finger touched down.</summary>
        public double SecondDownTime { get; internal set; }

        public Vector2 SecondDownPositionDp { get; internal set; }
    }

    /// <summary>
    /// Base of the platform double taps. Subscribe with <c>touch.Get&lt;DoubleTapGesture&gt;()</c>,
    /// whatever the platform. The variants share every check and differ only in when they are
    /// recognised: on the second finger <b>down</b> (<see cref="AndroidDoubleTap"/>) or <b>up</b>
    /// (<see cref="IosDoubleTap"/>, <see cref="WindowsDoubleTap"/>).
    /// </summary>
    /// <remarks>
    /// <para>The checks (plan.md section 3), each with its Fail reason:</para>
    /// <list type="number">
    /// <item>The first touch is a valid tap: within the touch slop (Moved), released before the long-press time (Held).</item>
    /// <item>Time-out: the second finger goes down within <see cref="TapSettings.doubleTapTimeoutMs"/> of the
    /// first lifting (Timeout). Driven by time: fails even if nothing touches the screen. This is the
    /// "single tap confirmed" signal.</item>
    /// <item>Not too fast: the gap is at least <see cref="TapSettings.doubleTapMinTimeMs"/> (TooFast).</item>
    /// <item>Proximity: the second finger lands within <see cref="TapSettings.doubleTapSlopDp"/> of where the
    /// first went down (TooFar).</item>
    /// <item>One finger at a time: another finger during the sequence (FingerCount).</item>
    /// </list>
    /// <para>As on Android, a second touch that is too fast or too far starts a new possible double tap.
    /// The time-outs only apply before Enter.</para>
    /// </remarks>
    public abstract class DoubleTapGesture : GestureBase<DoubleTapInstance>
    {
        /// <summary>True: Enter on the second finger down and stay Active until it lifts (Android). False: Enter and Exit on the second finger up.</summary>
        protected abstract bool EntersOnSecondDown { get; }

        protected sealed override void OnFrame(TouchFrame frame)
        {
            // 1. Existing instances first, so a time-out is decided before a new touch is matched to it.
            foreach (var instance in LiveInstances())
                UpdateInstance(instance, frame);

            // 2. Touches that began this update.
            foreach (var touch in EligibleTouches)
            {
                if (touch.phase != TouchPointPhase.Began)
                    continue;
                OnTouchDown(touch, frame);
            }
        }

        private void OnTouchDown(TouchPoint touch, TouchFrame frame)
        {
            var live = LiveInstances();
            var instance = live.Count > 0 ? live[0] : null;

            if (instance != null && instance.Progress == DoubleTapInstance.Step.WaitingForSecond)
            {
                var tap = Settings.Tap;
                var gapMs = (Time - instance.FirstUpTime) * 1000.0;
                if (gapMs < tap.doubleTapMinTimeMs - TimeToleranceMs)
                {
                    FailInstance(instance, GestureReason.TooFast);
                }
                else if ((touch.positionDp - instance.StartPositionDp).magnitude > tap.doubleTapSlopDp)
                {
                    FailInstance(instance, GestureReason.TooFar);
                }
                else
                {
                    AddFinger(instance, touch.touchId);
                    instance.Progress = DoubleTapInstance.Step.SecondDown;
                    instance.SecondFingerId = touch.touchId;
                    instance.SecondDownTime = Time;
                    instance.SecondDownPositionDp = touch.positionDp;
                    SetTapCount(instance, 2);
                    if (EntersOnSecondDown)
                        EnterInstance(instance);
                    return;
                }
                // Too fast or too far: this touch may be the first tap of a new double tap.
                Begin(touch);
                return;
            }

            if (instance != null)
            {
                // Another finger while a finger of the sequence is down: not a one-finger double tap.
                if (instance.State == GestureState.Possible)
                    FailInstance(instance, GestureReason.FingerCount);
                return; // an Active (Android) double tap ignores extra fingers
            }

            // Only start when this is the only finger on the screen.
            if (frame.InContactCount == 1)
                Begin(touch);
        }

        private void Begin(TouchPoint touch) =>
            BeginPossible(touch.touchId, i =>
            {
                i.Progress = DoubleTapInstance.Step.FirstDown;
                SetTapCount(i, 1);
            });

        private void UpdateInstance(DoubleTapInstance instance, TouchFrame frame)
        {
            var slop = Settings.Motion.touchSlopDp;
            var longPress = Settings.LongPress.longPressTimeoutMs;

            switch (instance.Progress)
            {
                case DoubleTapInstance.Step.FirstDown:
                {
                    if (!frame.TryGet(instance.PrimaryFingerId, out var first)) return;
                    if ((first.positionDp - instance.StartPositionDp).magnitude > slop)
                        FailInstance(instance, GestureReason.Moved);
                    else if (first.phase == TouchPointPhase.Ended)
                    {
                        instance.Progress = DoubleTapInstance.Step.WaitingForSecond;
                        instance.FirstUpTime = Time;
                    }
                    else if (HasElapsed(instance.StartTime, longPress))
                        FailInstance(instance, GestureReason.Held);
                    break;
                }

                case DoubleTapInstance.Step.WaitingForSecond:
                    if (HasElapsed(instance.FirstUpTime, Settings.Tap.doubleTapTimeoutMs))
                        FailInstance(instance, GestureReason.Timeout);
                    break;

                case DoubleTapInstance.Step.SecondDown:
                {
                    if (!frame.TryGet(instance.SecondFingerId, out var second)) return;
                    if (instance.State == GestureState.Active)
                    {
                        // Android: the held second finger keeps it active (double-tap-then-drag), no time-out.
                        if (second.phase == TouchPointPhase.Ended)
                            ExitInstance(instance);
                        return;
                    }

                    // iOS / Windows: the second touch must also be a valid tap.
                    if ((second.positionDp - instance.SecondDownPositionDp).magnitude > slop)
                        FailInstance(instance, GestureReason.Moved);
                    else if (second.phase == TouchPointPhase.Ended)
                        RecogniseInstance(instance);
                    else if (HasElapsed(instance.SecondDownTime, longPress))
                        FailInstance(instance, GestureReason.Held);
                    break;
                }
            }
        }
    }
}
