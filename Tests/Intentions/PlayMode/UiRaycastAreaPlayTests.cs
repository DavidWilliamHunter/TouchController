using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using Threads.TouchController.Intentions;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace Threads.TouchController.Tests
{
    /// <summary>UiRaycastArea against real uGUI raycasting (raycasters register in OnEnable, so this runs in Play Mode).</summary>
    public class UiRaycastAreaPlayTests
    {
        private readonly List<GameObject> _created = new List<GameObject>();

        [TearDown]
        public void TearDown()
        {
            foreach (var go in _created)
                if (go != null) Object.Destroy(go);
            _created.Clear();
        }

        private GameObject Make(string name, Transform parent, params System.Type[] components)
        {
            var go = new GameObject(name, components);
            if (parent != null) go.transform.SetParent(parent, false);
            else _created.Add(go);
            return go;
        }

        private static IntentionPoint AtPx(Vector2 px) => new IntentionPoint(1, 0, px, 1f, new Vector2(Screen.width, Screen.height), 0.0);

        [UnityTest]
        public IEnumerator FindsTheButtonUnderTheFinger_InsideTheRoot()
        {
            var eventSystem = Make("EventSystem", null, typeof(EventSystem)).GetComponent<EventSystem>();
            var canvas = Make("Canvas", null, typeof(Canvas), typeof(GraphicRaycaster));
            canvas.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
            var frame = Make("Frame", canvas.transform, typeof(RectTransform), typeof(Image));
            ((RectTransform)frame.transform).sizeDelta = new Vector2(200, 200);
            var button = Make("Button", frame.transform, typeof(RectTransform), typeof(Image), typeof(Button));
            ((RectTransform)button.transform).sizeDelta = new Vector2(40, 20);
            var other = Make("Other", canvas.transform, typeof(RectTransform), typeof(Image));
            ((RectTransform)other.transform).anchoredPosition = new Vector2(0, -150);
            ((RectTransform)other.transform).sizeDelta = new Vector2(40, 20);
            yield return null;
            Canvas.ForceUpdateCanvases();

            var buttonCentre = (Vector2)RectTransformUtility.WorldToScreenPoint(null, button.transform.position);
            var frameOnly = buttonCentre + new Vector2(60, 60);
            var otherCentre = (Vector2)RectTransformUtility.WorldToScreenPoint(null, other.transform.position);

            var buttons = UiRaycastArea.For<Button>(eventSystem, frame.transform);
            Assert.IsTrue(buttons.Accepts(AtPx(buttonCentre), out var target), "Raycast should hit the button");
            Assert.AreSame(button.GetComponent<Button>(), target);
            Assert.IsFalse(buttons.Accepts(AtPx(frameOnly), out _), "Frame, not a button");

            var inFrame = new UiRaycastArea(eventSystem, frame.transform);
            Assert.IsTrue(inFrame.Accepts(AtPx(frameOnly), out var hit));
            Assert.AreSame(frame, hit);
            Assert.IsFalse(inFrame.Accepts(AtPx(otherCentre), out _), "UI outside the root");
            Assert.IsTrue(new UiRaycastArea(eventSystem).Accepts(AtPx(otherCentre), out _), "Any UI");
        }
    }
}
