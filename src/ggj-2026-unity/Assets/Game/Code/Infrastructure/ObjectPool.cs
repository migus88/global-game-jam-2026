using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Game.Infrastructure
{
    public class ObjectPool<T> : IDisposable where T : Component
    {
        private readonly T _prefab;
        private readonly Transform _parent;
        private readonly Queue<T> _available;
        private readonly List<T> _all;
        private readonly int _initialSize;
        private bool _isDisposed;

        public ObjectPool(T prefab, int initialSize = 5, Transform parent = null)
        {
            _prefab = prefab;
            _initialSize = initialSize;
            _parent = parent;
            _available = new Queue<T>(initialSize);
            _all = new List<T>(initialSize);

            if (_prefab != null)
            {
                WarmUp(initialSize);
            }
        }

        private void WarmUp(int count)
        {
            for (int i = 0; i < count; i++)
            {
                var instance = CreateInstance();
                instance.gameObject.SetActive(false);
                _available.Enqueue(instance);
            }
        }

        private T CreateInstance()
        {
            var instance = _parent != null
                ? Object.Instantiate(_prefab, _parent)
                : Object.Instantiate(_prefab);

            _all.Add(instance);
            return instance;
        }

        public T Get(Vector3 position, Quaternion rotation)
        {
            if (_isDisposed || _prefab == null)
            {
                return null;
            }

            T instance;

            if (_available.Count > 0)
            {
                instance = _available.Dequeue();
            }
            else
            {
                instance = CreateInstance();
            }

            var transform = instance.transform;
            transform.position = position;
            transform.rotation = rotation;
            instance.gameObject.SetActive(true);

            return instance;
        }

        public T Get(Vector3 position)
        {
            return Get(position, Quaternion.identity);
        }

        public void Return(T instance)
        {
            if (_isDisposed || instance == null)
            {
                return;
            }

            instance.gameObject.SetActive(false);

            if (!_available.Contains(instance))
            {
                _available.Enqueue(instance);
            }
        }

        public async UniTask ReturnAfterDelay(T instance, float delay)
        {
            if (_isDisposed || instance == null)
            {
                return;
            }

            await UniTask.Delay(TimeSpan.FromSeconds(delay));

            if (!_isDisposed && instance != null)
            {
                Return(instance);
            }
        }

        public void Clear()
        {
            foreach (var instance in _all)
            {
                if (instance != null)
                {
                    Object.Destroy(instance.gameObject);
                }
            }

            _available.Clear();
            _all.Clear();
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
