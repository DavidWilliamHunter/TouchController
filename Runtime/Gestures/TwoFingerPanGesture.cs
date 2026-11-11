using System.Collections.Generic;
using UnityEngine;

namespace Threads.TouchController
{
    /// <summary>Per-instance data for <see cref="TwoFingerPanGesture"/>.</summary>
    public sealed class TwoFingerPanInstance : MultiFingerInstance
    {
        internal Vector2 Offset;
        internal Vector2 LastReportedDp;
    }

    /// <summary>
    /// Two-finger (or more) pan (continuous). Possible with two or more fingers; Enter when their centroid
    /// has moved beyond the touch slop; Stay reports the movement (<see cref="GestureEventData.DeltaDp"/>);
    /// Exit(Completed) when fewer than two fingers remain. Can be active together with Pinch and Rotate.
    /// </summary>
    /// <remarks>When a finger joins or leaves, the reported position continues smoothly (the centroid jump is absorbed).</remarks>
    [CreateAssetMenu(fileName = "TwoFingerPan", menuName = "Threads/TouchController/Gestures/Two Finger Pan")]
    public sealed class TwoFingerPanGesture : MultiFingerGesture<TwoFingerPanInstance>
    {
        protected override void InitialiseInstance(TwoFingerPanInstance instance, IReadOnlyList<int> contact, TouchFrame frame)
        {
            instance.Offset = Vector2.zero;
            instance.LastReportedDp = instance.PositionDp;
        }

        protected override void OnUpdate(TwoFingerPanInstance instance, IReadOnlyList<int> contact, TouchFrame frame, bool fingersChanged)
        {
            var centroid = Centroid(frame, contact);
            if (fingersChanged)
                instance.Offset = instance.LastReportedDp - centroid;

            var reported = centroid + instance.Offset;
            SetPosition(instance, reported);
            instance.LastReportedDp = reported;

            if (instance.State == GestureState.Possible &&
                (reported - instance.StartPositionDp).magnitude > Settings.Motion.touchSlopDp)
                EnterInstance(instance);
        }
    }
}
