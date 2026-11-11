using System;
using UnityEngine;

namespace Threads.TouchController
{
    /// <summary>One gesture in a <see cref="TouchSettings"/> lineup.</summary>
    [Serializable]
    public sealed class GestureEntry
    {
        [Tooltip("The gesture asset. The same asset can appear in several settings assets (e.g. Tap in every platform's lineup).")]
        public GestureBase gesture;

        [Tooltip("Whether the gesture starts enabled.")]
        public bool enabled = true;

        public GestureEntry()
        {
        }

        public GestureEntry(GestureBase gesture, bool enabled = true)
        {
            this.gesture = gesture;
            this.enabled = enabled;
        }
    }
}
