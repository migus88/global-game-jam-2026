using Game.Events;
using Game.GameState.Events;
using UnityEngine;
using VContainer;
using VContainer.Unity;

namespace Game.GameState
{
    public class GameOverUI : MonoBehaviour
    {
        [SerializeField]
        private GameObject _container;

        private EventAggregator _eventAggregator;

        [Inject]
        public void Construct(EventAggregator eventAggregator)
        {
            _eventAggregator = eventAggregator;
        }

        private void Start()
        {
            ResolveDependenciesIfNeeded();

            _container?.SetActive(false);

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
            _eventAggregator?.Unsubscribe<GameOverEvent>(OnGameOver);
        }

        private void OnGameOver(GameOverEvent evt)
        {
            _container?.SetActive(true);
        }
    }
}
