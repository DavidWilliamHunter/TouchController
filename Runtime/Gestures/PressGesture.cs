using UnityEngine;

namespace Threads.TouchController
{
    /// <summary>
    /// Raw contact as a gesture: Enter as the finger touches down (Possible and Enter in the same
    /// update), Stay while it is down, Exit(Completed) when it lifts. Release is Press's Exit.
    /// Useful for classifying each finger at touch-down (Possible) and cleaning up at its end (Exit).
    /// </summary>
    [CreateAssetMenu(fileName = "Press", menuName = "Threads/TouchController/Gestures/Press")]
    public sealed class PressGesture : SingleFingerGesture<GestureInstance>
    {
        protected override void OnBegan(GestureInstance instance, TouchPoint touch) => EnterInstance(instance);

        protected override void OnTouch(GestureInstance instance, TouchPoint touch, TouchFrame frame)
        {
            if (touch.phase == TouchPointPhase.Ended)
                ExitInstance(instance);
        }
    }
}
