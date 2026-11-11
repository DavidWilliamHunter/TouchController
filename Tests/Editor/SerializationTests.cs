using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using UnityEngine;

namespace Threads.TouchController.Tests
{
    /// <summary>
    /// TouchSettingsData round-trips through Newtonsoft (used by tests and the demo only) and
    /// JsonUtility, without the runtime package depending on Newtonsoft.
    /// </summary>
    public class SerializationTests
    {
        private static readonly string[] SectionNames =
        {
            "Density", "Motion", "Tap", "LongPress", "Fling", "Pinch", "Rotate", "Edge", "Policy",
        };

        private TouchSettings _source;

        [SetUp]
        public void SetUp() => _source = TestSettings.CopyOfDefault(TouchPlatform.Android);

        [TearDown]
        public void TearDown() => Object.DestroyImmediate(_source);

        [Test]
        public void Newtonsoft_SerialisesExactlyTheSections()
        {
            var json = JObject.Parse(JsonConvert.SerializeObject(_source.Data));
            CollectionAssert.AreEquivalent(SectionNames, json.Properties().Select(p => p.Name));
        }

        [Test]
        public void Newtonsoft_SectionContainsValues()
        {
            var json = JObject.Parse(JsonConvert.SerializeObject(_source.Data));
            Assert.AreEqual(8f, json["Motion"]["touchSlopDp"].Value<float>());
            Assert.AreEqual(400f, json["LongPress"]["longPressTimeoutMs"].Value<float>());
            Assert.IsTrue(json["Policy"]["useOsValues"].Value<bool>());
        }

        [Test]
        public void Newtonsoft_HasNoUnityObjectFields()
        {
            var text = JsonConvert.SerializeObject(_source.Data);
            StringAssert.DoesNotContain("hideFlags", text);
            StringAssert.DoesNotContain("\"name\"", text);
        }

        [Test]
        public void Newtonsoft_PopulateObject_RoundTrips_AndKeepsSections()
        {
            _source.Data.Motion.touchSlopDp = 11.5f;
            _source.Data.Tap.doubleTapTimeoutMs = 275f;
            _source.Data.Policy.useOsValues = false;
            var json = JsonConvert.SerializeObject(_source.Data);

            var target = new TouchSettingsData();
            var motion = target.Motion;
            JsonConvert.PopulateObject(json, target);

            Assert.AreSame(motion, target.Motion, "Sections must be populated in place, not replaced.");
            Assert.AreEqual(JsonConvert.SerializeObject(_source.Data), JsonConvert.SerializeObject(target));
            Assert.AreEqual(11.5f, target.Motion.touchSlopDp);
            Assert.AreEqual(275f, target.Tap.doubleTapTimeoutMs);
            Assert.IsFalse(target.Policy.useOsValues);
        }

        [Test]
        public void Newtonsoft_PartialJson_OnlyChangesGivenValues()
        {
            var target = new TouchSettingsData();
            target.CopyFrom(_source.Data);

            JsonConvert.PopulateObject("{\"LongPress\":{\"longPressTimeoutMs\":900}}", target);

            Assert.AreEqual(900f, target.LongPress.longPressTimeoutMs);
            Assert.AreEqual(8f, target.Motion.touchSlopDp);
        }

        [Test]
        public void JsonUtility_RoundTrips()
        {
            _source.Data.Edge.edgeSlopDp = 20f;
            var json = JsonUtility.ToJson(_source.Data);

            var target = new TouchSettingsData();
            JsonUtility.FromJsonOverwrite(json, target);

            Assert.AreEqual(20f, target.Edge.edgeSlopDp);
            Assert.AreEqual(JsonConvert.SerializeObject(_source.Data), JsonConvert.SerializeObject(target));
        }
    }
}
