using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;
using UnityEngine.ResourceManagement.ResourceLocations;
using UnityEngine.ResourceManagement.ResourceProviders;
using UnityEngine.SceneManagement;

namespace Game.Scenes
{
    public class AddressableSceneLoader : IDisposable
    {
        private readonly SceneConfiguration _configuration;
        private readonly List<AsyncOperationHandle<SceneInstance>> _loadedSceneHandles = new();
        private readonly System.Random _random = new();

        private bool _isDisposed;

        public AddressableSceneLoader(SceneConfiguration configuration)
        {
            _configuration = configuration;
        }

        public async UniTask<SceneInstance> LoadMainMenuSceneAsync()
        {
            return await LoadSceneAsync(_configuration.MainMenuScene);
        }

        public async UniTask<SceneInstance> LoadLoadingSceneAsync()
        {
            return await LoadSceneAsync(_configuration.LoadingScene);
        }

        public async UniTask<SceneInstance> LoadRandomGameSceneAsync()
        {
            var locations = await LoadSceneLocationsAsync(_configuration.GameSceneLabel);

            if (locations == null || locations.Count == 0)
            {
                Debug.LogError($"No scenes found with label '{_configuration.GameSceneLabel}'");
                return default;
            }

            var randomIndex = _random.Next(locations.Count);
            var selectedLocation = locations[randomIndex];

            Debug.Log($"Loading random game scene: {selectedLocation.PrimaryKey}");

            return await LoadSceneByLocationAsync(selectedLocation);
        }

        public async UniTask<SceneInstance> LoadGameSceneByNameAsync(string sceneName)
        {
            var locations = await LoadSceneLocationsAsync(_configuration.GameSceneLabel);

            if (locations == null || locations.Count == 0)
            {
                Debug.LogError($"No scenes found with label '{_configuration.GameSceneLabel}'");
                return default;
            }

            foreach (var location in locations)
            {
                if (location.PrimaryKey.Contains(sceneName))
                {
                    Debug.Log($"Loading game scene by name: {location.PrimaryKey}");
                    return await LoadSceneByLocationAsync(location);
                }
            }

            Debug.LogError($"Scene '{sceneName}' not found in game scenes");
            return default;
        }

        public async UniTask UnloadAllScenesExceptAsync(string sceneNameToKeep)
        {
            var handlesToRemove = new List<AsyncOperationHandle<SceneInstance>>();

            foreach (var handle in _loadedSceneHandles)
            {
                if (!handle.IsValid())
                {
                    handlesToRemove.Add(handle);
                    continue;
                }

                var sceneInstance = handle.Result;
                if (sceneInstance.Scene.name == sceneNameToKeep)
                {
                    continue;
                }

                try
                {
                    await Addressables.UnloadSceneAsync(handle).ToUniTask();
                    handlesToRemove.Add(handle);
                }
                catch (Exception ex)
                {
                    Debug.LogError($"Failed to unload scene: {ex.Message}");
                }
            }

            foreach (var handle in handlesToRemove)
            {
                _loadedSceneHandles.Remove(handle);
            }
        }

        public async UniTask UnloadSceneAsync(SceneInstance sceneInstance)
        {
            if (!sceneInstance.Scene.IsValid())
            {
                return;
            }

            var handleToRemove = default(AsyncOperationHandle<SceneInstance>);

            foreach (var handle in _loadedSceneHandles)
            {
                if (handle.IsValid() && handle.Result.Scene == sceneInstance.Scene)
                {
                    handleToRemove = handle;
                    break;
                }
            }

            if (handleToRemove.IsValid())
            {
                await Addressables.UnloadSceneAsync(handleToRemove).ToUniTask();
                _loadedSceneHandles.Remove(handleToRemove);
            }
        }

        public async UniTask UnloadAllLoadedScenesAsync()
        {
            var tasks = new List<UniTask>();

            foreach (var handle in _loadedSceneHandles)
            {
                if (handle.IsValid())
                {
                    tasks.Add(Addressables.UnloadSceneAsync(handle).ToUniTask());
                }
            }

            await UniTask.WhenAll(tasks);
            _loadedSceneHandles.Clear();
        }

        private async UniTask<SceneInstance> LoadSceneAsync(AssetReference sceneReference)
        {
            if (sceneReference == null || !sceneReference.RuntimeKeyIsValid())
            {
                Debug.LogError("Invalid scene reference");
                return default;
            }

            var handle = Addressables.LoadSceneAsync(sceneReference, LoadSceneMode.Additive);
            await handle.ToUniTask();

            if (handle.Status == AsyncOperationStatus.Succeeded)
            {
                _loadedSceneHandles.Add(handle);
                return handle.Result;
            }

            Debug.LogError($"Failed to load scene: {sceneReference.RuntimeKey}");
            return default;
        }

        private async UniTask<IList<IResourceLocation>> LoadSceneLocationsAsync(string label)
        {
            var handle = Addressables.LoadResourceLocationsAsync(label, typeof(SceneInstance));
            await handle.ToUniTask();

            if (handle.Status == AsyncOperationStatus.Succeeded)
            {
                return handle.Result;
            }

            Debug.LogError($"Failed to load scene locations for label: {label}");
            return null;
        }

        private async UniTask<SceneInstance> LoadSceneByLocationAsync(IResourceLocation location)
        {
            var handle = Addressables.LoadSceneAsync(location, LoadSceneMode.Additive);
            await handle.ToUniTask();

            if (handle.Status == AsyncOperationStatus.Succeeded)
            {
                _loadedSceneHandles.Add(handle);
                return handle.Result;
            }

            Debug.LogError($"Failed to load scene at location: {location.PrimaryKey}");
            return default;
        }

        public void Dispose()
        {
            if (_isDisposed)
            {
                return;
            }

            _isDisposed = true;

            foreach (var handle in _loadedSceneHandles)
            {
                if (handle.IsValid())
                {
                    Addressables.UnloadSceneAsync(handle);
                }
            }

            _loadedSceneHandles.Clear();
        }
    }
}
