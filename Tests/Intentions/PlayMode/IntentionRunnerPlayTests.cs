using System.Collections;
using System.Linq;
using NUnit.Framework;
using Threads.TouchController.Intentions;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.TestTools;

namespace Threads.TouchController.Tests.Intentions
{
    /// <summary>
    /// IntentionRunner and IntentionBehaviour end to end: a simulated touchscreen through the real Input System,
    /// EnhancedTouch, TouchSystem, IntentionSystem and the component's UnityEvents, plus the component lifecycle.
    /// </summary>
    public class IntentionRunnerPlayTests : InputTestFixture
    {
        private GameObject _runnerObject;
        private GameObject _target;

        public override void TearDown()
        {
            if (_target != null) Object.Destroy(_target);
            if (_runnerObject != null) Object.Destroy(_runnerObject);
            base.TearDown();
        }

        [UnityTest]
        public IEnumerator Runner_DrivesTheBehaviour_AndFollowsTheComponentLifecycle()
        {
            InputSystem.AddDevice<Touchscreen>();

            _runnerObject = new GameObject("Runner");
            _runnerObject.SetActive(false);
            var runner = _runnerObject.AddComponent<IntentionRunner>();
            runner.SimulateTouchInEditor = false;
            runner.PlatformOverride = TouchPlatform.Android;

            _target = new GameObject("Card");
            _target.SetActive(false);
            var behaviour = _target.AddComponent<IntentionBehaviour>();
            behaviour.Gestures.Add(DefaultTouchSettings.Load(TouchPlatform.Android).Lineup.First(e => e.gesture is DragGesture).gesture);
            behaviour.Runner = runner;
            int started = 0, ended = 0;
            behaviour.OnStarted.AddListener(_ => started++);
            behaviour.OnEnded.AddListener(_ => ended++);

            _target.SetActive(true);        // joins the runner before it runs...
            Assert.IsNull(behaviour.Intention);
            _runnerObject.SetActive(true);  // ...and is attached when it starts
            yield return null;
            Assert.IsTrue(runner.IsRunning);
            Assert.IsTrue(behaviour.Intention.Enabled);

            BeginTouch(1, new Vector2(100, 100));
            yield return null;
            for (var x = 120; x <= 300; x += 20)
            {
                MoveTouch(1, new Vector2(x, 100));
                yield return null;
            }
            EndTouch(1, new Vector2(300, 100));
            yield return null;
            yield return null;
            Assert.AreEqual(1, started, "A real touchscreen drag reached the component's UnityEvent");
            Assert.AreEqual(1, ended);

            behaviour.enabled = false;
            Assert.IsFalse(behaviour.Intention?.Enabled ?? false, "Disabling the component disables the intention");
            behaviour.enabled = true;
            Assert.IsTrue(behaviour.Intention.Enabled);

            var before = behaviour.Intention;
            _runnerObject.SetActive(false); // the runner shuts down and detaches its members
            Assert.IsNull(behaviour.Intention);
            Assert.IsNull(runner.Touch);
            _runnerObject.SetActive(true);  // and attaches them again, with a fresh intention
            yield return null;
            Assert.IsNotNull(behaviour.Intention);
            Assert.AreNotSame(before, behaviour.Intention);
            Assert.IsTrue(behaviour.Intention.Enabled);
        }
    }
}
