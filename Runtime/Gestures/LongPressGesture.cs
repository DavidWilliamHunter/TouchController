using UnityEngine;

namespace Threads.TouchController
{
    /// <summary>
    /// Press and hold (continuous). Enter once the finger has been held for
    /// <see cref="LongPressSettings.longPressTimeoutMs"/> (the OS / accessibility value where available)
    /// without moving beyond the touch slop. The finger may move after Enter (the position keeps
    /// updating, as on Android), so long-press-then-drag is LongPress active while a Drag enters.
    /// Exit(Completed) when the finger lifts. Fails with Moved or Released. One instance per finger.
    /// </summary>
    [CreateAssetMenu(fileName = "LongPress", menuName = "Threads/TouchController/Gestures/Long Press")]
    public sealed class LongPressGesture : SingleFingerGesture<GestureInstance>
    {
        protected override void OnTouch(GestureInstance instance, TouchPoint touch, TouchFrame frame)
        {
            if (instance.State == GestureState.Active)
            {
                if (touch.phase == TouchPointPhase.Ended)
                    ExitInstance(instance);
                return;
            }

            if (MovedBeyondSlop(instance, touch))
                FailInstance(instance, GestureReason.Moved);
            else if (touch.phase == TouchPointPhase.Ended)
                FailInstance(instance, GestureReason.Released);
            else if (HasElapsed(instance.StartTime, Settings.LongPress.longPressTimeoutMs))
                EnterInstance(instance); // driven by time: the finger need not move
        }
    }
}
