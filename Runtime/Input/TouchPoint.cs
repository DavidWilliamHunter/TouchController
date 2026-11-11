using System;
using UnityEngine;

namespace Threads.TouchController
{
    /// <summary>Lifecycle phase of one contact in a <see cref="TouchFrame"/>.</summary>
    public enum TouchPointPhase
    {
        /// <summary>The contact touched down this frame.</summary>
        Began = 0,

        /// <summary>The contact moved this frame.</summary>
        Moved = 1,

        /// <summary>The contact is down but did not move this frame.</summary>
        Stationary = 2,

        /// <summary>The contact lifted this frame. This is its last frame.</summary>
        Ended = 3,

        /// <summary>The OS cancelled the contact this frame (e.g. a system gesture took it). This is its last frame.</summary>
        Canceled = 4,
    }

    /// <summary>
    /// One contact in a <see cref="TouchFrame"/>. Positions are in the platform's density-independent
    /// unit (dp / pt / epx); use <see cref="TouchFrame.DpToPx(Vector2)"/> for pixels.
    /// </summary>
    [Serializable]
    public struct TouchPoint
    {
        /// <summary>Identifies this contact for its whole life. Never reused within a session.</summary>
        public int touchId;

        public TouchPointPhase phase;

        /// <summary>Current position (dp, screen space, origin bottom-left).</summary>
        public Vector2 positionDp;

        /// <summary>Where the contact touched down (dp).</summary>
        public Vector2 startPositionDp;

        /// <summary>When the contact touched down (seconds, same clock as <see cref="TouchFrame.Time"/>).</summary>
        public double startTime;

        /// <summary>
        /// The Input System's own ID for this touch (<c>TouchControl.touchId</c>), for use with Unity APIs,
        /// e.g. <c>EventSystem.current.IsPointerOverGameObject(inputTouchId)</c> with the
        /// InputSystemUIInputModule. Only unique per device and may be reused; use <see cref="touchId"/>
        /// to track contacts. 0 if the touch did not come from the Input System (scripted or recorded input).
        /// </summary>
        public int inputTouchId;

        /// <summary>
        /// <c>InputDevice.deviceId</c> of the touchscreen the touch came from (look it up with
        /// <c>InputSystem.GetDeviceById</c>). 0 (<c>InputDevice.InvalidDeviceId</c>) if not from the Input System.
        /// </summary>
        public int inputDeviceId;

        public TouchPoint(int touchId, TouchPointPhase phase, Vector2 positionDp, Vector2 startPositionDp, double startTime,
            int inputTouchId = 0, int inputDeviceId = 0)
        {
            this.touchId = touchId;
            this.phase = phase;
            this.positionDp = positionDp;
            this.startPositionDp = startPositionDp;
            this.startTime = startTime;
            this.inputTouchId = inputTouchId;
            this.inputDeviceId = inputDeviceId;
        }

        /// <summary>True while the finger is on the screen (Began, Moved or Stationary).</summary>
        public bool IsInContact => phase == TouchPointPhase.Began || phase == TouchPointPhase.Moved || phase == TouchPointPhase.Stationary;

        /// <summary>True on the contact's last frame (Ended or Canceled).</summary>
        public bool HasEnded => phase == TouchPointPhase.Ended || phase == TouchPointPhase.Canceled;

        public override string ToString() => $"Touch {touchId} {phase} @ {positionDp} dp";
    }
}
