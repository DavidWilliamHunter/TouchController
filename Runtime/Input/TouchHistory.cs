using System;
using System.Collections.Generic;
using UnityEngine;

namespace Threads.TouchController
{
    /// <summary>
    /// Recent positions of each contact, and their velocity. Fed from <see cref="TouchFrame"/>s, so it
    /// works the same with live, scripted or recorded input.
    /// </summary>
    /// <remarks>
    /// Velocity is a least-squares fit of position against time over the last
    /// <see cref="FlingSettings.velocityWindowMs"/> (read live from the settings section). Every frame
    /// adds a sample, including Stationary ones, so a finger that stops before lifting has ~zero
    /// velocity (as on Android, where a paused finger does not fling). A contact's history is kept for
    /// the frame in which it ends, so its release velocity can be read, and removed on the next update.
    /// </remarks>
    public sealed class TouchHistory
    {
        /// <summary>Upper bound on samples kept per contact, whatever the window.</summary>
        public const int MaxSamples = 64;

        private readonly FlingSettings _fling;
        private readonly Dictionary<int, Track> _tracks = new Dictionary<int, Track>();
        private readonly List<int> _scratch = new List<int>();
        private double _now;

        public TouchHistory(FlingSettings fling)
        {
            _fling = fling ?? throw new ArgumentNullException(nameof(fling));
        }

        /// <summary>Number of contacts being tracked.</summary>
        public int Count => _tracks.Count;

        public void Update(TouchFrame frame)
        {
            if (frame == null) throw new ArgumentNullException(nameof(frame));
            _now = frame.Time;

            // Drop contacts that ended last frame or are no longer reported.
            _scratch.Clear();
            foreach (var pair in _tracks)
            {
                if (pair.Value.Ended || !frame.TryGet(pair.Key, out _))
                    _scratch.Add(pair.Key);
            }
            foreach (var id in _scratch)
                _tracks.Remove(id);

            var windowSeconds = _fling.velocityWindowMs / 1000.0;
            var touches = frame.Touches;
            for (var i = 0; i < touches.Count; i++)
            {
                var touch = touches[i];
                if (!_tracks.TryGetValue(touch.touchId, out var track))
                {
                    track = new Track();
                    _tracks.Add(touch.touchId, track);
                }
                else if (touch.phase == TouchPointPhase.Began)
                {
                    track.Samples.Clear(); // a new contact: never mix in an older one's samples
                }
                track.Add(frame.Time, touch.positionDp);
                track.Prune(frame.Time - windowSeconds);
                track.Ended = touch.HasEnded;
            }
        }

        /// <summary>Velocity in dp per second; zero if unknown or fewer than two samples.</summary>
        public Vector2 GetVelocityDps(int touchId) =>
            TryGetVelocityDps(touchId, out var velocity) ? velocity : Vector2.zero;

        /// <summary>False if the contact is not tracked.</summary>
        public bool TryGetVelocityDps(int touchId, out Vector2 velocity)
        {
            velocity = Vector2.zero;
            if (!_tracks.TryGetValue(touchId, out var track))
                return false;
            velocity = track.Velocity(_now - _fling.velocityWindowMs / 1000.0);
            return true;
        }

        /// <summary>Number of samples currently kept for a contact (0 if not tracked).</summary>
        public int SampleCount(int touchId) => _tracks.TryGetValue(touchId, out var track) ? track.Samples.Count : 0;

        public bool IsTracking(int touchId) => _tracks.ContainsKey(touchId);

        public void Clear() => _tracks.Clear();

        private sealed class Track
        {
            public readonly List<(double time, Vector2 position)> Samples = new List<(double, Vector2)>();
            public bool Ended;

            public void Add(double time, Vector2 position)
            {
                if (Samples.Count == MaxSamples)
                    Samples.RemoveAt(0);
                Samples.Add((time, position));
            }

            /// <summary>Removes samples older than <paramref name="oldest"/>, always keeping the latest.</summary>
            public void Prune(double oldest)
            {
                var remove = 0;
                while (remove < Samples.Count - 1 && Samples[remove].time < oldest)
                    remove++;
                if (remove > 0)
                    Samples.RemoveRange(0, remove);
            }

            public Vector2 Velocity(double oldest)
            {
                // Ordinary least squares: slope of position against time, per axis.
                double count = 0, sumT = 0, sumX = 0, sumY = 0;
                var reference = Samples.Count > 0 ? Samples[Samples.Count - 1].time : 0.0;
                foreach (var (time, position) in Samples)
                {
                    if (time < oldest) continue;
                    var t = time - reference;
                    count++;
                    sumT += t;
                    sumX += position.x;
                    sumY += position.y;
                }
                if (count < 2) return Vector2.zero;

                double meanT = sumT / count, meanX = sumX / count, meanY = sumY / count;
                double stt = 0, stx = 0, sty = 0;
                foreach (var (time, position) in Samples)
                {
                    if (time < oldest) continue;
                    var dt = time - reference - meanT;
                    stt += dt * dt;
                    stx += dt * (position.x - meanX);
                    sty += dt * (position.y - meanY);
                }
                if (stt <= 1e-12) return Vector2.zero;
                return new Vector2((float)(stx / stt), (float)(sty / stt));
            }
        }
    }
}
