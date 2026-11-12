using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Threads.TouchController.Intentions
{
    /// <summary>
    /// Presses Unity UI elements (Buttons, Toggles, any pointer handler) from touches, through the intention
    /// system, so they take part in priority and claims like everything else. For screens where uGUI's
    /// input module does <b>not</b> handle touch.
    /// </summary>
    /// <remarks>
    /// <para>It is a tap intention (<see cref="Intention"/>, which you can configure before enabling: e.g.
    /// <c>RequireFailure&lt;DoubleTapGesture&gt;()</c>). For the element under the finger:</para>
    /// <list type="bullet">
    /// <item>Possible (touch down): pointer enter + pointer down, so the element shows its pressed look.</item>
    /// <item>Rejected (moved, held, or another intention took the finger): pointer up + exit, no click.</item>
    /// <item>Performed (a tap): pointer up, click, exit.</item>
    /// </list>
    /// <para>A higher-priority exclusive intention, e.g. a frame drag, therefore cancels the press. Don't
    /// also let uGUI's input module process touches on the same canvas, or elements are pressed twice.</para>
    /// </remarks>
    public sealed class UiPointerIntention : IDisposable
    {
        private readonly EventSystem _eventSystem;
        private readonly Dictionary<int, (GameObject target, PointerEventData data)> _pressed =
            new Dictionary<int, (GameObject, PointerEventData)>();

        /// <summary>Presses <see cref="Selectable"/>s under <paramref name="root"/> (any, if null), found by a UI raycast.</summary>
        public UiPointerIntention(IntentionSystem system, EventSystem eventSystem, Transform root = null)
            : this(system, eventSystem, UiRaycastArea.For<Selectable>(eventSystem, root))
        {
        }

        /// <summary>Presses what <paramref name="area"/> reports as its target (a Component or GameObject).</summary>
        public UiPointerIntention(IntentionSystem system, EventSystem eventSystem, IIntentionArea area)
        {
            _eventSystem = eventSystem != null ? eventSystem : throw new ArgumentNullException(nameof(eventSystem));
            Intention = new Intention("UI pointer", system ?? throw new ArgumentNullException(nameof(system)))
                .Bind<TapGesture>()
                .InArea(area ?? throw new ArgumentNullException(nameof(area)));
            Intention.Possible += OnPossible;
            Intention.Rejected += OnRejected;
            Intention.Performed += OnPerformed;
        }

        /// <summary>The underlying intention: set its priority or requirements before enabling.</summary>
        public Intention Intention { get; }

        public bool Enabled
        {
            get => Intention.Enabled;
            set => Intention.Enabled = value;
        }

        /// <summary>Disables it (releasing any pressed element) and unsubscribes.</summary>
        public void Dispose()
        {
            Intention.Enabled = false;
            Intention.Possible -= OnPossible;
            Intention.Rejected -= OnRejected;
            Intention.Performed -= OnPerformed;
            _pressed.Clear();
        }

        private void OnPossible(IntentionEventData e)
        {
            var target = e.Target switch
            {
                Component c => c != null ? c.gameObject : null,
                GameObject g => g,
                _ => null,
            };
            if (target == null) return;

            var data = new PointerEventData(_eventSystem)
            {
                button = PointerEventData.InputButton.Left,
                pointerId = e.FingerId,
                position = e.PositionPx,
                pressPosition = e.PositionPx,
                clickCount = 1,
                clickTime = (float)e.Time,
                eligibleForClick = true,
            };
            data.pointerEnter = target;
            data.pointerPress = ExecuteEvents.GetEventHandler<IPointerDownHandler>(target) ?? target;
            data.rawPointerPress = target;
            _pressed[e.InstanceId] = (target, data);

            ExecuteEvents.ExecuteHierarchy(target, data, ExecuteEvents.pointerEnterHandler);
            ExecuteEvents.ExecuteHierarchy(target, data, ExecuteEvents.pointerDownHandler);
        }

        private void OnRejected(IntentionEventData e) => Release(e, click: false);

        private void OnPerformed(IntentionEventData e) => Release(e, click: true);

        private void Release(IntentionEventData e, bool click)
        {
            if (!_pressed.TryGetValue(e.InstanceId, out var pressed)) return;
            _pressed.Remove(e.InstanceId);
            var (target, data) = pressed;
            if (target == null) return; // destroyed meanwhile

            data.position = e.PositionPx;
            ExecuteEvents.ExecuteHierarchy(target, data, ExecuteEvents.pointerUpHandler);
            if (click)
                ExecuteEvents.ExecuteHierarchy(target, data, ExecuteEvents.pointerClickHandler);
            data.eligibleForClick = false;
            ExecuteEvents.ExecuteHierarchy(target, data, ExecuteEvents.pointerExitHandler);
        }
    }
}
