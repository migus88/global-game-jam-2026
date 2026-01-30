using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AddressableAssets;

namespace Game.LevelEditor.Data
{
    [Serializable]
    public class EnemySpawnData
    {
        [SerializeField] private AssetReferenceGameObject _enemyPrefab;
        [SerializeField] private Vector2Int _spawnPosition;
        [SerializeField, Range(0f, 360f)] private float _initialRotation;
        [SerializeField] private List<PatrolWaypoint> _patrolPath = new();

        public AssetReferenceGameObject EnemyPrefab
        {
            get => _enemyPrefab;
            set => _enemyPrefab = value;
        }

        public Vector2Int SpawnPosition
        {
            get => _spawnPosition;
            set => _spawnPosition = value;
        }

        public float InitialRotation
        {
            get => _initialRotation;
            set => _initialRotation = value;
        }

        public List<PatrolWaypoint> PatrolPath => _patrolPath;

        public EnemySpawnData()
        {
        }

        public EnemySpawnData(Vector2Int spawnPosition)
        {
            _spawnPosition = spawnPosition;
        }

        public void AddWaypoint(PatrolWaypoint waypoint)
        {
            _patrolPath.Add(waypoint);
        }

        public void RemoveWaypoint(int index)
        {
            if (index >= 0 && index < _patrolPath.Count)
            {
                _patrolPath.RemoveAt(index);
            }
        }

        public void ClearPatrolPath()
        {
            _patrolPath.Clear();
        }
    }
}
