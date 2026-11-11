using UnityEngine;

namespace Threads.TouchController
{
    /// <summary>
    /// Windows touch (e.g. Surface) double tap. ASSUMED to be recognised when the second finger lifts
    /// (Windows reports a double tap as a tap with a count of 2, and taps complete on release).
    /// To be checked on a Surface (plan.md Q11); see the asset's notes. A separate class so it can
    /// change without affecting the other platforms.
    /// </summary>
    [CreateAssetMenu(fileName = "WindowsDoubleTap", menuName = "Threads/TouchController/Gestures/Double Tap (Windows)")]
    public sealed class WindowsDoubleTap : DoubleTapGesture
    {
        protected override bool EntersOnSecondDown => false;
    }
}
