using System;
using UnityEngine;

namespace Threads.TouchController.Intentions
{
    /// <summary>A position offered to an <see cref="IIntentionArea"/>.</summary>
    public readonly struct IntentionPoint
    {
        public IntentionPoint(int fingerId, int inputTouchId, Vector2 positionDp, float pixelsPerDp, Vector2 screenSizeDp, double time)
        {
            FingerId = fingerId;
            InputTouchId = inputTouchId;
            PositionDp = positionDp;
            PixelsPerDp = pixelsPerDp;
            ScreenSizeDp = screenSizeDp;
            Time = time;
        }

        /// <summary><see cref="TouchPoint.touchId"/> (or a source's pseudo-pointer ID).</summary>
        public int FingerId { get; }

        /// <summary>The Input System's touch ID, for Unity APIs. 0 if not from the Input System.</summary>
        public int InputTouchId { get; }

        /// <summary>Screen space, origin bottom-left.</summary>
        public Vector2 PositionDp { get; }

        /// <summary>For raycasts and UI.</summary>
        public Vector2 PositionPx => PositionDp * PixelsPerDp;

        public float PixelsPerDp { get; }

        public Vector2 ScreenSizeDp { get; }

        public Vector2 ScreenSizePx => ScreenSizeDp * PixelsPerDp;

        public double Time { get; }
    }

    /// <summary>
    /// Decides whether an intention applies at a position. Injected into an <see cref="Intention"/>
    /// (<see cref="Intention.InArea(IIntentionArea, AreaCheck)"/>); with none, the intention applies everywhere.
    /// </summary>
    /// <remarks>
    /// By default it is asked once per finger, at the position where the finger touched down, and the
    /// answer is cached for that finger. Implementations must not change Unity state.
    /// </remarks>
    public interface IIntentionArea
    {
        /// <param name="point">Where the finger is (or touched down).</param>
        /// <param name="target">What was hit (e.g. a Button or Collider), or null. Reported as <see cref="IntentionEventData.Target"/>.</param>
        bool Accepts(in IntentionPoint point, out object target);
    }

    /// <summary>An area from a function.</summary>
    public sealed class FuncArea : IIntentionArea
    {
        private readonly Func<IntentionPoint, bool> _accepts;
        private readonly Func<IntentionPoint, object> _target;

        public FuncArea(Func<IntentionPoint, bool> accepts, Func<IntentionPoint, object> target = null)
        {
            _accepts = accepts ?? throw new ArgumentNullException(nameof(accepts));
            _target = target;
        }

        public bool Accepts(in IntentionPoint point, out object target)
        {
            if (_accepts(point))
            {
                target = _target?.Invoke(point);
                return true;
            }
            target = null;
            return false;
        }
    }

    /// <summary>A rectangle on the screen, in dp, pixels or a fraction of the screen. Pure maths: no Unity state.</summary>
    public sealed class ScreenRectArea : IIntentionArea
    {
        private enum Unit { Dp, Px, Fraction }

        private readonly Unit _unit;
        private readonly Rect _rect;
        private readonly Vector2 _anchor;
        private readonly bool _anchored;

        private ScreenRectArea(Unit unit, Rect rect, Vector2 anchor, bool anchored)
        {
            _unit = unit;
            _rect = rect;
            _anchor = anchor;
            _anchored = anchored;
        }

        /// <summary>A rectangle in dp (origin bottom-left).</summary>
        public static ScreenRectArea Dp(Rect rectDp) => new ScreenRectArea(Unit.Dp, rectDp, default, false);

        /// <summary>A rectangle in pixels (origin bottom-left).</summary>
        public static ScreenRectArea Px(Rect rectPx) => new ScreenRectArea(Unit.Px, rectPx, default, false);

        /// <summary>A rectangle as fractions of the screen: (0, 0, 0.5, 1) is the left half.</summary>
        public static ScreenRectArea Fraction(Rect rect01) => new ScreenRectArea(Unit.Fraction, rect01, default, false);

        /// <summary>
        /// A rectangle of <paramref name="sizeDp"/> placed like a UI element: its <paramref name="pivot"/>
        /// (0..1 within the rectangle) sits at <paramref name="anchor"/> (0..1 of the screen) plus
        /// <paramref name="offsetDp"/>. E.g. a 120 dp square in the top-right corner:
        /// <c>Anchored(new Vector2(1, 1), new Vector2(1, 1), Vector2.zero, new Vector2(120, 120))</c>.
        /// </summary>
        public static ScreenRectArea Anchored(Vector2 anchor, Vector2 pivot, Vector2 offsetDp, Vector2 sizeDp) =>
            new ScreenRectArea(Unit.Dp, new Rect(offsetDp - Vector2.Scale(pivot, sizeDp), sizeDp), anchor, true);

        /// <summary>The rectangle in dp for a screen of <paramref name="screenSizeDp"/>.</summary>
        public Rect ToDp(Vector2 screenSizeDp, float pixelsPerDp)
        {
            switch (_unit)
            {
                case Unit.Px:
                    return new Rect(_rect.position / pixelsPerDp, _rect.size / pixelsPerDp);
                case Unit.Fraction:
                    return new Rect(Vector2.Scale(_rect.position, screenSizeDp), Vector2.Scale(_rect.size, screenSizeDp));
                default:
                    return _anchored ? new Rect(_rect.position + Vector2.Scale(_anchor, screenSizeDp), _rect.size) : _rect;
            }
        }

        public bool Accepts(in IntentionPoint point, out object target)
        {
            target = null;
            return ToDp(point.ScreenSizeDp, point.PixelsPerDp).Contains(point.PositionDp);
        }
    }

    /// <summary>Building and combining areas.</summary>
    public static class Area
    {
        /// <summary>An area from a predicate (and optionally the target to report).</summary>
        public static IIntentionArea From(Func<IntentionPoint, bool> accepts, Func<IntentionPoint, object> target = null) =>
            new FuncArea(accepts, target);

        /// <summary>Everywhere except <paramref name="area"/>, e.g. "not on the HUD". Reports no target.</summary>
        public static IIntentionArea Not(IIntentionArea area) => new NotArea(area ?? throw new ArgumentNullException(nameof(area)));

        /// <summary>Inside every area. Reports the first target found.</summary>
        public static IIntentionArea All(params IIntentionArea[] areas) => new AllArea(Check(areas));

        /// <summary>Inside at least one area. Reports the target of the first that accepts.</summary>
        public static IIntentionArea Any(params IIntentionArea[] areas) => new AnyArea(Check(areas));

        private static IIntentionArea[] Check(IIntentionArea[] areas)
        {
            if (areas == null || areas.Length == 0) throw new ArgumentException("Give at least one area.", nameof(areas));
            foreach (var a in areas)
                if (a == null) throw new ArgumentException("Areas cannot be null.", nameof(areas));
            return (IIntentionArea[])areas.Clone();
        }

        private sealed class NotArea : IIntentionArea
        {
            private readonly IIntentionArea _area;
            public NotArea(IIntentionArea area) => _area = area;

            public bool Accepts(in IntentionPoint point, out object target)
            {
                target = null;
                return !_area.Accepts(point, out _);
            }
        }

        private sealed class AllArea : IIntentionArea
        {
            private readonly IIntentionArea[] _areas;
            public AllArea(IIntentionArea[] areas) => _areas = areas;

            public bool Accepts(in IntentionPoint point, out object target)
            {
                target = null;
                foreach (var area in _areas)
                {
                    if (!area.Accepts(point, out var t))
                    {
                        target = null;
                        return false;
                    }
                    if (target == null) target = t;
                }
                return true;
            }
        }

        private sealed class AnyArea : IIntentionArea
        {
            private readonly IIntentionArea[] _areas;
            public AnyArea(IIntentionArea[] areas) => _areas = areas;

            public bool Accepts(in IntentionPoint point, out object target)
            {
                foreach (var area in _areas)
                    if (area.Accepts(point, out target)) return true;
                target = null;
                return false;
            }
        }
    }
}
