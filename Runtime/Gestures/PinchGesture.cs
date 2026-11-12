using System.Collections.Generic;
using UnityEngine;

namespace Threads.TouchController
{
    /// <summary>Per-instance data for <see cref="PinchGesture"/>.</summary>
    public sealed class PinchInstance : MultiFingerInstance
    {
        /// <summary>The span that corresponds to <see cref="GestureInstance.Scale"/> = 1 (dp).</summary>
        public float StartSpanDp { get; internal set; }

        /// <summary>The current span (dp): twice the mean distance of the fingers from their centroid; with two fingers, their distance apart.</summary>
        public float SpanDp { get; internal set; }
    }

    /// <summary>
    /// Pinch / scale (continuous), over all fingers. Possible with two or more fingers; Enter when the
    /// span is at least <see cref="PinchSettings.minScalingSpanDp"/> and has changed by more than
    /// 2 × the touch slop (as Android's ScaleGestureDetector). Stay reports <see cref="GestureEventData.Scale"/>
    /// (since Possible) and <see cref="GestureEventData.ScaleDelta"/> (multiply to apply). Exit(Completed)
    /// when fewer than two fingers remain or the span drops below the minimum. Can be active together with
    /// Rotate and TwoFingerPan.
    /// </summary>
    /// <remarks>Span = twice the mean distance of the fingers from their centroid, as Android's ScaleGestureDetector
    /// (whose values <c>minScalingSpanDp</c> and the 2 × slop threshold come from). With two fingers it is their
    /// distance apart. When a finger joins or leaves, the scale continues from its current value (no jump).</remarks>
    [CreateAssetMenu(fileName = "Pinch", menuName = "Threads/TouchController/Gestures/Pinch")]
    public sealed class PinchGesture : MultiFingerGesture<PinchInstance>
    {
        protected override void InitialiseInstance(PinchInstance instance, IReadOnlyList<int> contact, TouchFrame frame)
        {
            instance.SpanDp = Span(frame, contact);
            instance.StartSpanDp = instance.SpanDp;
        }

        protected override void OnUpdate(PinchInstance instance, IReadOnlyList<int> contact, TouchFrame frame, bool fingersChanged)
        {
            var span = Span(frame, contact);
            instance.SpanDp = span;

            if (fingersChanged)
            {
                // Re-baseline so the scale continues smoothly with the new set of fingers.
                instance.StartSpanDp = instance.State == GestureState.Active && instance.Scale > 0f ? span / instance.Scale : span;
                return;
            }

            var pinch = Settings.Pinch;
            if (instance.State == GestureState.Possible)
            {
                if (span >= pinch.minScalingSpanDp && Mathf.Abs(span - instance.StartSpanDp) > 2f * Settings.Motion.touchSlopDp)
                {
                    SetScale(instance, ScaleOf(instance, span));
                    EnterInstance(instance);
                }
                return;
            }

            if (span < pinch.minScalingSpanDp)
            {
                ExitInstance(instance, GestureReason.Completed);
                return;
            }
            SetScale(instance, ScaleOf(instance, span));
        }

        private static float ScaleOf(PinchInstance instance, float span) =>
            instance.StartSpanDp > 0f ? span / instance.StartSpanDp : 1f;

        private static float Span(TouchFrame frame, IReadOnlyList<int> fingers)
        {
            var centre = Centroid(frame, fingers);
            float sum = 0f;
            var count = 0;
            foreach (var id in fingers)
            {
                if (!frame.TryGet(id, out var touch)) continue;
                sum += (touch.positionDp - centre).magnitude;
                count++;
            }
            return count > 0 ? 2f * sum / count : 0f; // a diameter, like Android's span (not a radius)
        }
    }
}
