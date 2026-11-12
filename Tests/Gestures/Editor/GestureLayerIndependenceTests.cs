using System.IO;
using System.Linq;
using NUnit.Framework;

namespace Threads.TouchController.Tests
{
    /// <summary>
    /// Gestures and intentions share one assembly, so the compiler no longer stops gesture code from using
    /// intention code. This test keeps the rule "gesture ≠ intention": only Runtime/Intentions may mention
    /// the Intentions namespace (intentions depend on gestures, never the other way round).
    /// </summary>
    public class GestureLayerIndependenceTests
    {
        [Test]
        public void GestureLayer_NeverUsesIntentions()
        {
            var runtime = Path.GetFullPath("Packages/com.threads.touchcontroller/Runtime");
            Assert.IsTrue(Directory.Exists(runtime), $"Runtime folder not found at {runtime}");
            var intentions = Path.Combine(runtime, "Intentions") + Path.DirectorySeparatorChar;

            var offenders = Directory.GetFiles(runtime, "*.cs", SearchOption.AllDirectories)
                .Where(path => !path.StartsWith(intentions))
                .Where(path => File.ReadAllText(path).Contains("TouchController.Intentions"))
                .Select(path => path.Substring(runtime.Length + 1))
                .ToList();

            Assert.IsEmpty(offenders, "Gesture-layer files that use Threads.TouchController.Intentions");
        }
    }
}
