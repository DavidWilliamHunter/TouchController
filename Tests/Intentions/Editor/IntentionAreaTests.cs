using NUnit.Framework;
using Threads.TouchController.Intentions;
using UnityEngine;

namespace Threads.TouchController.Tests.Intentions
{
    public class IntentionAreaTests
    {
        private static readonly Vector2 Screen = new Vector2(400, 800);

        private static IntentionPoint At(float x, float y, float pixelsPerDp = 1f) =>
            new IntentionPoint(1, 0, new Vector2(x, y), pixelsPerDp, Screen, 0.0);

        private static bool In(IIntentionArea area, IntentionPoint p) => area.Accepts(p, out _);

        [Test]
        public void ScreenRect_Dp()
        {
            var area = ScreenRectArea.Dp(new Rect(10, 10, 100, 50));
            Assert.IsTrue(In(area, At(50, 30)));
            Assert.IsFalse(In(area, At(150, 30)));
        }

        [Test]
        public void ScreenRect_Px_UsesPixelsPerDp()
        {
            var area = ScreenRectArea.Px(new Rect(0, 0, 200, 200)); // 100 x 100 dp at 2 px/dp
            Assert.IsTrue(In(area, At(90, 90, 2f)));
            Assert.IsFalse(In(area, At(110, 90, 2f)));
        }

        [Test]
        public void ScreenRect_Fraction_LeftHalf()
        {
            var area = ScreenRectArea.Fraction(new Rect(0, 0, 0.5f, 1f));
            Assert.IsTrue(In(area, At(199, 700)));
            Assert.IsFalse(In(area, At(201, 700)));
        }

        [Test]
        public void ScreenRect_Anchored_TopRightCorner()
        {
            var area = ScreenRectArea.Anchored(Vector2.one, Vector2.one, Vector2.zero, new Vector2(100, 100));
            Assert.AreEqual(new Rect(300, 700, 100, 100), area.ToDp(Screen, 1f));
            Assert.IsTrue(In(area, At(350, 750)));
            Assert.IsFalse(In(area, At(250, 750)));
        }

        [Test]
        public void Combinators()
        {
            var left = Area.From(p => p.PositionDp.x < 200, p => "left");
            var bottom = Area.From(p => p.PositionDp.y < 400, p => "bottom");

            Assert.IsTrue(Area.Not(left).Accepts(At(300, 100), out var notTarget));
            Assert.IsNull(notTarget);
            Assert.IsFalse(In(Area.Not(left), At(100, 100)));

            Assert.IsTrue(Area.All(left, bottom).Accepts(At(100, 100), out var allTarget));
            Assert.AreEqual("left", allTarget);
            Assert.IsFalse(In(Area.All(left, bottom), At(100, 500)));

            Assert.IsTrue(Area.Any(left, bottom).Accepts(At(300, 100), out var anyTarget));
            Assert.AreEqual("bottom", anyTarget);
            Assert.IsFalse(In(Area.Any(left, bottom), At(300, 500)));
        }
    }
}
