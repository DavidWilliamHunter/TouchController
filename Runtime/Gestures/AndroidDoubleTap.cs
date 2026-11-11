using UnityEngine;

namespace Threads.TouchController
{
    /// <summary>
    /// Android double tap: Enter when the second finger touches down (as GestureDetector.onDoubleTap),
    /// Stay while it is held (so double-tap-then-drag works), Exit(Completed) when it lifts.
    /// </summary>
    [CreateAssetMenu(fileName = "AndroidDoubleTap", menuName = "Threads/TouchController/Gestures/Double Tap (Android)")]
    public sealed class AndroidDoubleTap : DoubleTapGesture
    {
        protected override bool EntersOnSecondDown => true;
    }
}
