using System;
using NUnit.Framework;
using UnityEngine;
using Threads.TouchController.Testing;

namespace Threads.TouchController.Tests
{
    /// <summary>The test input itself must behave like TouchSource, or every gesture test built on it is suspect.</summary>
    public class ScriptedTouchInputTests
    {
        private ScriptedTouchInput _input;

        [SetUp]
        public void SetUp() => _input = new ScriptedTouchInput(pixelsPerDp: 2f, screenSizeDp: new Vector2(400, 800));

        private TouchPoint Only(TouchFrame frame)
        {
            Assert.AreEqual(1, frame.Count);
            return frame.Touches[0];
        }

        [Test]
        public void Lifecycle_Began_Stationary_Moved_Ended_ThenGone()
        {
            _input.Down(1, new Vector2(10, 10));
            Assert.AreEqual(TouchPointPhase.Began, Only(_input.Step()).phase);
            Assert.AreEqual(TouchPointPhase.Stationary, Only(_input.Step()).phase);

            _input.Move(1, new Vector2(20, 10));
            var moved = Only(_input.Step());
            Assert.AreEqual(TouchPointPhase.Moved, moved.phase);
            Assert.AreEqual(new Vector2(20, 10), moved.positionDp);
            Assert.AreEqual(new Vector2(10, 10), moved.startPositionDp);

            _input.Up(1);
            var ended = Only(_input.Step());
            Assert.AreEqual(TouchPointPhase.Ended, ended.phase);
            Assert.AreEqual(new Vector2(20, 10), ended.positionDp);

            Assert.AreEqual(0, _input.Step().Count);
        }

        [Test]
        public void ScriptedTouches_HaveNoInputSystemIds()
        {
            _input.Down(1, Vector2.zero);
            var touch = Only(_input.Step());
            Assert.AreEqual(0, touch.inputTouchId);
            Assert.AreEqual(0, touch.inputDeviceId);
        }

        [Test]
        public void Cancel_ProducesCanceled()
        {
            _input.Down(1, Vector2.zero);
            _input.Step();
            _input.Cancel(1);
            Assert.AreEqual(TouchPointPhase.Canceled, Only(_input.Step()).phase);
        }

        [Test]
        public void Step_AdvancesTime_AndSetsStartTime()
        {
            _input.Step(100);
            _input.Down(1, Vector2.zero);
            var began = Only(_input.Step(50));
            Assert.AreEqual(0.15, _input.Time, 1e-9);
            Assert.AreEqual(0.15, began.startTime, 1e-9);
            Assert.AreEqual(0.15, _input.Step(10).Touches[0].startTime, 1e-9);
        }

        [Test]
        public void Frame_CarriesScaleAndScreen()
        {
            var frame = _input.Step();
            Assert.AreEqual(2f, frame.PixelsPerDp);
            Assert.AreEqual(new Vector2(400, 800), frame.ScreenSizeDp);
        }

        [Test]
        public void BeginAndEndInSameFrame_Throws()
        {
            _input.Down(1, Vector2.zero);
            Assert.Throws<InvalidOperationException>(() => _input.Up(1));
        }

        [Test]
        public void DownTwice_Throws()
        {
            _input.Down(1, Vector2.zero);
            Assert.Throws<InvalidOperationException>(() => _input.Down(1, Vector2.one));
        }

        [Test]
        public void MoveOrUpUnknownTouch_Throws()
        {
            Assert.Throws<InvalidOperationException>(() => _input.Move(9, Vector2.zero));
            Assert.Throws<InvalidOperationException>(() => _input.Up(9));
        }

        [Test]
        public void IdCanBeReused_AfterEnd()
        {
            _input.Down(1, Vector2.zero);
            _input.Step();
            _input.Up(1);
            _input.Step();
            _input.Down(1, Vector2.one);
            Assert.AreEqual(TouchPointPhase.Began, Only(_input.Step()).phase);
        }

        [Test]
        public void TwoTouches_AreIndependent()
        {
            _input.Down(1, Vector2.zero);
            _input.Step();
            _input.Down(2, Vector2.one);
            var frame = _input.Step();
            Assert.AreEqual(2, frame.Count);
            Assert.IsTrue(frame.TryGet(1, out var first));
            Assert.IsTrue(frame.TryGet(2, out var second));
            Assert.AreEqual(TouchPointPhase.Stationary, first.phase);
            Assert.AreEqual(TouchPointPhase.Began, second.phase);
        }

        [Test]
        public void Hold_ProducesFramesUntilTimePassed()
        {
            var frames = 0;
            _input.FrameProduced += _ => frames++;
            _input.Hold(100, stepMs: 20);
            Assert.AreEqual(5, frames);
            Assert.AreEqual(0.1, _input.Time, 1e-9);
        }

        [Test]
        public void Drag_MovesLinearlyToTarget()
        {
            _input.Down(1, new Vector2(0, 0));
            _input.Step();
            var frames = 0;
            _input.FrameProduced += f =>
            {
                frames++;
                Assert.AreEqual(TouchPointPhase.Moved, f.Touches[0].phase);
            };

            var last = _input.Drag(1, new Vector2(100, 0), durationMs: 100, stepMs: 25);

            Assert.AreEqual(4, frames);
            Assert.AreEqual(100f, last.Touches[0].positionDp.x, 1e-4f);
        }
    }
}
