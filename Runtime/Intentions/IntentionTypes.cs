namespace Threads.TouchController.Intentions
{
    /// <summary>The events every intention instance can send. See plan.md §9.2.</summary>
    public enum IntentionEventType
    {
        /// <summary>A bound gesture became Possible on fingers the area accepted. Always the first event of an instance.</summary>
        Possible = 0,

        /// <summary>The instance ended without starting. Only after Possible; never followed by anything.</summary>
        Rejected = 1,

        /// <summary>The intention started: the gesture entered, requirements were met and the claim was granted. Followed by a Performed in the same update, unless a Started handler ends it first (then Ended follows at once).</summary>
        Started = 2,

        /// <summary>Every update while started, carrying the movement since the previous Performed. A discrete gesture sends exactly one.</summary>
        Performed = 3,

        /// <summary>The intention is over. Only after Started (normally after its Performed); exactly once.</summary>
        Ended = 4,
    }

    /// <summary>State of one intention instance.</summary>
    public enum IntentionState
    {
        /// <summary>A candidate: the gesture is Possible.</summary>
        Possible = 0,

        /// <summary>The gesture entered, but a required gesture has not failed yet (<see cref="Intention.RequireFailure{T}"/>).</summary>
        Waiting = 1,

        /// <summary>Started and not yet ended.</summary>
        Active = 2,

        /// <summary>Rejected or ended. Removed from <see cref="Intention.Instances"/> at the end of the update.</summary>
        Finished = 3,
    }

    /// <summary>Why an intention instance was rejected or ended.</summary>
    public enum IntentionReason
    {
        /// <summary>No reason (Possible, Started and Performed).</summary>
        None = 0,

        /// <summary>Ended: the gesture finished normally.</summary>
        Completed = 1,

        /// <summary>Rejected or Ended: the OS cancelled the touch, or the gesture exited with Cancelled.</summary>
        Cancelled = 2,

        /// <summary>Ended: a higher-priority exclusive intention took a finger.</summary>
        Preempted = 3,

        /// <summary>Rejected or Ended: the intention, or its gesture, was disabled (or the system shut down).</summary>
        Disabled = 4,

        /// <summary>Rejected: the bound gesture failed.</summary>
        GestureFailed = 5,

        /// <summary>Rejected: a higher-priority exclusive intention holds, or took, a finger.</summary>
        LostToPriority = 6,

        /// <summary>Rejected: a gesture named in <see cref="Intention.RequireFailure{T}"/> entered instead of failing.</summary>
        RequiredGestureEntered = 7,

        /// <summary>Rejected: a gesture named in <see cref="Intention.RequireActive{T}"/> was not active on the fingers.</summary>
        RequirementNotMet = 8,

        /// <summary>Rejected or Ended: with <see cref="AreaCheck.Continuous"/>, the fingers left the area.</summary>
        LeftArea = 9,

        /// <summary>Rejected: the condition given to <see cref="Intention.StartWhen"/> returned false (or threw) when the gesture entered.</summary>
        StartConditionFailed = 10,
    }

    /// <summary>How an intention shares its fingers with other intentions.</summary>
    public enum ClaimMode
    {
        /// <summary>Default. Never blocks or ends another intention (intentions overlap, like gestures).</summary>
        Shared = 0,

        /// <summary>
        /// When it starts, it takes its fingers: lower-priority instances on them are rejected (if not started)
        /// or ended with <see cref="IntentionReason.Preempted"/>. While active, it blocks intentions of
        /// equal or lower priority from joining or starting on those fingers. Instances of the same intention
        /// never block or preempt each other.
        /// </summary>
        Exclusive = 1,
    }

    /// <summary>When the intention's area is checked.</summary>
    public enum AreaCheck
    {
        /// <summary>Default. Once per finger, at the position where it touched down.</summary>
        AtStart = 0,

        /// <summary>At touch-down, and again with every gesture update; leaving the area ends the instance (<see cref="IntentionReason.LeftArea"/>).</summary>
        Continuous = 1,
    }

    /// <summary>Which fingers of a multi-finger gesture must be in the area.</summary>
    public enum FingerRule
    {
        /// <summary>Default. At least one finger started in the area.</summary>
        Any = 0,

        /// <summary>Every finger started in the area.</summary>
        All = 1,

        /// <summary>The gesture's primary (first) finger started in the area.</summary>
        Primary = 2,
    }

    /// <summary>What drove an intention instance.</summary>
    public enum IntentionSourceKind
    {
        Gesture = 0,
        Manual = 1,
        InputAction = 2,
    }
}
