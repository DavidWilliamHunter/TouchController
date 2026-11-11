using System;
using System.Collections.Generic;
using UnityEngine;

namespace Threads.TouchController
{
    /// <summary>
    /// All touch threshold settings as a plain serialisable object. This is what JSON reads and
    /// writes (it contains no UnityEngine.Object fields).
    /// </summary>
    /// <remarks>
    /// Sections are exposed as get-only properties and are never replaced, so gestures can hold a
    /// reference to a section and always see current values. Use <see cref="CopyFrom"/> to change
    /// all values at once. With Newtonsoft, use <c>JsonConvert.PopulateObject</c> to load JSON into
    /// an existing instance.
    /// </remarks>
    [Serializable]
    public sealed class TouchSettingsData
    {
        [SerializeField] private DensitySettings density = new DensitySettings();
        [SerializeField] private MotionSettings motion = new MotionSettings();
        [SerializeField] private TapSettings tap = new TapSettings();
        [SerializeField] private LongPressSettings longPress = new LongPressSettings();
        [SerializeField] private FlingSettings fling = new FlingSettings();
        [SerializeField] private PinchSettings pinch = new PinchSettings();
        [SerializeField] private RotateSettings rotate = new RotateSettings();
        [SerializeField] private EdgeSettings edge = new EdgeSettings();
        [SerializeField] private PolicySettings policy = new PolicySettings();

        public DensitySettings Density => density;
        public MotionSettings Motion => motion;
        public TapSettings Tap => tap;
        public LongPressSettings LongPress => longPress;
        public FlingSettings Fling => fling;
        public PinchSettings Pinch => pinch;
        public RotateSettings Rotate => rotate;
        public EdgeSettings Edge => edge;
        public PolicySettings Policy => policy;

        /// <summary>Copies every value from <paramref name="other"/>. Section instances are kept.</summary>
        public void CopyFrom(TouchSettingsData other)
        {
            if (other == null) throw new ArgumentNullException(nameof(other));
            density.CopyFrom(other.density);
            motion.CopyFrom(other.motion);
            tap.CopyFrom(other.tap);
            longPress.CopyFrom(other.longPress);
            fling.CopyFrom(other.fling);
            pinch.CopyFrom(other.pinch);
            rotate.CopyFrom(other.rotate);
            edge.CopyFrom(other.edge);
            policy.CopyFrom(other.policy);
        }

        /// <summary>Returns a description of every invalid value. Empty when all values are valid.</summary>
        public IReadOnlyList<string> Validate()
        {
            var errors = new List<string>();
            density.Validate(errors);
            motion.Validate(errors);
            tap.Validate(errors);
            longPress.Validate(errors);
            fling.Validate(errors);
            pinch.Validate(errors);
            rotate.Validate(errors);
            edge.Validate(errors);
            return errors;
        }
    }
}
