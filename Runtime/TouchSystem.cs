using System;
using System.Collections.Generic;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Threads.TouchController
{
    /// <summary>
    /// What a <see cref="TouchSystem"/> is built from. Every field is optional; unset fields use the
    /// Unity defaults (platform from the running app, the platform's shipped settings, the
    /// Input System via <see cref="TouchSource"/>).
    /// </summary>
    public sealed class TouchSystemOptions
    {
        /// <summary>Settings asset to use (thresholds and gesture lineup). Null: the shipped default for <see cref="Platform"/>.</summary>
        public TouchSettings Settings;

        /// <summary>Platform whose conventions apply. Null: <see cref="ITouchHost.Platform"/>.</summary>
        public TouchPlatform? Platform;

        /// <summary>Reads OS values (accessibility). Null: the reader for the platform.</summary>
        public IOsTouchConfigProvider OsProvider;

        /// <summary>Where touches come from. Null: a new <see cref="TouchSource"/> (Input System).</summary>
        public ITouchInput Input;

        /// <summary>Platform, screen and clock. Null: a new <see cref="UnityTouchHost"/>.</summary>
        public ITouchHost Host;

        /// <summary>When <see cref="Input"/> is null: simulate touch from the mouse in the Editor.</summary>
        public bool SimulateTouchInEditor = true;

        internal TouchSystemOptions Clone() => (TouchSystemOptions)MemberwiseClone();
    }

    /// <summary>
    /// The touch system: the single place it is started and shut down. Owns the resolved settings,
    /// the touch input, the per-frame <see cref="TouchFrame"/>, the <see cref="TouchHistory"/> and the
    /// running gestures.
    /// </summary>
    /// <remarks>
    /// <para>No global state: create as many as you like, and pass the instance to the code that
    /// needs it. If you want global access, wrap it in your own singleton or register it with a DI
    /// container (e.g. VContainer). Then you decide when it lives and dies.</para>
    /// <para>Lifecycle: constructing does nothing. <see cref="Startup"/> acquires everything,
    /// <see cref="Update"/> runs once per frame (you choose when, relative to your other systems), and
    /// <see cref="Shutdown"/> (or <see cref="Dispose"/>) releases everything. A system can be started
    /// again after shutting down.</para>
    /// <para>Gestures: <see cref="Startup"/> makes a runtime copy of every gesture in the settings'
    /// lineup (the assets are never run or changed). The runtime settings copy's lineup points at
    /// those copies, so everything stays reachable from <see cref="Settings"/>.</para>
    /// <para>The Input System's EnhancedTouch is global by Unity's design. <see cref="TouchSource"/>
    /// uses its reference-counted enable, so several systems, or other code, can use it safely.</para>
    /// </remarks>
    public sealed class TouchSystem : IDisposable
    {
        private readonly TouchSystemOptions _options;
        private readonly List<GestureBase> _gestures = new List<GestureBase>();
        private readonly List<GestureBase> _pendingDestroy = new List<GestureBase>();
        private GestureBase[] _gestureSnapshot = Array.Empty<GestureBase>();
        private ITouchHost _host;
        private ITouchInput _input;
        private bool _inputEnabled;
        private TouchEnvironment _environment;
        private TouchHistory _history;
        private TouchFrame _frame;
        private GestureContext _gestureContext;
        private bool _updating;

        /// <summary>Configures the system. Nothing is started until <see cref="Startup"/>.</summary>
        public TouchSystem(TouchSystemOptions options = null)
        {
            _options = options?.Clone() ?? new TouchSystemOptions();
        }

        /// <summary>True between <see cref="Startup"/> and <see cref="Shutdown"/>.</summary>
        public bool IsRunning { get; private set; }

        /// <summary>Resolved settings and screen density.</summary>
        public TouchEnvironment Environment => RequireRunning(_environment);

        /// <summary>The active (runtime copy of the) settings, including the runtime gesture lineup.</summary>
        public TouchSettings Settings => Environment.Settings;

        public TouchPlatform Platform => Environment.Platform;

        /// <summary>The touches captured by the last <see cref="Update"/>. Reused every frame.</summary>
        public TouchFrame Frame => RequireRunning(_frame);

        /// <summary>Recent positions and velocity of each touch.</summary>
        public TouchHistory History => RequireRunning(_history);

        /// <summary>The running gestures, in update order.</summary>
        public IReadOnlyList<IGesture> Gestures => RequireRunning(_gestures);

        /// <summary>Acquires everything: resolves settings, creates the gestures, enables touch input. Throws if already running.</summary>
        public void Startup()
        {
            if (IsRunning)
                throw new InvalidOperationException("TouchSystem is already running. Call Shutdown() first.");

            try
            {
                _host = _options.Host ?? new UnityTouchHost();
                var platform = _options.Platform ?? _host.Platform;
                var settings = _options.Settings != null ? _options.Settings : DefaultTouchSettings.Load(platform);
                var osProvider = _options.OsProvider ?? OsTouchConfigProviders.ForPlatform(platform);

                _environment = TouchSettingsResolver.Resolve(settings, platform, osProvider, _host.ScreenDpi);
                _history = new TouchHistory(_environment.Settings.Data.Fling);
                _frame = new TouchFrame();
                _gestureContext = new GestureContext(_environment.Settings.Data, _history);
                CreateGestures();

                _input = _options.Input ?? new TouchSource(_options.SimulateTouchInEditor);
                _input.Enable();
                _inputEnabled = true;

                IsRunning = true;
            }
            catch
            {
                Release(); // leave nothing half-started
                throw;
            }
        }

        /// <summary>
        /// Captures this frame's touches, updates the history, then updates every gesture in lineup
        /// order (their events are sent from here). Call once per frame while running.
        /// </summary>
        public void Update()
        {
            RequireRunning();
            _input.Capture(_frame, _environment.PixelsPerDp, _host.Time, _host.ScreenSizePx);
            _history.Update(_frame);

            _updating = true;
            try
            {
                foreach (var gesture in _gestureSnapshot)
                {
                    if (_gestures.Contains(gesture)) // skip gestures removed by an event handler this update
                        gesture.UpdateGesture(_frame);
                }
            }
            finally
            {
                _updating = false;
                foreach (var gesture in _pendingDestroy)
                    DestroyObject(gesture);
                _pendingDestroy.Clear();
            }
        }

        /// <summary>
        /// Re-reads OS values and screen density, e.g. when the app regains focus (the user may have
        /// changed an accessibility setting). Resets runtime edits to the settings asset's values.
        /// The gesture lineup is not affected.
        /// </summary>
        public void Refresh()
        {
            RequireRunning();
            TouchSettingsResolver.Refresh(_environment, _host.ScreenDpi);
        }

        /// <summary>The first running gesture of type <typeparamref name="T"/> (a class or interface). Throws if there is none.</summary>
        public T Get<T>() where T : class
        {
            if (TryGet<T>(out var gesture))
                return gesture;
            throw new InvalidOperationException($"No gesture of type {typeof(T).Name} in the lineup.");
        }

        public bool TryGet<T>(out T gesture) where T : class
        {
            RequireRunning();
            foreach (var candidate in _gestures)
            {
                if (candidate is T match)
                {
                    gesture = match;
                    return true;
                }
            }
            gesture = null;
            return false;
        }

        /// <summary>
        /// Adds a gesture at runtime: makes a runtime copy of <paramref name="gestureAsset"/>, starts it,
        /// and adds it to the runtime settings' lineup. It runs from the next update.
        /// </summary>
        /// <returns>The running copy (subscribe to this, not the asset).</returns>
        public T Add<T>(T gestureAsset, bool enabled = true) where T : GestureBase
        {
            RequireRunning();
            if (gestureAsset == null) throw new ArgumentNullException(nameof(gestureAsset));
            var runtime = CreateRuntimeGesture(gestureAsset, enabled);
            Settings.LineupList.Add(new GestureEntry(runtime, enabled));
            return runtime;
        }

        /// <summary>
        /// Removes a running gesture. If enabled it is disabled first (sending OnDisabled), then destroyed.
        /// Safe to call from an event handler.
        /// </summary>
        /// <returns>False if the gesture is not running in this system.</returns>
        public bool Remove(IGesture gesture)
        {
            RequireRunning();
            if (!(gesture is GestureBase runtime) || !_gestures.Remove(runtime))
                return false;

            runtime.Enabled = false;
            Settings.LineupList.RemoveAll(entry => entry != null && entry.gesture == runtime);
            _gestureSnapshot = _gestures.ToArray();
            if (_updating)
                _pendingDestroy.Add(runtime);
            else
                DestroyObject(runtime);
            return true;
        }

        /// <summary>Releases everything acquired by <see cref="Startup"/>. Safe to call when not running.</summary>
        public void Shutdown() => Release();

        /// <summary>Same as <see cref="Shutdown"/>.</summary>
        public void Dispose() => Release();

        private void CreateGestures()
        {
            var lineup = _environment.Settings.LineupList;
            foreach (var error in _environment.Settings.ValidateLineup())
                Debug.LogWarning($"TouchSystem: {error}", _environment.Defaults);

            foreach (var entry in lineup)
            {
                if (entry == null || entry.gesture == null)
                    continue; // reported above
                entry.gesture = CreateRuntimeGesture(entry.gesture, entry.enabled);
            }
        }

        private T CreateRuntimeGesture<T>(T asset, bool enabled) where T : GestureBase
        {
            if (asset.IsInitialised)
                throw new ArgumentException($"'{asset.name}' is already a running gesture. Pass the gesture asset.", nameof(asset));

            var runtime = Object.Instantiate(asset);
            runtime.name = asset.name;
            runtime.hideFlags = HideFlags.DontSave;
            runtime.Initialise(_gestureContext, enabled);
            _gestures.Add(runtime);
            _gestureSnapshot = _gestures.ToArray();
            return runtime;
        }

        private void Release()
        {
            IsRunning = false;
            try
            {
                if (_inputEnabled)
                    _input.Disable();
            }
            finally
            {
                _inputEnabled = false;
                _input = null;

                foreach (var gesture in _gestures)
                    DestroyObject(gesture);
                foreach (var gesture in _pendingDestroy)
                    DestroyObject(gesture);
                _gestures.Clear();
                _pendingDestroy.Clear();
                _gestureSnapshot = Array.Empty<GestureBase>();
                _gestureContext = null;

                _history?.Clear();
                _history = null;
                _frame = null;
                _environment?.Dispose();
                _environment = null;
                _host = null;
            }
        }

        private static void DestroyObject(Object obj)
        {
            if (obj == null) return;
            if (Application.isPlaying)
                Object.Destroy(obj);
            else
                Object.DestroyImmediate(obj);
        }

        private void RequireRunning()
        {
            if (!IsRunning)
                throw new InvalidOperationException("TouchSystem is not running. Call Startup() first.");
        }

        private T RequireRunning<T>(T value)
        {
            RequireRunning();
            return value;
        }
    }
}
