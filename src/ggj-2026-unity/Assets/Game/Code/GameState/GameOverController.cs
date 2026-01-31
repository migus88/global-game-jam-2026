using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using Game.Events;
using Game.GameState.Events;
using Game.Scenes;
using Game.Scenes.Events;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.UI;
using VContainer;
using VContainer.Unity;

namespace Game.GameState
{
    [Serializable]
    public class GameOverImageEntry
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

    public class GameOverController : MonoBehaviour
    {
        [SerializeField]
        private GameObject _container;

        [SerializeField]
        private GameObject _hintText;

        [SerializeField]
        private List<GameOverImageEntry> _images = new();

        private EventAggregator _eventAggregator;
        private SceneConfiguration _sceneConfiguration;

        private bool _isGameOver;
        private bool _canReturnToMenu;
        private CancellationTokenSource _animationCts;

        [Inject]
        public void Construct(EventAggregator eventAggregator, SceneConfiguration sceneConfiguration)
        {
            _eventAggregator = eventAggregator;
            _sceneConfiguration = sceneConfiguration;
        }

        private void Start()
        {
            ResolveDependenciesIfNeeded();

            Hide();

            _eventAggregator?.Subscribe<GameOverEvent>(OnGameOver);
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
            if (_eventAggregator != null && _sceneConfiguration != null)
            {
                return;
            }

            var lifetimeScope = FindAnyObjectByType<LifetimeScope>();

            if (lifetimeScope == null)
            {
                return;
            }

            _eventAggregator ??= lifetimeScope.Container.Resolve<EventAggregator>();
            _sceneConfiguration ??= lifetimeScope.Container.Resolve<SceneConfiguration>();
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
                _isGameOver = false;
                _eventAggregator?.Publish(new ReturnToMainMenuRequestedEvent());
            }
        }

        private void OnDestroy()
        {
            _animationCts?.Cancel();
            _animationCts?.Dispose();
            _eventAggregator?.Unsubscribe<GameOverEvent>(OnGameOver);
            _eventAggregator?.Unsubscribe<LoadingStartedEvent>(OnLoadingStarted);
        }

        private void OnLoadingStarted(LoadingStartedEvent evt)
        {
            Hide();
        }

        private void OnGameOver(GameOverEvent evt)
        {
            _isGameOver = true;
            _canReturnToMenu = false;

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

            var fadeTasks = new List<UniTask>();
            foreach (var entry in _images)
            {
                if (entry.Image != null)
                {
                    fadeTasks.Add(FadeInImageAsync(entry, token));
                }
            }

            try
            {
                await UniTask.WhenAll(fadeTasks);
            }
            catch (OperationCanceledException)
            {
                return;
            }

            if (!_isGameOver || token.IsCancellationRequested)
            {
                return;
            }

            await ShowHintAfterDelayAsync(token);
        }

        private async UniTask FadeInImageAsync(GameOverImageEntry entry, CancellationToken token)
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
            var delay = _sceneConfiguration != null ? _sceneConfiguration.GameOverHintDelay : 3f;

            try
            {
                await UniTask.Delay((int)(delay * 1000), cancellationToken: token);
            }
            catch (OperationCanceledException)
            {
                return;
            }

            if (!_isGameOver || token.IsCancellationRequested)
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
            _isGameOver = false;
            _canReturnToMenu = false;
            _container?.SetActive(false);
            _hintText?.SetActive(false);
            SetAllImagesAlpha(0f);
        }
    }
}
