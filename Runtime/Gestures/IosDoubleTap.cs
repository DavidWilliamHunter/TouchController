using UnityEngine;

namespace Threads.TouchController
{
    /// <summary>
    /// iOS double tap: recognised (Enter and Exit together) when the second finger lifts, like
    /// UITapGestureRecognizer with two taps. The second touch must also be a valid tap.
    /// </summary>
    [CreateAssetMenu(fileName = "IosDoubleTap", menuName = "Threads/TouchController/Gestures/Double Tap (iOS)")]
    public sealed class IosDoubleTap : DoubleTapGesture
    {
        protected override bool EntersOnSecondDown => false;
    }
}
