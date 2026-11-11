using NUnit.Framework;
using UnityEngine;

namespace Threads.TouchController.Tests
{
    public class TouchSettingsDataTests
    {
        private TouchSettings _settings;
        private TouchSettingsData Data => _settings.Data;

        [SetUp]
        public void SetUp() => _settings = TestSettings.CopyOfDefault(TouchPlatform.Android);

        [TearDown]
        public void TearDown() => Object.DestroyImmediate(_settings);

        [Test]
        public void NewData_HasAllSections()
        {
            var data = new TouchSettingsData();
            Assert.IsNotNull(data.Density);
            Assert.IsNotNull(data.Motion);
            Assert.IsNotNull(data.Tap);
            Assert.IsNotNull(data.LongPress);
            Assert.IsNotNull(data.Fling);
            Assert.IsNotNull(data.Pinch);
            Assert.IsNotNull(data.Rotate);
            Assert.IsNotNull(data.Edge);
            Assert.IsNotNull(data.Policy);
        }

        [Test]
        public void NewData_HoldsNoDefaultsInCode()
        {
            // Values live only in the assets (normalisation): a fresh instance is empty and invalid.
            var data = new TouchSettingsData();
            Assert.AreEqual(0f, data.Motion.touchSlopDp);
            Assert.IsNotEmpty(data.Validate());
        }

        [Test]
        public void CopyFrom_CopiesAllValues_AndKeepsSectionInstances()
        {
            var target = new TouchSettingsData();
            var motion = target.Motion;
            var tap = target.Tap;

            target.CopyFrom(Data);

            Assert.AreSame(motion, target.Motion);
            Assert.AreSame(tap, target.Tap);
            Assert.AreEqual(Data.Density.referenceDpi, target.Density.referenceDpi);
            Assert.AreEqual(Data.Density.fallbackDpi, target.Density.fallbackDpi);
            Assert.AreEqual(Data.Motion.touchSlopDp, target.Motion.touchSlopDp);
            Assert.AreEqual(Data.Tap.doubleTapTimeoutMs, target.Tap.doubleTapTimeoutMs);
            Assert.AreEqual(Data.Tap.doubleTapMinTimeMs, target.Tap.doubleTapMinTimeMs);
            Assert.AreEqual(Data.Tap.doubleTapSlopDp, target.Tap.doubleTapSlopDp);
            Assert.AreEqual(Data.Tap.multiFingerTapWindowMs, target.Tap.multiFingerTapWindowMs);
            Assert.AreEqual(Data.LongPress.longPressTimeoutMs, target.LongPress.longPressTimeoutMs);
            Assert.AreEqual(Data.Fling.minFlingVelocityDps, target.Fling.minFlingVelocityDps);
            Assert.AreEqual(Data.Fling.maxFlingVelocityDps, target.Fling.maxFlingVelocityDps);
            Assert.AreEqual(Data.Fling.velocityWindowMs, target.Fling.velocityWindowMs);
            Assert.AreEqual(Data.Fling.swipeAngleToleranceDeg, target.Fling.swipeAngleToleranceDeg);
            Assert.AreEqual(Data.Pinch.minScalingSpanDp, target.Pinch.minScalingSpanDp);
            Assert.AreEqual(Data.Rotate.minRotationDeg, target.Rotate.minRotationDeg);
            Assert.AreEqual(Data.Edge.edgeSlopDp, target.Edge.edgeSlopDp);
            Assert.AreEqual(Data.Policy.useOsValues, target.Policy.useOsValues);
        }

        [Test]
        public void CopyFrom_Null_Throws()
        {
            Assert.Throws<System.ArgumentNullException>(() => Data.CopyFrom(null));
        }

        [Test]
        public void Validate_ReferenceDpiZero_Reported()
        {
            Data.Density.referenceDpi = 0f;
            AssertSingleErrorMentions(SettingKeys.ReferenceDpi);
        }

        [Test]
        public void Validate_NegativeSlop_Reported()
        {
            Data.Motion.touchSlopDp = -1f;
            AssertSingleErrorMentions(SettingKeys.TouchSlop);
        }

        [Test]
        public void Validate_NaN_Reported()
        {
            Data.Edge.edgeSlopDp = float.NaN;
            AssertSingleErrorMentions(SettingKeys.EdgeSlop);
        }

        [Test]
        public void Validate_DoubleTapMinTimeNotBelowTimeout_Reported()
        {
            Data.Tap.doubleTapMinTimeMs = Data.Tap.doubleTapTimeoutMs;
            AssertSingleErrorMentions(SettingKeys.DoubleTapMinTime);
        }

        [Test]
        public void Validate_MaxFlingNotAboveMin_Reported()
        {
            Data.Fling.maxFlingVelocityDps = Data.Fling.minFlingVelocityDps;
            AssertSingleErrorMentions(SettingKeys.MaxFlingVelocity);
        }

        [TestCase(0f)]
        [TestCase(46f)]
        public void Validate_SwipeAngleOutOfRange_Reported(float angle)
        {
            Data.Fling.swipeAngleToleranceDeg = angle;
            AssertSingleErrorMentions(SettingKeys.SwipeAngleTolerance);
        }

        [Test]
        public void Validate_LongPressZero_Reported()
        {
            Data.LongPress.longPressTimeoutMs = 0f;
            AssertSingleErrorMentions(SettingKeys.LongPressTimeout);
        }

        [Test]
        public void SettingKeys_UseSectionDotField()
        {
            Assert.AreEqual("Motion.touchSlopDp", SettingKeys.TouchSlop);
            Assert.AreEqual("Tap.doubleTapTimeoutMs", SettingKeys.DoubleTapTimeout);
            Assert.AreEqual("Policy.useOsValues", SettingKeys.UseOsValues);
        }

        private void AssertSingleErrorMentions(string key)
        {
            var errors = Data.Validate();
            Assert.AreEqual(1, errors.Count, string.Join("\n", errors));
            StringAssert.Contains(key, errors[0]);
        }
    }
}
