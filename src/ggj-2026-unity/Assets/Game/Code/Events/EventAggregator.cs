using System;
using System.Collections.Generic;

namespace Game.Events
{
    public class EventAggregator : IDisposable
    {
        private readonly Dictionary<Type, List<Delegate>> _subscriptions = new();
        private readonly object _lock = new();
        private bool _isDisposed;

        public void Subscribe<T>(Action<T> handler) where T : IEvent
        {
            if (_isDisposed || handler == null)
            {
                return;
            }

            var eventType = typeof(T);

            lock (_lock)
            {
                if (!_subscriptions.TryGetValue(eventType, out var handlers))
                {
                    handlers = new List<Delegate>();
                    _subscriptions[eventType] = handlers;
                }

                if (!handlers.Contains(handler))
                {
                    handlers.Add(handler);
                }
            }
        }

        public void Unsubscribe<T>(Action<T> handler) where T : IEvent
        {
            if (_isDisposed || handler == null)
            {
                return;
            }

            var eventType = typeof(T);

            lock (_lock)
            {
                if (_subscriptions.TryGetValue(eventType, out var handlers))
                {
                    handlers.Remove(handler);
                }
            }
        }

        public void Publish<T>(T eventData) where T : IEvent
        {
            if (_isDisposed)
            {
                return;
            }

            var eventType = typeof(T);
            List<Delegate> handlersCopy;

            lock (_lock)
            {
                if (!_subscriptions.TryGetValue(eventType, out var handlers) || handlers.Count == 0)
                {
                    return;
                }

                handlersCopy = new List<Delegate>(handlers);
            }

            foreach (var handler in handlersCopy)
            {
                try
                {
                    ((Action<T>)handler)?.Invoke(eventData);
                }
                catch (Exception ex)
                {
                    UnityEngine.Debug.LogException(ex);
                }
            }
        }

        public void Clear()
        {
            lock (_lock)
            {
                _subscriptions.Clear();
            }
        }

        public void Dispose()
        {
            if (_isDisposed)
            {
                return;
            }

            _isDisposed = true;
            Clear();
        }
    }
}
