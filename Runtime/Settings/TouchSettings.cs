using System.Collections.Generic;
using UnityEngine;

namespace Threads.TouchController
{
    /// <summary>
    /// The single settings asset for TouchController. Every setting is reachable from here: the
    /// thresholds (<see cref="Data"/>) and the gesture lineup (<see cref="Lineup"/>).
    /// The package ships one per platform (Android, iOS, Windows) in
    /// <c>Runtime/Resources/Threads.TouchController</c>; see <see cref="DefaultTouchSettings"/>.
    /// </summary>
    /// <remarks>
    /// Never modify a shipped asset at runtime. <see cref="TouchSystem"/> works on a runtime copy,
    /// whose lineup points at runtime copies of the gestures.
    /// </remarks>
    [CreateAssetMenu(fileName = "TouchSettings", menuName = "Threads/TouchController/Touch Settings", order = 0)]
    public sealed class TouchSettings : ScriptableObject
    {
        [SerializeField] private TouchSettingsData data = new TouchSettingsData();

        [Tooltip("The gestures to run, in update order.")]
        [SerializeField] private List<GestureEntry> lineup = new List<GestureEntry>();

        [Tooltip("Where the values come from, and which are guesses still to be checked on real devices.")]
        [SerializeField, TextArea(3, 30)] private string notes = "";

        /// <summary>All threshold values.</summary>
        public TouchSettingsData Data => data;

        /// <summary>The gestures to run, in update order. Change it at runtime through <see cref="TouchSystem.Add"/> / <see cref="TouchSystem.Remove"/>.</summary>
        public IReadOnlyList<GestureEntry> Lineup => lineup;

        /// <summary>Human-readable notes about the values' sources and open questions.</summary>
        public string Notes => notes;

        internal List<GestureEntry> LineupList => lineup;

        /// <summary>Returns a description of every problem in the lineup. Empty when valid.</summary>
        public IReadOnlyList<string> ValidateLineup()
        {
            var errors = new List<string>();
            var seen = new HashSet<GestureBase>();
            for (var i = 0; i < lineup.Count; i++)
            {
                var entry = lineup[i];
                if (entry == null || entry.gesture == null)
                    errors.Add($"Lineup[{i}] has no gesture.");
                else if (!seen.Add(entry.gesture))
                    errors.Add($"Lineup[{i}] repeats gesture '{entry.gesture.name}'.");
            }
            return errors;
        }

        private void OnValidate()
        {
            foreach (var error in data.Validate())
                Debug.LogWarning($"{name}: {error}", this);
            foreach (var error in ValidateLineup())
                Debug.LogWarning($"{name}: {error}", this);
        }
    }
}
