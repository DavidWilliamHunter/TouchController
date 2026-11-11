using UnityEngine;

namespace Threads.TouchController
{
    /// <summary>
    /// A single tap (discrete): touch down and lift without moving beyond the touch slop and before
    /// the long-press time. Enter and Exit(Completed) in the update the finger lifts. Fails with
    /// <see cref="GestureReason.Moved"/> or <see cref="GestureReason.Held"/>. One instance per finger.
    /// </summary>
    /// <remarks>
    /// There is no separate maximum tap time: a tap is a release before
    /// <see cref="LongPressSettings.longPressTimeoutMs"/>, as on Android. Every tap is reported at once,
    /// even if it turns out to be the first half of a double tap (a DoubleTap Fail(Timeout) confirms
    /// a single tap).
    /// </remarks>
    [CreateAssetMenu(fileName = "Tap", menuName = "Threads/TouchController/Gestures/Tap")]
    public sealed class TapGesture : SingleFingerGesture<GestureInstance>
    {
        protected override void InitialiseInstance(GestureInstance instance, TouchPoint touch, TouchFrame frame) =>
            SetTapCount(instance, 1);

        protected override void OnTouch(GestureInstance instance, TouchPoint touch, TouchFrame frame)
        {
            if (MovedBeyondSlop(instance, touch))
                FailInstance(instance, GestureReason.Moved);
            else if (touch.phase == TouchPointPhase.Ended)
                RecogniseInstance(instance);
            else if (HasElapsed(instance.StartTime, Settings.LongPress.longPressTimeoutMs))
                FailInstance(instance, GestureReason.Held);
        }
    }
}
