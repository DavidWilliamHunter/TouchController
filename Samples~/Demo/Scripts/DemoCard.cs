using System.Collections.Generic;
using UnityEngine;

namespace Threads.TouchController.Demo
{
    /// <summary>
    /// A card to play with: a small example of context code turning gestures into intentions.
    /// Plain class with one start-up and one shut-down point; it is given the TouchSystem.
    /// </summary>
    /// <remarks>
    /// Intentions (all decided here, not in the package):
    /// <list type="bullet">
    /// <item>Drag a finger that started on the card: move it (unless a two-finger gesture is active).</item>
    /// <item>Two-finger pan / pinch / rotate with a finger on the card: move / scale / rotate it.</item>
    /// <item>Swipe that started on the card: fling it.</item>
    /// <item>Double tap on the card: reset it.</item>
    /// <item>Long press on the card: toggle its highlight.</item>
    /// <item>Fingers that start on the UI panel are ignored ("blocking is the context code's job").</item>
    /// </list>
    /// </remarks>
    public sealed class DemoCard
    {
        private enum Target { Background, Card, Ui }

        private readonly TouchSystem _touch;
        private readonly System.Func<Vector2, bool> _isOverUi;
        private readonly Dictionary<int, Target> _fingers = new Dictionary<int, Target>();

        private PressGesture _press;
        private DragGesture _drag;
        private TwoFingerPanGesture _pan;
        private PinchGesture _pinch;
        private RotateGesture _rotate;
        private SwipeGesture _swipe;
        private DoubleTapGesture _doubleTap;
        private LongPressGesture _longPress;
        private Vector2 _velocityDps;

        /// <param name="touch">The running touch system.</param>
        /// <param name="isOverUi">Whether a position (dp, origin bottom-left) is over the demo's UI.</param>
        public DemoCard(TouchSystem touch, System.Func<Vector2, bool> isOverUi)
        {
            _touch = touch;
            _isOverUi = isOverUi;
        }

        public Vector2 CentreDp { get; private set; }
        public Vector2 SizeDp { get; } = new Vector2(240f, 150f);
        public float Scale { get; private set; } = 1f;

        /// <summary>Counter-clockwise, degrees.</summary>
        public float AngleDeg { get; private set; }

        public bool Highlighted { get; private set; }

        /// <summary>A finger that started on the card is still down.</summary>
        public bool Held
        {
            get
            {
                foreach (var target in _fingers.Values)
                    if (target == Target.Card) return true;
                return false;
            }
        }

        private System.Func<Vector2> _homeDp;

        /// <summary>Call after the TouchSystem has started. <paramref name="homeDp"/> gives the reset position (it can change, e.g. on rotation).</summary>
        public void Startup(System.Func<Vector2> homeDp)
        {
            _homeDp = homeDp;
            Reset();

            if (_touch.TryGet(out _press))
            {
                _press.Possible += OnFingerDown;
                _press.Exit += OnFingerUp;
                _press.OnDisabled += OnPressDisabled;
            }
            if (_touch.TryGet(out _drag))
            {
                _drag.Enter += OnDrag;
                _drag.Stay += OnDrag;
            }
            if (_touch.TryGet(out _pan))
            {
                _pan.Enter += OnPan;
                _pan.Stay += OnPan;
            }
            if (_touch.TryGet(out _pinch))
            {
                _pinch.Enter += OnPinch;
                _pinch.Stay += OnPinch;
            }
            if (_touch.TryGet(out _rotate))
            {
                _rotate.Enter += OnRotate;
                _rotate.Stay += OnRotate;
            }
            if (_touch.TryGet(out _swipe))
                _swipe.Enter += OnSwipe;
            if (_touch.TryGet(out _doubleTap))
                _doubleTap.Enter += OnDoubleTap;
            if (_touch.TryGet(out _longPress))
                _longPress.Enter += OnLongPress;
        }

        /// <summary>Call before the TouchSystem shuts down.</summary>
        public void Shutdown()
        {
            if (_press != null)
            {
                _press.Possible -= OnFingerDown;
                _press.Exit -= OnFingerUp;
                _press.OnDisabled -= OnPressDisabled;
            }
            if (_drag != null)
            {
                _drag.Enter -= OnDrag;
                _drag.Stay -= OnDrag;
            }
            if (_pan != null)
            {
                _pan.Enter -= OnPan;
                _pan.Stay -= OnPan;
            }
            if (_pinch != null)
            {
                _pinch.Enter -= OnPinch;
                _pinch.Stay -= OnPinch;
            }
            if (_rotate != null)
            {
                _rotate.Enter -= OnRotate;
                _rotate.Stay -= OnRotate;
            }
            if (_swipe != null) _swipe.Enter -= OnSwipe;
            if (_doubleTap != null) _doubleTap.Enter -= OnDoubleTap;
            if (_longPress != null) _longPress.Enter -= OnLongPress;
            _fingers.Clear();
        }

        /// <summary>Glide after a fling, staying on screen.</summary>
        public void Tick(float deltaSeconds, Vector2 screenSizeDp)
        {
            if (_velocityDps.sqrMagnitude > 1f && !Held)
            {
                CentreDp += _velocityDps * deltaSeconds;
                _velocityDps *= Mathf.Exp(-3f * deltaSeconds);
            }
            else
            {
                _velocityDps = Vector2.zero;
            }

            var clamped = new Vector2(Mathf.Clamp(CentreDp.x, 0f, screenSizeDp.x), Mathf.Clamp(CentreDp.y, 0f, screenSizeDp.y));
            if (clamped.x != CentreDp.x) _velocityDps.x = -_velocityDps.x * 0.5f;
            if (clamped.y != CentreDp.y) _velocityDps.y = -_velocityDps.y * 0.5f;
            CentreDp = clamped;
        }

        public bool Contains(Vector2 pointDp)
        {
            var local = Rotate(pointDp - CentreDp, -AngleDeg);
            var half = SizeDp * (Scale * 0.5f);
            return Mathf.Abs(local.x) <= half.x && Mathf.Abs(local.y) <= half.y;
        }

        public void Reset()
        {
            CentreDp = _homeDp();
            Scale = 1f;
            AngleDeg = 0f;
            Highlighted = false;
            _velocityDps = Vector2.zero;
        }

        // ---- Intentions ------------------------------------------------------------------------

        private void OnFingerDown(GestureEventData e) =>
            _fingers[e.FingerId] = _isOverUi(e.PositionDp) ? Target.Ui : Contains(e.PositionDp) ? Target.Card : Target.Background;

        private void OnFingerUp(GestureEventData e) => _fingers.Remove(e.FingerId);

        private void OnPressDisabled(IGesture gesture) => _fingers.Clear(); // no Exit will come

        private bool OnCard(int fingerId) => _fingers.TryGetValue(fingerId, out var t) && t == Target.Card;

        private bool AnyOnCard(IReadOnlyList<int> fingerIds)
        {
            foreach (var id in fingerIds)
                if (OnCard(id)) return true;
            return false;
        }

        private bool TwoFingerGestureActive =>
            (_pinch != null && _pinch.IsActive) || (_rotate != null && _rotate.IsActive) || (_pan != null && _pan.IsActive);

        private void OnDrag(GestureEventData e)
        {
            if (!OnCard(e.FingerId) || TwoFingerGestureActive) return;
            CentreDp += e.DeltaDp;
            _velocityDps = Vector2.zero;
        }

        private void OnPan(GestureEventData e)
        {
            if (AnyOnCard(e.FingerIds)) CentreDp += e.DeltaDp;
        }

        private void OnPinch(GestureEventData e)
        {
            if (AnyOnCard(e.FingerIds)) Scale = Mathf.Clamp(Scale * e.ScaleDelta, 0.3f, 5f);
        }

        private void OnRotate(GestureEventData e)
        {
            if (AnyOnCard(e.FingerIds)) AngleDeg += e.AngleDeltaDeg;
        }

        private void OnSwipe(GestureEventData e)
        {
            if (OnCard(e.FingerId)) _velocityDps = e.VelocityDps;
        }

        private void OnDoubleTap(GestureEventData e)
        {
            if (Contains(e.PositionDp)) Reset();
        }

        private void OnLongPress(GestureEventData e)
        {
            if (OnCard(e.FingerId)) Highlighted = !Highlighted;
        }

        private static Vector2 Rotate(Vector2 v, float degrees)
        {
            var r = degrees * Mathf.Deg2Rad;
            return new Vector2(v.x * Mathf.Cos(r) - v.y * Mathf.Sin(r), v.x * Mathf.Sin(r) + v.y * Mathf.Cos(r));
        }
    }
}
