using System;
using NUnit.Framework;
using UnityEngine;
using Threads.TouchController.Testing;

namespace Threads.TouchController.Tests
{
    public class TouchHistoryTests
    {
        private FlingSettings _fling;
        private TouchHistory _history;
        private ScriptedTouchInput _input;

        [SetUp]
        public void SetUp()
        {
            _fling = new FlingSettings { velocityWindowMs = 100f };
            _history = new TouchHistory(_fling);
            _input = new ScriptedTouchInput();
            _input.FrameProduced += _history.Update;
        }

        private void Begin(int id, Vector2 at)
        {
            _input.Down(id, at);
            _input.Step();
        }

        [Test]
        public void Constructor_NullSettings_Throws()
        {
            Assert.Throws<ArgumentNullException>(() => new TouchHistory(null));
        }

        [Test]
        public void UnknownTouch_HasZeroVelocity()
        {
            Assert.IsFalse(_history.TryGetVelocityDps(42, out var velocity));
            Assert.AreEqual(Vector2.zero, velocity);
            Assert.AreEqual(Vector2.zero, _history.GetVelocityDps(42));
        }

        [Test]
        public void SingleSample_HasZeroVelocity()
        {
            Begin(1, Vector2.zero);
            Assert.IsTrue(_history.TryGetVelocityDps(1, out var velocity));
            Assert.AreEqual(Vector2.zero, velocity);
        }

        [Test]
        public void ConstantSpeed_IsMeasuredExactly()
        {
            Begin(1, Vector2.zero);
            // 500 dp/s to the right: 10 dp every 20 ms.
            _input.Drag(1, new Vector2(100, 0), durationMs: 200, stepMs: 20);

            var velocity = _history.GetVelocityDps(1);
            Assert.AreEqual(500f, velocity.x, 0.01f);
            Assert.AreEqual(0f, velocity.y, 0.01f);
        }

        [Test]
        public void Diagonal_MeasuresBothAxes()
        {
            Begin(1, Vector2.zero);
            _input.Drag(1, new Vector2(-60, 80), durationMs: 200, stepMs: 20); // (-300, 400) dp/s

            var velocity = _history.GetVelocityDps(1);
            Assert.AreEqual(-300f, velocity.x, 0.01f);
            Assert.AreEqual(400f, velocity.y, 0.01f);
        }

        [Test]
        public void StoppedLongerThanWindow_HasZeroVelocity()
        {
            Begin(1, Vector2.zero);
            _input.Drag(1, new Vector2(100, 0), durationMs: 100, stepMs: 20);
            _input.Hold(150, stepMs: 20);

            Assert.AreEqual(0f, _history.GetVelocityDps(1).magnitude, 0.01f);
        }

        [Test]
        public void ReleaseVelocity_ReadableInEndFrame_ThenRemoved()
        {
            Begin(1, Vector2.zero);
            _input.Drag(1, new Vector2(100, 0), durationMs: 200, stepMs: 20);
            _input.Up(1);
            _input.Step(20); // Ended, at the same position: finger stops for one frame

            Assert.IsTrue(_history.IsTracking(1), "History must survive the end frame so swipes can read release velocity.");
            Assert.Greater(_history.GetVelocityDps(1).x, 300f);

            _input.Step(20);
            Assert.IsFalse(_history.IsTracking(1));
            Assert.AreEqual(0, _history.Count);
        }

        [Test]
        public void Canceled_IsRemovedNextFrame()
        {
            Begin(1, Vector2.zero);
            _input.Cancel(1);
            _input.Step();
            Assert.IsTrue(_history.IsTracking(1));
            _input.Step();
            Assert.IsFalse(_history.IsTracking(1));
        }

        [Test]
        public void TouchMissingFromFrame_IsRemoved()
        {
            Begin(1, Vector2.zero);
            var frame = new TouchFrame();
            frame.Reset(_input.Time + 0.02, 1f, Vector2.one);
            _history.Update(frame);
            Assert.IsFalse(_history.IsTracking(1));
        }

        [Test]
        public void Window_LimitsSamples_AndIsReadLive()
        {
            Begin(1, Vector2.zero);
            _input.Hold(200, stepMs: 10);
            Assert.That(_history.SampleCount(1), Is.InRange(10, 12)); // ~100 ms of 10 ms frames

            _fling.velocityWindowMs = 50f; // settings change applies without re-creating the history
            _input.Hold(10, stepMs: 10);
            Assert.That(_history.SampleCount(1), Is.InRange(5, 7));
        }

        [Test]
        public void Samples_AreCapped()
        {
            _fling.velocityWindowMs = 100000f;
            Begin(1, Vector2.zero);
            _input.Hold(1000, stepMs: 5);
            Assert.AreEqual(TouchHistory.MaxSamples, _history.SampleCount(1));
        }

        [Test]
        public void TwoTouches_HaveIndependentVelocities()
        {
            _input.Down(1, Vector2.zero);
            _input.Down(2, new Vector2(0, 100));
            _input.Step();
            for (var i = 1; i <= 10; i++)
            {
                _input.Move(1, new Vector2(10 * i, 0));     // +500 dp/s in x
                _input.Move(2, new Vector2(0, 100 - 4 * i)); // -200 dp/s in y
                _input.Step(20);
            }

            Assert.AreEqual(500f, _history.GetVelocityDps(1).x, 0.01f);
            Assert.AreEqual(0f, _history.GetVelocityDps(1).y, 0.01f);
            Assert.AreEqual(0f, _history.GetVelocityDps(2).x, 0.01f);
            Assert.AreEqual(-200f, _history.GetVelocityDps(2).y, 0.01f);
        }

        [Test]
        public void Began_WithKnownId_StartsFreshHistory()
        {
            // Hand-made frames can reuse an ID without an end frame; samples must not mix.
            var frame = new TouchFrame();
            frame.Reset(0.00, 1f, Vector2.one);
            frame.Add(new TouchPoint(1, TouchPointPhase.Began, Vector2.zero, Vector2.zero, 0));
            _history.Update(frame);
            frame.Reset(0.02, 1f, Vector2.one);
            frame.Add(new TouchPoint(1, TouchPointPhase.Moved, new Vector2(10, 0), Vector2.zero, 0));
            _history.Update(frame);
            frame.Reset(0.04, 1f, Vector2.one);
            frame.Add(new TouchPoint(1, TouchPointPhase.Began, new Vector2(500, 0), new Vector2(500, 0), 0.04));
            _history.Update(frame);

            Assert.AreEqual(1, _history.SampleCount(1));
            Assert.AreEqual(Vector2.zero, _history.GetVelocityDps(1));
        }

        [Test]
        public void Clear_RemovesEverything()
        {
            Begin(1, Vector2.zero);
            _history.Clear();
            Assert.AreEqual(0, _history.Count);
        }
    }
}
