using System;
using Cysharp.Threading.Tasks;
using Game.Events;
using Game.GameState;
using Game.Scenes.Events;
using Migs.MLock.Interfaces;
using UnityEngine;
using UnityEngine.ResourceManagement.ResourceProviders;
using VContainer;
using VContainer.Unity;

namespace Game.Scenes
{
    public class GameSceneManager : MonoBehaviour
    {
        public static GameSceneManager Instance { get; private set; }

        private EventAggregator _eventAggregator;
        private AddressableSceneLoader _sceneLoader;
        private GameLockService _gameLockService;

        private SceneInstance _currentLoadingScene;
        private SceneInstance _currentGameOverScene;
        private SceneInstance _currentWinScene;
        private SceneInstance _currentGameScene;
        private SceneInstance _currentMainMenuScene;

        private bool _isTransitioning;
        private ILock<GameLockTags> _currentLock;

        [Inject]
        public void Construct(
            EventAggregator eventAggregator,
            AddressableSceneLoader sceneLoader,
            GameLockService gameLockService)
        {
            _eventAggregator = eventAggregator;
            _sceneLoader = sceneLoader;
            _gameLockService = gameLockService;
        }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            DontDestroyOnLoad(gameObject);
        }

        private void Start()
        {
            ResolveDependenciesIfNeeded();

            _eventAggregator?.Subscribe<StartGameRequestedEvent>(OnStartGameRequested);
            _eventAggregator?.Subscribe<ReturnToMainMenuRequestedEvent>(OnReturnToMainMenuRequested);
        }

        private void ResolveDependenciesIfNeeded()
        {
            if (_eventAggregator != null && _sceneLoader != null && _gameLockService != null)
            {
                return;
            }

            var lifetimeScope = FindAnyObjectByType<LifetimeScope>();

            if (lifetimeScope == null)
            {
                return;
            }

            _eventAggregator ??= lifetimeScope.Container.Resolve<EventAggregator>();
            _sceneLoader ??= lifetimeScope.Container.Resolve<AddressableSceneLoader>();
            _gameLockService ??= lifetimeScope.Container.Resolve<GameLockService>();
        }

        private void OnDestroy()
        {
            if (Instance == this)
            {
                Instance = null;
            }

            _eventAggregator?.Unsubscribe<StartGameRequestedEvent>(OnStartGameRequested);
            _eventAggregator?.Unsubscribe<ReturnToMainMenuRequestedEvent>(OnReturnToMainMenuRequested);
        }

        private void OnStartGameRequested(StartGameRequestedEvent evt)
        {
            if (_isTransitioning)
            {
                return;
            }

            StartGameAsync().Forget();
        }

        private void OnReturnToMainMenuRequested(ReturnToMainMenuRequestedEvent evt)
        {
            if (_isTransitioning)
            {
                return;
            }

            ReturnToMainMenuAsync().Forget();
        }

        private async UniTaskVoid StartGameAsync()
        {
            _isTransitioning = true;

            try
            {
                _currentLock = _gameLockService?.LockAll();

                _eventAggregator?.Publish(new LoadingStartedEvent(isTransitioningToGame: true));

                await UniTask.Delay(100);

                if (_currentMainMenuScene.Scene.IsValid())
                {
                    await _sceneLoader.UnloadSceneAsync(_currentMainMenuScene);
                    _currentMainMenuScene = default;
                }

                _currentGameScene = await _sceneLoader.LoadRandomGameSceneAsync();

                if (_currentGameScene.Scene.IsValid())
                {
                    _eventAggregator?.Publish(new SceneLoadedEvent(_currentGameScene.Scene.name));
                }

                await UniTask.Delay(100);

                _eventAggregator?.Publish(new LoadingCompletedEvent(isInGame: true));

                await UniTask.Delay(100);

                _currentLock?.Dispose();
                _currentLock = null;

                _eventAggregator?.Publish(new GameSceneReadyEvent());
            }
            catch (Exception ex)
            {
                Debug.LogError($"Failed to start game: {ex.Message}");
                _currentLock?.Dispose();
                _currentLock = null;
            }
            finally
            {
                _isTransitioning = false;
            }
        }

        private async UniTaskVoid ReturnToMainMenuAsync()
        {
            _isTransitioning = true;

            try
            {
                _currentLock = _gameLockService?.LockAll();

                _eventAggregator?.Publish(new LoadingStartedEvent(isTransitioningToGame: false));

                await UniTask.Delay(100);

                if (_currentGameScene.Scene.IsValid())
                {
                    await _sceneLoader.UnloadSceneAsync(_currentGameScene);
                    _currentGameScene = default;
                }

                _currentMainMenuScene = await _sceneLoader.LoadMainMenuSceneAsync();

                if (_currentMainMenuScene.Scene.IsValid())
                {
                    _eventAggregator?.Publish(new SceneLoadedEvent(_currentMainMenuScene.Scene.name));
                }

                await UniTask.Delay(100);

                _eventAggregator?.Publish(new LoadingCompletedEvent(isInGame: false));

                _currentLock?.Dispose();
                _currentLock = null;

                _eventAggregator?.Publish(new MainMenuReadyEvent());
            }
            catch (Exception ex)
            {
                Debug.LogError($"Failed to return to main menu: {ex.Message}");
                _currentLock?.Dispose();
                _currentLock = null;
            }
            finally
            {
                _isTransitioning = false;
            }
        }

        public void RegisterMainMenuScene(SceneInstance mainMenuScene)
        {
            _currentMainMenuScene = mainMenuScene;
        }

        public void RegisterLoadingScene(SceneInstance loadingScene)
        {
            _currentLoadingScene = loadingScene;
        }

        public void RegisterGameOverScene(SceneInstance gameOverScene)
        {
            _currentGameOverScene = gameOverScene;
        }

        public void RegisterWinScene(SceneInstance winScene)
        {
            _currentWinScene = winScene;
        }

        public void RegisterGameScene(SceneInstance gameScene)
        {
            _currentGameScene = gameScene;
        }
    }
}
