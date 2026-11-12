using System;
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
using Object = UnityEngine.Object;

namespace Threads.TouchController.Tests.Intentions
{
    /// <summary>
    /// Unity's default controls inside a frame dragged by an intention, with the real uGUI input module, a
    /// simulated touchscreen, and the TouchSystem/IntentionSystem run by an <see cref="IntentionRunner"/>.
    /// These tests are the measured version of plan.md §10's table: which controls work alongside a frame drag,
    /// which need <see cref="UiRaycastArea.DragOwners"/> or <see cref="Intention.StartWhen"/>, and what
    /// <see cref="UiPointerIntention"/> (mode b) cannot do.
    /// </summary>
    public class DefaultControlsPlayTests : InputTestFixture
    {
        private readonly List<GameObject> _created = new List<GameObject>();
        private IntentionRunner _runner;
        private EventSystem _eventSystem;
        private RectTransform _frame;
        private Vector2 _frameMoved;
        private bool _moveFrame = true;
        private Intention _frameDrag;
        private Font _font;

        public override void TearDown()
        {
            foreach (var go in _created)
                if (go != null) Object.Destroy(go);
            _created.Clear();
            _frameDrag = null;
            base.TearDown();
        }

        // ---- Scene -----------------------------------------------------------------------------------

        /// <summary>
        /// An EventSystem (with or without uGUI's input module), a runner, and a 500 × 500 frame with an
        /// <see cref="IntentionDragRelay"/> in the middle of an overlay canvas.
        /// </summary>
        private IEnumerator Build(bool withInputModule = true)
        {
            InputSystem.AddDevice<Touchscreen>();
            _font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

            var eventSystemGo = Keep(new GameObject("EventSystem", typeof(EventSystem)));
            if (withInputModule)
                eventSystemGo.AddComponent<InputSystemUIInputModule>().AssignDefaultActions();
            _eventSystem = eventSystemGo.GetComponent<EventSystem>();

            var runnerGo = new GameObject("Runner");
            runnerGo.SetActive(false);
            Keep(runnerGo);
            _runner = runnerGo.AddComponent<IntentionRunner>();
            _runner.SimulateTouchInEditor = false;
            _runner.PlatformOverride = TouchPlatform.Android;
            _runner.EventSystem = _eventSystem;
            runnerGo.SetActive(true);

            var canvas = Keep(new GameObject("Canvas", typeof(Canvas), typeof(GraphicRaycaster)));
            canvas.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;

            _frame = Rect("Frame", canvas.transform, new Vector2(500, 500), Vector2.zero, typeof(Image));
            _frame.gameObject.AddComponent<IntentionDragRelay>();
            _frameMoved = Vector2.zero;
            _moveFrame = true;
            yield return null;
        }

        /// <summary>The frame's drag intention: priority 10, exclusive, inside the frame, optionally minus drag owners.</summary>
        private Intention FrameDrag(bool excludeDragOwners, Func<IntentionEventData, bool> startWhen = null)
        {
            IIntentionArea area = new RectTransformArea(_frame);
            if (excludeDragOwners)
                area = Area.All(area, Area.Not(UiRaycastArea.DragOwners(_eventSystem, _frame)));
            _frameDrag = new Intention("Frame drag", _runner.Intentions)
                .Bind<DragGesture>()
                .InArea(area)
                .WithPriority(10)
                .Exclusive()
                .StartWhen(startWhen);
            _frameDrag.Performed += e =>
            {
                if (_moveFrame) _frame.anchoredPosition += e.DeltaPx;
                _frameMoved += e.DeltaPx;
            };
            _frameDrag.Enabled = true;
            return _frameDrag;
        }

        private GameObject Keep(GameObject go)
        {
            _created.Add(go);
            return go;
        }

        private static RectTransform Rect(string name, Transform parent, Vector2 size, Vector2 position, params Type[] components)
        {
            var go = new GameObject(name, typeof(RectTransform));
            foreach (var c in components) go.AddComponent(c);
            var rect = (RectTransform)go.transform;
            rect.SetParent(parent, false);
            rect.sizeDelta = size;
            rect.anchoredPosition = position;
            return rect;
        }

        private static RectTransform Stretch(string name, Transform parent, params Type[] components)
        {
            var rect = Rect(name, parent, Vector2.zero, Vector2.zero, components);
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
            return rect;
        }

        /// <summary>A horizontal slider (300 × 40, value 0.5) with a 40 × 40 handle.</summary>
        private Slider MakeSlider(Vector2 position, Slider.Direction direction = Slider.Direction.LeftToRight)
        {
            var size = direction == Slider.Direction.LeftToRight ? new Vector2(300, 40) : new Vector2(40, 300);
            var slider = Rect("Slider", _frame, size, position, typeof(Image), typeof(Slider)).GetComponent<Slider>();
            var area = Stretch("Handle Slide Area", slider.transform);
            var handle = Rect("Handle", area, new Vector2(40, 40), Vector2.zero, typeof(Image));
            slider.direction = direction;
            slider.handleRect = handle;
            slider.targetGraphic = handle.GetComponent<Image>();
            slider.SetValueWithoutNotify(0.5f);
            return slider;
        }

        /// <summary>A vertical scroll view (300 × 200) whose content (300 × 800) holds a button that fills its top part.</summary>
        private (ScrollRect scroll, Button button) MakeScrollView(Vector2 position)
        {
            var scroll = Rect("Scroll View", _frame, new Vector2(300, 200), position, typeof(Image), typeof(ScrollRect)).GetComponent<ScrollRect>();
            var viewport = Stretch("Viewport", scroll.transform, typeof(RectMask2D), typeof(Image));
            var content = Rect("Content", viewport, new Vector2(0, 800), Vector2.zero);
            content.anchorMin = new Vector2(0, 1);
            content.anchorMax = new Vector2(1, 1);
            content.pivot = new Vector2(0.5f, 1);
            content.anchoredPosition = Vector2.zero;
            var button = Rect("Button", content, new Vector2(0, 400), Vector2.zero, typeof(Image), typeof(Button)).GetComponent<Button>();
            var b = (RectTransform)button.transform;
            b.anchorMin = new Vector2(0, 1);
            b.anchorMax = new Vector2(1, 1);
            b.pivot = new Vector2(0.5f, 1);
            b.anchoredPosition = Vector2.zero;
            scroll.viewport = viewport;
            scroll.content = content;
            scroll.horizontal = false;
            scroll.vertical = true;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            scroll.inertia = false;
            return (scroll, button);
        }

        private Toggle MakeToggle(Vector2 position)
        {
            var toggle = Rect("Toggle", _frame, new Vector2(200, 200), position, typeof(Image), typeof(Toggle)).GetComponent<Toggle>();
            toggle.targetGraphic = toggle.GetComponent<Image>();
            toggle.SetIsOnWithoutNotify(false);
            return toggle;
        }

        private InputField MakeInputField(Vector2 position)
        {
            var field = Rect("InputField", _frame, new Vector2(300, 60), position, typeof(Image), typeof(InputField)).GetComponent<InputField>();
            var text = Stretch("Text", field.transform, typeof(Text)).GetComponent<Text>();
            text.font = _font;
            text.supportRichText = false;
            field.textComponent = text;
            field.targetGraphic = field.GetComponent<Image>();
            field.text = "Some text to select";
            return field;
        }

        private static Vector2 Centre(Component c)
        {
            Canvas.ForceUpdateCanvases();
            return RectTransformUtility.WorldToScreenPoint(null, c.transform.position);
        }

        // ---- Input -----------------------------------------------------------------------------------

        /// <summary>Touch at <paramref name="from"/>, move by <paramref name="by"/> in 15 px steps (one per frame), lift.</summary>
        private IEnumerator Drag(Vector2 from, Vector2 by)
        {
            BeginTouch(1, from);
            yield return null;
            var steps = Mathf.Max(1, Mathf.CeilToInt(by.magnitude / 15f));
            for (var i = 1; i <= steps; i++)
            {
                MoveTouch(1, from + by * ((float)i / steps));
                yield return null;
            }
            EndTouch(1, from + by);
            yield return null;
            yield return null;
        }

        private IEnumerator Tap(Vector2 at)
        {
            BeginTouch(1, at);
            yield return null;
            EndTouch(1, at);
            yield return null;
            yield return null;
        }

        private static bool Horizontal(IntentionEventData e)
        {
            var d = e.PositionDp - e.StartPositionDp;
            return Mathf.Abs(d.x) > Mathf.Abs(d.y);
        }

        // ---- (a) uGUI owns its controls ---------------------------------------------------------------

        [UnityTest]
        public IEnumerator Toggle_ADragMovesTheFrameWithoutToggling_ATapToggles()
        {
            yield return Build();
            var toggle = MakeToggle(Vector2.zero);
            FrameDrag(excludeDragOwners: true);
            yield return null;

            yield return Drag(Centre(toggle), new Vector2(120, 0));
            Assert.Greater(_frameMoved.x, 60f, "the frame followed the drag");
            Assert.IsFalse(toggle.isOn, "the relay made uGUI release the toggle without a click");

            yield return Tap(Centre(toggle));
            Assert.IsTrue(toggle.isOn);
        }

        [UnityTest]
        public IEnumerator Slider_WithoutExclusion_TheSliderAndTheFrameBothMove()
        {
            // Why DragOwners exists: uGUI sends the drag to the slider (the nearest drag handler), so the frame's
            // relay never sees it, while the intention, which knows nothing about uGUI, moves the frame too.
            yield return Build();
            var slider = MakeSlider(Vector2.zero);
            FrameDrag(excludeDragOwners: false);
            _moveFrame = false; // record the frame's drag only, so the slider stays put under the finger
            yield return null;

            yield return Drag(Centre(slider.handleRect), new Vector2(90, 0));
            Assert.Greater(slider.value, 0.5f, "the slider moved");
            Assert.Greater(_frameMoved.x, 45f, "and so did the frame");
        }

        [UnityTest]
        public IEnumerator Slider_WithExclusion_OnlyTheSliderMoves_AndTheFrameStillDragsElsewhere()
        {
            yield return Build();
            var slider = MakeSlider(new Vector2(0, 150));
            FrameDrag(excludeDragOwners: true);
            yield return null;

            yield return Drag(Centre(slider.handleRect), new Vector2(90, 0));
            Assert.Greater(slider.value, 0.5f);
            Assert.AreEqual(Vector2.zero, _frameMoved, "a drag that starts on the slider is the slider's");

            var value = slider.value;
            yield return Drag(Centre(_frame) - new Vector2(0, 100), new Vector2(90, 0));
            Assert.Greater(_frameMoved.x, 45f, "elsewhere on the frame, the frame drags");
            Assert.AreEqual(value, slider.value);
        }

        [UnityTest]
        public IEnumerator ScrollView_WithExclusion_ScrollsWithoutMovingTheFrame_OrClickingItsButton()
        {
            yield return Build();
            var (scroll, button) = MakeScrollView(Vector2.zero);
            var clicks = 0;
            button.onClick.AddListener(() => clicks++);
            FrameDrag(excludeDragOwners: true);
            yield return null;

            yield return Drag(Centre(scroll), new Vector2(0, 90));
            Assert.Greater(scroll.content.anchoredPosition.y, 20f, "the content scrolled up");
            Assert.AreEqual(Vector2.zero, _frameMoved);
            Assert.AreEqual(0, clicks, "uGUI's scroll view cancels the click on its own button");

            yield return Tap(Centre(scroll));
            Assert.AreEqual(1, clicks, "a tap on the button in the scroll view still clicks");
        }

        [UnityTest]
        public IEnumerator ScrollView_AxisLock_VerticalScrolls_HorizontalMovesTheFrame()
        {
            // The perpendicular case, without the exclusion: a frame that only drags sideways (StartWhen)
            // over a vertical scroll view. uGUI's scroll view ignores the horizontal part of a drag.
            yield return Build();
            var (scroll, _) = MakeScrollView(Vector2.zero);
            FrameDrag(excludeDragOwners: false, startWhen: Horizontal);
            yield return null;

            yield return Drag(Centre(scroll), new Vector2(0, 90));
            Assert.Greater(scroll.content.anchoredPosition.y, 20f, "a vertical drag scrolls");
            Assert.AreEqual(Vector2.zero, _frameMoved, "and is rejected by the frame (StartConditionFailed)");

            var scrolled = scroll.content.anchoredPosition.y;
            yield return Drag(Centre(scroll), new Vector2(120, 0));
            Assert.Greater(_frameMoved.x, 60f, "a horizontal drag moves the frame");
            Assert.AreEqual(scrolled, scroll.content.anchoredPosition.y, 0.5f, "and doesn't scroll");
        }

        [UnityTest]
        public IEnumerator InputField_ATapFocusesIt_AndWithExclusionADragDoesNotMoveTheFrame()
        {
            yield return Build();
            var field = MakeInputField(Vector2.zero);
            FrameDrag(excludeDragOwners: true);
            yield return null;

            yield return Tap(Centre(field));
            yield return null;
            Assert.IsTrue(field.isFocused, "a tap focuses the field (the on-screen keyboard opens on devices)");

            yield return Drag(Centre(field), new Vector2(-90, 0));
            Assert.AreEqual(Vector2.zero, _frameMoved, "a drag on the field selects text instead of moving the frame");
        }

        // ---- (b) intentions drive uGUI: UiPointerIntention ---------------------------------------------

        [UnityTest]
        public IEnumerator UiPointerIntention_TapsWork_ButDragControlsDoNot()
        {
            yield return Build(withInputModule: false);
            var toggle = MakeToggle(new Vector2(0, 120));
            var slider = MakeSlider(new Vector2(0, -150));
            using var pointer = new UiPointerIntention(_runner.Intentions, _eventSystem, _frame);
            pointer.Enabled = true;
            yield return null;

            yield return Tap(Centre(toggle));
            Assert.IsTrue(toggle.isOn, "tap controls work without uGUI's input module");

            yield return Drag(Centre(slider.handleRect), new Vector2(90, 0));
            Assert.AreEqual(0.5f, slider.value, 1e-4f, "it sends no drag events, so sliders (and scroll views, text selection) don't move");
        }
    }
}
