using UnityEngine;

namespace Threads.TouchController
{
    /// <summary>
    /// Swipe / flick (discrete): the finger lifts while moving fast along one axis. Recognised (Enter
    /// and Exit) in the update the finger lifts, usually alongside a Drag's Exit. Reports
    /// <see cref="GestureEventData.Direction"/> (the axis: left, right, up or down) and the release
    /// velocity (clamped to <see cref="FlingSettings.maxFlingVelocityDps"/>).
    /// </summary>
    /// <remarks>
    /// Fails with Released (lifted without moving beyond the slop: a tap, not a swipe), TooSlow
    /// (release speed below <see cref="FlingSettings.minFlingVelocityDps"/>, including a finger that paused
    /// before lifting) or Direction (more than <see cref="FlingSettings.swipeAngleToleranceDeg"/> from an axis).
    /// </remarks>
    [CreateAssetMenu(fileName = "Swipe", menuName = "Threads/TouchController/Gestures/Swipe")]
    public sealed class SwipeGesture : SingleFingerGesture<GestureInstance>
    {
        protected override void OnTouch(GestureInstance instance, TouchPoint touch, TouchFrame frame)
        {
            if (touch.phase != TouchPointPhase.Ended)
                return;

            if (!MovedBeyondSlop(instance, touch))
            {
                FailInstance(instance, GestureReason.Released);
                return;
            }

            var fling = Settings.Fling;
            var velocity = History.GetVelocityDps(touch.touchId);
            var speed = velocity.magnitude;
            if (speed < fling.minFlingVelocityDps || speed <= 0f)
            {
                FailInstance(instance, GestureReason.TooSlow);
                return;
            }

            var axis = Mathf.Abs(velocity.x) >= Mathf.Abs(velocity.y)
                ? new Vector2(Mathf.Sign(velocity.x), 0f)
                : new Vector2(0f, Mathf.Sign(velocity.y));
            if (Vector2.Angle(velocity, axis) > fling.swipeAngleToleranceDeg + 1e-3f)
            {
                FailInstance(instance, GestureReason.Direction);
                return;
            }

            SetDirection(instance, axis);
            SetVelocity(instance, speed > fling.maxFlingVelocityDps ? velocity * (fling.maxFlingVelocityDps / speed) : velocity);
            RecogniseInstance(instance);
        }
    }
}
