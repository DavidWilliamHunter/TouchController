using UnityEngine;

namespace Threads.TouchController
{
    /// <summary>
    /// One-finger drag / pan (continuous). Enter when the finger moves beyond the touch slop; Stay with
    /// the movement every update; Exit(Completed) when it lifts. Fails with Released if the finger
    /// lifts without moving. One instance per finger, so two thumbs drag independently.
    /// The Enter event's delta includes the movement through the slop.
    /// </summary>
    [CreateAssetMenu(fileName = "Drag", menuName = "Threads/TouchController/Gestures/Drag")]
    public sealed class DragGesture : SingleFingerGesture<GestureInstance>
    {
        protected override void OnTouch(GestureInstance instance, TouchPoint touch, TouchFrame frame)
        {
            if (instance.State == GestureState.Possible)
            {
                if (MovedBeyondSlop(instance, touch))
                    EnterInstance(instance);
                else if (touch.phase == TouchPointPhase.Ended)
                    FailInstance(instance, GestureReason.Released);
            }

            // Not "else": a drag that enters on the update the finger lifts still exits.
            if (instance.State == GestureState.Active && touch.phase == TouchPointPhase.Ended)
                ExitInstance(instance);
        }
    }
}
