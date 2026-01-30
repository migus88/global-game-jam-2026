using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using Game.Events;
using Game.Hiding.Events;
using Game.Infrastructure;
using UnityEngine;
using VContainer;

namespace Game.Hiding
{
    public class PlayerHideController : MonoBehaviour
    {
        private EventAggregator _eventAggregator;
        private HideConfiguration _configuration;
        private ObjectPool<Transform> _effectPool;

        private bool _isHidden;
        private bool _isOnCooldown;
        private float _cooldownTimer;
        private Transform _currentHidingSpot;
        private readonly List<GameObject> _hiddenChildren = new();

        public bool IsHidden => _isHidden;

        [Inject]
        public void Construct(EventAggregator eventAggregator, HideConfiguration configuration)
        {
            _eventAggregator = eventAggregator;
            _configuration = configuration;

            InitializeEffectPool();
            SubscribeToEvents();
        }

        private void InitializeEffectPool()
        {
            if (_configuration?.HideEffectPrefab == null)
            {
                return;
            }

            var poolContainer = new GameObject("[HideEffectPool]");
            poolContainer.transform.SetParent(null);
            Object.DontDestroyOnLoad(poolContainer);

            _effectPool = new ObjectPool<Transform>(
                _configuration.HideEffectPrefab.transform,
                _configuration.EffectPoolSize,
                poolContainer.transform);
        }

        private void SubscribeToEvents()
        {
            _eventAggregator?.Subscribe<HideActionRequestedEvent>(OnHideActionRequested);
        }

        private void OnDestroy()
        {
            _eventAggregator?.Unsubscribe<HideActionRequestedEvent>(OnHideActionRequested);
            _effectPool?.Dispose();
        }

        private void Update()
        {
            UpdateCooldown();
        }

        private void UpdateCooldown()
        {
            if (!_isOnCooldown)
            {
                return;
            }

            _cooldownTimer -= Time.deltaTime;

            if (_cooldownTimer <= 0f)
            {
                _isOnCooldown = false;
            }
        }

        private void OnHideActionRequested(HideActionRequestedEvent evt)
        {
            if (_isOnCooldown)
            {
                return;
            }

            if (_isHidden)
            {
                Unhide();
            }
            else
            {
                Hide(evt.HidingSpot, evt.HidePosition);
            }
        }

        private void Hide(Transform hidingSpot, Vector3 hidePosition)
        {
            if (_isHidden)
            {
                return;
            }

            _isHidden = true;
            _currentHidingSpot = hidingSpot;

            HidePlayerChildren();
            SpawnHideEffect(hidePosition);
            StartCooldown();

            _eventAggregator?.Publish(new PlayerHideStateChangedEvent(true, hidingSpot));
        }

        private void Unhide()
        {
            if (!_isHidden)
            {
                return;
            }

            var previousSpot = _currentHidingSpot;

            _isHidden = false;
            _currentHidingSpot = null;

            ShowPlayerChildren();

            if (previousSpot != null)
            {
                SpawnHideEffect(previousSpot.position);
            }

            StartCooldown();

            _eventAggregator?.Publish(new PlayerHideStateChangedEvent(false, previousSpot));
        }

        private void HidePlayerChildren()
        {
            _hiddenChildren.Clear();

            foreach (Transform child in transform)
            {
                if (child.gameObject.activeSelf)
                {
                    _hiddenChildren.Add(child.gameObject);
                    child.gameObject.SetActive(false);
                }
            }
        }

        private void ShowPlayerChildren()
        {
            foreach (var child in _hiddenChildren)
            {
                if (child != null)
                {
                    child.SetActive(true);
                }
            }

            _hiddenChildren.Clear();
        }

        private void SpawnHideEffect(Vector3 position)
        {
            if (_effectPool == null || _configuration == null)
            {
                return;
            }

            var effect = _effectPool.Get(position);

            if (effect != null)
            {
                _effectPool.ReturnAfterDelay(effect, _configuration.EffectDuration).Forget();
            }
        }

        private void StartCooldown()
        {
            if (_configuration == null)
            {
                return;
            }

            _isOnCooldown = true;
            _cooldownTimer = _configuration.HideCooldown;
        }
    }
}
