using System;
using System.Collections.Generic;
using UnityEngine;

namespace Ronin7.Core
{
    /// <summary>
    /// Lightweight, type-keyed publish/subscribe bus so gameplay systems
    /// (Combat, UI, Audio, Enemies) can talk without hard references to one another.
    /// Events are plain structs/classes; subscribe to a type, publish an instance.
    /// </summary>
    public static class EventBus
    {
        private static readonly Dictionary<Type, Delegate> Handlers = new();

        public static void Subscribe<T>(Action<T> handler)
        {
            if (handler == null) return;
            Handlers.TryGetValue(typeof(T), out var existing);
            Handlers[typeof(T)] = (existing as Action<T>) + handler;
        }

        public static void Unsubscribe<T>(Action<T> handler)
        {
            if (handler == null) return;
            if (!Handlers.TryGetValue(typeof(T), out var existing)) return;
            var updated = (existing as Action<T>) - handler;
            if (updated == null) Handlers.Remove(typeof(T));
            else Handlers[typeof(T)] = updated;
        }

        public static void Publish<T>(T evt)
        {
            if (Handlers.TryGetValue(typeof(T), out var existing))
                (existing as Action<T>)?.Invoke(evt);
        }

        /// <summary>Clears all subscriptions. Call on hard scene/game resets to avoid leaks.</summary>
        public static void Clear() => Handlers.Clear();

        // Editor sessions with "Enter Play Mode (no domain reload)" keep static state across Play
        // cycles, which would leak handlers from a previous run into the next. This resets the bus
        // at the very start of every Play session in both Editor and player builds.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetOnDomainReload() => Handlers.Clear();
    }
}
