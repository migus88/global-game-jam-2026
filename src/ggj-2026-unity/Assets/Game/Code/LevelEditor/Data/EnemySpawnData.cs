using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AddressableAssets;

namespace Game.LevelEditor.Data
{
    [Serializable]
    public class EnemySpawnData
    {
        [field: SerializeField]
        public AssetReferenceGameObject EnemyPrefab { get; set; }

        [field: SerializeField]
        public Vector2Int SpawnPosition { get; set; }

        [field: SerializeField, Range(0f, 360f)]
        public float InitialRotation { get; set; }

        [field: SerializeField]
        public List<PatrolWaypoint> PatrolPath { get; private set; } = new();

        public EnemySpawnData()
        {
        }

        public EnemySpawnData(Vector2Int spawnPosition)
        {
            SpawnPosition = spawnPosition;
        }

        public void AddWaypoint(PatrolWaypoint waypoint)
        {
            PatrolPath.Add(waypoint);
        }

        public void RemoveWaypoint(int index)
        {
            if (index >= 0 && index < PatrolPath.Count)
            {
                PatrolPath.RemoveAt(index);
            }
        }

        public void ClearPatrolPath()
        {
            PatrolPath.Clear();
        }
    }
}
