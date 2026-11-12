using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Threads.TouchController.Intentions;
using Threads.TouchController.Intentions.Testing;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Threads.TouchController.Tests.Intentions
{
    /// <summary>TouchJoystick on the Android lineup, driven by scripted touches (screen 400 × 800 dp).</summary>
    public class TouchJoystickTests
    {
        private IntentionSystemHarness _h;
        private readonly List<TouchJoystick> _joysticks = new List<TouchJoystick>();

        [SetUp]
        public void SetUp() => _h = new IntentionSystemHarness();

        [TearDown]
        public void TearDown()
        {
            foreach (var j in _joysticks) j.Dispose();
            _joysticks.Clear();
            _h.Dispose();
        }

        private TouchJoystick Make(TouchJoystickOptions options)
        {
            var joystick = new TouchJoystick(_h.Intentions, options);
            _joysticks.Add(joystick);
            joystick.Enabled = true;
            return joystick;
        }

        private TouchJoystick Fixed(Vector2 centre, float deadZone = 0f) => Make(new TouchJoystickOptions
        {
            Mode = JoystickMode.Fixed,
            CentreDp = () => centre,
            RadiusDp = 50f,
            TouchMarginDp = 10f,
            DeadZone = deadZone,
        });

        private TouchJoystick Floating(Rect zone, bool follow = false) => Make(new TouchJoystickOptions
        {
            Mode = JoystickMode.Floating,
            ActivationArea = ScreenRectArea.Dp(zone),
            RadiusDp = 50f,
            DeadZone = 0f,
            Follow = follow,
        });

        private void Down(int id, Vector2 at)
        {
            _h.Input.Down(id, at);
            _h.Input.Step();
        }

        private void MoveTo(int id, Vector2 to)
        {
            _h.Input.Move(id, to);
            _h.Input.Step();
        }

        private void Up(int id)
        {
            _h.Input.Up(id);
            _h.Input.Step();
        }

        private static void AreEqual(Vector2 expected, Vector2 actual, string message = null) =>
            Assert.That(Vector2.Distance(expected, actual), Is.LessThan(1e-3f), $"expected {expected}, got {actual}. {message}");

        // ---- Fixed --------------------------------------------------------------------------------------

        [Test]
        public void Fixed_ValueIsTheOffsetOverTheRadius_ClampedToOne_AndZeroOnRelease()
        {
            var stick = Fixed(new Vector2(100, 100));
            int pressed = 0, released = 0;
            var changes = new List<Vector2>();
            stick.Pressed += _ => pressed++;
            stick.Released += _ => released++;
            stick.Changed += changes.Add;

            Down(1, new Vector2(100, 100));
            Assert.IsTrue(stick.IsHeld, "it responds at touch-down, with no slop delay");
            Assert.AreEqual(1, pressed);
            Assert.AreEqual(1, stick.FingerId);
            AreEqual(Vector2.zero, stick.Value);
            AreEqual(new Vector2(100, 100), stick.OriginDp);

            MoveTo(1, new Vector2(125, 100));
            AreEqual(new Vector2(0.5f, 0f), stick.Value);
            AreEqual(new Vector2(125, 100), stick.ThumbDp);

            MoveTo(1, new Vector2(100, 300));
            AreEqual(new Vector2(0f, 1f), stick.Value, "clamped to full deflection");
            AreEqual(new Vector2(100, 150), stick.ThumbDp, "the knob stays on the rim");
            AreEqual(new Vector2(100, 100), stick.OriginDp, "a fixed base doesn't follow");

            Up(1);
            Assert.IsFalse(stick.IsHeld);
            Assert.AreEqual(1, released);
            Assert.AreEqual(-1, stick.FingerId);
            AreEqual(Vector2.zero, stick.Value);
            Assert.AreEqual(3, changes.Count, "Changed only when the value changes: 0.5, up, zero");
        }

        [Test]
        public void Fixed_OnlyTouchesOnTheBaseOrItsMarginGrabIt()
        {
            var stick = Fixed(new Vector2(100, 100));

            Down(1, new Vector2(100, 165)); // 65 dp: outside 50 + 10
            Assert.IsFalse(stick.IsHeld);
            Up(1);

            Down(2, new Vector2(100, 155)); // 55 dp: in the margin
            Assert.IsTrue(stick.IsHeld);
            AreEqual(new Vector2(0f, 1f), stick.Value, "grabbed at the rim: already deflected");
        }

        [Test]
        public void DeadZoneAndResponseCurve()
        {
            var stick = Make(new TouchJoystickOptions
            {
                CentreDp = () => Vector2.zero,
                RadiusDp = 100f,
                DeadZone = 0.2f,
                ResponseExponent = 2f,
            });

            AreEqual(Vector2.zero, stick.Evaluate(new Vector2(20, 0)), "inside the dead zone");
            AreEqual(new Vector2(0.25f, 0f), stick.Evaluate(new Vector2(60, 0)), "rescaled (0.6 - 0.2) / 0.8 = 0.5, squared");
            AreEqual(new Vector2(0f, -1f), stick.Evaluate(new Vector2(0, -300)));
            Assert.That(stick.Evaluate(new Vector2(300, 300)).magnitude, Is.EqualTo(1f).Within(1e-4f), "diagonals are radial, not square");
        }

        [Test]
        public void Options_AreValidated()
        {
            Assert.Throws<ArgumentNullException>(() => new TouchJoystick(null, new TouchJoystickOptions { CentreDp = () => Vector2.zero }));
            Assert.Throws<ArgumentNullException>(() => new TouchJoystick(_h.Intentions, null));
            Assert.Throws<ArgumentException>(() => new TouchJoystick(_h.Intentions, new TouchJoystickOptions()), "fixed needs a centre");
            Assert.Throws<ArgumentOutOfRangeException>(() => new TouchJoystick(_h.Intentions, new TouchJoystickOptions { Mode = JoystickMode.Floating, RadiusDp = 0f }));
            Assert.Throws<ArgumentOutOfRangeException>(() => new TouchJoystick(_h.Intentions, new TouchJoystickOptions { Mode = JoystickMode.Floating, DeadZone = 1f }));
            Assert.Throws<ArgumentOutOfRangeException>(() => new TouchJoystick(_h.Intentions, new TouchJoystickOptions { Mode = JoystickMode.Floating, ResponseExponent = 0f }));
        }

        // ---- Floating -----------------------------------------------------------------------------------

        [Test]
        public void Floating_AppearsWhereTheFingerTouchesDown_InsideItsArea()
        {
            var stick = Floating(new Rect(0, 0, 200, 800)); // the left half

            Down(1, new Vector2(300, 300));
            Assert.IsFalse(stick.IsHeld, "outside the activation area");
            Up(1);

            Down(2, new Vector2(120, 300));
            Assert.IsTrue(stick.IsHeld);
            AreEqual(new Vector2(120, 300), stick.OriginDp);
            MoveTo(2, new Vector2(120, 275));
            AreEqual(new Vector2(0f, -0.5f), stick.Value, "relative to the first touch");
        }

        [Test]
        public void Floating_OriginIsClampedSoTheWholeBaseIsOnScreen()
        {
            var stick = Floating(new Rect(0, 0, 400, 800));

            Down(1, new Vector2(10, 790));
            AreEqual(new Vector2(50, 750), stick.OriginDp, "radius 50 from the screen's edges");
            AreEqual(new Vector2(-1f, 1f).normalized, stick.Value, "the finger is (−40, 40) from the clamped origin: past the radius");
        }

        [Test]
        public void Floating_Follow_TheBaseTrailsTheFingerPastTheRadius()
        {
            var stick = Floating(new Rect(0, 0, 400, 800), follow: true);

            Down(1, new Vector2(100, 100));
            MoveTo(1, new Vector2(250, 100));
            AreEqual(new Vector2(200, 100), stick.OriginDp, "dragged along, 50 dp behind");
            AreEqual(new Vector2(1f, 0f), stick.Value);

            MoveTo(1, new Vector2(225, 100));
            AreEqual(new Vector2(0.5f, 0f), stick.Value, "coming back reads from the new origin");
        }

        // ---- With other fingers and intentions ------------------------------------------------------------

        [Test]
        public void TwoThumbs_DriveTwoJoysticksIndependently()
        {
            var left = Floating(new Rect(0, 0, 200, 800));
            var right = Floating(new Rect(200, 0, 200, 800));

            Down(1, new Vector2(100, 200));
            Down(2, new Vector2(300, 200));
            _h.Input.Move(1, new Vector2(100, 225));
            _h.Input.Move(2, new Vector2(275, 200));
            _h.Input.Step();

            AreEqual(new Vector2(0f, 0.5f), left.Value);
            AreEqual(new Vector2(-0.5f, 0f), right.Value);
            Assert.AreEqual(1, left.FingerId);
            Assert.AreEqual(2, right.FingerId);

            Up(1);
            Assert.IsFalse(left.IsHeld);
            Assert.IsTrue(right.IsHeld);
            AreEqual(new Vector2(-0.5f, 0f), right.Value);
        }

        [Test]
        public void ASecondFingerInTheZone_IsLeftForOtherIntentions()
        {
            var stick = Floating(new Rect(0, 0, 400, 800));
            var look = _h.Enable(new Intention("Look", _h.Intentions).Bind<DragGesture>());
            var log = _h.Log(look);

            Down(1, new Vector2(100, 100));
            Down(2, new Vector2(300, 400));
            _h.Input.Drag(2, new Vector2(300, 600), 200);

            Assert.AreEqual(1, stick.FingerId, "the joystick keeps its first finger");
            Assert.IsTrue(look.IsActive, "the second finger drives the look intention");
            Assert.IsEmpty(_h.Violations());
            Assert.IsTrue(log.Entries.Any(e => e.StartsWith("Started")));
        }

        [Test]
        public void TheJoysticksFinger_IsNotAlsoADrag()
        {
            Floating(new Rect(0, 0, 400, 800));
            var drag = _h.Enable(new Intention("Pan camera", _h.Intentions).Bind<DragGesture>().Exclusive());
            var log = _h.Log(drag);

            Down(1, new Vector2(100, 100));
            _h.Input.Drag(1, new Vector2(300, 100), 200);
            Up(1);

            CollectionAssert.AreEqual(new[] { "Possible", "Rejected(LostToPriority)" },
                log.Entries.Select(e => System.Text.RegularExpressions.Regex.Replace(e, "#\\d+$", "")));
        }

        [Test]
        public void DisablingWhileHeld_Releases()
        {
            var stick = Fixed(new Vector2(100, 100));
            var released = 0;
            stick.Released += _ => released++;
            Down(1, new Vector2(120, 100));
            Assert.IsTrue(stick.IsHeld);

            stick.Enabled = false;
            Assert.IsFalse(stick.IsHeld);
            Assert.AreEqual(1, released);
            AreEqual(Vector2.zero, stick.Value);
        }
    }

    /// <summary>The TouchSticks device and JoystickInputOutput: touch joysticks read through Input System actions.</summary>
    public class TouchSticksInputTests : InputTestFixture
    {
        private IntentionSystemHarness _h;

        public override void Setup()
        {
            base.Setup();
            _h = new IntentionSystemHarness();
        }

        public override void TearDown()
        {
            _h.Dispose();
            base.TearDown();
        }

        [Test]
        public void ActionsBoundToTheDevice_ReadTheJoystick()
        {
            using var sticks = new TouchSticksDevice();
            using var stick = new TouchJoystick(_h.Intentions, new TouchJoystickOptions
            {
                CentreDp = () => new Vector2(100, 100),
                RadiusDp = 50f,
                DeadZone = 0f,
            });
            stick.Enabled = true;
            using var output = new JoystickInputOutput(stick, sticks, sticks.LeftStick);

            var move = new InputAction("Move", InputActionType.Value, "<Gamepad>/leftStick");
            move.AddBinding("<TouchSticks>/leftStick");
            var look = new InputAction("Look", InputActionType.Value, "*/{Secondary2DMotion}");
            move.Enable();
            look.Enable();
            try
            {
                Assert.AreEqual(1, look.controls.Count(c => c.device == sticks.Device), "usages resolve too");

                _h.Input.Down(1, new Vector2(100, 100));
                _h.Input.Step();
                _h.Input.Move(1, new Vector2(125, 100));
                _h.Input.Step();
                InputSystem.Update();
                Assert.That(Vector2.Distance(new Vector2(0.5f, 0f), move.ReadValue<Vector2>()), Is.LessThan(1e-4f),
                    "exact: the joystick's dead zone only, no stick processors on top");
                Assert.AreEqual(Vector2.zero, sticks.RightStick.ReadValue());

                _h.Input.Up(1);
                _h.Input.Step();
                InputSystem.Update();
                Assert.AreEqual(Vector2.zero, move.ReadValue<Vector2>(), "released: back to zero");
            }
            finally
            {
                move.Dispose();
                look.Dispose();
            }
        }

        [Test]
        public void DisposingTheOutputZeroesTheStick_AndDisposingTheDeviceRemovesIt()
        {
            var sticks = new TouchSticksDevice();
            var device = sticks.Device;
            Assert.IsTrue(device.added);
            Assert.AreEqual(TouchSticks.LayoutName, device.layout);

            using (var stick = new TouchJoystick(_h.Intentions, new TouchJoystickOptions { CentreDp = () => new Vector2(100, 100), DeadZone = 0f }))
            {
                stick.Enabled = true;
                var output = new JoystickInputOutput(stick, sticks, sticks.RightStick);
                _h.Input.Down(1, new Vector2(100, 160));
                _h.Input.Step();
                InputSystem.Update();
                Assert.Greater(sticks.RightStick.ReadValue().y, 0.9f);

                output.Dispose();
                InputSystem.Update();
                Assert.AreEqual(Vector2.zero, sticks.RightStick.ReadValue());
            }

            sticks.Dispose();
            Assert.IsFalse(device.added);
            Assert.IsNull(sticks.Device);
            Assert.DoesNotThrow(() => sticks.Write(device.leftStick, Vector2.one), "writing after dispose is ignored");
        }

        [Test]
        public void OutputArgumentsAreChecked()
        {
            using var a = new TouchSticksDevice();
            using var b = new TouchSticksDevice();
            using var stick = new TouchJoystick(_h.Intentions, new TouchJoystickOptions { CentreDp = () => Vector2.zero });
            Assert.Throws<ArgumentException>(() => new JoystickInputOutput(stick, a, b.LeftStick), "a stick of another device");
            Assert.Throws<ArgumentNullException>(() => new JoystickInputOutput(null, a, a.LeftStick));
        }
    }
}
