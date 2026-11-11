using System;
using System.Collections.Generic;
using UnityEngine;

// Settings sections. Each value is defined exactly once; values that can be derived
// from others (maximum tap time, pinch slop, pixel values) are calculated, never stored.
// Sections are classes so gestures can hold a reference to them without copying values.
// Distances are in the platform's density-independent unit ("dp"), see DensitySettings.referenceDpi.

namespace Threads.TouchController
{
    /// <summary>How the platform's density-independent unit relates to pixels.</summary>
    [Serializable]
    public sealed class DensitySettings
    {
        [Tooltip("DPI at which one density-independent unit equals one pixel. Android dp = 160, iOS pt = 163, Windows epx = 96.")]
        [Min(0f)] public float referenceDpi;

        [Tooltip("DPI used when neither the OS nor Screen.dpi reports a usable value.")]
        [Min(0f)] public float fallbackDpi;

        public void CopyFrom(DensitySettings other)
        {
            referenceDpi = other.referenceDpi;
            fallbackDpi = other.fallbackDpi;
        }

        internal void Validate(List<string> errors)
        {
            SettingValidation.Positive(errors, SettingKeys.ReferenceDpi, referenceDpi);
            SettingValidation.Positive(errors, SettingKeys.FallbackDpi, fallbackDpi);
        }
    }

    /// <summary>Movement thresholds shared by several gestures (Tap, LongPress, Drag, Pinch, ...).</summary>
    [Serializable]
    public sealed class MotionSettings
    {
        [Tooltip("Distance a finger can move and still count as stationary. Beyond it, taps and long presses fail and drags begin.")]
        [Min(0f)] public float touchSlopDp;

        public void CopyFrom(MotionSettings other)
        {
            touchSlopDp = other.touchSlopDp;
        }

        internal void Validate(List<string> errors)
        {
            SettingValidation.NonNegative(errors, SettingKeys.TouchSlop, touchSlopDp);
        }
    }

    /// <summary>Multi-tap timing and distance. The maximum duration of a single tap is <see cref="LongPressSettings.longPressTimeoutMs"/>.</summary>
    [Serializable]
    public sealed class TapSettings
    {
        [Tooltip("Maximum gap from the first finger up to the second finger down for a double tap.")]
        [Min(0f)] public float doubleTapTimeoutMs;

        [Tooltip("Minimum gap from the first finger up to the second finger down. Rejects bounce / one tap counted twice.")]
        [Min(0f)] public float doubleTapMinTimeMs;

        [Tooltip("Maximum distance between the first and second finger-down positions of a double tap.")]
        [Min(0f)] public float doubleTapSlopDp;

        [Tooltip("Time within which the second finger must land for a two-finger tap.")]
        [Min(0f)] public float multiFingerTapWindowMs;

        public void CopyFrom(TapSettings other)
        {
            doubleTapTimeoutMs = other.doubleTapTimeoutMs;
            doubleTapMinTimeMs = other.doubleTapMinTimeMs;
            doubleTapSlopDp = other.doubleTapSlopDp;
            multiFingerTapWindowMs = other.multiFingerTapWindowMs;
        }

        internal void Validate(List<string> errors)
        {
            SettingValidation.Positive(errors, SettingKeys.DoubleTapTimeout, doubleTapTimeoutMs);
            SettingValidation.NonNegative(errors, SettingKeys.DoubleTapMinTime, doubleTapMinTimeMs);
            SettingValidation.NonNegative(errors, SettingKeys.DoubleTapSlop, doubleTapSlopDp);
            SettingValidation.NonNegative(errors, SettingKeys.MultiFingerTapWindow, multiFingerTapWindowMs);
            if (doubleTapMinTimeMs >= doubleTapTimeoutMs)
                errors.Add($"{SettingKeys.DoubleTapMinTime} ({doubleTapMinTimeMs}) must be less than {SettingKeys.DoubleTapTimeout} ({doubleTapTimeoutMs}).");
        }
    }

    /// <summary>Long press timing. Also the maximum duration of a tap.</summary>
    [Serializable]
    public sealed class LongPressSettings
    {
        [Tooltip("Time a finger must be held (within the touch slop) to become a long press. Also the maximum duration of a tap.")]
        [Min(0f)] public float longPressTimeoutMs;

        public void CopyFrom(LongPressSettings other)
        {
            longPressTimeoutMs = other.longPressTimeoutMs;
        }

        internal void Validate(List<string> errors)
        {
            SettingValidation.Positive(errors, SettingKeys.LongPressTimeout, longPressTimeoutMs);
        }
    }

    /// <summary>Velocity tracking and swipe / fling thresholds.</summary>
    [Serializable]
    public sealed class FlingSettings
    {
        [Tooltip("Minimum release speed for a swipe / fling.")]
        [Min(0f)] public float minFlingVelocityDps;

        [Tooltip("Speeds above this are clamped.")]
        [Min(0f)] public float maxFlingVelocityDps;

        [Tooltip("How much recent finger history is used to estimate velocity.")]
        [Min(0f)] public float velocityWindowMs;

        [Tooltip("Maximum angle between a swipe and the nearest axis (degrees). Must be in (0, 45].")]
        [Range(0f, 45f)] public float swipeAngleToleranceDeg;

        public void CopyFrom(FlingSettings other)
        {
            minFlingVelocityDps = other.minFlingVelocityDps;
            maxFlingVelocityDps = other.maxFlingVelocityDps;
            velocityWindowMs = other.velocityWindowMs;
            swipeAngleToleranceDeg = other.swipeAngleToleranceDeg;
        }

        internal void Validate(List<string> errors)
        {
            SettingValidation.NonNegative(errors, SettingKeys.MinFlingVelocity, minFlingVelocityDps);
            SettingValidation.Positive(errors, SettingKeys.VelocityWindow, velocityWindowMs);
            if (maxFlingVelocityDps <= minFlingVelocityDps)
                errors.Add($"{SettingKeys.MaxFlingVelocity} ({maxFlingVelocityDps}) must be greater than {SettingKeys.MinFlingVelocity} ({minFlingVelocityDps}).");
            if (!(swipeAngleToleranceDeg > 0f && swipeAngleToleranceDeg <= 45f))
                errors.Add($"{SettingKeys.SwipeAngleTolerance} ({swipeAngleToleranceDeg}) must be greater than 0 and at most 45.");
        }
    }

    /// <summary>Pinch (scale) thresholds. The span-change slop is derived: 2 x <see cref="MotionSettings.touchSlopDp"/>.</summary>
    [Serializable]
    public sealed class PinchSettings
    {
        [Tooltip("Minimum distance between fingers before scaling can start; scaling ends below it.")]
        [Min(0f)] public float minScalingSpanDp;

        public void CopyFrom(PinchSettings other)
        {
            minScalingSpanDp = other.minScalingSpanDp;
        }

        internal void Validate(List<string> errors)
        {
            SettingValidation.NonNegative(errors, SettingKeys.MinScalingSpan, minScalingSpanDp);
        }
    }

    /// <summary>Rotate (twist) thresholds.</summary>
    [Serializable]
    public sealed class RotateSettings
    {
        [Tooltip("Angle the fingers must turn before a rotation is recognised (degrees).")]
        [Min(0f)] public float minRotationDeg;

        public void CopyFrom(RotateSettings other)
        {
            minRotationDeg = other.minRotationDeg;
        }

        internal void Validate(List<string> errors)
        {
            SettingValidation.NonNegative(errors, SettingKeys.MinRotation, minRotationDeg);
        }
    }

    /// <summary>Screen-edge gesture thresholds.</summary>
    [Serializable]
    public sealed class EdgeSettings
    {
        [Tooltip("Width of the strip along each screen edge where an edge swipe can start.")]
        [Min(0f)] public float edgeSlopDp;

        public void CopyFrom(EdgeSettings other)
        {
            edgeSlopDp = other.edgeSlopDp;
        }

        internal void Validate(List<string> errors)
        {
            SettingValidation.NonNegative(errors, SettingKeys.EdgeSlop, edgeSlopDp);
        }
    }

    /// <summary>How settings are resolved at runtime.</summary>
    [Serializable]
    public sealed class PolicySettings
    {
        [Tooltip("Replace asset values with operating-system values where the OS provides them (accessibility). Screen density is always taken from the OS when available.")]
        public bool useOsValues;

        public void CopyFrom(PolicySettings other)
        {
            useOsValues = other.useOsValues;
        }
    }

    internal static class SettingValidation
    {
        public static void Positive(List<string> errors, string key, float value)
        {
            if (!(value > 0f) || float.IsInfinity(value))
                errors.Add($"{key} ({value}) must be greater than 0.");
        }

        public static void NonNegative(List<string> errors, string key, float value)
        {
            if (!(value >= 0f) || float.IsInfinity(value))
                errors.Add($"{key} ({value}) must be 0 or greater.");
        }
    }
}
