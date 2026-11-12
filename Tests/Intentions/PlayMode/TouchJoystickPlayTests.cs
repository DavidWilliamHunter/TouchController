using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using Threads.TouchController.Intentions;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace Threads.TouchController.Tests.Intentions
{
    /// <summary>
    /// TouchJoystickBehaviour end to end: a simulated touchscreen through the Input System, the runner (with its
    /// TouchSticks device), the joystick, its uGUI visuals, and an InputAction bound to &lt;TouchSticks&gt;/leftStick.
    /// </summary>
    public class TouchJoystickPlayTests : InputTestFixture
    {
        private readonly List<GameObject> _created = new List<GameObject>();

        public override void TearDown()
        {
            foreach (var go in _created)
                if (go != null) Object.Destroy(go);
            _created.Clear();
            base.TearDown();
        }

        [UnityTest]
        public IEnumerator FloatingJoystick_DrivesAnInputAction_AndItsVisuals()
        {
            InputSystem.AddDevice<Touchscreen>();

            var runnerGo = new GameObject("Runner");
            _created.Add(runnerGo);
            runnerGo.SetActive(false);
            var runner = runnerGo.AddComponent<IntentionRunner>();
            runner.SimulateTouchInEditor = false;
            runner.PlatformOverride = TouchPlatform.Android;
            runner.CreateTouchSticks = true;

            var canvas = new GameObject("Canvas", typeof(Canvas), typeof(CanvasScaler));
            _created.Add(canvas);
            canvas.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;

            // The left half of the screen is the zone; base and knob are children of the canvas.
            var zone = new GameObject("Move zone", typeof(RectTransform));
            zone.SetActive(false);
            var zoneRect = (RectTransform)zone.transform;
            zoneRect.SetParent(canvas.transform, false);
            zoneRect.anchorMin = Vector2.zero;
            zoneRect.anchorMax = new Vector2(0.5f, 1f);
            zoneRect.offsetMin = zoneRect.offsetMax = Vector2.zero;
            var baseGraphic = new GameObject("Base", typeof(RectTransform), typeof(Image)).GetComponent<RectTransform>();
            baseGraphic.SetParent(canvas.transform, false);
            var knob = new GameObject("Knob", typeof(RectTransform), typeof(Image)).GetComponent<RectTransform>();
            knob.SetParent(canvas.transform, false);

            var joystick = zone.AddComponent<TouchJoystickBehaviour>();
            joystick.Runner = runner;
            joystick.Mode = JoystickMode.Floating;
            joystick.DeadZone = 0f;
            joystick.Output = TouchJoystickBehaviour.StickOutput.LeftStick;
            joystick.BaseGraphic = baseGraphic;
            joystick.Knob = knob;
            var events = new List<Vector2>();
            joystick.OnValueChanged.AddListener(events.Add);

            zone.SetActive(true);
            runnerGo.SetActive(true);
            yield return null;
            Assert.IsNotNull(runner.TouchSticks, "the runner created the device");
            Assert.IsNotNull(joystick.Joystick);
            Assert.IsFalse(baseGraphic.gameObject.activeSelf, "a floating joystick is hidden until touched");

            var move = new InputAction("Move", InputActionType.Value, "<TouchSticks>/leftStick");
            move.Enable();
            try
            {
                var ppd = runner.Touch.Frame.PixelsPerDp;
                var down = new Vector2(Screen.width * 0.25f, Screen.height * 0.5f);
                BeginTouch(1, down);
                yield return null;
                Assert.IsTrue(joystick.Joystick.IsHeld);
                Assert.IsTrue(baseGraphic.gameObject.activeSelf);
                Assert.That(Vector2.Distance(down, baseGraphic.position), Is.LessThan(1f), "the base appeared under the finger");

                var offsetPx = new Vector2(0f, 30f * ppd); // half of the default 60 dp radius
                MoveTouch(1, down + offsetPx);
                yield return null;
                yield return null; // the device's state event is applied at the next Input System update

                Assert.That(Vector2.Distance(new Vector2(0f, 0.5f), joystick.Value), Is.LessThan(0.02f));
                Assert.That(Vector2.Distance(joystick.Value, move.ReadValue<Vector2>()), Is.LessThan(1e-4f), "the action reads what the joystick says");
                Assert.That(Vector2.Distance(down + offsetPx, knob.position), Is.LessThan(1f), "the knob follows the finger");
                Assert.IsNotEmpty(events);

                EndTouch(1, down + offsetPx);
                yield return null;
                yield return null;
                Assert.IsFalse(joystick.Joystick.IsHeld);
                Assert.AreEqual(Vector2.zero, move.ReadValue<Vector2>());
                Assert.IsFalse(baseGraphic.gameObject.activeSelf, "hidden again");

                var device = runner.TouchSticks.Device;
                runnerGo.SetActive(false);
                Assert.IsNull(joystick.Joystick, "detached when the runner stops");
                Assert.IsFalse(device.added, "and the device is removed");
            }
            finally
            {
                move.Dispose();
            }
        }
    }
}
