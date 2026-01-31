using Game.Events;
using Game.Scenes.Events;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using VContainer;
using VContainer.Unity;

namespace Game.Scenes
{
    public class MainMenuController : MonoBehaviour
    {
        [SerializeField]
        private GameObject _menuContainer;

        [SerializeField]
        private Selectable _firstSelected;

        private EventAggregator _eventAggregator;

        [Inject]
        public void Construct(EventAggregator eventAggregator)
        {
            _eventAggregator = eventAggregator;
        }

        private void Start()
        {
            ResolveDependenciesIfNeeded();

            ShowMenu();
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

        public void OnPlayButtonClicked()
        {
            _eventAggregator?.Publish(new StartGameRequestedEvent());
        }

        public void OnQuitButtonClicked()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }

        public void ShowMenu()
        {
            _menuContainer?.SetActive(true);
            SelectFirstButton();
        }

        private void SelectFirstButton()
        {
            if (_firstSelected == null)
            {
                return;
            }

            EventSystem.current?.SetSelectedGameObject(_firstSelected.gameObject);
        }

        public void HideMenu()
        {
            _menuContainer?.SetActive(false);
        }
    }
}
