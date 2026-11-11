namespace Threads.TouchController
{
    /// <summary>
    /// Stable names for individual settings, in the form "Section.field". Used for validation
    /// messages and to record where a resolved value came from (<see cref="TouchEnvironment.GetSource"/>).
    /// These are names only; the values live in <see cref="TouchSettingsData"/>.
    /// </summary>
    public static class SettingKeys
    {
        public const string ReferenceDpi = nameof(TouchSettingsData.Density) + "." + nameof(DensitySettings.referenceDpi);
        public const string FallbackDpi = nameof(TouchSettingsData.Density) + "." + nameof(DensitySettings.fallbackDpi);

        public const string TouchSlop = nameof(TouchSettingsData.Motion) + "." + nameof(MotionSettings.touchSlopDp);

        public const string DoubleTapTimeout = nameof(TouchSettingsData.Tap) + "." + nameof(TapSettings.doubleTapTimeoutMs);
        public const string DoubleTapMinTime = nameof(TouchSettingsData.Tap) + "." + nameof(TapSettings.doubleTapMinTimeMs);
        public const string DoubleTapSlop = nameof(TouchSettingsData.Tap) + "." + nameof(TapSettings.doubleTapSlopDp);
        public const string MultiFingerTapWindow = nameof(TouchSettingsData.Tap) + "." + nameof(TapSettings.multiFingerTapWindowMs);

        public const string LongPressTimeout = nameof(TouchSettingsData.LongPress) + "." + nameof(LongPressSettings.longPressTimeoutMs);

        public const string MinFlingVelocity = nameof(TouchSettingsData.Fling) + "." + nameof(FlingSettings.minFlingVelocityDps);
        public const string MaxFlingVelocity = nameof(TouchSettingsData.Fling) + "." + nameof(FlingSettings.maxFlingVelocityDps);
        public const string VelocityWindow = nameof(TouchSettingsData.Fling) + "." + nameof(FlingSettings.velocityWindowMs);
        public const string SwipeAngleTolerance = nameof(TouchSettingsData.Fling) + "." + nameof(FlingSettings.swipeAngleToleranceDeg);

        public const string MinScalingSpan = nameof(TouchSettingsData.Pinch) + "." + nameof(PinchSettings.minScalingSpanDp);

        public const string MinRotation = nameof(TouchSettingsData.Rotate) + "." + nameof(RotateSettings.minRotationDeg);

        public const string EdgeSlop = nameof(TouchSettingsData.Edge) + "." + nameof(EdgeSettings.edgeSlopDp);

        public const string UseOsValues = nameof(TouchSettingsData.Policy) + "." + nameof(PolicySettings.useOsValues);
    }
}
