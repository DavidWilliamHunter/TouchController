namespace Threads.TouchController
{
    /// <summary>
    /// Shared base for one-finger gestures: one instance per finger, begun when the finger touches down.
    /// Subclasses decide what happens to each instance every update in <see cref="OnTouch"/>.
    /// </summary>
    public abstract class SingleFingerGesture<TInstance> : GestureBase<TInstance> where TInstance : GestureInstance, new()
    {
        /// <summary>Whether a finger that just touched down starts an instance (default: always).</summary>
        protected virtual bool ShouldBegin(TouchPoint touch, TouchFrame frame) => true;

        /// <summary>Sets instance values before its Possible event (e.g. which screen edge).</summary>
        protected virtual void InitialiseInstance(TInstance instance, TouchPoint touch, TouchFrame frame)
        {
        }

        /// <summary>Called right after the instance's Possible event.</summary>
        protected virtual void OnBegan(TInstance instance, TouchPoint touch)
        {
        }

        /// <summary>Called every update for each unfinished instance, with its finger's touch (including the update it began).</summary>
        protected abstract void OnTouch(TInstance instance, TouchPoint touch, TouchFrame frame);

        /// <summary>True if the finger has moved further than the touch slop from where the instance began.</summary>
        protected bool MovedBeyondSlop(GestureInstance instance, TouchPoint touch) =>
            (touch.positionDp - instance.StartPositionDp).magnitude > Settings.Motion.touchSlopDp;

        protected sealed override void OnFrame(TouchFrame frame)
        {
            foreach (var touch in EligibleTouches)
            {
                if (touch.phase != TouchPointPhase.Began || !ShouldBegin(touch, frame))
                    continue;
                var began = touch;
                var instance = BeginPossible(touch.touchId, i => InitialiseInstance(i, began, frame));
                OnBegan(instance, touch);
            }

            foreach (var instance in LiveInstances())
            {
                if (instance.State == GestureState.Finished)
                    continue; // finished by an earlier OnTouch / OnBegan this update
                if (frame.TryGet(instance.PrimaryFingerId, out var touch))
                    OnTouch(instance, touch, frame);
            }
        }
    }

    /// <summary>Per-instance data for <see cref="MultiFingerGesture{TInstance}"/>.</summary>
    public class MultiFingerInstance : GestureInstance
    {
        /// <summary>The fingers in contact at the previous update, to detect fingers joining or leaving.</summary>
        internal readonly System.Collections.Generic.List<int> PreviousContact = new System.Collections.Generic.List<int>();
    }

    /// <summary>
    /// Shared base for gestures over all fingers (pinch, rotate, two-finger pan): one instance at a time,
    /// Possible once two or more fingers are down. Fingers that touch down later join it. When fewer than
    /// two fingers remain, a Possible instance fails (<see cref="GestureReason.FingerCount"/>) and an
    /// Active one exits (<see cref="GestureReason.Completed"/>).
    /// </summary>
    public abstract class MultiFingerGesture<TInstance> : GestureBase<TInstance> where TInstance : MultiFingerInstance, new()
    {
        private readonly System.Collections.Generic.List<int> _eligibleContact = new System.Collections.Generic.List<int>();
        private readonly System.Collections.Generic.List<int> _instanceContact = new System.Collections.Generic.List<int>();

        /// <summary>Sets instance values before its Possible event. <paramref name="contact"/>: the fingers (in contact).</summary>
        protected virtual void InitialiseInstance(TInstance instance, System.Collections.Generic.IReadOnlyList<int> contact, TouchFrame frame)
        {
        }

        /// <summary>
        /// Called every update while two or more of the instance's fingers are in contact.
        /// <paramref name="fingersChanged"/> is true when a finger joined or left since the last update.
        /// </summary>
        protected abstract void OnUpdate(TInstance instance, System.Collections.Generic.IReadOnlyList<int> contact, TouchFrame frame, bool fingersChanged);

        /// <summary>Centroid of the given fingers (dp).</summary>
        protected static UnityEngine.Vector2 Centroid(TouchFrame frame, System.Collections.Generic.IReadOnlyList<int> fingers)
        {
            var sum = UnityEngine.Vector2.zero;
            var count = 0;
            foreach (var id in fingers)
            {
                if (!frame.TryGet(id, out var touch)) continue;
                sum += touch.positionDp;
                count++;
            }
            return count > 0 ? sum / count : sum;
        }

        protected sealed override void OnFrame(TouchFrame frame)
        {
            _eligibleContact.Clear();
            foreach (var touch in EligibleTouches)
                if (touch.IsInContact)
                    _eligibleContact.Add(touch.touchId);

            var live = LiveInstances();
            var instance = live.Count > 0 ? live[0] : null;

            if (instance == null)
            {
                if (_eligibleContact.Count >= 2)
                {
                    instance = BeginPossible(_eligibleContact, i => InitialiseInstance(i, _eligibleContact, frame));
                    instance.PreviousContact.Clear();
                    instance.PreviousContact.AddRange(_eligibleContact);
                }
                return;
            }

            foreach (var id in _eligibleContact)
                if (!Contains(instance.FingerIds, id))
                    AddFinger(instance, id);

            _instanceContact.Clear();
            foreach (var id in instance.FingerIds)
                if (frame.TryGet(id, out var touch) && touch.IsInContact)
                    _instanceContact.Add(id);

            if (_instanceContact.Count < 2)
            {
                if (instance.State == GestureState.Possible)
                    FailInstance(instance, GestureReason.FingerCount);
                else
                    ExitInstance(instance, GestureReason.Completed);
                return;
            }

            var changed = !SameFingers(instance.PreviousContact, _instanceContact);
            instance.PreviousContact.Clear();
            instance.PreviousContact.AddRange(_instanceContact);
            OnUpdate(instance, _instanceContact, frame, changed);
        }

        private static bool Contains(System.Collections.Generic.IReadOnlyList<int> list, int value)
        {
            for (var i = 0; i < list.Count; i++)
                if (list[i] == value) return true;
            return false;
        }

        private static bool SameFingers(System.Collections.Generic.List<int> a, System.Collections.Generic.List<int> b)
        {
            if (a.Count != b.Count) return false;
            for (var i = 0; i < a.Count; i++)
                if (a[i] != b[i]) return false;
            return true;
        }
    }
}
