using Cysharp.Threading.Tasks;
using Game.Events;
using Game.Scenes.Events;
using UnityEngine;
using VContainer;
using VContainer.Unity;

namespace Game.Scenes
{
    public class GameBootstrap : MonoBehaviour
    {
        public static bool IsInitialized { get; private set; }

        private EventAggregator _eventAggregator;
        private AddressableSceneLoader _sceneLoader;

        [Inject]
        public void Construct(EventAggregator eventAggregator, AddressableSceneLoader sceneLoader)
        {
            _eventAggregator = eventAggregator;
            _sceneLoader = sceneLoader;
        }

        private void Start()
        {
            ResolveDependenciesIfNeeded();
            EnsureGameSceneManagerExists();
            InitializeGameAsync().Forget();
        }

        private void ResolveDependenciesIfNeeded()
        {
            if (_eventAggregator != null && _sceneLoader != null)
            {
                return;
            }

            var lifetimeScope = FindAnyObjectByType<LifetimeScope>();

            if (lifetimeScope == null)
            {
                Debug.LogError("GameBootstrap: No LifetimeScope found");
                return;
            }

            _eventAggregator ??= lifetimeScope.Container.Resolve<EventAggregator>();
            _sceneLoader ??= lifetimeScope.Container.Resolve<AddressableSceneLoader>();
        }

        private void EnsureGameSceneManagerExists()
        {
            if (GameSceneManager.Instance != null)
            {
                return;
            }

            var go = new GameObject("GameSceneManager");
            go.AddComponent<GameSceneManager>();
        }

        private async UniTaskVoid InitializeGameAsync()
        {
            await UniTask.Delay(100);

            var loadingScene = await _sceneLoader.LoadLoadingSceneAsync();

            if (!loadingScene.Scene.IsValid())
            {
                Debug.LogError("GameBootstrap: Failed to load loading scene");
                return;
            }

            GameSceneManager.Instance?.RegisterLoadingScene(loadingScene);

            var mainMenuScene = await _sceneLoader.LoadMainMenuSceneAsync();

            if (mainMenuScene.Scene.IsValid())
            {
                GameSceneManager.Instance?.RegisterMainMenuScene(mainMenuScene);
                _eventAggregator?.Publish(new SceneLoadedEvent(mainMenuScene.Scene.name));
                _eventAggregator?.Publish(new MainMenuReadyEvent());
            }

            IsInitialized = true;
            Debug.Log("GameBootstrap: Game initialized successfully");
        }
    }
}
