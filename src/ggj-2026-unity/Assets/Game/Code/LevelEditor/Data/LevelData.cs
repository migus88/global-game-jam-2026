using System.Collections.Generic;
using UnityEngine;

namespace Game.LevelEditor.Data
{
    [CreateAssetMenu(fileName = "LevelData", menuName = "Game/Level Data")]
    public class LevelData : ScriptableObject
    {
        [field: SerializeField]
        public Vector2Int GridSize { get; private set; } = new(20, 20);

        [field: SerializeField]
        public float CellSize { get; private set; } = 1f;

        [field: SerializeField]
        public Vector3 GridOrigin { get; private set; } = Vector3.zero;

        [SerializeField] private List<Vector2Int> _wallPositions = new();
        [SerializeField] private Vector2Int _playerSpawnPosition;
        [SerializeField] private bool _hasPlayerSpawn;
        [SerializeField] private List<EnemySpawnData> _enemySpawns = new();

        public IReadOnlyList<Vector2Int> WallPositions => _wallPositions;
        public Vector2Int PlayerSpawnPosition => _playerSpawnPosition;
        public bool HasPlayerSpawn => _hasPlayerSpawn;
        public IReadOnlyList<EnemySpawnData> EnemySpawns => _enemySpawns;

        public Vector3 GridToWorld(Vector2Int gridPos)
        {
            return GridOrigin + new Vector3(
                gridPos.x * CellSize + CellSize * 0.5f,
                0f,
                gridPos.y * CellSize + CellSize * 0.5f
            );
        }

        public Vector2Int WorldToGrid(Vector3 worldPos)
        {
            Vector3 localPos = worldPos - GridOrigin;
            return new Vector2Int(
                Mathf.FloorToInt(localPos.x / CellSize),
                Mathf.FloorToInt(localPos.z / CellSize)
            );
        }

        public bool IsValidGridPosition(Vector2Int gridPos)
        {
            return gridPos.x >= 0 && gridPos.x < GridSize.x &&
                   gridPos.y >= 0 && gridPos.y < GridSize.y;
        }

        public bool HasWallAt(Vector2Int gridPos)
        {
            return _wallPositions.Contains(gridPos);
        }

#if UNITY_EDITOR
        public void SetGridSize(Vector2Int size)
        {
            GridSize = size;
            MarkDirty();
        }

        public void SetCellSize(float size)
        {
            CellSize = Mathf.Max(0.1f, size);
            MarkDirty();
        }

        public void SetGridOrigin(Vector3 origin)
        {
            GridOrigin = origin;
            MarkDirty();
        }

        public void AddWall(Vector2Int gridPos)
        {
            if (!IsValidGridPosition(gridPos))
            {
                return;
            }

            if (!_wallPositions.Contains(gridPos))
            {
                _wallPositions.Add(gridPos);
                MarkDirty();
            }
        }

        public void RemoveWall(Vector2Int gridPos)
        {
            if (_wallPositions.Remove(gridPos))
            {
                MarkDirty();
            }
        }

        public void SetPlayerSpawn(Vector2Int gridPos)
        {
            if (!IsValidGridPosition(gridPos))
            {
                return;
            }

            _playerSpawnPosition = gridPos;
            _hasPlayerSpawn = true;
            MarkDirty();
        }

        public void ClearPlayerSpawn()
        {
            _hasPlayerSpawn = false;
            MarkDirty();
        }

        public EnemySpawnData AddEnemySpawn(string enemyId, Vector2Int gridPos)
        {
            if (!IsValidGridPosition(gridPos))
            {
                return null;
            }

            var spawn = new EnemySpawnData(enemyId, gridPos);
            _enemySpawns.Add(spawn);
            MarkDirty();
            return spawn;
        }

        public void RemoveEnemySpawn(EnemySpawnData spawn)
        {
            if (_enemySpawns.Remove(spawn))
            {
                MarkDirty();
            }
        }

        public void RemoveEnemySpawnAt(int index)
        {
            if (index >= 0 && index < _enemySpawns.Count)
            {
                _enemySpawns.RemoveAt(index);
                MarkDirty();
            }
        }

        public void ClearAll()
        {
            _wallPositions.Clear();
            _hasPlayerSpawn = false;
            _enemySpawns.Clear();
            MarkDirty();
        }

        private void MarkDirty()
        {
            UnityEditor.EditorUtility.SetDirty(this);
        }
#endif
    }
}
