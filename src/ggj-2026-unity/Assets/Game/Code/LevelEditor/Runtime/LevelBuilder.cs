using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using Game.Camera;
using Game.LevelEditor.Data;
using Game.Player;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;
using VContainer;
using VContainer.Unity;

namespace Game.LevelEditor.Runtime
{
    public class LevelBuilder
    {
        private const int ObstacleLayer = 6;

        private readonly IObjectResolver _resolver;
        private readonly LevelConfiguration _config;

        private Transform _levelRoot;
        private Transform _wallsContainer;
        private Transform _enemiesContainer;
        private GameObject _playerInstance;
        private readonly List<GameObject> _wallInstances = new();
        private readonly List<GameObject> _enemyInstances = new();
        private readonly List<AsyncOperationHandle<GameObject>> _loadHandles = new();

        [Inject]
        public LevelBuilder(IObjectResolver resolver, LevelConfiguration config = null)
        {
            _resolver = resolver;
            _config = config;

            if (_config == null)
            {
                Debug.LogWarning("LevelBuilder: No LevelConfiguration provided. Walls and player spawning will be disabled.");
            }
        }

        public async UniTask BuildLevelAsync(LevelData levelData)
        {
            ClearLevel();
            CreateContainers();

            await SpawnWallsAsync(levelData);
            await SpawnEnemiesAsync(levelData);
            await SpawnPlayerAsync(levelData);
        }

        public void ClearLevel()
        {
            if (_levelRoot != null)
            {
                Object.Destroy(_levelRoot.gameObject);
            }

            // Release all addressable handles
            foreach (var handle in _loadHandles)
            {
                if (handle.IsValid())
                {
                    Addressables.Release(handle);
                }
            }

            _loadHandles.Clear();
            _wallInstances.Clear();
            _enemyInstances.Clear();
            _playerInstance = null;
        }

        private void CreateContainers()
        {
            _levelRoot = new GameObject("Level").transform;
            _wallsContainer = new GameObject("Walls").transform;
            _wallsContainer.SetParent(_levelRoot);
            _enemiesContainer = new GameObject("Enemies").transform;
            _enemiesContainer.SetParent(_levelRoot);
        }

        private async UniTask SpawnWallsAsync(LevelData levelData)
        {
            if (_config == null)
            {
                return;
            }

            if (_config.WallPrefab == null || !_config.WallPrefab.RuntimeKeyIsValid())
            {
                Debug.LogWarning("No wall prefab assigned in level configuration");
                return;
            }

            // Load wall prefab once
            var handle = _config.WallPrefab.LoadAssetAsync<GameObject>();
            await handle.ToUniTask();

            if (handle.Status != AsyncOperationStatus.Succeeded)
            {
                Debug.LogError("Failed to load wall prefab");
                return;
            }

            _loadHandles.Add(handle);
            var wallPrefab = handle.Result;

            int batchSize = 50;
            int count = 0;

            foreach (var wallPos in levelData.WallPositions)
            {
                SpawnWall(levelData, wallPos, wallPrefab);
                count++;

                if (count % batchSize == 0)
                {
                    await UniTask.Yield();
                }
            }
        }

        private void SpawnWall(LevelData levelData, Vector2Int gridPos, GameObject wallPrefab)
        {
            Vector3 worldPos = levelData.GridToWorld(gridPos);
            worldPos.y = _config.WallSize.y * 0.5f;

            var wall = _resolver.Instantiate(wallPrefab, worldPos, Quaternion.identity, _wallsContainer);
            wall.name = $"Wall_{gridPos.x}_{gridPos.y}";
            wall.layer = ObstacleLayer;
            wall.transform.localScale = _config.WallSize;

            _wallInstances.Add(wall);
        }

        private async UniTask SpawnEnemiesAsync(LevelData levelData)
        {
            foreach (var enemyData in levelData.EnemySpawns)
            {
                await SpawnEnemyAsync(levelData, enemyData);
            }
        }

        private async UniTask SpawnEnemyAsync(LevelData levelData, EnemySpawnData enemyData)
        {
            if (enemyData.EnemyPrefab == null || !enemyData.EnemyPrefab.RuntimeKeyIsValid())
            {
                Debug.LogWarning($"Enemy spawn at {enemyData.SpawnPosition} has no prefab assigned");
                return;
            }

            var handle = enemyData.EnemyPrefab.LoadAssetAsync<GameObject>();
            await handle.ToUniTask();

            if (handle.Status != AsyncOperationStatus.Succeeded)
            {
                Debug.LogError($"Failed to load enemy prefab at {enemyData.SpawnPosition}");
                return;
            }

            _loadHandles.Add(handle);
            var prefab = handle.Result;

            Vector3 worldPos = levelData.GridToWorld(enemyData.SpawnPosition);
            Quaternion rotation = Quaternion.Euler(0f, enemyData.InitialRotation, 0f);

            var enemy = _resolver.Instantiate(prefab, worldPos, rotation, _enemiesContainer);
            enemy.name = $"Enemy_{enemyData.SpawnPosition.x}_{enemyData.SpawnPosition.y}";

            // Setup patrol controller
            var patrolController = enemy.GetComponent<EnemyPatrolController>();
            if (patrolController == null)
            {
                patrolController = enemy.AddComponent<EnemyPatrolController>();
            }

            patrolController.Initialize(enemyData.PatrolPath, levelData, worldPos);
            patrolController.StartPatrol();

            _enemyInstances.Add(enemy);
        }

        private async UniTask SpawnPlayerAsync(LevelData levelData)
        {
            if (!levelData.HasPlayerSpawn)
            {
                Debug.LogWarning("No player spawn position defined in level data");
                return;
            }

            if (_config == null)
            {
                return;
            }

            if (_config.PlayerPrefab == null || !_config.PlayerPrefab.RuntimeKeyIsValid())
            {
                Debug.LogWarning("No player prefab assigned in level configuration");
                return;
            }

            var handle = _config.PlayerPrefab.LoadAssetAsync<GameObject>();
            await handle.ToUniTask();

            if (handle.Status != AsyncOperationStatus.Succeeded)
            {
                Debug.LogError("Failed to load player prefab");
                return;
            }

            _loadHandles.Add(handle);
            var prefab = handle.Result;

            Vector3 worldPos = levelData.GridToWorld(levelData.PlayerSpawnPosition);
            _playerInstance = _resolver.Instantiate(prefab, worldPos, Quaternion.identity, _levelRoot);
            _playerInstance.name = "Player";

            // Connect camera to player
            ConnectCameraToPlayer();
        }

        private void ConnectCameraToPlayer()
        {
            if (_playerInstance == null)
            {
                return;
            }

            var playerCameraTarget = _playerInstance.GetComponent<PlayerCameraTarget>();
            if (playerCameraTarget == null)
            {
                Debug.LogWarning("Player does not have PlayerCameraTarget component");
                return;
            }

            var cameraConnector = Object.FindFirstObjectByType<CameraTargetConnector>();
            if (cameraConnector != null)
            {
                cameraConnector.SetTarget(playerCameraTarget.CameraTarget);
            }
            else
            {
                Debug.LogWarning("No CameraTargetConnector found in scene");
            }
        }
    }
}
