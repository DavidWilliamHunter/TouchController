using UnityEngine;

namespace Threads.TouchController
{
    /// <summary>
    /// Edge swipe (continuous): a drag that starts within <see cref="EdgeSettings.edgeSlopDp"/> of a
    /// screen edge and moves inwards. Possible only for touches that start in an edge strip (others never
    /// become Possible). Enter when the finger moves beyond the touch slop within 45° of straight inwards;
    /// Stay while dragging; Exit(Completed) on lift. Fails with Direction (first movement outward or along
    /// the edge) or Released. Reports <see cref="GestureEventData.Edge"/> and the inward
    /// <see cref="GestureEventData.Direction"/>.
    /// </summary>
    /// <remarks>
    /// The OS's own edge gestures (Android back gesture, iOS home indicator, Windows edge swipes) may take
    /// the touch first, which arrives as a cancel (Fail / Exit with Cancelled).
    /// </remarks>
    [CreateAssetMenu(fileName = "EdgeSwipe", menuName = "Threads/TouchController/Gestures/Edge Swipe")]
    public sealed class EdgeSwipeGesture : SingleFingerGesture<GestureInstance>
    {
        private static readonly float Cos45 = Mathf.Cos(45f * Mathf.Deg2Rad);

        [Tooltip("Recognise swipes from the left edge.")]
        public bool left = true;

        [Tooltip("Recognise swipes from the right edge.")]
        public bool right = true;

        [Tooltip("Recognise swipes from the bottom edge.")]
        public bool bottom = true;

        [Tooltip("Recognise swipes from the top edge.")]
        public bool top = true;

        protected override bool ShouldBegin(TouchPoint touch, TouchFrame frame) =>
            NearestEdge(touch.positionDp, frame.ScreenSizeDp) != ScreenEdge.None;

        protected override void InitialiseInstance(GestureInstance instance, TouchPoint touch, TouchFrame frame)
        {
            var edge = NearestEdge(touch.positionDp, frame.ScreenSizeDp);
            SetEdge(instance, edge);
            SetDirection(instance, Inward(edge));
        }

        protected override void OnTouch(GestureInstance instance, TouchPoint touch, TouchFrame frame)
        {
            if (instance.State == GestureState.Possible)
            {
                if (MovedBeyondSlop(instance, touch))
                {
                    var moved = (touch.positionDp - instance.StartPositionDp).normalized;
                    if (Vector2.Dot(moved, Inward(instance.Edge)) >= Cos45 - 1e-4f)
                        EnterInstance(instance);
                    else
                        FailInstance(instance, GestureReason.Direction);
                }
                else if (touch.phase == TouchPointPhase.Ended)
                {
                    FailInstance(instance, GestureReason.Released);
                }
            }

            if (instance.State == GestureState.Active && touch.phase == TouchPointPhase.Ended)
                ExitInstance(instance);
        }

        /// <summary>The enabled edge nearest to <paramref name="positionDp"/> if within the edge strip; otherwise None.</summary>
        internal ScreenEdge NearestEdge(Vector2 positionDp, Vector2 screenSizeDp)
        {
            var strip = Settings.Edge.edgeSlopDp;
            var best = ScreenEdge.None;
            var bestDistance = float.MaxValue;
            Consider(left, ScreenEdge.Left, positionDp.x);
            Consider(right, ScreenEdge.Right, screenSizeDp.x - positionDp.x);
            Consider(bottom, ScreenEdge.Bottom, positionDp.y);
            Consider(top, ScreenEdge.Top, screenSizeDp.y - positionDp.y);
            return best;

            void Consider(bool enabled, ScreenEdge edge, float distance)
            {
                if (!enabled || distance < 0f || distance > strip || distance >= bestDistance) return;
                best = edge;
                bestDistance = distance;
            }
        }

        private static Vector2 Inward(ScreenEdge edge)
        {
            switch (edge)
            {
                case ScreenEdge.Left: return Vector2.right;
                case ScreenEdge.Right: return Vector2.left;
                case ScreenEdge.Bottom: return Vector2.up;
                case ScreenEdge.Top: return Vector2.down;
                default: return Vector2.zero;
            }
        }
    }
}
