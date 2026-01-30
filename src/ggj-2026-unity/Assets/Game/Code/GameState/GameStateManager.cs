using Game.Events;
using Game.GameState.Events;
using UnityEngine;
using VContainer;
using VContainer.Unity;

namespace Game.GameState
{
    public class GameStateManager : MonoBehaviour
    {
        public static GameStateManager Instance { get; private set; }

        private EventAggregator _eventAggregator;

        private bool _isPaused;
        private PauseReason _currentPauseReason;

        public bool IsPaused => _isPaused;
        public PauseReason CurrentPauseReason => _currentPauseReason;

        [Inject]
        public void Construct(EventAggregator eventAggregator)
        {
            _eventAggregator = eventAggregator;
        }

        private void Awake()
        {
            Instance = this;
        }

        private void Start()
        {
            ResolveDependenciesIfNeeded();

            _eventAggregator?.Subscribe<GamePausedEvent>(OnGamePaused);
            _eventAggregator?.Subscribe<GameResumedEvent>(OnGameResumed);
            _eventAggregator?.Subscribe<GameOverEvent>(OnGameOver);
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
            if (Instance == this)
            {
                Instance = null;
            }

            _eventAggregator?.Unsubscribe<GamePausedEvent>(OnGamePaused);
            _eventAggregator?.Unsubscribe<GameResumedEvent>(OnGameResumed);
            _eventAggregator?.Unsubscribe<GameOverEvent>(OnGameOver);
        }

        private void OnGamePaused(GamePausedEvent evt)
        {
            _isPaused = true;
            _currentPauseReason = evt.Reason;
        }

        private void OnGameResumed(GameResumedEvent evt)
        {
            _isPaused = false;
            _currentPauseReason = PauseReason.None;
        }

        private void OnGameOver(GameOverEvent evt)
        {
            _isPaused = true;
            _currentPauseReason = PauseReason.GameOver;
        }
    }
}
