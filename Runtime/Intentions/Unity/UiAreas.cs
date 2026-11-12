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

        /// <summary>
        /// Where the top-most UI hit is inside a control that handles drags itself (Slider, Scrollbar,
        /// ScrollRect, InputField, TMP_InputField, or any interactable <see cref="IDragHandler"/> other than an
        /// <see cref="IntentionDragRelay"/>) that is inside <paramref name="root"/>. The control is the target.
        /// </summary>
        /// <remarks>
        /// uGUI sends a drag to the nearest drag handler above the touched object, so a frame's
        /// <see cref="IntentionDragRelay"/> never sees drags that start on such a control, and the control and
        /// a frame-drag intention would both move. Exclude them from the frame's area:
        /// <code>Area.All(new RectTransformArea(frame), Area.Not(UiRaycastArea.DragOwners(eventSystem, frame)))</code>
        /// A drag handler above <paramref name="root"/> (e.g. a ScrollRect the frame sits in) doesn't count.
        /// </remarks>
        public static IIntentionArea DragOwners(EventSystem eventSystem, Transform root = null) =>
            new DragOwnerArea(new UiRaycastArea(eventSystem, root, null), root);

        internal static IIntentionArea DragOwners(EventSystem eventSystem, Transform root,
            Action<PointerEventData, List<RaycastResult>> raycastAll) =>
            new DragOwnerArea(new UiRaycastArea(eventSystem, root, null, raycastAll), root);

        public bool Accepts(in IntentionPoint point, out object target)
        {
            target = null;
            var hit = TopHit(point);
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

        /// <summary>The top-most UI object under the point, or null.</summary>
        internal GameObject TopHit(in IntentionPoint point)
        {
            if (_eventSystem == null || !_eventSystem.isActiveAndEnabled) return null;

            _pointer ??= new PointerEventData(_eventSystem);
            _pointer.Reset();
            _pointer.position = point.PositionPx;
            _pointer.pointerId = point.InputTouchId;
            _hits.Clear();
            _raycastAll(_pointer, _hits);
            if (_hits.Count == 0) return null;

            var hit = _hits[0].gameObject; // sorted: the top-most first
            _hits.Clear();
            return hit;
        }

        private sealed class DragOwnerArea : IIntentionArea
        {
            private readonly UiRaycastArea _raycast;
            private readonly Transform _root;
            private readonly List<Component> _components = new List<Component>();

            public DragOwnerArea(UiRaycastArea raycast, Transform root)
            {
                _raycast = raycast;
                _root = root;
            }

            public bool Accepts(in IntentionPoint point, out object target)
            {
                target = null;
                var hit = _raycast.TopHit(point);
                if (hit == null) return false;
                var owner = FindDragOwner(hit);
                if (owner == null || (_root != null && !owner.transform.IsChildOf(_root))) return false;
                target = owner;
                return true;
            }

            /// <summary>The drag handler uGUI would send a drag starting on <paramref name="hit"/> to, if it is a control.</summary>
            private Component FindDragOwner(GameObject hit)
            {
                var handlerObject = ExecuteEvents.GetEventHandler<IDragHandler>(hit);
                if (handlerObject == null) return null;
                Component owner = null;
                handlerObject.GetComponents(_components);
                foreach (var c in _components)
                {
                    if (!(c is IDragHandler) || c is IntentionDragRelay) continue;
                    if (c is Behaviour b && !b.isActiveAndEnabled) continue;
                    if (c is UnityEngine.UI.Selectable s && !s.IsInteractable()) continue; // ignores drags
                    owner = c;
                    break;
                }
                _components.Clear();
                return owner;
            }
        }
    }
}
