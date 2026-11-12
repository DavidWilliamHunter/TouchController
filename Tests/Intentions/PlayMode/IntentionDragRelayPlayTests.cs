using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using Threads.TouchController.Intentions;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace Threads.TouchController.Tests.Intentions
{
    /// <summary>
    /// <see cref="IntentionDragRelay"/> with the real uGUI input module and a simulated touchscreen: a drag that
    /// starts on a button inside the frame must not click it (that is the relay's whole job), while a tap does.
    /// </summary>
    public class IntentionDragRelayPlayTests : InputTestFixture
    {
        private readonly List<GameObject> _created = new List<GameObject>();
        private IntentionDragRelay _relay;
        private Vector2 _buttonCentre;
        private int _clicks;

        public override void TearDown()
        {
            foreach (var go in _created)
                if (go != null) Object.Destroy(go);
            _created.Clear();
            base.TearDown();
        }

        /// <summary>A frame (optionally with the relay) holding a large button, run by uGUI's Input System module.</summary>
        private IEnumerator Build(bool withRelay)
        {
            InputSystem.AddDevice<Touchscreen>();

            var eventSystemGo = new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
            _created.Add(eventSystemGo);
            eventSystemGo.GetComponent<InputSystemUIInputModule>().AssignDefaultActions();
            eventSystemGo.GetComponent<EventSystem>().pixelDragThreshold = 10;

            var canvas = new GameObject("Canvas", typeof(Canvas), typeof(GraphicRaycaster));
            _created.Add(canvas);
            canvas.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;

            var frame = new GameObject("Frame", typeof(RectTransform), typeof(Image));
            frame.transform.SetParent(canvas.transform, false);
            ((RectTransform)frame.transform).sizeDelta = new Vector2(300, 300);
            if (withRelay)
                _relay = frame.AddComponent<IntentionDragRelay>();

            var button = new GameObject("Button", typeof(RectTransform), typeof(Image), typeof(Button));
            button.transform.SetParent(frame.transform, false);
            ((RectTransform)button.transform).sizeDelta = new Vector2(200, 200);
            _clicks = 0;
            button.GetComponent<Button>().onClick.AddListener(() => _clicks++);

            yield return null;
            Canvas.ForceUpdateCanvases();
            _buttonCentre = RectTransformUtility.WorldToScreenPoint(null, button.transform.position);
        }

        /// <summary>Touch the button's centre, drag 60 px (past the 10 px threshold, still on the button), lift.</summary>
        private IEnumerator DragOnTheButton(System.Action whileDragging = null)
        {
            BeginTouch(1, _buttonCentre);
            yield return null;
            for (var x = 15; x <= 60; x += 15)
            {
                MoveTouch(1, _buttonCentre + new Vector2(x, 0));
                yield return null;
            }
            whileDragging?.Invoke();
            EndTouch(1, _buttonCentre + new Vector2(60, 0));
            yield return null;
            yield return null;
        }

        [UnityTest]
        public IEnumerator WithoutTheRelay_ADragOnTheButtonClicksIt()
        {
            // Why the relay exists: uGUI on its own clicks a button released over it, however far the finger moved.
            yield return Build(withRelay: false);
            yield return DragOnTheButton();
            Assert.AreEqual(1, _clicks);
        }

        [UnityTest]
        public IEnumerator WithTheRelay_ADragOnTheButtonDoesNotClickIt()
        {
            yield return Build(withRelay: true);
            var draggingMidway = false;
            yield return DragOnTheButton(() => draggingMidway = _relay.IsDragging);

            Assert.IsTrue(draggingMidway, "uGUI dragged the frame (through the relay)");
            Assert.IsFalse(_relay.IsDragging, "and ended the drag on release");
            Assert.AreEqual(0, _clicks, "so it released the button without a click");
        }

        [UnityTest]
        public IEnumerator WithTheRelay_ATapStillClicks()
        {
            yield return Build(withRelay: true);
            BeginTouch(1, _buttonCentre);
            yield return null;
            Assert.IsFalse(_relay.IsDragging);
            EndTouch(1, _buttonCentre);
            yield return null;
            yield return null;

            Assert.AreEqual(1, _clicks);
            Assert.IsFalse(_relay.IsDragging);
        }

        [UnityTest]
        public IEnumerator DisablingTheRelayMidDrag_ClearsIsDragging()
        {
            yield return Build(withRelay: true);
            var disabledWhileDragging = false;
            yield return DragOnTheButton(() =>
            {
                disabledWhileDragging = _relay.IsDragging;
                _relay.enabled = false;
                disabledWhileDragging &= !_relay.IsDragging;
            });
            Assert.IsTrue(disabledWhileDragging);
        }
    }
}
