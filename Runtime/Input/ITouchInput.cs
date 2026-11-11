using UnityEngine;

namespace Threads.TouchController
{
    /// <summary>A source of touches. <see cref="TouchSource"/> reads the Input System; tests and replays supply their own.</summary>
    public interface ITouchInput
    {
        /// <summary>Starts receiving input. Called once by <see cref="TouchSystem.Startup"/>.</summary>
        void Enable();

        /// <summary>Stops receiving input and releases anything acquired in <see cref="Enable"/>. Called by <see cref="TouchSystem.Shutdown"/>.</summary>
        void Disable();

        /// <summary>Clears <paramref name="frame"/> and fills it with the current touches.</summary>
        void Capture(TouchFrame frame, float pixelsPerDp, double time, Vector2 screenSizePx);
    }
}
