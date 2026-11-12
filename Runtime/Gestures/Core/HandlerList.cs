using System;
using UnityEngine;

namespace Threads.TouchController
{
    /// <summary>
    /// Subscribers to one event. Each handler is called on its own, so one that throws is logged and the
    /// others still run (a multicast delegate would stop at the first exception). The array is copied on
    /// subscribe/unsubscribe (rare) and never changed in place, so sending allocates nothing and handlers
    /// may subscribe or unsubscribe while it is sent.
    /// </summary>
    internal sealed class HandlerList<T>
    {
        private Action<T>[] _handlers = Array.Empty<Action<T>>();

        public void Add(Action<T> handler)
        {
            if (handler == null) return;
            var old = _handlers;
            var updated = new Action<T>[old.Length + 1];
            old.CopyTo(updated, 0);
            updated[old.Length] = handler;
            _handlers = updated;
        }

        public void Remove(Action<T> handler)
        {
            if (handler == null) return;
            var old = _handlers;
            var index = Array.LastIndexOf(old, handler); // like delegate removal: the last matching subscription
            if (index < 0) return;
            var updated = new Action<T>[old.Length - 1];
            Array.Copy(old, 0, updated, 0, index);
            Array.Copy(old, index + 1, updated, index, old.Length - index - 1);
            _handlers = updated;
        }

        public bool IsEmpty => _handlers.Length == 0;

        /// <param name="keepSending">Asked before each handler; when it returns false the remaining handlers are skipped
        /// (e.g. a handler disabled the gesture: nothing more is sent).</param>
        public void Invoke(T value, UnityEngine.Object context = null, Func<bool> keepSending = null)
        {
            foreach (var handler in _handlers)
            {
                if (keepSending != null && !keepSending()) return;
                try
                {
                    handler(value);
                }
                catch (Exception e)
                {
                    // A failing consumer must not break recognition, or the other consumers.
                    Debug.LogException(e, context);
                }
            }
        }
    }
}
