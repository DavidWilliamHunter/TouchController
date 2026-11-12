using NUnit.Framework;
using UnityEngine;

namespace Threads.TouchController.Tests
{
    /// <summary>Real OS readers, as far as they can run in the Editor.</summary>
    public class OsProviderTests
    {
        [Test]
        public void ForPlatform_ReturnsMatchingReader()
        {
            Assert.IsInstanceOf<AndroidOsTouchConfigProvider>(OsTouchConfigProviders.ForPlatform(TouchPlatform.Android));
            Assert.IsInstanceOf<WindowsOsTouchConfigProvider>(OsTouchConfigProviders.ForPlatform(TouchPlatform.Windows));
            Assert.IsInstanceOf<NullOsTouchConfigProvider>(OsTouchConfigProviders.ForPlatform(TouchPlatform.iOS));
        }

        [Test]
        public void Null_ReturnsEmptyValues()
        {
            var values = new NullOsTouchConfigProvider().Read();
            Assert.IsNotNull(values);
            Assert.IsNull(values.densityDpi);
            Assert.IsNull(values.longPressTimeoutMs);
        }

        [Test]
        public void Android_InEditor_ReturnsEmptyValues_WithoutThrowing()
        {
            var values = new AndroidOsTouchConfigProvider().Read();
            Assert.IsNotNull(values);
            Assert.IsNull(values.densityDpi);
            Assert.IsNull(values.touchSlopPx);
        }

        [Test]
        public void Windows_OnWindowsEditor_ReadsPlausibleValues()
        {
            if (Application.platform != RuntimePlatform.WindowsEditor)
                Assert.Ignore("Only meaningful in the Windows Editor.");

            var values = new WindowsOsTouchConfigProvider().Read();

            Assert.IsTrue(values.doubleTapTimeoutMs.HasValue, "GetDoubleClickTime should be readable.");
            Assert.That(values.doubleTapTimeoutMs.Value, Is.InRange(100f, 5000f));
            if (values.densityDpi.HasValue) // may be absent in batch mode (no active window) on old Windows
                Assert.That(values.densityDpi.Value, Is.InRange(72f, 1000f));
        }
    }
}
