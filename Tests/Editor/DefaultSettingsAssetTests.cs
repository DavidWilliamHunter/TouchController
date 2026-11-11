using System.Linq;
using NUnit.Framework;

namespace Threads.TouchController.Tests
{
    /// <summary>The shipped platform assets exist, are valid, and hold the published defaults.</summary>
    public class DefaultSettingsAssetTests
    {
        private static readonly TouchPlatform[] AllPlatforms = { TouchPlatform.Android, TouchPlatform.iOS, TouchPlatform.Windows };

        [TestCaseSource(nameof(AllPlatforms))]
        public void Asset_LoadsWithExpectedName(TouchPlatform platform)
        {
            var asset = DefaultTouchSettings.Load(platform);
            Assert.IsNotNull(asset);
            Assert.AreEqual(DefaultTouchSettings.AssetName(platform), asset.name);
        }

        [TestCaseSource(nameof(AllPlatforms))]
        public void Asset_IsValid(TouchPlatform platform)
        {
            var errors = DefaultTouchSettings.Load(platform).Data.Validate();
            Assert.IsEmpty(errors, string.Join("\n", errors));
        }

        [TestCaseSource(nameof(AllPlatforms))]
        public void Asset_LineupIsValid(TouchPlatform platform)
        {
            var errors = DefaultTouchSettings.Load(platform).ValidateLineup();
            Assert.IsEmpty(errors, string.Join("\n", errors));
        }

        [TestCaseSource(nameof(AllPlatforms))]
        public void Asset_UsesOsValuesByDefault(TouchPlatform platform)
        {
            Assert.IsTrue(DefaultTouchSettings.Load(platform).Data.Policy.useOsValues);
        }

        [TestCaseSource(nameof(AllPlatforms))]
        public void Asset_NotesDocumentGuesses(TouchPlatform platform)
        {
            var notes = DefaultTouchSettings.Load(platform).Notes;
            StringAssert.Contains("GUESS", notes);
            StringAssert.Contains("Q2", notes);
        }

        [Test]
        public void Windows_NotesReferenceSurfaceQuestions()
        {
            var notes = DefaultTouchSettings.Load(TouchPlatform.Windows).Notes;
            StringAssert.Contains("Q9", notes);
            StringAssert.Contains("Q11", notes);
        }

        [Test]
        public void Android_HasAospDefaults()
        {
            var d = DefaultTouchSettings.Load(TouchPlatform.Android).Data;
            Assert.AreEqual(160f, d.Density.referenceDpi);
            Assert.AreEqual(8f, d.Motion.touchSlopDp);
            Assert.AreEqual(400f, d.LongPress.longPressTimeoutMs);
            Assert.AreEqual(300f, d.Tap.doubleTapTimeoutMs);
            Assert.AreEqual(40f, d.Tap.doubleTapMinTimeMs);
            Assert.AreEqual(100f, d.Tap.doubleTapSlopDp);
            Assert.AreEqual(50f, d.Fling.minFlingVelocityDps);
            Assert.AreEqual(8000f, d.Fling.maxFlingVelocityDps);
            Assert.AreEqual(12f, d.Edge.edgeSlopDp);
        }

        [Test]
        public void iOS_HasUIKitDefaults()
        {
            var d = DefaultTouchSettings.Load(TouchPlatform.iOS).Data;
            Assert.AreEqual(163f, d.Density.referenceDpi);
            Assert.AreEqual(10f, d.Motion.touchSlopDp);
            Assert.AreEqual(500f, d.LongPress.longPressTimeoutMs);
        }

        [Test]
        public void Windows_UsesEffectivePixelsAndDoubleClickTime()
        {
            var d = DefaultTouchSettings.Load(TouchPlatform.Windows).Data;
            Assert.AreEqual(96f, d.Density.referenceDpi);
            Assert.AreEqual(500f, d.Tap.doubleTapTimeoutMs);
        }

        [Test]
        public void AssetNames_AreDistinct()
        {
            var names = AllPlatforms.Select(DefaultTouchSettings.AssetName).ToArray();
            CollectionAssert.AllItemsAreUnique(names);
        }
    }
}
