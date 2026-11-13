using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using Threads.TouchController.Intentions;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.TestTools;

namespace Threads.TouchController.Tests.Intentions
{
    /// <summary>
    /// The runner's TouchGestures device end to end: a simulated touchscreen through the Input System, the runner,
    /// its GestureInputOutput, and an InputAction bound to &lt;TouchGestures&gt;/tap.
    /// </summary>
    public class TouchGesturesPlayTests : InputTestFixture
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
        public IEnumerator Tap_OnTheTouchscreen_PerformsAnActionBoundToTheGesture()
        {
            InputSystem.AddDevice<Touchscreen>();

            var runnerGo = new GameObject("Runner");
            _created.Add(runnerGo);
            runnerGo.SetActive(false);
            var runner = runnerGo.AddComponent<IntentionRunner>();
            runner.SimulateTouchInEditor = false;
            runner.PlatformOverride = TouchPlatform.Android;
            runner.CreateTouchGestures = true;
            runnerGo.SetActive(true);
            yield return null;
            Assert.IsNotNull(runner.TouchGestures, "the runner created the device");
            Assert.AreEqual(TouchGestureKinds.All, runner.GestureInput.Mapped);

            var select = new InputAction("Select", InputActionType.Button, "<TouchGestures>/tap");
            select.AddBinding("<Keyboard>/space");
            var performed = 0;
            select.performed += _ => performed++;
            select.Enable();
            try
            {
                var at = new Vector2(Screen.width * 0.5f, Screen.height * 0.5f);
                BeginTouch(1, at);
                yield return null;
                EndTouch(1, at);
                yield return null; // the tap is recognised
                yield return null; // and seen by the Input System at its next update
                Assert.AreEqual(1, performed);
                Assert.That(Vector2.Distance(at, runner.TouchGestures.Device.position.ReadValue()), Is.LessThan(1f));

                var device = runner.TouchGestures.Device;
                runnerGo.SetActive(false);
                Assert.IsNull(runner.TouchGestures);
                Assert.IsNull(runner.GestureInput);
                Assert.IsFalse(device.added, "the device is removed when the runner stops");
            }
            finally
            {
                select.Dispose();
            }
        }
    }
}
