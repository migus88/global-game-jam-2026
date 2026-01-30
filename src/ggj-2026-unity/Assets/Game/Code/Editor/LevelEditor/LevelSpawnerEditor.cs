using System.Collections.Generic;
using Game.AI;
using Game.LevelEditor.Data;
using Game.LevelEditor.Runtime;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEngine;
using UnityEngine.AI;

namespace Game.Editor.LevelEditor
{
    [CustomEditor(typeof(LevelSpawner))]
    public class LevelSpawnerEditor : UnityEditor.Editor
    {
        private const int ObstacleLayer = 6;

        private LevelSpawner _spawner;

        private void OnEnable()
        {
            _spawner = (LevelSpawner)target;
        }

        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();

            EditorGUILayout.Space(10);
            EditorGUILayout.LabelField("Level Spawning", EditorStyles.boldLabel);

            bool hasLevelData = _spawner.LevelData != null;
            bool hasConfig = _spawner.Config != null;

            if (!hasLevelData)
            {
                EditorGUILayout.HelpBox("Assign a LevelData asset to spawn the level.", MessageType.Warning);
            }

            if (!hasConfig)
            {
                EditorGUILayout.HelpBox("Assign a LevelConfiguration asset for prefab references.", MessageType.Warning);
            }

            EditorGUI.BeginDisabledGroup(!hasLevelData || !hasConfig);

            EditorGUILayout.BeginHorizontal();

            if (GUILayout.Button("Spawn Level", GUILayout.Height(30)))
            {
                SpawnLevel();
            }

            if (GUILayout.Button("Clear Level", GUILayout.Height(30)))
            {
                ClearLevel();
            }

            EditorGUILayout.EndHorizontal();

            EditorGUI.EndDisabledGroup();

            EditorGUILayout.Space(5);

            EditorGUILayout.BeginHorizontal();

            if (GUILayout.Button("Spawn Walls Only"))
            {
                SpawnWalls();
            }

            if (GUILayout.Button("Spawn Enemies Only"))
            {
                SpawnEnemies();
            }

            if (GUILayout.Button("Spawn Player Only"))
            {
                SpawnPlayer();
            }

            EditorGUILayout.EndHorizontal();

            // Show spawned content info
            EditorGUILayout.Space(10);
            EditorGUILayout.LabelField("Spawned Content", EditorStyles.boldLabel);

            EditorGUI.BeginDisabledGroup(true);
            EditorGUILayout.ObjectField("Walls Container", _spawner.WallsContainer, typeof(Transform), true);
            EditorGUILayout.ObjectField("Enemies Container", _spawner.EnemiesContainer, typeof(Transform), true);
            EditorGUILayout.ObjectField("Player", _spawner.PlayerInstance, typeof(GameObject), true);
            EditorGUI.EndDisabledGroup();
        }

        private void SpawnLevel()
        {
            ClearLevel();

            var levelData = _spawner.LevelData;
            var config = _spawner.Config;

            // Create containers
            var wallsContainer = new GameObject("Walls").transform;
            wallsContainer.SetParent(_spawner.transform);

            var enemiesContainer = new GameObject("Enemies").transform;
            enemiesContainer.SetParent(_spawner.transform);

            // Spawn walls
            SpawnWallsInternal(levelData, config, wallsContainer);

            // Spawn enemies
            SpawnEnemiesInternal(levelData, config, enemiesContainer);

            // Spawn player
            GameObject player = SpawnPlayerInternal(levelData, config);

            // Store references
            _spawner.SetContainers(wallsContainer, enemiesContainer, player);

            Undo.RegisterCreatedObjectUndo(wallsContainer.gameObject, "Spawn Level");
            Undo.RegisterCreatedObjectUndo(enemiesContainer.gameObject, "Spawn Level");
            if (player != null)
            {
                Undo.RegisterCreatedObjectUndo(player, "Spawn Level");
            }

            EditorUtility.SetDirty(_spawner);
            Debug.Log($"[LevelSpawner] Spawned level with {levelData.WallPositions.Count} walls, " +
                      $"{levelData.EnemySpawns.Count} enemies, player: {player != null}");
        }

        private void ClearLevel()
        {
            if (_spawner.WallsContainer != null)
            {
                Undo.DestroyObjectImmediate(_spawner.WallsContainer.gameObject);
            }

            if (_spawner.EnemiesContainer != null)
            {
                Undo.DestroyObjectImmediate(_spawner.EnemiesContainer.gameObject);
            }

            if (_spawner.PlayerInstance != null)
            {
                Undo.DestroyObjectImmediate(_spawner.PlayerInstance);
            }

            _spawner.ClearContainerReferences();
        }

        private void SpawnWalls()
        {
            if (_spawner.WallsContainer != null)
            {
                Undo.DestroyObjectImmediate(_spawner.WallsContainer.gameObject);
            }

            var wallsContainer = new GameObject("Walls").transform;
            wallsContainer.SetParent(_spawner.transform);

            SpawnWallsInternal(_spawner.LevelData, _spawner.Config, wallsContainer);

            _spawner.SetContainers(wallsContainer, _spawner.EnemiesContainer, _spawner.PlayerInstance);
            Undo.RegisterCreatedObjectUndo(wallsContainer.gameObject, "Spawn Walls");
        }

        private void SpawnEnemies()
        {
            if (_spawner.EnemiesContainer != null)
            {
                Undo.DestroyObjectImmediate(_spawner.EnemiesContainer.gameObject);
            }

            var enemiesContainer = new GameObject("Enemies").transform;
            enemiesContainer.SetParent(_spawner.transform);

            SpawnEnemiesInternal(_spawner.LevelData, _spawner.Config, enemiesContainer);

            _spawner.SetContainers(_spawner.WallsContainer, enemiesContainer, _spawner.PlayerInstance);
            Undo.RegisterCreatedObjectUndo(enemiesContainer.gameObject, "Spawn Enemies");
        }

        private void SpawnPlayer()
        {
            if (_spawner.PlayerInstance != null)
            {
                Undo.DestroyObjectImmediate(_spawner.PlayerInstance);
            }

            GameObject player = SpawnPlayerInternal(_spawner.LevelData, _spawner.Config);
            _spawner.SetContainers(_spawner.WallsContainer, _spawner.EnemiesContainer, player);

            if (player != null)
            {
                Undo.RegisterCreatedObjectUndo(player, "Spawn Player");
            }
        }

        private void SpawnWallsInternal(LevelData levelData, LevelConfiguration config, Transform container)
        {
            if (config.WallPrefab == null || !config.WallPrefab.RuntimeKeyIsValid())
            {
                Debug.LogWarning("[LevelSpawner] No wall prefab assigned");
                return;
            }

            // Load the prefab from addressables in editor
            var wallPrefab = LoadAddressablePrefab(config.WallPrefab);
            if (wallPrefab == null)
            {
                Debug.LogError("[LevelSpawner] Failed to load wall prefab");
                return;
            }

            foreach (var wallPos in levelData.WallPositions)
            {
                Vector3 worldPos = levelData.GridToWorld(wallPos);
                worldPos.y = config.WallSize.y * 0.5f;

                var wall = (GameObject)PrefabUtility.InstantiatePrefab(wallPrefab, container);
                wall.transform.position = worldPos;
                wall.transform.rotation = Quaternion.identity;
                wall.transform.localScale = config.WallSize;
                wall.name = $"Wall_{wallPos.x}_{wallPos.y}";
                wall.layer = ObstacleLayer;
            }
        }

        private void SpawnEnemiesInternal(LevelData levelData, LevelConfiguration config, Transform container)
        {
            foreach (var enemyData in levelData.EnemySpawns)
            {
                if (enemyData.EnemyPrefab == null || !enemyData.EnemyPrefab.RuntimeKeyIsValid())
                {
                    Debug.LogWarning($"[LevelSpawner] Enemy at {enemyData.SpawnPosition} has no prefab");
                    continue;
                }

                var prefab = LoadAddressablePrefab(enemyData.EnemyPrefab);
                if (prefab == null)
                {
                    Debug.LogError($"[LevelSpawner] Failed to load enemy prefab at {enemyData.SpawnPosition}");
                    continue;
                }

                Vector3 worldPos = levelData.GridToWorld(enemyData.SpawnPosition);
                Quaternion rotation = Quaternion.Euler(0f, enemyData.InitialRotation, 0f);

                var enemy = (GameObject)PrefabUtility.InstantiatePrefab(prefab, container);
                enemy.transform.position = worldPos;
                enemy.transform.rotation = rotation;
                enemy.name = $"Enemy_{enemyData.SpawnPosition.x}_{enemyData.SpawnPosition.y}";

                // Setup patrol controller with waypoints
                var patrolController = enemy.GetComponent<EnemyPatrolController>();
                if (patrolController != null && enemyData.PatrolPath != null)
                {
                    // Convert grid waypoints to world positions
                    foreach (var waypoint in enemyData.PatrolPath)
                    {
                        Vector3 waypointWorld = levelData.GridToWorld(waypoint.GridPosition);
                        patrolController.AddWaypoint(waypointWorld);

                        // Copy waypoint properties
                        var addedWaypoints = patrolController.PatrolWaypoints;
                        if (addedWaypoints.Count > 0)
                        {
                            var lastWaypoint = addedWaypoints[addedWaypoints.Count - 1];
                            lastWaypoint.WaitDelay = waypoint.WaitDelay;
                            lastWaypoint.IsObservation = waypoint.IsObservation;
                            lastWaypoint.AnimatorParameterName = waypoint.AnimatorParameterName;
                            lastWaypoint.AnimatorParameterValue = waypoint.AnimatorParameterValue;
                        }
                    }
                }

                // Ensure NavMeshAgent is present
                if (enemy.GetComponent<NavMeshAgent>() == null)
                {
                    enemy.AddComponent<NavMeshAgent>();
                }

                // Ensure EnemyBehavior is present
                if (enemy.GetComponent<EnemyBehavior>() == null)
                {
                    enemy.AddComponent<EnemyBehavior>();
                }
            }
        }

        private GameObject SpawnPlayerInternal(LevelData levelData, LevelConfiguration config)
        {
            if (!levelData.HasPlayerSpawn)
            {
                Debug.LogWarning("[LevelSpawner] No player spawn position in level data");
                return null;
            }

            if (config.PlayerPrefab == null || !config.PlayerPrefab.RuntimeKeyIsValid())
            {
                Debug.LogWarning("[LevelSpawner] No player prefab assigned");
                return null;
            }

            var prefab = LoadAddressablePrefab(config.PlayerPrefab);
            if (prefab == null)
            {
                Debug.LogError("[LevelSpawner] Failed to load player prefab");
                return null;
            }

            Vector3 worldPos = levelData.GridToWorld(levelData.PlayerSpawnPosition);

            var player = (GameObject)PrefabUtility.InstantiatePrefab(prefab, _spawner.transform);
            player.transform.position = worldPos;
            player.transform.rotation = Quaternion.identity;
            player.name = "Player";

            return player;
        }

        private GameObject LoadAddressablePrefab(UnityEngine.AddressableAssets.AssetReference assetRef)
        {
            // In editor, we can get the asset directly from the AssetReference
            var settings = AddressableAssetSettingsDefaultObject.Settings;
            if (settings == null)
            {
                Debug.LogError("[LevelSpawner] Addressable settings not found");
                return null;
            }

            string guid = assetRef.AssetGUID;
            string path = AssetDatabase.GUIDToAssetPath(guid);

            if (string.IsNullOrEmpty(path))
            {
                Debug.LogError($"[LevelSpawner] Could not find asset path for GUID: {guid}");
                return null;
            }

            return AssetDatabase.LoadAssetAtPath<GameObject>(path);
        }
    }
}
