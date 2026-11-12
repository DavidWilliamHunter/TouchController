using Threads.TouchController.Intentions;
using UnityEngine;

namespace Threads.TouchController.Demo
{
    /// <summary>
    /// A card to play with, driven by intentions. Plain class with one start-up and one shut-down point; it
    /// is given the IntentionSystem. Compare with the hand-written version in Usage.md §7: the per-finger
    /// bookkeeping (what each finger touched, which gestures suppress which) is now declared.
    /// </summary>
    /// <remarks>
    /// Intentions (all limited to fingers that touched down on the card, not on the UI panel):
    /// <list type="bullet">
    /// <item><b>Drag</b> (one finger): move the card.</item>
    /// <item><b>Manipulate</b> (two-finger pan, pinch and rotate, together): move, scale and turn it. Priority 20
    /// and exclusive: it takes the fingers from the one-finger drag. One intention bound to all three gestures,
    /// so they don't compete with each other.</item>
    /// <item><b>Fling</b> (swipe): set the card gliding.</item>
    /// <item><b>Reset</b> (double tap): put it back.</item>
    /// <item><b>Highlight</b> (long press): toggle its highlight.</item>
    /// <item><b>Touching</b> (press): no action; while any finger that touched the card is down, the card doesn't glide.
    /// Shared, with the highest priority, so nothing takes it away.</item>
    /// </list>
    /// </remarks>
    public sealed class DemoCard
    {
        private readonly IntentionSystem _intentions;
        private readonly System.Func<Vector2, bool> _isOverUi;

        private Intention _drag;
        private Intention _manipulate;
        private Intention _fling;
        private Intention _reset;
        private Intention _highlight;
        private Intention _touching;
        private Vector2 _velocityDps;
        private System.Func<Vector2> _homeDp;

        /// <param name="intentions">The running intention system.</param>
        /// <param name="isOverUi">Whether a position (dp, origin bottom-left) is over the demo's UI.</param>
        public DemoCard(IntentionSystem intentions, System.Func<Vector2, bool> isOverUi)
        {
            _intentions = intentions;
            _isOverUi = isOverUi;
        }

        public Vector2 CentreDp { get; private set; }
        public Vector2 SizeDp { get; } = new Vector2(240f, 150f);
        public float Scale { get; private set; } = 1f;

        /// <summary>Counter-clockwise, degrees.</summary>
        public float AngleDeg { get; private set; }

        public bool Highlighted { get; private set; }

        /// <summary>A finger that touched down on the card is still down.</summary>
        public bool Held => _touching != null && _touching.IsActive;

        /// <summary>Call after the IntentionSystem has started. <paramref name="homeDp"/> gives the reset position (it can change, e.g. on rotation).</summary>
        public void Startup(System.Func<Vector2> homeDp)
        {
            _homeDp = homeDp;
            Reset();

            // Checked once per finger, where it touched down (the card moves, the decision doesn't).
            var onCard = Area.From(p => !_isOverUi(p.PositionDp) && Contains(p.PositionDp));

            _drag = new Intention("Card drag", _intentions).Bind<DragGesture>().InArea(onCard);
            _drag.Performed += OnDrag;

            _manipulate = new Intention("Card manipulate", _intentions)
                .Bind<TwoFingerPanGesture>().Bind<PinchGesture>().Bind<RotateGesture>()
                .InArea(onCard).WithPriority(20).Exclusive();
            _manipulate.Performed += OnManipulate;

            _fling = new Intention("Card fling", _intentions).Bind<SwipeGesture>().InArea(onCard);
            _fling.Performed += OnFling;

            _reset = new Intention("Card reset", _intentions).Bind<DoubleTapGesture>().InArea(onCard);
            _reset.Started += OnReset;

            _highlight = new Intention("Card highlight", _intentions).Bind<LongPressGesture>().InArea(onCard);
            _highlight.Started += OnHighlight;

            _touching = new Intention("Card touched", _intentions).Bind<PressGesture>().InArea(onCard).WithPriority(100);

            foreach (var intention in All) intention.Enabled = true;
        }

        /// <summary>Call before the IntentionSystem shuts down.</summary>
        public void Shutdown()
        {
            if (_drag == null) return;
            foreach (var intention in All) intention.Enabled = false;
            _drag.Performed -= OnDrag;
            _manipulate.Performed -= OnManipulate;
            _fling.Performed -= OnFling;
            _reset.Started -= OnReset;
            _highlight.Started -= OnHighlight;
            _drag = _manipulate = _fling = _reset = _highlight = _touching = null;
        }

        private Intention[] All => new[] { _drag, _manipulate, _fling, _reset, _highlight, _touching };

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

        // ---- Actions: what each intention does to the card ---------------------------------------------

        private void OnDrag(IntentionEventData e)
        {
            CentreDp += e.DeltaDp;
            _velocityDps = Vector2.zero;
        }

        private void OnManipulate(IntentionEventData e)
        {
            switch (e.Gesture)
            {
                case TwoFingerPanGesture _:
                    CentreDp += e.DeltaDp;
                    break;
                case PinchGesture _:
                    Scale = Mathf.Clamp(Scale * e.ScaleDelta, 0.3f, 5f);
                    break;
                case RotateGesture _:
                    AngleDeg += e.AngleDeltaDeg;
                    break;
            }
        }

        private void OnFling(IntentionEventData e) => _velocityDps = e.VelocityDps;

        private void OnReset(IntentionEventData e) => Reset();

        private void OnHighlight(IntentionEventData e) => Highlighted = !Highlighted;

        private static Vector2 Rotate(Vector2 v, float degrees)
        {
            var r = degrees * Mathf.Deg2Rad;
            return new Vector2(v.x * Mathf.Cos(r) - v.y * Mathf.Sin(r), v.x * Mathf.Sin(r) + v.y * Mathf.Cos(r));
        }
    }
}
