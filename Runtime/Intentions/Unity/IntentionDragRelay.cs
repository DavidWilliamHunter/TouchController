using UnityEngine;
using UnityEngine.EventSystems;

namespace Threads.TouchController.Intentions
{
    /// <summary>
    /// Put this on a draggable UI element (e.g. a frame) whose drag is done by an <see cref="Intention"/>,
    /// when Unity UI handles the buttons inside it. It makes uGUI's own rule apply: once a drag starts on
    /// the frame, uGUI releases the pressed button <b>without a click</b>. The frame then moves (by your
    /// intention) and the button doesn't fire, wherever the drag started.
    /// </summary>
    /// <remarks>
    /// It does nothing itself: being a drag handler above the buttons is enough. Use
    /// <see cref="EventSystemSync.Apply"/> so uGUI's drag threshold matches the touch slop. Don't use it on a
    /// canvas driven by <see cref="UiPointerIntention"/> (that one isn't run by uGUI's input module).
    /// </remarks>
    [DisallowMultipleComponent]
    [AddComponentMenu("Threads/TouchController/Intention Drag Relay")]
    public sealed class IntentionDragRelay : MonoBehaviour, IInitializePotentialDragHandler, IBeginDragHandler, IDragHandler, IEndDragHandler
    {
        /// <summary>True while uGUI considers the element dragged.</summary>
        public bool IsDragging { get; private set; }

        public void OnInitializePotentialDrag(PointerEventData eventData) => eventData.useDragThreshold = true;

        public void OnBeginDrag(PointerEventData eventData) => IsDragging = true;

        public void OnDrag(PointerEventData eventData)
        {
        }

        public void OnEndDrag(PointerEventData eventData) => IsDragging = false;

        private void OnDisable() => IsDragging = false;
    }
}
