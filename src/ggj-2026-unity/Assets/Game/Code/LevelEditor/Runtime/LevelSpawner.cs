using Game.LevelEditor.Data;
using UnityEngine;

namespace Game.LevelEditor.Runtime
{
    public class LevelSpawner : MonoBehaviour
    {
        [Header("Level Data")]
        [SerializeField] private LevelData _levelData;
        [SerializeField] private LevelConfiguration _config;

        [Header("Spawned Content")]
        [SerializeField] private Transform _wallsContainer;
        [SerializeField] private Transform _enemiesContainer;
        [SerializeField] private GameObject _playerInstance;

        public LevelData LevelData => _levelData;
        public LevelConfiguration Config => _config;
        public Transform WallsContainer => _wallsContainer;
        public Transform EnemiesContainer => _enemiesContainer;
        public GameObject PlayerInstance => _playerInstance;

#if UNITY_EDITOR
        public void SetContainers(Transform walls, Transform enemies, GameObject player)
        {
            _wallsContainer = walls;
            _enemiesContainer = enemies;
            _playerInstance = player;
            UnityEditor.EditorUtility.SetDirty(this);
        }

        public void ClearContainerReferences()
        {
            _wallsContainer = null;
            _enemiesContainer = null;
            _playerInstance = null;
            UnityEditor.EditorUtility.SetDirty(this);
        }
#endif
    }
}
