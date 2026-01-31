using System.Threading;
using Cysharp.Threading.Tasks;
using Game.Events;
using Game.GameState;
using Game.Scenes.Events;
using Game.Sound;
using Migs.MLock.Interfaces;
using TMPro;
using UnityEngine;
using VContainer;
using VContainer.Unity;

namespace Game.Scenes
{
    public class IntroController : MonoBehaviour
    {
        [SerializeField, Header("UI Text Display")]
        private GameObject _textContainer;

        [SerializeField]
        private TextMeshProUGUI _textDisplay;

        [SerializeField]
        private float _additionalDisplayTime = 1f;

        private EventAggregator _eventAggregator;
        private SoundConfiguration _soundConfiguration;
        private SoundManager _soundManager;
        private GameLockService _lockService;

        private ILock<GameLockTags> _currentLock;
        private CancellationTokenSource _cancellationTokenSource;
        private bool _isPlayingIntro;

        [Inject]
        public void Construct(
            EventAggregator eventAggregator,
            SoundConfiguration soundConfiguration,
            SoundManager soundManager,
            GameLockService lockService)
        {
            _eventAggregator = eventAggregator;
            _soundConfiguration = soundConfiguration;
            _soundManager = soundManager;
            _lockService = lockService;
        }

        private void Start()
        {
            ResolveDependenciesIfNeeded();

            _cancellationTokenSource = new CancellationTokenSource();

            _eventAggregator?.Subscribe<GameSceneReadyEvent>(OnGameSceneReady);
            _eventAggregator?.Subscribe<LoadingStartedEvent>(OnLoadingStarted);

            HideText();
        }

        private void ResolveDependenciesIfNeeded()
        {
            if (_eventAggregator != null && _soundConfiguration != null && _soundManager != null && _lockService != null)
            {
                return;
            }

            var lifetimeScope = FindAnyObjectByType<LifetimeScope>();

            if (lifetimeScope == null)
            {
                return;
            }

            _eventAggregator ??= lifetimeScope.Container.Resolve<EventAggregator>();
            _soundConfiguration ??= lifetimeScope.Container.Resolve<SoundConfiguration>();
            _soundManager ??= lifetimeScope.Container.Resolve<SoundManager>();
            _lockService ??= lifetimeScope.Container.Resolve<GameLockService>();
        }

        private void OnDestroy()
        {
            _eventAggregator?.Unsubscribe<GameSceneReadyEvent>(OnGameSceneReady);
            _eventAggregator?.Unsubscribe<LoadingStartedEvent>(OnLoadingStarted);

            _cancellationTokenSource?.Cancel();
            _cancellationTokenSource?.Dispose();

            _currentLock?.Dispose();
        }

        private void OnGameSceneReady(GameSceneReadyEvent evt)
        {
            PlayIntroAsync().Forget();
        }

        private void OnLoadingStarted(LoadingStartedEvent evt)
        {
            // Cancel any ongoing intro when transitioning
            _cancellationTokenSource?.Cancel();
            HideText();
            _currentLock?.Dispose();
            _currentLock = null;
            _isPlayingIntro = false;
        }

        private async UniTaskVoid PlayIntroAsync()
        {
            var introPhrase = _soundConfiguration?.IntroPhrase;

            if (introPhrase == null || (introPhrase.Clip == null && string.IsNullOrEmpty(introPhrase.Text)))
            {
                return;
            }

            _isPlayingIntro = true;

            // Recreate cancellation token source
            _cancellationTokenSource?.Cancel();
            _cancellationTokenSource?.Dispose();
            _cancellationTokenSource = new CancellationTokenSource();
            var token = _cancellationTokenSource.Token;

            // Lock player input during intro
            _currentLock = _lockService?.Lock(GameLockTags.All);

            // Show text
            ShowText(introPhrase.Text);

            // Play audio
            if (introPhrase.Clip != null)
            {
                _soundManager?.PlayClip2D(introPhrase.Clip);
            }

            // Wait for audio duration + additional time
            var displayTime = introPhrase.Clip != null
                ? introPhrase.Clip.length + _additionalDisplayTime
                : _additionalDisplayTime + 2f;

            try
            {
                await UniTask.Delay(
                    System.TimeSpan.FromSeconds(displayTime),
                    cancellationToken: token);
            }
            catch (System.OperationCanceledException)
            {
                return;
            }

            // Hide text and unlock input
            HideText();

            _currentLock?.Dispose();
            _currentLock = null;
            _isPlayingIntro = false;
        }

        private void ShowText(string text)
        {
            if (_textDisplay != null)
            {
                _textDisplay.text = text;
            }

            if (_textContainer != null)
            {
                _textContainer.SetActive(true);
            }
        }

        private void HideText()
        {
            if (_textContainer != null)
            {
                _textContainer.SetActive(false);
            }
        }
    }
}
