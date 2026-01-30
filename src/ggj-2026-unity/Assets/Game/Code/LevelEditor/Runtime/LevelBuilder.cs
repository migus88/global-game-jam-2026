using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using Game.LevelEditor.Data;
using UnityEngine;
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

        [Inject]
        public LevelBuilder(IObjectResolver resolver, LevelConfiguration config)
        {
            _resolver = resolver;
            _config = config;
        }

        public async UniTask BuildLevelAsync(LevelData levelData)
        {
            ClearLevel();
            CreateContainers();

            await SpawnWallsAsync(levelData);
            await SpawnEnemiesAsync(levelData);
            SpawnPlayer(levelData);
        }

        public void ClearLevel()
        {
            if (_levelRoot != null)
            {
                Object.Destroy(_levelRoot.gameObject);
            }

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
            int batchSize = 50;
            int count = 0;

            foreach (var wallPos in levelData.WallPositions)
            {
                SpawnWall(levelData, wallPos);
                count++;

                if (count % batchSize == 0)
                {
                    await UniTask.Yield();
                }
            }
        }

        private void SpawnWall(LevelData levelData, Vector2Int gridPos)
        {
            var wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
            wall.name = $"Wall_{gridPos.x}_{gridPos.y}";
            wall.layer = ObstacleLayer;

            Vector3 worldPos = levelData.GridToWorld(gridPos);
            worldPos.y = _config.WallSize.y * 0.5f;
            wall.transform.position = worldPos;
            wall.transform.localScale = _config.WallSize;
            wall.transform.SetParent(_wallsContainer);

            if (_config.WallMaterial != null)
            {
                wall.GetComponent<MeshRenderer>().sharedMaterial = _config.WallMaterial;
            }

            _wallInstances.Add(wall);
        }

        private async UniTask SpawnEnemiesAsync(LevelData levelData)
        {
            foreach (var enemyData in levelData.EnemySpawns)
            {
                SpawnEnemy(levelData, enemyData);
                await UniTask.Yield();
            }
        }

        private void SpawnEnemy(LevelData levelData, EnemySpawnData enemyData)
        {
            var prefab = _config.GetEnemyPrefab(enemyData.EnemyId);
            if (prefab == null)
            {
                Debug.LogWarning($"Cannot spawn enemy: prefab not found for ID '{enemyData.EnemyId}'");
                return;
            }

            Vector3 worldPos = levelData.GridToWorld(enemyData.SpawnPosition);
            Quaternion rotation = Quaternion.Euler(0f, enemyData.InitialRotation, 0f);

            var enemy = _resolver.Instantiate(prefab, worldPos, rotation, _enemiesContainer);
            enemy.name = $"Enemy_{enemyData.EnemyId}_{enemyData.SpawnPosition.x}_{enemyData.SpawnPosition.y}";

            // Setup patrol controller
            var patrolController = enemy.GetComponent<EnemyPatrolController>();
            if (patrolController == null)
            {
                patrolController = enemy.AddComponent<EnemyPatrolController>();
            }

            patrolController.Initialize(enemyData.PatrolPath, levelData);

            if (enemyData.PatrolPath.Count > 0)
            {
                patrolController.StartPatrol();
            }

            _enemyInstances.Add(enemy);
        }

        private void SpawnPlayer(LevelData levelData)
        {
            if (!levelData.HasPlayerSpawn)
            {
                Debug.LogWarning("No player spawn position defined in level data");
                return;
            }

            if (_config.PlayerPrefab == null)
            {
                Debug.LogWarning("No player prefab assigned in level configuration");
                return;
            }

            Vector3 worldPos = levelData.GridToWorld(levelData.PlayerSpawnPosition);
            _playerInstance = _resolver.Instantiate(_config.PlayerPrefab, worldPos, Quaternion.identity, _levelRoot);
            _playerInstance.name = "Player";
        }
    }
}
