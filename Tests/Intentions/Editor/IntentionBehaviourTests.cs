using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using NUnit.Framework;
using Threads.TouchController.Intentions;
using Threads.TouchController.Intentions.Testing;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace Threads.TouchController.Tests.Intentions
{
    /// <summary>
    /// IntentionBehaviour's set-up through Attach (OnEnable/OnDisable don't run in edit mode; the runner and the
    /// component lifecycle are tested in Play Mode).
    /// </summary>
    public class IntentionBehaviourTests
    {
        private IntentionSystemHarness _h;
        private readonly List<Object> _created = new List<Object>();

        [SetUp]
        public void SetUp() => _h = new IntentionSystemHarness();

        [TearDown]
        public void TearDown()
        {
            foreach (var o in _created)
                if (o != null) Object.DestroyImmediate(o);
            _created.Clear();
            _h?.Dispose();
        }

        private static GestureBase Asset<T>() where T : GestureBase =>
            DefaultTouchSettings.Load(TouchPlatform.Android).Lineup.First(e => e.gesture is T).gesture;

        private IntentionBehaviour Make(string name, params System.Type[] components)
        {
            var go = new GameObject(name, components);
            _created.Add(go);
            return go.AddComponent<IntentionBehaviour>();
        }

        private void DragFinger(int id, Vector2 from, Vector2 to)
        {
            _h.Input.Down(id, from);
            _h.Input.Step();
            _h.Input.Drag(id, to, 200);
            _h.Input.Up(id);
            _h.Input.Step();
        }

        [Test]
        public void Attach_BuildsTheIntention_AndForwardsEveryEvent()
        {
            var behaviour = Make("Card");
            behaviour.Gestures.Add(Asset<DragGesture>());
            behaviour.Priority = 3;
            behaviour.Claim = ClaimMode.Exclusive;
            var seen = new List<string>();
            behaviour.OnPossible.AddListener(e => seen.Add("Possible"));
            behaviour.OnStarted.AddListener(e => seen.Add("Started"));
            behaviour.OnPerformed.AddListener(e => { if (seen.Last() != "Performed") seen.Add("Performed"); });
            behaviour.OnEnded.AddListener(e => seen.Add($"Ended({e.Reason})"));
            behaviour.OnRejected.AddListener(e => seen.Add($"Rejected({e.Reason})"));

            behaviour.Attach(_h.Intentions);

            Assert.AreEqual("Card", behaviour.Intention.Name);
            Assert.AreEqual(3, behaviour.Intention.Priority);
            Assert.AreEqual(ClaimMode.Exclusive, behaviour.Intention.Claim);
            Assert.IsTrue(behaviour.Intention.Enabled);
            DragFinger(1, new Vector2(100, 100), new Vector2(200, 100));
            Tap(2, new Vector2(100, 100)); // a drag that never moved: Rejected
            CollectionAssert.AreEqual(new[] { "Possible", "Started", "Performed", "Ended(Completed)", "Possible", "Rejected(GestureFailed)" }, seen);

            behaviour.Priority = 7; // live changes reach the intention
            Assert.AreEqual(7, behaviour.Intention.Priority);
        }

        private void Tap(int id, Vector2 at)
        {
            _h.Input.Down(id, at);
            _h.Input.Step();
            _h.Input.Up(id);
            _h.Input.Step();
        }

        [Test]
        public void Requirements_FromAssets()
        {
            var behaviour = Make("Move unit");
            behaviour.Gestures.Add(Asset<TapGesture>());
            behaviour.RequireFailure.Add(Asset<DoubleTapGesture>());
            behaviour.Attach(_h.Intentions);
            var log = _h.Log(behaviour.Intention);

            Tap(1, new Vector2(100, 100));
            Assert.AreEqual(IntentionState.Waiting, behaviour.Intention.Instances[0].State);
            _h.Input.Hold(350);
            Assert.AreEqual(1, log.Count(IntentionEventType.Performed));

            var afterHold = Make("Drag unit");
            afterHold.Gestures.Add(Asset<DragGesture>());
            afterHold.RequireActive.Add(Asset<LongPressGesture>());
            afterHold.Attach(_h.Intentions);
            var dragLog = _h.Log(afterHold.Intention);
            DragFinger(2, new Vector2(100, 100), new Vector2(200, 100));
            CollectionAssert.AreEqual(new[] { "Possible", "Rejected(RequirementNotMet)" }, dragLog.Entries.Select(e => Regex.Replace(e, "#\\d+$", "")));
        }

        [Test]
        public void Area_RectTransform_IsThisObject()
        {
            var behaviour = Make("Frame", typeof(RectTransform));
            var rect = (RectTransform)behaviour.transform;
            rect.sizeDelta = new Vector2(100, 100);
            rect.position = new Vector3(100, 100, 0); // no camera: world = screen pixels (1 px per dp here)
            behaviour.Gestures.Add(Asset<DragGesture>());
            behaviour.Area = IntentionBehaviour.AreaKind.RectTransform;
            behaviour.Attach(_h.Intentions);
            var log = _h.Log(behaviour.Intention);

            DragFinger(1, new Vector2(300, 300), new Vector2(350, 300));
            Assert.IsEmpty(log.Entries);
            DragFinger(2, new Vector2(100, 100), new Vector2(300, 100));
            Assert.AreEqual(1, log.Count(IntentionEventType.Started));
            Assert.AreSame(rect, log.Events[0].Target);
        }

#if THREADS_TC_PHYSICS
        [Test]
        public void Area_Physics_OnlyCollidersOnThisObject()
        {
            var camera = new GameObject("Camera", typeof(Camera)).GetComponent<Camera>();
            _created.Add(camera.gameObject);
            camera.transform.position = new Vector3(0, 0, -10);
            var unit = Make("Unit");
            var body = new GameObject("Body", typeof(BoxCollider));
            body.transform.SetParent(unit.transform, false);
            var rock = new GameObject("Rock", typeof(BoxCollider));
            _created.Add(rock);
            rock.transform.position = new Vector3(3, 0, 0);
            Physics.SyncTransforms();

            unit.Gestures.Add(Asset<TapGesture>());
            unit.Area = IntentionBehaviour.AreaKind.Physics;
            unit.AreaCamera = camera;
            unit.Attach(_h.Intentions);
            var log = _h.Log(unit.Intention);

            Tap(1, camera.WorldToScreenPoint(rock.transform.position));
            Assert.IsEmpty(log.Entries, "Another object's collider");
            Tap(2, camera.WorldToScreenPoint(Vector3.zero));
            Assert.AreEqual(1, log.Count(IntentionEventType.Performed));
            Assert.AreSame(body, ((WorldHit)log.Events[0].Target).GameObject);
        }
#endif

        [Test]
        public void Misconfiguration_IsReported_NotThrown()
        {
            var missing = Make("Missing gesture");
            missing.Gestures.Add(Asset<DragGesture>());
            missing.Gestures.Add(DefaultTouchSettings.Load(TouchPlatform.iOS).Lineup.First(e => e.gesture is IosDoubleTap).gesture);
            LogAssert.Expect(LogType.Error, new Regex("could not be enabled"));
            missing.Attach(_h.Intentions);
            Assert.IsFalse(missing.Intention.Enabled);

            var noRect = Make("No rect");
            noRect.Gestures.Add(Asset<DragGesture>());
            noRect.Area = IntentionBehaviour.AreaKind.RectTransform;
            LogAssert.Expect(LogType.Error, new Regex("needs a RectTransform"));
            noRect.Attach(_h.Intentions);

            var noEventSystem = Make("No event system");
            noEventSystem.Gestures.Add(Asset<DragGesture>());
            noEventSystem.Area = IntentionBehaviour.AreaKind.UiRaycast;
            LogAssert.Expect(LogType.Error, new Regex("needs an EventSystem"));
            noEventSystem.Attach(_h.Intentions);

            var noCamera = Make("No camera");
            noCamera.Gestures.Add(Asset<DragGesture>());
            noCamera.Area = IntentionBehaviour.AreaKind.Physics;
            LogAssert.Expect(LogType.Error, new Regex("needs an Area Camera"));
            noCamera.Attach(_h.Intentions);

            var log = _h.Log(noRect.Intention);
            DragFinger(1, new Vector2(100, 100), new Vector2(200, 100));
            Assert.IsEmpty(log.Entries, "A misconfigured area applies nowhere");
            Assert.Throws<System.ArgumentNullException>(() => noRect.Attach(null));
        }

        [Test]
        public void Detach_EndsTheIntention_AndReattachRebuildsIt()
        {
            var behaviour = Make("Drag");
            behaviour.Gestures.Add(Asset<DragGesture>());
            behaviour.Attach(_h.Intentions);
            var first = behaviour.Intention;
            var log = _h.Log(first);
            _h.Input.Down(1, new Vector2(100, 100));
            _h.Input.Step();
            _h.Input.Drag(1, new Vector2(150, 100), 100);

            behaviour.Detach();
            Assert.IsNull(behaviour.Intention);
            Assert.AreEqual("Ended(Disabled)", Regex.Replace(log.Entries.Last(), "#\\d+$", ""));

            behaviour.Attach(_h.Intentions);
            Assert.AreNotSame(first, behaviour.Intention);
            Assert.IsTrue(behaviour.Intention.Enabled);
        }
    }
}
