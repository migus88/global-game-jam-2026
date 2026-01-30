using System;
using System.Collections.Generic;
using UnityEngine;

namespace Game.LevelEditor.Data
{
    [CreateAssetMenu(fileName = "LevelConfiguration", menuName = "Game/Level Configuration")]
    public class LevelConfiguration : ScriptableObject
    {
        [field: SerializeField, Header("Player")]
        public GameObject PlayerPrefab { get; private set; }

        [field: SerializeField, Header("Walls")]
        public Material WallMaterial { get; private set; }

        [field: SerializeField]
        public Vector3 WallSize { get; private set; } = new(1f, 2f, 1f);

        [SerializeField, Header("Enemies")]
        private List<EnemyPrefabEntry> _enemyPrefabs = new();

        public GameObject GetEnemyPrefab(string enemyId)
        {
            foreach (var entry in _enemyPrefabs)
            {
                if (entry.EnemyId == enemyId)
                {
                    return entry.Prefab;
                }
            }

            Debug.LogWarning($"Enemy prefab not found for ID: {enemyId}");
            return null;
        }

        public IReadOnlyList<EnemyPrefabEntry> EnemyPrefabs => _enemyPrefabs;
    }

    [Serializable]
    public class EnemyPrefabEntry
    {
        [field: SerializeField]
        public string EnemyId { get; private set; } = string.Empty;

        [field: SerializeField]
        public GameObject Prefab { get; private set; }
    }
}
