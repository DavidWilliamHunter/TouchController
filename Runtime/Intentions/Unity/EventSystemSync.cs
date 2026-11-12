using System;
using UnityEngine;
using UnityEngine.EventSystems;

namespace Threads.TouchController.Intentions
{
    /// <summary>
    /// Keeps Unity UI's drag threshold the same as the touch slop, so uGUI decides "this is a drag" (and
    /// cancels a pending button click) in the same update that a <see cref="DragGesture"/> enters.
    /// </summary>
    /// <remarks>Call <see cref="Apply"/> after <c>TouchSystem.Startup</c> and after every <c>TouchSystem.Refresh</c>
    /// (the slop and screen density can change when the user changes an accessibility setting).</remarks>
    public static class EventSystemSync
    {
        /// <summary>The touch slop in whole pixels (at least 1).</summary>
        public static int DragThresholdPx(TouchSystem touch)
        {
            if (touch == null) throw new ArgumentNullException(nameof(touch));
            return Mathf.Max(1, Mathf.RoundToInt(touch.Settings.Data.Motion.touchSlopDp * touch.Environment.PixelsPerDp));
        }

        /// <summary>Sets <c>EventSystem.pixelDragThreshold</c> from the touch slop.</summary>
        public static void Apply(EventSystem eventSystem, TouchSystem touch)
        {
            if (eventSystem == null) throw new ArgumentNullException(nameof(eventSystem));
            eventSystem.pixelDragThreshold = DragThresholdPx(touch);
        }
    }
}
