using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using Game.Events;
using Game.GameState;
using Game.Scenes;
using Game.Scenes.Events;
using Game.Sound;
using Migs.MLock.Interfaces;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.UI;
using VContainer;
using VContainer.Unity;

namespace Game.WinCondition
{
    [Serializable]
    public class WinImageEntry
    {
        [SerializeField]
        private Image _image;

        [SerializeField, Range(0f, 10f)]
        private float _delay;

        [SerializeField, Range(0.1f, 5f)]
        private float _fadeDuration = 1f;

        public Image Image => _image;
        public float Delay => _delay;
        public float FadeDuration => _fadeDuration;
    }

    public class WinController : MonoBehaviour
    {
        [SerializeField]
        private GameObject _container;

        [SerializeField]
        private GameObject _hintText;

        [SerializeField]
        private List<WinImageEntry> _images = new();

        [SerializeField]
        private AudioSource _audioSource;

        [SerializeField, Range(0.5f, 10f)]
        private float _hintDelay = 3f;

        private EventAggregator _eventAggregator;
        private GameLockService _lockService;
        private SoundManager _soundManager;

        private bool _isWin;
        private bool _canReturnToMenu;
        private CancellationTokenSource _animationCts;
        private ILock<GameLockTags> _inputLock;

        [Inject]
        public void Construct(EventAggregator eventAggregator, GameLockService lockService, SoundManager soundManager)
        {
            _eventAggregator = eventAggregator;
            _lockService = lockService;
            _soundManager = soundManager;
        }

        private void Start()
        {
            ResolveDependenciesIfNeeded();

            Hide();

            _eventAggregator?.Subscribe<PlayerWonEvent>(OnPlayerWon);
            _eventAggregator?.Subscribe<LoadingStartedEvent>(OnLoadingStarted);
        }

        private void OnEnable()
        {
            InputSystem.onEvent += OnInputEvent;
        }

        private void OnDisable()
        {
            InputSystem.onEvent -= OnInputEvent;
        }

        private void ResolveDependenciesIfNeeded()
        {
            if (_eventAggregator != null && _lockService != null && _soundManager != null)
            {
                return;
            }

            var lifetimeScope = FindAnyObjectByType<LifetimeScope>();

            if (lifetimeScope == null)
            {
                return;
            }

            _eventAggregator ??= lifetimeScope.Container.Resolve<EventAggregator>();
            _lockService ??= lifetimeScope.Container.Resolve<GameLockService>();
            _soundManager ??= lifetimeScope.Container.Resolve<SoundManager>();
        }

        private void OnInputEvent(InputEventPtr eventPtr, InputDevice device)
        {
            if (!_canReturnToMenu)
            {
                return;
            }

            if (!eventPtr.IsA<StateEvent>() && !eventPtr.IsA<DeltaStateEvent>())
            {
                return;
            }

            if (eventPtr.HasButtonPress())
            {
                _canReturnToMenu = false;
                _isWin = false;
                _eventAggregator?.Publish(new ReturnToMainMenuRequestedEvent());
            }
        }

        private void OnDestroy()
        {
            _animationCts?.Cancel();
            _animationCts?.Dispose();
            _eventAggregator?.Unsubscribe<PlayerWonEvent>(OnPlayerWon);
            _eventAggregator?.Unsubscribe<LoadingStartedEvent>(OnLoadingStarted);
        }

        private void OnLoadingStarted(LoadingStartedEvent evt)
        {
            Hide();
        }

        private void OnPlayerWon(PlayerWonEvent evt)
        {
            _isWin = true;
            _canReturnToMenu = false;

            _inputLock = _lockService?.LockAll();

            ShowAnimatedAsync().Forget();
        }

        private async UniTaskVoid ShowAnimatedAsync()
        {
            _animationCts?.Cancel();
            _animationCts?.Dispose();
            _animationCts = new CancellationTokenSource();

            var token = _animationCts.Token;

            SetAllImagesAlpha(0f);

            _container?.SetActive(true);
            _hintText?.SetActive(false);
            _soundManager?.StopAllSounds();
            _audioSource?.Play();

            foreach (var entry in _images)
            {
                if (entry.Image == null)
                {
                    continue;
                }

                try
                {
                    await FadeInImageAsync(entry, token);
                }
                catch (OperationCanceledException)
                {
                    return;
                }

                if (!_isWin || token.IsCancellationRequested)
                {
                    return;
                }
            }

            await ShowHintAfterDelayAsync(token);
        }

        private async UniTask FadeInImageAsync(WinImageEntry entry, CancellationToken token)
        {
            if (entry.Delay > 0f)
            {
                await UniTask.Delay((int)(entry.Delay * 1000), cancellationToken: token);
            }

            if (token.IsCancellationRequested)
            {
                return;
            }

            var image = entry.Image;
            var startColor = image.color;
            startColor.a = 0f;
            image.color = startColor;

            var elapsed = 0f;
            while (elapsed < entry.FadeDuration)
            {
                if (token.IsCancellationRequested)
                {
                    return;
                }

                elapsed += Time.unscaledDeltaTime;
                var t = Mathf.Clamp01(elapsed / entry.FadeDuration);

                var color = image.color;
                color.a = t;
                image.color = color;

                await UniTask.Yield(PlayerLoopTiming.Update, token);
            }

            var finalColor = image.color;
            finalColor.a = 1f;
            image.color = finalColor;
        }

        private async UniTask ShowHintAfterDelayAsync(CancellationToken token)
        {
            try
            {
                await UniTask.Delay((int)(_hintDelay * 1000), cancellationToken: token);
            }
            catch (OperationCanceledException)
            {
                return;
            }

            if (!_isWin || token.IsCancellationRequested)
            {
                return;
            }

            _hintText?.SetActive(true);
            _canReturnToMenu = true;
        }

        private void SetAllImagesAlpha(float alpha)
        {
            foreach (var entry in _images)
            {
                if (entry.Image != null)
                {
                    var color = entry.Image.color;
                    color.a = alpha;
                    entry.Image.color = color;
                }
            }
        }

        public void Hide()
        {
            _animationCts?.Cancel();
            _isWin = false;
            _canReturnToMenu = false;
            _container?.SetActive(false);
            _hintText?.SetActive(false);
            _audioSource?.Stop();
            SetAllImagesAlpha(0f);

            _inputLock?.Dispose();
            _inputLock = null;
        }
    }
}
