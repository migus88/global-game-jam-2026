using Game.LevelEditor.Data;
using UnityEditor;
using UnityEngine;
using UnityEngine.AddressableAssets;

namespace Game.Editor.LevelEditor
{
    [CustomEditor(typeof(LevelData))]
    public class LevelDataEditor : UnityEditor.Editor
    {
        private static readonly Color GridColor = new(1f, 1f, 1f, 0.3f);
        private static readonly Color WallColor = new(0.5f, 0.5f, 0.5f, 0.8f);
        private static readonly Color PlayerSpawnColor = new(0.2f, 0.5f, 1f, 0.8f);
        private static readonly Color EnemySpawnColor = new(1f, 0.3f, 0.3f, 0.8f);
        private static readonly Color PatrolPathColor = new(1f, 0.8f, 0.2f, 0.8f);
        private static readonly Color PatrolPathBlockedColor = new(1f, 0f, 0f, 1f);
        private static readonly Color WaypointColor = new(1f, 0.6f, 0f, 0.9f);

        public static int SelectedEnemyIndex { get; set; } = -1;

        private LevelData _levelData;

        private void OnEnable()
        {
            _levelData = (LevelData)target;
            SceneView.duringSceneGui += OnSceneGUI;
        }

        private void OnDisable()
        {
            SceneView.duringSceneGui -= OnSceneGUI;
        }

        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();

            EditorGUILayout.Space(10);

            if (GUILayout.Button("Open Level Editor Window"))
            {
                LevelEditorWindow.Open(_levelData);
            }

            EditorGUILayout.Space(5);

            EditorGUILayout.LabelField("Statistics", EditorStyles.boldLabel);
            EditorGUILayout.LabelField($"Walls: {_levelData.WallPositions.Count}");
            EditorGUILayout.LabelField($"Enemies: {_levelData.EnemySpawns.Count}");
            EditorGUILayout.LabelField($"Player Spawn: {(_levelData.HasPlayerSpawn ? _levelData.PlayerSpawnPosition.ToString() : "Not Set")}");

            EditorGUILayout.Space(5);

            if (GUILayout.Button("Clear All Data"))
            {
                if (EditorUtility.DisplayDialog("Clear Level Data",
                        "Are you sure you want to clear all level data? This cannot be undone.",
                        "Clear", "Cancel"))
                {
                    Undo.RecordObject(_levelData, "Clear Level Data");
                    _levelData.ClearAll();
                }
            }
        }

        private void OnSceneGUI(SceneView sceneView)
        {
            if (_levelData == null)
            {
                return;
            }

            DrawGrid();
            DrawWalls();
            DrawPlayerSpawn();
            DrawEnemySpawns();
        }

        private void DrawGrid()
        {
            Handles.color = GridColor;

            Vector3 origin = _levelData.GridOrigin;
            float cellSize = _levelData.CellSize;
            Vector2Int gridSize = _levelData.GridSize;

            // Draw vertical lines
            for (int x = 0; x <= gridSize.x; x++)
            {
                Vector3 start = origin + new Vector3(x * cellSize, 0f, 0f);
                Vector3 end = start + new Vector3(0f, 0f, gridSize.y * cellSize);
                Handles.DrawLine(start, end);
            }

            // Draw horizontal lines
            for (int y = 0; y <= gridSize.y; y++)
            {
                Vector3 start = origin + new Vector3(0f, 0f, y * cellSize);
                Vector3 end = start + new Vector3(gridSize.x * cellSize, 0f, 0f);
                Handles.DrawLine(start, end);
            }
        }

        private void DrawWalls()
        {
            Handles.color = WallColor;

            foreach (var wallPos in _levelData.WallPositions)
            {
                Vector3 worldPos = _levelData.GridToWorld(wallPos);
                worldPos.y = 0.5f;
                Handles.DrawWireCube(worldPos, new Vector3(_levelData.CellSize * 0.9f, 1f, _levelData.CellSize * 0.9f));
            }
        }

        private void DrawPlayerSpawn()
        {
            if (!_levelData.HasPlayerSpawn)
            {
                return;
            }

            Handles.color = PlayerSpawnColor;
            Vector3 worldPos = _levelData.GridToWorld(_levelData.PlayerSpawnPosition);
            Handles.DrawSolidDisc(worldPos, Vector3.up, _levelData.CellSize * 0.3f);
            Handles.Label(worldPos + Vector3.up * 0.5f, "Player", EditorStyles.boldLabel);
        }

        private void DrawEnemySpawns()
        {
            for (int i = 0; i < _levelData.EnemySpawns.Count; i++)
            {
                var spawn = _levelData.EnemySpawns[i];
                bool isSelected = i == SelectedEnemyIndex;
                DrawEnemySpawn(spawn, i, isSelected);
            }
        }

        private void DrawEnemySpawn(EnemySpawnData spawn, int index, bool isSelected)
        {
            Vector3 worldPos = _levelData.GridToWorld(spawn.SpawnPosition);

            // Draw spawn disc
            Handles.color = isSelected ? new Color(1f, 0.8f, 0.2f, 1f) : EnemySpawnColor;
            Handles.DrawSolidDisc(worldPos, Vector3.up, _levelData.CellSize * 0.25f);

            // Draw direction arrow
            Quaternion rotation = Quaternion.Euler(0f, spawn.InitialRotation, 0f);
            Vector3 forward = rotation * Vector3.forward * _levelData.CellSize * 0.4f;
            Handles.DrawLine(worldPos, worldPos + forward);
            Handles.ConeHandleCap(0, worldPos + forward, rotation, 0.1f, EventType.Repaint);

            // Draw label
            string label = GetAssetReferenceName(spawn.EnemyPrefab, index);
            Handles.Label(worldPos + Vector3.up * 0.3f, label);

            // Draw patrol path with wall validation for selected enemy
            DrawPatrolPath(spawn, worldPos, isSelected);
        }

        private static string GetAssetReferenceName(AssetReferenceGameObject assetRef, int index)
        {
            if (assetRef == null || !assetRef.RuntimeKeyIsValid())
            {
                return $"Enemy {index}";
            }

            var path = AssetDatabase.GUIDToAssetPath(assetRef.AssetGUID);
            if (!string.IsNullOrEmpty(path))
            {
                var asset = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (asset != null)
                {
                    return asset.name;
                }
            }

            return $"Enemy {index}";
        }

        private void DrawPatrolPath(EnemySpawnData spawn, Vector3 spawnWorldPos, bool validateWalls)
        {
            if (spawn.PatrolPath.Count == 0)
            {
                return;
            }

            Vector2Int previousGridPos = spawn.SpawnPosition;
            Vector3 previousPos = spawnWorldPos;

            for (int i = 0; i < spawn.PatrolPath.Count; i++)
            {
                var waypoint = spawn.PatrolPath[i];
                Vector3 waypointPos = _levelData.GridToWorld(waypoint.GridPosition);

                // Check if path segment goes through a wall
                bool isBlocked = validateWalls && IsPathBlocked(previousGridPos, waypoint.GridPosition);

                // Draw line to waypoint
                Handles.color = isBlocked ? PatrolPathBlockedColor : PatrolPathColor;
                if (isBlocked)
                {
                    // Draw solid thick line for blocked paths
                    Handles.DrawAAPolyLine(4f, previousPos, waypointPos);
                }
                else
                {
                    DrawDottedLine(previousPos, waypointPos);
                }

                // Draw waypoint marker
                Handles.color = WaypointColor;
                Handles.DrawSolidDisc(waypointPos, Vector3.up, _levelData.CellSize * 0.15f);

                // Draw waypoint index
                Handles.Label(waypointPos + Vector3.up * 0.2f, $"{i + 1}");

                // Draw delay label if present
                if (waypoint.WaitDelay > 0f)
                {
                    Handles.Label(waypointPos + Vector3.up * 0.4f + Vector3.right * 0.2f, $"{waypoint.WaitDelay:F1}s");
                }

                // Draw animator param if present
                if (!string.IsNullOrEmpty(waypoint.AnimatorParameterName))
                {
                    string paramLabel = $"{waypoint.AnimatorParameterName}={waypoint.AnimatorParameterValue}";
                    Handles.Label(waypointPos + Vector3.up * 0.6f + Vector3.right * 0.2f, paramLabel);
                }

                previousGridPos = waypoint.GridPosition;
                previousPos = waypointPos;
            }

            // Draw line back to spawn
            bool returnBlocked = validateWalls && IsPathBlocked(previousGridPos, spawn.SpawnPosition);
            Handles.color = returnBlocked ? PatrolPathBlockedColor : PatrolPathColor;
            if (returnBlocked)
            {
                Handles.DrawAAPolyLine(4f, previousPos, spawnWorldPos);
            }
            else
            {
                DrawDottedLine(previousPos, spawnWorldPos);
            }
        }

        private bool IsPathBlocked(Vector2Int from, Vector2Int to)
        {
            // Use Bresenham's line algorithm to check all cells between two points
            int x0 = from.x;
            int y0 = from.y;
            int x1 = to.x;
            int y1 = to.y;

            int dx = Mathf.Abs(x1 - x0);
            int dy = Mathf.Abs(y1 - y0);
            int sx = x0 < x1 ? 1 : -1;
            int sy = y0 < y1 ? 1 : -1;
            int err = dx - dy;

            while (true)
            {
                // Skip start and end positions
                if ((x0 != from.x || y0 != from.y) && (x0 != to.x || y0 != to.y))
                {
                    if (_levelData.HasWallAt(new Vector2Int(x0, y0)))
                    {
                        return true;
                    }
                }

                if (x0 == x1 && y0 == y1)
                {
                    break;
                }

                int e2 = 2 * err;
                if (e2 > -dy)
                {
                    err -= dy;
                    x0 += sx;
                }

                if (e2 < dx)
                {
                    err += dx;
                    y0 += sy;
                }
            }

            return false;
        }

        private void DrawDottedLine(Vector3 start, Vector3 end)
        {
            float dotLength = 0.2f;
            float gapLength = 0.1f;
            float totalLength = Vector3.Distance(start, end);
            Vector3 direction = (end - start).normalized;

            float distance = 0f;
            bool drawing = true;

            while (distance < totalLength)
            {
                float segmentLength = drawing ? dotLength : gapLength;
                float nextDistance = Mathf.Min(distance + segmentLength, totalLength);

                if (drawing)
                {
                    Vector3 segStart = start + direction * distance;
                    Vector3 segEnd = start + direction * nextDistance;
                    Handles.DrawLine(segStart, segEnd);
                }

                distance = nextDistance;
                drawing = !drawing;
            }
        }
    }
}
