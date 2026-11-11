using System;
using System.Collections.Generic;

namespace Threads.TouchController
{
    /// <summary>
    /// What consumers (context / intention code) see of a gesture: its events, its state and whether
    /// it is enabled. Implemented by <see cref="GestureBase"/>.
    /// </summary>
    public interface IGesture
    {
        /// <summary>The gesture asset's name.</summary>
        string Name { get; }

        /// <summary>
        /// Disabling sends <see cref="OnDisabled"/> and silently discards every instance (no Fail or Exit).
        /// Enabling sends <see cref="OnEnabled"/>, resets the gesture, and waits until no fingers are on
        /// the screen before recognising again.
        /// </summary>
        bool Enabled { get; set; }

        GestureRunState RunState { get; }

        /// <summary>True if any instance is Active (between Enter and Exit).</summary>
        bool IsActive { get; }

        /// <summary>True if any instance is Possible.</summary>
        bool IsPossible { get; }

        /// <summary>Current instances. Finished ones are removed at the end of each update.</summary>
        IReadOnlyList<GestureInstance> Instances { get; }

        event Action<GestureEventData> Possible;
        event Action<GestureEventData> Fail;
        event Action<GestureEventData> Enter;
        event Action<GestureEventData> Stay;
        event Action<GestureEventData> Exit;

        /// <summary>The gesture was enabled. Sent while disabled → enabled.</summary>
        event Action<IGesture> OnEnabled;

        /// <summary>The gesture was disabled. Every instance was discarded without Fail or Exit: drop any you are tracking.</summary>
        event Action<IGesture> OnDisabled;
    }

    /// <summary>
    /// What gestures share within one <see cref="TouchSystem"/>: the active settings, touch history and
    /// instance IDs. Created by the TouchSystem (or a test harness); no global state.
    /// </summary>
    public sealed class GestureContext
    {
        private int _nextInstanceId = 1;

        public GestureContext(TouchSettingsData settings, TouchHistory history)
        {
            Settings = settings ?? throw new ArgumentNullException(nameof(settings));
            History = history ?? throw new ArgumentNullException(nameof(history));
        }

        /// <summary>The active settings. Sections are never replaced, so references stay valid.</summary>
        public TouchSettingsData Settings { get; }

        public TouchHistory History { get; }

        internal int NextInstanceId() => _nextInstanceId++;
    }
}
