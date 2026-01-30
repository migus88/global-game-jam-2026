using Game.Events;
using Game.GameState.Events;
using UnityEngine;
using UnityEngine.InputSystem;
using VContainer;
using VContainer.Unity;

namespace Game.GameState
{
    public class PausableInputHandler : MonoBehaviour
    {
        [SerializeField]
        private PlayerInput _playerInput;

        private EventAggregator _eventAggregator;

        [Inject]
        public void Construct(EventAggregator eventAggregator)
        {
            _eventAggregator = eventAggregator;
        }

        private void Start()
        {
            ResolveDependenciesIfNeeded();

            if (_playerInput == null)
            {
                _playerInput = GetComponent<PlayerInput>();
            }

            _eventAggregator?.Subscribe<GamePausedEvent>(OnGamePaused);
            _eventAggregator?.Subscribe<GameResumedEvent>(OnGameResumed);
        }

        private void ResolveDependenciesIfNeeded()
        {
            if (_eventAggregator != null)
            {
                return;
            }

            var lifetimeScope = FindAnyObjectByType<LifetimeScope>();

            if (lifetimeScope == null)
            {
                return;
            }

            _eventAggregator ??= lifetimeScope.Container.Resolve<EventAggregator>();
        }

        private void OnDestroy()
        {
            _eventAggregator?.Unsubscribe<GamePausedEvent>(OnGamePaused);
            _eventAggregator?.Unsubscribe<GameResumedEvent>(OnGameResumed);
        }

        private void OnGamePaused(GamePausedEvent evt)
        {
            if (_playerInput != null)
            {
                _playerInput.DeactivateInput();
            }
        }

        private void OnGameResumed(GameResumedEvent evt)
        {
            if (_playerInput != null)
            {
                _playerInput.ActivateInput();
            }
        }
    }
}
