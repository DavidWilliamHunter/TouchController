using NUnit.Framework;
using UnityEngine;

namespace Threads.TouchController.Tests
{
    public class PlatformDetectorTests
    {
        [TestCase(RuntimePlatform.Android, TouchPlatform.Android)]
        [TestCase(RuntimePlatform.IPhonePlayer, TouchPlatform.iOS)]
        [TestCase(RuntimePlatform.WindowsPlayer, TouchPlatform.Windows)]
        public void Player_MapsSupportedPlatforms(RuntimePlatform runtime, TouchPlatform expected)
        {
            Assert.AreEqual(expected, PlatformDetector.Detect(runtime, editorBuildTarget: null));
        }

        [TestCase(RuntimePlatform.OSXPlayer)]
        [TestCase(RuntimePlatform.LinuxPlayer)]
        [TestCase(RuntimePlatform.WebGLPlayer)]
        public void Player_UnknownPlatform_FallsBackToAndroid(RuntimePlatform runtime)
        {
            Assert.IsNull(PlatformDetector.FromRuntimePlatform(runtime));
            Assert.AreEqual(TouchPlatform.Android, PlatformDetector.Detect(runtime, editorBuildTarget: null));
        }

        [TestCase(RuntimePlatform.WindowsEditor, TouchPlatform.iOS)]
        [TestCase(RuntimePlatform.OSXEditor, TouchPlatform.Windows)]
        [TestCase(RuntimePlatform.LinuxEditor, TouchPlatform.Android)]
        public void Editor_UsesBuildTarget(RuntimePlatform editor, TouchPlatform target)
        {
            Assert.AreEqual(target, PlatformDetector.Detect(editor, target));
        }

        [TestCase(RuntimePlatform.WindowsEditor)]
        [TestCase(RuntimePlatform.OSXEditor)]
        public void Editor_NonTouchBuildTarget_FallsBackToAndroid(RuntimePlatform editor)
        {
            Assert.AreEqual(TouchPlatform.Android, PlatformDetector.Detect(editor, editorBuildTarget: null));
        }

        [Test]
        public void Editor_IgnoresRuntimeMappingOfEditorPlatform()
        {
            // WindowsEditor must not be treated as the Windows *player*.
            Assert.AreEqual(TouchPlatform.Android, PlatformDetector.Detect(RuntimePlatform.WindowsEditor, null));
        }
    }
}
