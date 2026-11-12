using System.Collections.Generic;
using NUnit.Framework;
using Threads.TouchController.Intentions;
using Threads.TouchController.Intentions.Testing;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace Threads.TouchController.Tests.Intentions
{
    /// <summary>The Unity-facing helpers (Threads.TouchController.Intentions.Unity): UI, physics and uGUI sync.</summary>
    public class UnityIntentionHelperTests
    {
        private readonly List<Object> _created = new List<Object>();

        [TearDown]
        public void TearDown()
        {
            foreach (var o in _created)
                if (o != null) Object.DestroyImmediate(o);
            _created.Clear();
        }

        private GameObject Make(string name, params System.Type[] components)
        {
            var go = new GameObject(name, components);
            _created.Add(go);
            return go;
        }

        private static IntentionPoint AtPx(Vector2 px) => new IntentionPoint(1, 0, px, 1f, new Vector2(1000, 1000), 0.0);

        // ---- EventSystemSync -------------------------------------------------------------------------

        [Test]
        public void EventSystemSync_DragThresholdIsTheSlopInPixels()
        {
            using var h = new IntentionSystemHarness(pixelsPerDp: 2.5f);
            var eventSystem = Make("EventSystem", typeof(EventSystem)).GetComponent<EventSystem>();

            EventSystemSync.Apply(eventSystem, h.Touch.System);

            Assert.AreEqual(20, eventSystem.pixelDragThreshold); // Android slop 8 dp × 2.5
        }

        // ---- RectTransformArea ------------------------------------------------------------------------

        [Test]
        public void RectTransformArea_InsideOutsideAndInactive()
        {
            var go = Make("Frame", typeof(RectTransform));
            var rect = (RectTransform)go.transform;
            rect.sizeDelta = new Vector2(50, 50);
            rect.position = new Vector3(100, 100, 0); // with no camera, world = screen

            var area = new RectTransformArea(rect);
            Assert.IsTrue(area.Accepts(AtPx(new Vector2(110, 90)), out var target));
            Assert.AreSame(rect, target);
            Assert.IsFalse(area.Accepts(AtPx(new Vector2(140, 100)), out _));

            go.SetActive(false);
            Assert.IsFalse(area.Accepts(AtPx(new Vector2(110, 90)), out _));
        }

        // ---- UiRaycastArea --------------------------------------------------------------------------
        // uGUI's raycasters only register in Play Mode, so here a scripted raycast stands in for
        // EventSystem.RaycastAll, and these tests cover what the area does with the hits. The real raycast is
        // tested in Tests/Intentions/PlayMode.

        /// <summary>A UiRaycastArea whose raycast returns <paramref name="hits"/> (top-most first) and records the pointer it was asked about.</summary>
        private static UiRaycastArea ScriptedUi(EventSystem eventSystem, Transform root, System.Type component,
            List<PointerEventData> asked, params GameObject[] hits) =>
            new UiRaycastArea(eventSystem, root, component, (data, results) =>
            {
                asked?.Add(data);
                foreach (var hit in hits)
                    results.Add(new RaycastResult { gameObject = hit });
            });

        private (EventSystem eventSystem, GameObject frame, GameObject button, GameObject label, GameObject outside) UiTree()
        {
            var eventSystem = Make("EventSystem", typeof(EventSystem)).GetComponent<EventSystem>();
            var frame = Make("Frame", typeof(RectTransform));
            var button = Make("Button", typeof(RectTransform), typeof(Button));
            button.transform.SetParent(frame.transform, false);
            var label = Make("Label", typeof(RectTransform));
            label.transform.SetParent(button.transform, false);
            var outside = Make("Outside", typeof(RectTransform), typeof(Button));
            return (eventSystem, frame, button, label, outside);
        }

        [Test]
        public void UiRaycast_AsksAboutTheFingerInPixels()
        {
            var (eventSystem, frame, _, _, _) = UiTree();
            var asked = new List<PointerEventData>();
            var area = ScriptedUi(eventSystem, null, null, asked, frame);

            Assert.IsTrue(area.Accepts(new IntentionPoint(3, 7, new Vector2(10, 20), 2f, new Vector2(400, 800), 0.0), out var target));
            Assert.AreSame(frame, target, "No component type: the target is the GameObject hit");
            Assert.AreEqual(new Vector2(20, 40), asked[0].position);
            Assert.AreEqual(7, asked[0].pointerId, "The Input System touch ID, as uGUI uses it");
        }

        [Test]
        public void UiRaycast_NothingHit_OrHitDestroyed_Rejects()
        {
            var (eventSystem, _, _, _, outside) = UiTree();
            Assert.IsFalse(ScriptedUi(eventSystem, null, null, null).Accepts(AtPx(Vector2.zero), out _));

            var gone = ScriptedUi(eventSystem, null, null, null, outside);
            Object.DestroyImmediate(outside);
            Assert.IsFalse(gone.Accepts(AtPx(Vector2.zero), out _));
        }

        [Test]
        public void UiRaycast_OnlyTheTopHitCounts_AndMustBeInsideTheRoot()
        {
            var (eventSystem, frame, button, _, outside) = UiTree();
            Assert.IsTrue(ScriptedUi(eventSystem, frame.transform, null, null, button, outside).Accepts(AtPx(Vector2.zero), out var target));
            Assert.AreSame(button, target);
            Assert.IsFalse(ScriptedUi(eventSystem, frame.transform, null, null, outside, button).Accepts(AtPx(Vector2.zero), out _),
                "The top hit covers the frame: the finger isn't on it");
        }

        [Test]
        public void UiRaycast_ComponentFilter()
        {
            var (eventSystem, frame, button, label, outside) = UiTree();

            Assert.IsTrue(UiRaycastAreaFor<Button>(eventSystem, frame, label).Accepts(AtPx(Vector2.zero), out var target));
            Assert.AreSame(button.GetComponent<Button>(), target, "A hit on the button's label is a hit on the button");
            Assert.IsFalse(UiRaycastAreaFor<Button>(eventSystem, frame, frame).Accepts(AtPx(Vector2.zero), out _), "The frame isn't in a button");

            // The component must be inside the root too: a root inside the button doesn't make the button count.
            Assert.IsFalse(ScriptedUi(eventSystem, label.transform, typeof(Button), null, label).Accepts(AtPx(Vector2.zero), out _));
            Assert.IsTrue(ScriptedUi(eventSystem, null, typeof(Button), null, outside).Accepts(AtPx(Vector2.zero), out var any));
            Assert.AreSame(outside.GetComponent<Button>(), any, "No root: any button");
        }

        private static UiRaycastArea UiRaycastAreaFor<T>(EventSystem eventSystem, GameObject root, GameObject hit) where T : Component =>
            ScriptedUi(eventSystem, root.transform, typeof(T), null, hit);

        [Test]
        public void UiRaycast_DisabledEventSystem_Rejects()
        {
            var (eventSystem, frame, _, _, _) = UiTree();
            eventSystem.enabled = false;
            Assert.IsFalse(ScriptedUi(eventSystem, null, null, null, frame).Accepts(AtPx(Vector2.zero), out _));
        }

        // ---- UiRaycastArea.DragOwners ----------------------------------------------------------------

        private static IIntentionArea ScriptedOwners(EventSystem eventSystem, Transform root, params GameObject[] hits) =>
            UiRaycastArea.DragOwners(eventSystem, root, (data, results) =>
            {
                foreach (var hit in hits)
                    results.Add(new RaycastResult { gameObject = hit });
            });

        private GameObject Child(string name, GameObject parent, params System.Type[] components)
        {
            var go = Make(name, components);
            go.transform.SetParent(parent.transform, false);
            return go;
        }

        [Test]
        public void DragOwners_SliderAndScrollRectInsideTheRoot_AreOwners()
        {
            var (eventSystem, frame, button, label, _) = UiTree();
            var slider = Child("Slider", frame, typeof(RectTransform), typeof(Slider));
            var handle = Child("Handle", slider, typeof(RectTransform));
            var scroll = Child("Scroll", frame, typeof(RectTransform), typeof(ScrollRect));
            var inScroll = Child("Button in scroll", scroll, typeof(RectTransform), typeof(Button));

            Assert.IsTrue(ScriptedOwners(eventSystem, frame.transform, handle).Accepts(AtPx(Vector2.zero), out var target));
            Assert.AreSame(slider.GetComponent<Slider>(), target, "a hit on the slider's handle belongs to the slider");
            Assert.IsTrue(ScriptedOwners(eventSystem, frame.transform, inScroll).Accepts(AtPx(Vector2.zero), out target));
            Assert.AreSame(scroll.GetComponent<ScrollRect>(), target, "uGUI sends a drag on a button in a scroll view to the scroll view");

            Assert.IsFalse(ScriptedOwners(eventSystem, frame.transform, label).Accepts(AtPx(Vector2.zero), out _), "a button doesn't handle drags");
            Assert.IsFalse(ScriptedOwners(eventSystem, frame.transform, frame).Accepts(AtPx(Vector2.zero), out _));
            Assert.IsFalse(ScriptedOwners(eventSystem, frame.transform).Accepts(AtPx(Vector2.zero), out _), "nothing hit");
        }

        [Test]
        public void DragOwners_NonInteractableOrOutsideTheRoot_AreNotOwners()
        {
            var (eventSystem, frame, _, _, _) = UiTree();
            var slider = Child("Slider", frame, typeof(RectTransform), typeof(Slider));
            slider.GetComponent<Slider>().interactable = false;
            Assert.IsFalse(ScriptedOwners(eventSystem, frame.transform, slider).Accepts(AtPx(Vector2.zero), out _), "a disabled slider ignores drags");

            // The frame sits inside a scroll view: that scroll view is not an owner *inside* the frame.
            var scroll = Make("Scroll", typeof(RectTransform), typeof(ScrollRect));
            frame.transform.SetParent(scroll.transform, false);
            var label = Child("Label", frame, typeof(RectTransform));
            Assert.IsFalse(ScriptedOwners(eventSystem, frame.transform, label).Accepts(AtPx(Vector2.zero), out _));
            Assert.IsTrue(ScriptedOwners(eventSystem, null, label).Accepts(AtPx(Vector2.zero), out _), "no root: any owner counts");
        }

        [Test]
        public void DragOwners_ExcludedFromAFrameArea()
        {
            var (eventSystem, frame, button, _, _) = UiTree();
            var slider = Child("Slider", frame, typeof(RectTransform), typeof(Slider));
            var frameArea = new FuncArea(_ => true, _ => frame);

            Assert.IsTrue(Area.All(frameArea, Area.Not(ScriptedOwners(eventSystem, frame.transform, button))).Accepts(AtPx(Vector2.zero), out _));
            Assert.IsFalse(Area.All(frameArea, Area.Not(ScriptedOwners(eventSystem, frame.transform, slider))).Accepts(AtPx(Vector2.zero), out _));
        }

        [Test]
        public void UiRaycast_ArgumentChecks()
        {
            var (eventSystem, _, _, _, _) = UiTree();
            Assert.Throws<System.ArgumentNullException>(() => new UiRaycastArea(null));
            Assert.Throws<System.ArgumentException>(() => new UiRaycastArea(eventSystem, null, typeof(string)));
        }

        // ---- Physics -------------------------------------------------------------------------------------

        private Camera MakeCamera(bool orthographic)
        {
            var camera = Make("Camera", typeof(Camera)).GetComponent<Camera>();
            camera.orthographic = orthographic;
            camera.transform.position = new Vector3(0, 0, -10);
            return camera;
        }

#if THREADS_TC_PHYSICS
        [Test]
        public void Physics3D_HitsTheColliderAndFiltersByComponent()
        {
            var camera = MakeCamera(false);
            var box = Make("Unit", typeof(BoxCollider));
            box.transform.position = new Vector3(0, 0, 5);
            Physics.SyncTransforms();
            var onBox = (Vector2)camera.WorldToScreenPoint(box.transform.position);
            var offBox = (Vector2)camera.WorldToScreenPoint(box.transform.position + new Vector3(5, 0, 0));

            var area = new PhysicsRaycastArea(camera, PhysicsWorld.Physics3D);
            Assert.IsTrue(area.Accepts(AtPx(onBox), out var target));
            var hit = (WorldHit)target;
            Assert.AreSame(box, hit.GameObject);
            Assert.IsFalse(hit.Is2D);
            Assert.AreEqual(4.5f, hit.Point.z, 0.01f); // the box's front face
            Assert.IsFalse(area.Accepts(AtPx(offBox), out _));

            Assert.IsFalse(PhysicsRaycastArea.For<Rigidbody>(camera).Accepts(AtPx(onBox), out _));
            var body = box.AddComponent<Rigidbody>();
            body.isKinematic = true;
            Assert.IsTrue(PhysicsRaycastArea.For<Rigidbody>(camera).Accepts(AtPx(onBox), out var withBody));
            Assert.AreSame(body, ((WorldHit)withBody).Component);
        }

        [Test]
        public void Physics3D_FilteredOutColliderInFrontDoesNotHideOneBehind()
        {
            var camera = MakeCamera(false);
            Make("Wall", typeof(BoxCollider)).transform.position = new Vector3(0, 0, 2);
            var unit = Make("Unit", typeof(BoxCollider), typeof(Rigidbody));
            unit.GetComponent<Rigidbody>().isKinematic = true;
            unit.transform.position = new Vector3(0, 0, 6);
            Physics.SyncTransforms();
            var centre = (Vector2)camera.WorldToScreenPoint(Vector3.zero);

            Assert.IsTrue(PhysicsRaycastArea.For<Rigidbody>(camera).Accepts(AtPx(centre), out var target));
            Assert.AreSame(unit, ((WorldHit)target).GameObject);
        }
#endif

#if THREADS_TC_PHYSICS2D
        [Test]
        public void Physics2D_HitsTheCollider()
        {
            var camera = MakeCamera(true);
            var box = Make("Card", typeof(BoxCollider2D));
            Physics2D.SyncTransforms();
            var onBox = (Vector2)camera.WorldToScreenPoint(Vector3.zero);
            var offBox = (Vector2)camera.WorldToScreenPoint(new Vector3(3, 0, 0));

            var area = new PhysicsRaycastArea(camera, PhysicsWorld.Physics2D);
            Assert.IsTrue(area.Accepts(AtPx(onBox), out var target));
            var hit = (WorldHit)target;
            Assert.AreSame(box, hit.GameObject);
            Assert.IsTrue(hit.Is2D);
            Assert.AreEqual(10f - camera.nearClipPlane, hit.Distance, 0.01f); // the ray starts on the near plane; sprite plane at z = 0
            Assert.IsFalse(area.Accepts(AtPx(offBox), out _));
        }
#endif

#if THREADS_TC_PHYSICS && THREADS_TC_PHYSICS2D
        [TestCase(-2f, "Card")] // the 2D card is in front of the 3D box
        [TestCase(8f, "Box")]   // the 2D card is behind it
        public void Both_NearestHitWins(float cardZ, string expected)
        {
            var camera = MakeCamera(true);
            var box = Make("Box", typeof(BoxCollider));
            box.transform.position = new Vector3(0, 0, 3);
            var card = Make("Card", typeof(BoxCollider2D));
            card.transform.position = new Vector3(0, 0, cardZ);
            Physics.SyncTransforms();
            Physics2D.SyncTransforms();
            var centre = (Vector2)camera.WorldToScreenPoint(Vector3.zero);

            Assert.IsTrue(new PhysicsRaycastArea(camera).Accepts(AtPx(centre), out var target));
            Assert.AreEqual(expected, ((WorldHit)target).GameObject.name);

            // Restricting to one world ignores the other.
            new PhysicsRaycastArea(camera, PhysicsWorld.Physics3D).Accepts(AtPx(centre), out var only3D);
            Assert.AreSame(box, ((WorldHit)only3D).GameObject);
            new PhysicsRaycastArea(camera, PhysicsWorld.Physics2D).Accepts(AtPx(centre), out var only2D);
            Assert.AreSame(card, ((WorldHit)only2D).GameObject);
        }
#endif

        // ---- uGUI drag relay ----------------------------------------------------------------------------

        [Test]
        public void DragRelay_IsTheDragHandlerAboveTheButtons()
        {
            var frame = Make("Frame", typeof(RectTransform), typeof(IntentionDragRelay));
            var button = Make("Button", typeof(RectTransform), typeof(Button));
            button.transform.SetParent(frame.transform, false);

            // uGUI drags whatever ExecuteEvents finds here; when the drag starts it releases the button without a click.
            Assert.AreSame(frame, ExecuteEvents.GetEventHandler<IDragHandler>(button));

            var eventSystem = Make("EventSystem", typeof(EventSystem)).GetComponent<EventSystem>();
            var data = new PointerEventData(eventSystem) { useDragThreshold = false };
            frame.GetComponent<IntentionDragRelay>().OnInitializePotentialDrag(data);
            Assert.IsTrue(data.useDragThreshold);
        }

        [Test]
        public void DragRelay_TracksTheDrag()
        {
            var relay = Make("Frame", typeof(RectTransform), typeof(IntentionDragRelay)).GetComponent<IntentionDragRelay>();
            var data = new PointerEventData(Make("EventSystem", typeof(EventSystem)).GetComponent<EventSystem>());

            Assert.IsFalse(relay.IsDragging);
            relay.OnBeginDrag(data);
            relay.OnDrag(data);
            Assert.IsTrue(relay.IsDragging);
            relay.OnEndDrag(data);
            Assert.IsFalse(relay.IsDragging);
            // OnDisable only runs in Play Mode: disabling mid-drag is tested in Tests/Intentions/PlayMode.
        }

        // ---- UiPointerIntention -------------------------------------------------------------------------

        /// <summary>Records the pointer events Unity UI would send (EventTrigger: test components can't come from an Editor assembly).</summary>
        private static List<string> RecordPointer(GameObject go)
        {
            var log = new List<string>();
            var trigger = go.AddComponent<EventTrigger>();
            void On(EventTriggerType type, string name)
            {
                var entry = new EventTrigger.Entry { eventID = type };
                entry.callback.AddListener(_ => log.Add(name));
                trigger.triggers.Add(entry);
            }
            On(EventTriggerType.PointerEnter, "Enter");
            On(EventTriggerType.PointerDown, "Down");
            On(EventTriggerType.PointerUp, "Up");
            On(EventTriggerType.PointerClick, "Click");
            On(EventTriggerType.PointerExit, "Exit");
            return log;
        }

        private (IntentionSystemHarness h, List<string> log, Button button, UiPointerIntention ui) PointerSetup()
        {
            var h = new IntentionSystemHarness();
            var eventSystem = Make("EventSystem", typeof(EventSystem)).GetComponent<EventSystem>();
            var go = Make("Button", typeof(RectTransform), typeof(Button));
            var button = go.GetComponent<Button>();
            // The button covers x < 200 dp (a FuncArea stands in for the UI raycast, tested above).
            var ui = new UiPointerIntention(h.Intentions, eventSystem, Area.From(p => p.PositionDp.x < 200, p => button));
            return (h, RecordPointer(go), button, ui);
        }

        [Test]
        public void UiPointer_TapPressesAndClicks()
        {
            var (h, log, button, ui) = PointerSetup();
            using (h)
            {
                var clicks = 0;
                button.onClick.AddListener(() => clicks++);
                ui.Enabled = true;

                h.Input.Down(1, new Vector2(100, 100));
                h.Input.Step();
                CollectionAssert.AreEqual(new[] { "Enter", "Down" }, log, "Pressed look at touch-down");
                h.Input.Up(1);
                h.Input.Step();

                CollectionAssert.AreEqual(new[] { "Enter", "Down", "Up", "Click", "Exit" }, log);
                Assert.AreEqual(1, clicks);
                ui.Dispose();
            }
        }

        [Test]
        public void UiPointer_RootConstructor_EnabledAndGameObjectTargets()
        {
            using var h = new IntentionSystemHarness();
            var eventSystem = Make("EventSystem", typeof(EventSystem)).GetComponent<EventSystem>();
            var root = Make("Root", typeof(RectTransform));

            using (var byRoot = new UiPointerIntention(h.Intentions, eventSystem, root.transform))
            {
                Assert.IsFalse(byRoot.Enabled);
                byRoot.Enabled = true;
                Assert.IsTrue(byRoot.Enabled);
                Assert.IsTrue(byRoot.Intention.Enabled);
                Assert.Throws<System.ArgumentNullException>(() => new UiPointerIntention(null, eventSystem, root.transform));
                Assert.Throws<System.ArgumentNullException>(() => new UiPointerIntention(h.Intentions, null, (IIntentionArea)null));
            }

            // An area may report a GameObject rather than a component; anything else is ignored.
            var target = Make("Target", typeof(RectTransform));
            var log = RecordPointer(target);
            using var ui = new UiPointerIntention(h.Intentions, eventSystem, Area.From(p => true, p => p.PositionDp.x < 200 ? (object)target : "not a UI element"));
            ui.Enabled = true;
            foreach (var x in new[] { 100f, 300f })
            {
                h.Input.Down(1, new Vector2(x, 100));
                h.Input.Step();
                h.Input.Up(1);
                h.Input.Step();
                h.Input.Hold(400);
            }
            CollectionAssert.AreEqual(new[] { "Enter", "Down", "Up", "Click", "Exit" }, log, "Pressed once: the second target was not a UI element");
        }

        [Test]
        public void UiPointer_FrameDragTakesTheFinger_NoClick()
        {
            var (h, log, button, ui) = PointerSetup();
            using (h)
            {
                var clicks = 0;
                button.onClick.AddListener(() => clicks++);
                ui.Enabled = true;
                var frame = h.Enable(new Intention("Frame", h.Intentions).Bind<DragGesture>().WithPriority(10).Exclusive());

                h.Input.Down(1, new Vector2(100, 100));
                h.Input.Step();
                h.Input.Drag(1, new Vector2(300, 100), 200);
                h.Input.Up(1);
                h.Input.Step();

                CollectionAssert.AreEqual(new[] { "Enter", "Down", "Up", "Exit" }, log);
                Assert.AreEqual(0, clicks);
                Assert.IsEmpty(h.Violations());
                ui.Dispose();
            }
        }
    }
}
