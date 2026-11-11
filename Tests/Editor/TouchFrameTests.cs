using System;
using NUnit.Framework;
using UnityEngine;

namespace Threads.TouchController.Tests
{
    public class TouchFrameTests
    {
        private static TouchPoint Point(int id, TouchPointPhase phase, float x = 0f, float y = 0f) =>
            new TouchPoint(id, phase, new Vector2(x, y), new Vector2(x, y), 0.0);

        [Test]
        public void Reset_SetsTimeScaleAndClears()
        {
            var frame = new TouchFrame();
            frame.Reset(1.0, 2f, new Vector2(400, 800));
            frame.Add(Point(1, TouchPointPhase.Began));

            frame.Reset(2.5, 3f, new Vector2(300, 600));

            Assert.AreEqual(2.5, frame.Time);
            Assert.AreEqual(3f, frame.PixelsPerDp);
            Assert.AreEqual(new Vector2(300, 600), frame.ScreenSizeDp);
            Assert.AreEqual(0, frame.Count);
        }

        [TestCase(0f)]
        [TestCase(-1f)]
        [TestCase(float.NaN)]
        [TestCase(float.PositiveInfinity)]
        public void Reset_InvalidScale_Throws(float pixelsPerDp)
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new TouchFrame().Reset(0, pixelsPerDp, Vector2.one));
        }

        [Test]
        public void Add_DuplicateTouchId_Throws()
        {
            var frame = new TouchFrame();
            frame.Reset(0, 1f, Vector2.one);
            frame.Add(Point(7, TouchPointPhase.Began));
            Assert.Throws<ArgumentException>(() => frame.Add(Point(7, TouchPointPhase.Moved)));
        }

        [Test]
        public void TryGet_FindsByTouchId()
        {
            var frame = new TouchFrame();
            frame.Reset(0, 1f, Vector2.one);
            frame.Add(Point(1, TouchPointPhase.Began, 10, 20));
            frame.Add(Point(2, TouchPointPhase.Moved, 30, 40));

            Assert.IsTrue(frame.TryGet(2, out var touch));
            Assert.AreEqual(new Vector2(30, 40), touch.positionDp);
            Assert.IsFalse(frame.TryGet(3, out _));
        }

        [Test]
        public void InContact_IgnoresEndedAndCanceled()
        {
            var frame = new TouchFrame();
            frame.Reset(0, 1f, Vector2.one);
            frame.Add(Point(1, TouchPointPhase.Ended));
            frame.Add(Point(2, TouchPointPhase.Canceled));
            Assert.IsFalse(frame.AnyInContact);
            Assert.AreEqual(0, frame.InContactCount);

            frame.Add(Point(3, TouchPointPhase.Stationary));
            Assert.IsTrue(frame.AnyInContact);
            Assert.AreEqual(1, frame.InContactCount);
        }

        [Test]
        public void DpToPx_UsesFrameScale()
        {
            var frame = new TouchFrame();
            frame.Reset(0, 2.5f, Vector2.one);
            Assert.AreEqual(new Vector2(25, 50), frame.DpToPx(new Vector2(10, 20)));
            Assert.AreEqual(20f, frame.DpToPx(8f));
        }

        [TestCase(TouchPointPhase.Began, true, false)]
        [TestCase(TouchPointPhase.Moved, true, false)]
        [TestCase(TouchPointPhase.Stationary, true, false)]
        [TestCase(TouchPointPhase.Ended, false, true)]
        [TestCase(TouchPointPhase.Canceled, false, true)]
        public void TouchPoint_PhaseHelpers(TouchPointPhase phase, bool inContact, bool ended)
        {
            var point = Point(1, phase);
            Assert.AreEqual(inContact, point.IsInContact);
            Assert.AreEqual(ended, point.HasEnded);
        }
    }
}
