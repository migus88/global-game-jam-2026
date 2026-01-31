using Game.Events;
using Game.Scenes.Events;
using UnityEngine;
using VContainer;
using VContainer.Unity;

namespace Game.Scenes
{
    public class LoadingController : MonoBehaviour
    {
        [SerializeField]
        private GameObject _loadingContainer;

        private EventAggregator _eventAggregator;

        [Inject]
        public void Construct(EventAggregator eventAggregator)
        {
            _eventAggregator = eventAggregator;
        }

        private void Start()
        {
            ResolveDependenciesIfNeeded();

            HideLoading();

            _eventAggregator?.Subscribe<LoadingStartedEvent>(OnLoadingStarted);
            _eventAggregator?.Subscribe<LoadingCompletedEvent>(OnLoadingCompleted);
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
            _eventAggregator?.Unsubscribe<LoadingStartedEvent>(OnLoadingStarted);
            _eventAggregator?.Unsubscribe<LoadingCompletedEvent>(OnLoadingCompleted);
        }

        private void OnLoadingStarted(LoadingStartedEvent evt)
        {
            ShowLoading();
        }

        private void OnLoadingCompleted(LoadingCompletedEvent evt)
        {
            HideLoading();
        }

        public void ShowLoading()
        {
            _loadingContainer?.SetActive(true);
        }

        public void HideLoading()
        {
            _loadingContainer?.SetActive(false);
        }
    }
}
