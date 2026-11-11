using System;
using UnityEngine;

namespace Threads.TouchController
{
    /// <summary>
    /// Reads Android's <c>ViewConfiguration</c> and <c>DisplayMetrics</c> through JNI. Values are
    /// device-scaled pixels and include the user's accessibility settings (e.g. "Touch &amp; hold delay").
    /// Returns empty values when not running on an Android device (including the Editor).
    /// </summary>
    public sealed class AndroidOsTouchConfigProvider : IOsTouchConfigProvider
    {
        public OsTouchValues Read()
        {
            var values = new OsTouchValues();
#if UNITY_ANDROID && !UNITY_EDITOR
            try
            {
                // Owned by Unity; do not dispose.
                var activity = UnityEngine.Android.AndroidApplication.currentActivity;

                using (var resources = activity.Call<AndroidJavaObject>("getResources"))
                using (var metrics = resources.Call<AndroidJavaObject>("getDisplayMetrics"))
                {
                    // densityDpi / 160 == DisplayMetrics.density, the factor Android itself uses for dp.
                    values.densityDpi = metrics.Get<int>("densityDpi");
                }

                using (var viewConfigurationClass = new AndroidJavaClass("android.view.ViewConfiguration"))
                using (var viewConfiguration = viewConfigurationClass.CallStatic<AndroidJavaObject>("get", activity))
                using (var version = new AndroidJavaClass("android.os.Build$VERSION"))
                {
                    values.touchSlopPx = viewConfiguration.Call<int>("getScaledTouchSlop");
                    values.doubleTapSlopPx = viewConfiguration.Call<int>("getScaledDoubleTapSlop");
                    values.edgeSlopPx = viewConfiguration.Call<int>("getScaledEdgeSlop");
                    values.minFlingVelocityPxPerSecond = viewConfiguration.Call<int>("getScaledMinimumFlingVelocity");
                    values.maxFlingVelocityPxPerSecond = viewConfiguration.Call<int>("getScaledMaximumFlingVelocity");
                    values.longPressTimeoutMs = viewConfigurationClass.CallStatic<int>("getLongPressTimeout");
                    values.doubleTapTimeoutMs = viewConfigurationClass.CallStatic<int>("getDoubleTapTimeout");

                    if (version.GetStatic<int>("SDK_INT") >= 29)
                        values.minScalingSpanPx = viewConfiguration.Call<int>("getScaledMinimumScalingSpan");
                }
            }
            catch (Exception e)
            {
                // Keep whatever was read before the failure; the asset supplies the rest.
                Debug.LogWarning($"TouchController: could not read Android ViewConfiguration: {e.Message}");
            }
#endif
            return values;
        }
    }
}
