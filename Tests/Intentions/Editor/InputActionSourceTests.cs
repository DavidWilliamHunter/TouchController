using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using NUnit.Framework;
using Threads.TouchController.Intentions;
using Threads.TouchController.Intentions.Testing;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Threads.TouchController.Tests.Intentions
{
    /// <summary>InputActionSource with a simulated mouse and gamepad (InputTestFixture); touch comes from scripted frames.</summary>
    public class InputActionSourceTests : InputTestFixture
    {
        private const float PixelsPerDp = 2f;
        private IntentionSystemHarness _h;
        private readonly List<InputAction> _actions = new List<InputAction>();

        [SetUp]
        public override void Setup()
        {
            base.Setup();
            _h = new IntentionSystemHarness(pixelsPerDp: PixelsPerDp);
        }

        [TearDown]
        public override void TearDown()
        {
            _h?.Dispose();
            foreach (var action in _actions) action.Dispose();
            _actions.Clear();
            base.TearDown();
        }

        private InputAction Action(InputActionType type, string binding)
        {
            var action = new InputAction(type: type, binding: binding);
            _actions.Add(action);
            return action;
        }

        private static List<string> Names(IEnumerable<string> entries) => entries.Select(e => Regex.Replace(e, "#-?\\d+$", "")).ToList();

        [Test]
        public void Pointer_MouseDragsTheFrame_InPixelsConvertedToDp()
        {
            var mouse = InputSystem.AddDevice<Mouse>();
            using var pointer = InputActionSource.Pointer(_h.Intentions,
                Action(InputActionType.Button, "<Mouse>/leftButton"), Action(InputActionType.Value, "<Mouse>/position"), "Mouse");
            var frame = _h.Enable(new Intention("Frame drag", _h.Intentions).Bind<DragGesture>().Bind(pointer.Source)
                .InArea(ScreenRectArea.Dp(new Rect(50, 50, 300, 300))));
            var log = _h.Log(frame);

            Set(mouse.position, new Vector2(200, 200)); // pixels: (100, 100) dp
            Press(mouse.leftButton);
            _h.Input.Step();
            Set(mouse.position, new Vector2(260, 220));
            _h.Input.Step();
            Release(mouse.leftButton);
            _h.Input.Step();

            CollectionAssert.AreEqual(new[] { "Possible", "Started", "Ended(Completed)" }, Names(log.WithoutPerformed()));
            Assert.AreEqual(IntentionSourceKind.InputAction, log.Events[0].Source);
            Assert.AreEqual(new Vector2(100, 100), log.Events[0].StartPositionDp);
            Assert.AreEqual(new Vector2(30, 10), log.Events.Aggregate(Vector2.zero, (sum, e) => sum + e.DeltaDp));
            Assert.IsEmpty(_h.Violations());
        }

        [Test]
        public void Pointer_PressOutsideTheArea_DoesNothing()
        {
            var mouse = InputSystem.AddDevice<Mouse>();
            using var pointer = InputActionSource.Pointer(_h.Intentions,
                Action(InputActionType.Button, "<Mouse>/leftButton"), Action(InputActionType.Value, "<Mouse>/position"));
            var frame = _h.Enable(new Intention("Frame drag", _h.Intentions).Bind(pointer.Source)
                .InArea(ScreenRectArea.Dp(new Rect(50, 50, 300, 300))));
            var log = _h.Log(frame);

            Set(mouse.position, new Vector2(780, 780)); // (390, 390) dp
            Press(mouse.leftButton);
            _h.Input.Step();
            Release(mouse.leftButton);
            _h.Input.Step();

            Assert.IsEmpty(log.Entries);
        }

        [Test]
        public void Stick_MovesAtItsSpeedWhileHeld()
        {
            var pad = InputSystem.AddDevice<Gamepad>();
            using var stick = InputActionSource.Stick(_h.Intentions, Action(InputActionType.Value, "<Gamepad>/leftStick"), 600f,
                () => new Vector2(200, 200), "Left stick");
            var frame = _h.Enable(new Intention("Frame drag", _h.Intentions).Bind(stick.Source)
                .InArea(ScreenRectArea.Dp(new Rect(50, 50, 300, 300))));
            var log = _h.Log(frame);

            _h.Input.Step();
            Set(pad.leftStick, new Vector2(1, 0));
            for (var i = 0; i < 30; i++) _h.Input.Step();
            Set(pad.leftStick, Vector2.zero);
            _h.Input.Step();

            CollectionAssert.AreEqual(new[] { "Possible", "Started", "Ended(Completed)" }, Names(log.WithoutPerformed()));
            Assert.AreEqual(new Vector2(200, 200), log.Events[0].StartPositionDp, "Begins where the stick acts");
            var started = log.Events.First(e => e.Type == IntentionEventType.Started).Time;
            var lastMove = log.Events.Last(e => e.Type == IntentionEventType.Performed).Time;
            var moved = log.Events.Aggregate(Vector2.zero, (sum, e) => sum + e.DeltaDp);
            Assert.AreEqual(600f * (float)(lastMove - started), moved.x, 1f);
            Assert.AreEqual(0f, moved.y, 1e-3f);
        }

        [Test]
        public void Dispose_DisablesOnlyWhatItEnabled_AndCancelsAPress()
        {
            var mouse = InputSystem.AddDevice<Mouse>();
            var press = Action(InputActionType.Button, "<Mouse>/leftButton");
            var position = Action(InputActionType.Value, "<Mouse>/position");
            position.Enable(); // shared with other code
            var pointer = InputActionSource.Pointer(_h.Intentions, press, position);
            Assert.IsTrue(press.enabled);
            var frame = _h.Enable(new Intention("Frame drag", _h.Intentions).Bind(pointer.Source));
            var log = _h.Log(frame);

            Set(mouse.position, new Vector2(200, 200));
            Press(mouse.leftButton);
            _h.Input.Step();
            pointer.Dispose();
            _h.Input.Step();

            Assert.IsFalse(press.enabled, "Enabled by the source: disabled again");
            Assert.IsTrue(position.enabled, "Already enabled: left alone");
            Assert.AreEqual("Ended(Cancelled)", Names(log.Entries).Last());
            pointer.Dispose(); // twice is fine
        }

        [Test]
        public void Misuse()
        {
            var press = Action(InputActionType.Button, "<Mouse>/leftButton");
            Assert.Throws<System.ArgumentNullException>(() => InputActionSource.Pointer(_h.Intentions, press, (InputAction)null));
            Assert.Throws<System.ArgumentNullException>(() => InputActionSource.Pointer(_h.Intentions, (InputActionReference)null, null));
            Assert.Throws<System.ArgumentOutOfRangeException>(() => InputActionSource.Stick(_h.Intentions, press, 0f, () => Vector2.zero));
            Assert.Throws<System.ArgumentNullException>(() => InputActionSource.Stick(_h.Intentions, press, 1f, null));
        }
    }
}
