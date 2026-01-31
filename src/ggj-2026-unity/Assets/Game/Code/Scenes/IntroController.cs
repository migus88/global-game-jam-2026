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
        [SerializeField, Header("Text Display")]
        private GameObject _textDisplayPrefab;

        [SerializeField]
        private Vector3 _textOffset = new(0f, 2f, 2f);

        [SerializeField]
        private float _additionalDisplayTime = 1f;

        private EventAggregator _eventAggregator;
        private SoundConfiguration _soundConfiguration;
        private SoundManager _soundManager;
        private GameLockService _lockService;

        private GameObject _currentTextDisplay;
        private TextMeshPro _currentTextMesh;
        private UnityEngine.Camera _mainCamera;
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

            _mainCamera = UnityEngine.Camera.main;
            _cancellationTokenSource = new CancellationTokenSource();

            _eventAggregator?.Subscribe<GameSceneReadyEvent>(OnGameSceneReady);
            _eventAggregator?.Subscribe<LoadingStartedEvent>(OnLoadingStarted);

            if (_textDisplayPrefab == null)
            {
                CreateDefaultTextDisplay();
            }
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

            if (_currentTextDisplay != null && _textDisplayPrefab == null)
            {
                Destroy(_currentTextDisplay);
            }
        }

        private void LateUpdate()
        {
            UpdateBillboard();
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

            // Find player and position text near them
            var player = FindPlayerTransform();

            if (player != null)
            {
                PositionTextNearPlayer(player);
            }

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

        private Transform FindPlayerTransform()
        {
            // Find player by layer (layer 3 is typically player)
            var players = FindObjectsByType<CharacterController>(FindObjectsSortMode.None);

            foreach (var player in players)
            {
                if (player.gameObject.layer == 3)
                {
                    return player.transform;
                }
            }

            // Fallback: find by tag
            var playerObject = GameObject.FindWithTag("Player");

            if (playerObject != null)
            {
                return playerObject.transform;
            }

            return null;
        }

        private void PositionTextNearPlayer(Transform player)
        {
            if (_currentTextDisplay == null)
            {
                return;
            }

            // Position text in front of the player
            var position = player.position + player.forward * _textOffset.z + Vector3.up * _textOffset.y;
            _currentTextDisplay.transform.position = position;
        }

        private void CreateDefaultTextDisplay()
        {
            _currentTextDisplay = new GameObject("IntroText");
            _currentTextDisplay.transform.SetParent(transform);

            _currentTextMesh = _currentTextDisplay.AddComponent<TextMeshPro>();
            _currentTextMesh.alignment = TextAlignmentOptions.Center;
            _currentTextMesh.fontSize = 4f;
            _currentTextMesh.color = Color.white;
            _currentTextMesh.enableWordWrapping = true;
            _currentTextMesh.rectTransform.sizeDelta = new Vector2(8f, 3f);

            _currentTextDisplay.SetActive(false);
        }

        private void ShowText(string text)
        {
            if (_textDisplayPrefab != null && _currentTextDisplay == null)
            {
                _currentTextDisplay = Instantiate(_textDisplayPrefab, transform);
                _currentTextMesh = _currentTextDisplay.GetComponent<TextMeshPro>();
            }

            if (_currentTextDisplay == null)
            {
                return;
            }

            if (_currentTextMesh != null)
            {
                _currentTextMesh.text = text;
            }

            _currentTextDisplay.SetActive(true);
        }

        private void HideText()
        {
            if (_currentTextDisplay != null)
            {
                _currentTextDisplay.SetActive(false);
            }
        }

        private void UpdateBillboard()
        {
            if (_currentTextDisplay == null || !_currentTextDisplay.activeSelf || _mainCamera == null)
            {
                return;
            }

            var directionToCamera = _mainCamera.transform.position - _currentTextDisplay.transform.position;
            directionToCamera.y = 0f;

            if (directionToCamera.sqrMagnitude > 0.001f)
            {
                _currentTextDisplay.transform.rotation = Quaternion.LookRotation(-directionToCamera);
            }
        }
    }
}
