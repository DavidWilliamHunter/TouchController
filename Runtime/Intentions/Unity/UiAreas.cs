using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;

namespace Threads.TouchController.Intentions
{
    /// <summary>
    /// Inside a <see cref="RectTransform"/> on the screen (<c>RectTransformUtility.RectangleContainsScreenPoint</c>).
    /// Cheap and needs no EventSystem, but ignores what covers the rectangle. Target: the RectTransform.
    /// </summary>
    public sealed class RectTransformArea : IIntentionArea
    {
        private readonly RectTransform _rect;
        private readonly Camera _camera;

        /// <param name="rect">The rectangle (e.g. a frame).</param>
        /// <param name="camera">The canvas's camera; null for a Screen Space - Overlay canvas.</param>
        public RectTransformArea(RectTransform rect, Camera camera = null)
        {
            _rect = rect != null ? rect : throw new ArgumentNullException(nameof(rect));
            _camera = camera;
        }

        public bool Accepts(in IntentionPoint point, out object target)
        {
            target = null;
            if (_rect == null || !_rect.gameObject.activeInHierarchy) return false; // destroyed or hidden
            if (!RectTransformUtility.RectangleContainsScreenPoint(_rect, point.PositionPx, _camera)) return false;
            target = _rect;
            return true;
        }
    }

    /// <summary>
    /// Asks Unity UI what is under the finger (<c>EventSystem.RaycastAll</c>), so only what is visible and a
    /// raycast target counts. Accepts when the top hit is <paramref name="root"/> or inside it (any UI if no
    /// root). With a component type, the hit must be (inside) such a component, which becomes the target;
    /// otherwise the target is the hit GameObject.
    /// </summary>
    public sealed class UiRaycastArea : IIntentionArea
    {
        private readonly EventSystem _eventSystem;
        private readonly Transform _root;
        private readonly Type _component;
        private readonly Action<PointerEventData, List<RaycastResult>> _raycastAll;
        private readonly List<RaycastResult> _hits = new List<RaycastResult>();
        private PointerEventData _pointer;

        /// <param name="eventSystem">The EventSystem to ask (passed in: no <c>EventSystem.current</c>).</param>
        /// <param name="root">Only hits on this transform or its children count. Null: any UI.</param>
        /// <param name="component">The hit must be inside a component of this type (e.g. <c>Selectable</c>); it becomes the target.</param>
        public UiRaycastArea(EventSystem eventSystem, Transform root = null, Type component = null)
            : this(eventSystem, root, component, null)
        {
        }

        /// <param name="raycastAll">Replaces <c>EventSystem.RaycastAll</c> (tests: uGUI raycasters only register in Play Mode).</param>
        internal UiRaycastArea(EventSystem eventSystem, Transform root, Type component, Action<PointerEventData, List<RaycastResult>> raycastAll)
        {
            _eventSystem = eventSystem != null ? eventSystem : throw new ArgumentNullException(nameof(eventSystem));
            _root = root;
            if (component != null && !typeof(Component).IsAssignableFrom(component))
                throw new ArgumentException($"{component.Name} is not a Component.", nameof(component));
            _component = component;
            _raycastAll = raycastAll ?? _eventSystem.RaycastAll;
        }

        /// <summary>Hits on a <typeparamref name="T"/> (e.g. <c>Selectable</c>, <c>Button</c>) inside <paramref name="root"/>; the component is the target.</summary>
        public static UiRaycastArea For<T>(EventSystem eventSystem, Transform root = null) where T : Component =>
            new UiRaycastArea(eventSystem, root, typeof(T));

        public bool Accepts(in IntentionPoint point, out object target)
        {
            target = null;
            if (_eventSystem == null || !_eventSystem.isActiveAndEnabled) return false;

            _pointer ??= new PointerEventData(_eventSystem);
            _pointer.Reset();
            _pointer.position = point.PositionPx;
            _pointer.pointerId = point.InputTouchId;
            _hits.Clear();
            _raycastAll(_pointer, _hits);
            if (_hits.Count == 0) return false;

            var hit = _hits[0].gameObject; // sorted: the top-most first
            _hits.Clear();
            if (hit == null) return false;
            if (_root != null && !hit.transform.IsChildOf(_root)) return false;

            if (_component == null)
            {
                target = hit;
                return true;
            }
            var component = hit.GetComponentInParent(_component);
            if (component == null || (_root != null && !component.transform.IsChildOf(_root))) return false;
            target = component;
            return true;
        }
    }
}
