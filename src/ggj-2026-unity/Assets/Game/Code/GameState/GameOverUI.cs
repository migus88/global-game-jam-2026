using Cysharp.Threading.Tasks;
using Game.Events;
using Game.GameState.Events;
using Game.Scenes;
using Game.Scenes.Events;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using VContainer;
using VContainer.Unity;

namespace Game.GameState
{
    public class GameOverUI : MonoBehaviour
    {
        [SerializeField]
        private GameObject _container;

        [SerializeField]
        private GameObject _hintText;

        private EventAggregator _eventAggregator;
        private SceneConfiguration _sceneConfiguration;

        private bool _isGameOver;
        private bool _canReturnToMenu;

        [Inject]
        public void Construct(EventAggregator eventAggregator, SceneConfiguration sceneConfiguration)
        {
            _eventAggregator = eventAggregator;
            _sceneConfiguration = sceneConfiguration;
        }

        private void Start()
        {
            ResolveDependenciesIfNeeded();

            _container?.SetActive(false);
            _hintText?.SetActive(false);

            _eventAggregator?.Subscribe<GameOverEvent>(OnGameOver);
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
            _eventAggregator?.Unsubscribe<GameOverEvent>(OnGameOver);
        }

        private void OnGameOver(GameOverEvent evt)
        {
            _isGameOver = true;
            _canReturnToMenu = false;
            _container?.SetActive(true);
            _hintText?.SetActive(false);

            ShowHintAfterDelayAsync().Forget();
        }

        private async UniTaskVoid ShowHintAfterDelayAsync()
        {
            var delay = _sceneConfiguration != null ? _sceneConfiguration.GameOverHintDelay : 3f;
            await UniTask.Delay((int)(delay * 1000), cancellationToken: this.GetCancellationTokenOnDestroy());

            if (!_isGameOver)
            {
                return;
            }

            _hintText?.SetActive(true);
            _canReturnToMenu = true;
        }

        public void Hide()
        {
            _isGameOver = false;
            _canReturnToMenu = false;
            _container?.SetActive(false);
            _hintText?.SetActive(false);
        }
    }
}
