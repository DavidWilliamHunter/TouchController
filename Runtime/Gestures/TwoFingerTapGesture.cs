using System.Collections.Generic;
using UnityEngine;

namespace Threads.TouchController
{
    /// <summary>Per-instance data for <see cref="TwoFingerTapGesture"/>.</summary>
    public sealed class TwoFingerTapInstance : GestureInstance
    {
        internal readonly List<int> Fingers = new List<int>();
        internal readonly Dictionary<int, Vector2> DownPositions = new Dictionary<int, Vector2>();
        internal int Lifted;
    }

    /// <summary>
    /// Two-finger tap (discrete). Possible when a finger touches down on an empty screen; the second finger
    /// must follow within <see cref="TapSettings.multiFingerTapWindowMs"/>. Recognised (Enter and Exit) when
    /// both have lifted. Fails with Timeout (no second finger in time), Released (the first lifted before the
    /// second arrived), Moved (a finger moved beyond the touch slop), Held (longer than the long-press time)
    /// or FingerCount (a third finger).
    /// </summary>
    [CreateAssetMenu(fileName = "TwoFingerTap", menuName = "Threads/TouchController/Gestures/Two Finger Tap")]
    public sealed class TwoFingerTapGesture : GestureBase<TwoFingerTapInstance>
    {
        protected override void OnFrame(TouchFrame frame)
        {
            foreach (var touch in EligibleTouches)
            {
                if (touch.phase != TouchPointPhase.Began)
                    continue;

                var live = LiveInstances();
                var instance = live.Count > 0 ? live[0] : null;
                if (instance == null)
                {
                    if (frame.InContactCount == 1 || FirstOfTwoBeganTogether(frame, touch))
                        BeginPossible(touch.touchId, i => Track(i, touch));
                }
                else if (instance.Fingers.Count == 1 && !HasElapsed(instance.StartTime, Settings.Tap.multiFingerTapWindowMs))
                {
                    AddFinger(instance, touch.touchId);
                    Track(instance, touch);
                }
                else
                {
                    FailInstance(instance, GestureReason.FingerCount);
                }
            }

            foreach (var instance in LiveInstances())
                UpdateInstance(instance, frame);
        }

        private void UpdateInstance(TwoFingerTapInstance instance, TouchFrame frame)
        {
            var slop = Settings.Motion.touchSlopDp;
            foreach (var id in instance.Fingers)
            {
                if (!frame.TryGet(id, out var touch)) continue; // lifted earlier
                if ((touch.positionDp - instance.DownPositions[id]).magnitude > slop)
                {
                    FailInstance(instance, GestureReason.Moved);
                    return;
                }
                if (touch.phase == TouchPointPhase.Ended)
                    instance.Lifted++;
            }

            if (instance.Fingers.Count == 1)
            {
                if (instance.Lifted > 0)
                    FailInstance(instance, GestureReason.Released);
                else if (HasElapsed(instance.StartTime, Settings.Tap.multiFingerTapWindowMs))
                    FailInstance(instance, GestureReason.Timeout);
                return;
            }

            if (instance.Lifted >= 2)
                RecogniseInstance(instance);
            else if (HasElapsed(instance.StartTime, Settings.LongPress.longPressTimeoutMs))
                FailInstance(instance, GestureReason.Held);
        }

        private static void Track(TwoFingerTapInstance instance, TouchPoint touch)
        {
            instance.Fingers.Add(touch.touchId);
            instance.DownPositions[touch.touchId] = touch.positionDp;
        }

        // Two fingers landing in the same update: InContactCount is 2, but the first may still start the tap.
        private static bool FirstOfTwoBeganTogether(TouchFrame frame, TouchPoint touch)
        {
            if (frame.InContactCount != 2) return false;
            foreach (var other in frame.Touches)
                if (other.touchId != touch.touchId && other.IsInContact && other.phase != TouchPointPhase.Began)
                    return false;
            return true;
        }
    }
}
