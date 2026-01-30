using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using Game.Events;
using Game.GameState;
using Game.Hiding.Events;
using Game.Infrastructure;
using Game.Sound;
using Migs.MLock.Interfaces;
using UnityEngine;
using VContainer;
using VContainer.Unity;

namespace Game.Hiding
{
    public class PlayerHideController : MonoBehaviour
    {
        [SerializeField]
        private LayerMask _hiddenLayer;

        private EventAggregator _eventAggregator;
        private HideConfiguration _configuration;
        private SoundManager _soundManager;
        private GameLockService _lockService;
        private ObjectPool<Transform> _effectPool;

        private bool _isHidden;
        private bool _isOnCooldown;
        private float _cooldownTimer;
        private float _hiddenDurationTimer;
        private bool _hasPlayedHiddenDurationSound;
        private Transform _currentHidingSpot;
        private int _originalLayer;
        private ILock<GameLockTags> _movementLock;
        private readonly List<GameObject> _hiddenChildren = new();

        public bool IsHidden => _isHidden;

        [Inject]
        public void Construct(EventAggregator eventAggregator, HideConfiguration configuration, SoundManager soundManager, GameLockService lockService)
        {
            _eventAggregator = eventAggregator;
            _configuration = configuration;
            _soundManager = soundManager;
            _lockService = lockService;
        }

        private void Start()
        {
            ResolveDependenciesIfNeeded();
            InitializeEffectPool();
            SubscribeToEvents();
        }

        private void ResolveDependenciesIfNeeded()
        {
            if (_eventAggregator != null && _configuration != null && _soundManager != null && _lockService != null)
            {
                return;
            }

            var lifetimeScope = Object.FindAnyObjectByType<LifetimeScope>();

            if (lifetimeScope == null)
            {
                return;
            }

            _eventAggregator ??= lifetimeScope.Container.Resolve<EventAggregator>();
            _configuration ??= lifetimeScope.Container.Resolve<HideConfiguration>();
            _soundManager ??= lifetimeScope.Container.Resolve<SoundManager>();
            _lockService ??= lifetimeScope.Container.Resolve<GameLockService>();
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
            UpdateHiddenDuration();
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

        private void UpdateHiddenDuration()
        {
            if (!_isHidden || _hasPlayedHiddenDurationSound || _configuration == null)
            {
                return;
            }

            _hiddenDurationTimer += Time.deltaTime;

            if (_hiddenDurationTimer >= _configuration.HiddenDurationThreshold)
            {
                _hasPlayedHiddenDurationSound = true;
                PlayHiddenDurationSound();
            }
        }

        private void PlayHiddenDurationSound()
        {
            if (_soundManager == null || _configuration == null)
            {
                return;
            }

            var soundName = _configuration.HiddenDurationSoundName;

            if (string.IsNullOrEmpty(soundName))
            {
                return;
            }

            var position = _currentHidingSpot != null ? _currentHidingSpot.position : transform.position;
            _soundManager.PlayNamedSoundEffect(soundName, position);
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
                Hide(evt.HidingSpot, evt.HidePosition, evt.EnterSoundName);
            }
        }

        private void Hide(Transform hidingSpot, Vector3 hidePosition, string enterSoundName)
        {
            if (_isHidden)
            {
                return;
            }

            _isHidden = true;
            _currentHidingSpot = hidingSpot;
            _hiddenDurationTimer = 0f;
            _hasPlayedHiddenDurationSound = false;

            _movementLock = _lockService?.Lock(GameLockTags.PlayerMovement);

            PlayEnterSound(enterSoundName, hidePosition);

            _originalLayer = gameObject.layer;
            SetLayerRecursively(gameObject, GetLayerFromMask(_hiddenLayer));

            HidePlayerChildren();
            SpawnHideEffect(hidePosition);
            StartCooldown();

            _eventAggregator?.Publish(new PlayerHideStateChangedEvent(true, hidingSpot));
        }

        private void PlayEnterSound(string soundName, Vector3 position)
        {
            if (_soundManager == null || string.IsNullOrEmpty(soundName))
            {
                return;
            }

            _soundManager.PlayNamedSoundEffect(soundName, position);
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

            _movementLock?.Dispose();
            _movementLock = null;

            SetLayerRecursively(gameObject, _originalLayer);
            ShowPlayerChildren();

            if (previousSpot != null)
            {
                SpawnHideEffect(previousSpot.position);
            }

            StartCooldown();

            _eventAggregator?.Publish(new PlayerHideStateChangedEvent(false, previousSpot));
        }

        private void SetLayerRecursively(GameObject obj, int layer)
        {
            obj.layer = layer;

            foreach (Transform child in obj.transform)
            {
                SetLayerRecursively(child.gameObject, layer);
            }
        }

        private int GetLayerFromMask(LayerMask mask)
        {
            var value = mask.value;

            if (value == 0)
            {
                return 0;
            }

            for (int i = 0; i < 32; i++)
            {
                if ((value & (1 << i)) != 0)
                {
                    return i;
                }
            }

            return 0;
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
