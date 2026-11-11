namespace Threads.TouchController
{
    /// <summary>The events every gesture instance can send. See plan.md section 2.</summary>
    public enum GestureEventType
    {
        /// <summary>The input might become this gesture. Always the first event of an instance.</summary>
        Possible = 0,

        /// <summary>The possibility ended without the gesture happening. Only after Possible; never followed by Exit.</summary>
        Fail = 1,

        /// <summary>The gesture was recognised. Only after Possible; followed by exactly one Exit (unless the gesture is disabled first).</summary>
        Enter = 2,

        /// <summary>Still active; sent every update between Enter and Exit (not in the Enter or Exit frame).</summary>
        Stay = 3,

        /// <summary>The gesture is over: <see cref="GestureReason.Completed"/> or <see cref="GestureReason.Cancelled"/>.</summary>
        Exit = 4,
    }

    /// <summary>State of one gesture instance.</summary>
    public enum GestureState
    {
        /// <summary>Waiting to see whether the gesture happens.</summary>
        Possible = 0,

        /// <summary>Recognised and ongoing (between Enter and Exit).</summary>
        Active = 1,

        /// <summary>Failed or exited. Removed from <see cref="IGesture.Instances"/> at the end of the update.</summary>
        Finished = 2,
    }

    /// <summary>Whole-gesture state, above the per-instance state machine.</summary>
    public enum GestureRunState
    {
        /// <summary>Disabled: no instances and no events (except OnEnabled / OnDisabled).</summary>
        Disabled = 0,

        /// <summary>Just enabled: silent until no fingers at all are on the screen (a new touch sequence must start).</summary>
        WaitingForClear = 1,

        /// <summary>Recognising.</summary>
        Running = 2,
    }

    /// <summary>Why a gesture instance failed or exited.</summary>
    public enum GestureReason
    {
        /// <summary>No reason (Possible, Enter and Stay events).</summary>
        None = 0,

        /// <summary>Exit: the gesture finished normally.</summary>
        Completed = 1,

        /// <summary>Fail or Exit: the OS cancelled a touch, or a touch disappeared.</summary>
        Cancelled = 2,

        /// <summary>Fail: a finger lifted too soon.</summary>
        Released = 3,

        /// <summary>Fail: a finger moved beyond the touch slop.</summary>
        Moved = 4,

        /// <summary>Fail: a finger was held too long (e.g. a tap that became a long press).</summary>
        Held = 5,

        /// <summary>Fail: a time limit passed (e.g. no second tap in time).</summary>
        Timeout = 6,

        /// <summary>Fail: something happened sooner than the minimum time.</summary>
        TooFast = 7,

        /// <summary>Fail: touches were too far apart.</summary>
        TooFar = 8,

        /// <summary>Fail: the movement was slower than required (e.g. a swipe).</summary>
        TooSlow = 9,

        /// <summary>Fail: the movement was in the wrong direction.</summary>
        Direction = 10,

        /// <summary>Fail: the wrong number of fingers.</summary>
        FingerCount = 11,
    }

    /// <summary>A screen edge, for edge gestures.</summary>
    public enum ScreenEdge
    {
        None = 0,
        Left = 1,
        Right = 2,
        Bottom = 3,
        Top = 4,
    }
}
