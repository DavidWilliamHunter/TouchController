using System.Collections.Generic;
using UnityEngine;

namespace Threads.TouchController
{
    /// <summary>Per-instance data for <see cref="RotateGesture"/>.</summary>
    public sealed class RotateInstance : MultiFingerInstance
    {
        internal int FingerA;
        internal int FingerB;
        internal float LastPairAngleDeg;
        internal float AccumulatedDeg;
    }

    /// <summary>
    /// Rotate / twist (continuous), over all fingers. The angle is measured between the first two fingers
    /// in contact. Possible with two or more fingers; Enter when the fingers have turned at least
    /// <see cref="RotateSettings.minRotationDeg"/>. Stay reports <see cref="GestureEventData.AngleDeg"/> (since
    /// Possible, counter-clockwise positive, not limited to ±180) and <see cref="GestureEventData.AngleDeltaDeg"/>.
    /// Exit(Completed) when fewer than two fingers remain. Can be active together with Pinch and TwoFingerPan.
    /// </summary>
    /// <remarks>When the measured pair of fingers changes, the angle continues from its current value (no jump).</remarks>
    [CreateAssetMenu(fileName = "Rotate", menuName = "Threads/TouchController/Gestures/Rotate")]
    public sealed class RotateGesture : MultiFingerGesture<RotateInstance>
    {
        protected override void InitialiseInstance(RotateInstance instance, IReadOnlyList<int> contact, TouchFrame frame)
        {
            instance.FingerA = contact[0];
            instance.FingerB = contact[1];
            instance.LastPairAngleDeg = PairAngle(frame, contact[0], contact[1]);
            instance.AccumulatedDeg = 0f;
        }

        protected override void OnUpdate(RotateInstance instance, IReadOnlyList<int> contact, TouchFrame frame, bool fingersChanged)
        {
            var a = contact[0];
            var b = contact[1];
            var angle = PairAngle(frame, a, b);

            if (a != instance.FingerA || b != instance.FingerB)
            {
                // A different pair: start measuring from here, keeping the accumulated angle.
                instance.FingerA = a;
                instance.FingerB = b;
            }
            else
            {
                instance.AccumulatedDeg += Mathf.DeltaAngle(instance.LastPairAngleDeg, angle);
            }
            instance.LastPairAngleDeg = angle;

            if (instance.State == GestureState.Possible)
            {
                if (Mathf.Abs(instance.AccumulatedDeg) >= Settings.Rotate.minRotationDeg - 1e-4f)
                {
                    SetAngle(instance, instance.AccumulatedDeg);
                    EnterInstance(instance);
                }
                return;
            }
            SetAngle(instance, instance.AccumulatedDeg);
        }

        private static float PairAngle(TouchFrame frame, int a, int b)
        {
            frame.TryGet(a, out var first);
            frame.TryGet(b, out var second);
            var v = second.positionDp - first.positionDp;
            return Mathf.Atan2(v.y, v.x) * Mathf.Rad2Deg;
        }
    }
}
