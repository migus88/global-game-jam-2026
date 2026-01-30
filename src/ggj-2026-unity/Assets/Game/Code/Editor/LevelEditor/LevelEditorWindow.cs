using Game.LevelEditor.Data;
using UnityEditor;
using UnityEditorInternal;
using UnityEngine;
using UnityEngine.AddressableAssets;

namespace Game.Editor.LevelEditor
{
    public class LevelEditorWindow : EditorWindow
    {
        private enum EditorTool
        {
            None = 0,
            PaintWall = 1,
            EraseWall = 2,
            PlacePlayer = 3,
            PlaceEnemy = 4,
            EditPatrol = 5
        }

        private static readonly Color GridColor = new(0.3f, 0.3f, 0.3f, 1f);
        private static readonly Color GridBackgroundColor = new(0.15f, 0.15f, 0.15f, 1f);
        private static readonly Color WallColor = new(0.6f, 0.6f, 0.6f, 1f);
        private static readonly Color PlayerSpawnColor = new(0.3f, 0.6f, 1f, 1f);
        private static readonly Color EnemySpawnColor = new(1f, 0.4f, 0.4f, 1f);
        private static readonly Color SelectedEnemyColor = new(1f, 0.8f, 0.2f, 1f);
        private static readonly Color PatrolPathColor = new(1f, 0.7f, 0.3f, 1f);
        private static readonly Color WaypointColor = new(1f, 0.5f, 0f, 1f);
        private static readonly Color HoverColor = new(1f, 1f, 1f, 0.3f);

        [SerializeField] private LevelData _levelData;

        private EditorTool _currentTool = EditorTool.None;
        private int _selectedEnemyIndex = -1;
        private Vector2 _scrollPosition;
        private Vector2 _gridOffset;
        private float _zoom = 20f;
        private bool _isPanning;
        private Vector2 _lastMousePos;
        private ReorderableList _waypointList;
        private Vector2 _sidebarScroll;
        private SerializedObject _serializedLevelData;

        private const float MinZoom = 5f;
        private const float MaxZoom = 50f;
        private const float SidebarWidth = 280f;
        private const float ToolbarHeight = 25f;

        [MenuItem("Game/Level Editor")]
        public static void ShowWindow()
        {
            var window = GetWindow<LevelEditorWindow>("Level Editor");
            window.minSize = new Vector2(600f, 400f);
        }

        public static void Open(LevelData levelData)
        {
            var window = GetWindow<LevelEditorWindow>("Level Editor");
            window.SetLevelData(levelData);
            window.minSize = new Vector2(600f, 400f);
            window.CenterGrid();
        }

        private void SetLevelData(LevelData levelData)
        {
            _levelData = levelData;
            _serializedLevelData = levelData != null ? new SerializedObject(levelData) : null;
            _selectedEnemyIndex = -1;
            _waypointList = null;
        }

        private void OnEnable()
        {
            SceneView.duringSceneGui += OnSceneGUI;
            Undo.undoRedoPerformed += OnUndoRedo;

            // Initialize serialized object if we have level data
            if (_levelData != null && _serializedLevelData == null)
            {
                _serializedLevelData = new SerializedObject(_levelData);
            }
        }

        private void OnUndoRedo()
        {
            // Refresh serialized object after undo/redo
            if (_levelData != null)
            {
                _serializedLevelData = new SerializedObject(_levelData);
            }

            Repaint();
        }

        private void OnDisable()
        {
            SceneView.duringSceneGui -= OnSceneGUI;
            Undo.undoRedoPerformed -= OnUndoRedo;
        }

        private void OnGUI()
        {
            DrawToolbar();

            EditorGUILayout.BeginHorizontal();

            // Main grid area
            Rect gridRect = new(0f, ToolbarHeight, position.width - SidebarWidth, position.height - ToolbarHeight);
            DrawGridArea(gridRect);

            // Sidebar
            Rect sidebarRect = new(position.width - SidebarWidth, ToolbarHeight, SidebarWidth, position.height - ToolbarHeight);
            DrawSidebar(sidebarRect);

            EditorGUILayout.EndHorizontal();

            HandleInput();
        }

        private void DrawToolbar()
        {
            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);

            // Level data field
            EditorGUI.BeginChangeCheck();
            var newLevelData = (LevelData)EditorGUILayout.ObjectField(_levelData, typeof(LevelData), false, GUILayout.Width(200f));
            if (EditorGUI.EndChangeCheck())
            {
                SetLevelData(newLevelData);
                CenterGrid();
            }

            GUILayout.Space(10f);

            // Tool buttons
            if (GUILayout.Toggle(_currentTool == EditorTool.PaintWall, "Paint Wall", EditorStyles.toolbarButton))
            {
                _currentTool = EditorTool.PaintWall;
            }

            if (GUILayout.Toggle(_currentTool == EditorTool.EraseWall, "Erase", EditorStyles.toolbarButton))
            {
                _currentTool = EditorTool.EraseWall;
            }

            if (GUILayout.Toggle(_currentTool == EditorTool.PlacePlayer, "Player", EditorStyles.toolbarButton))
            {
                _currentTool = EditorTool.PlacePlayer;
            }

            if (GUILayout.Toggle(_currentTool == EditorTool.PlaceEnemy, "Enemy", EditorStyles.toolbarButton))
            {
                _currentTool = EditorTool.PlaceEnemy;
            }

            if (GUILayout.Toggle(_currentTool == EditorTool.EditPatrol, "Patrol", EditorStyles.toolbarButton))
            {
                _currentTool = EditorTool.EditPatrol;
            }

            GUILayout.FlexibleSpace();

            // Zoom controls
            GUILayout.Label($"Zoom: {_zoom:F0}", EditorStyles.miniLabel);
            if (GUILayout.Button("-", EditorStyles.toolbarButton, GUILayout.Width(20f)))
            {
                _zoom = Mathf.Clamp(_zoom - 5f, MinZoom, MaxZoom);
            }

            if (GUILayout.Button("+", EditorStyles.toolbarButton, GUILayout.Width(20f)))
            {
                _zoom = Mathf.Clamp(_zoom + 5f, MinZoom, MaxZoom);
            }

            if (GUILayout.Button("Center", EditorStyles.toolbarButton))
            {
                CenterGrid();
            }

            EditorGUILayout.EndHorizontal();
        }

        private void DrawGridArea(Rect rect)
        {
            // Draw background
            EditorGUI.DrawRect(rect, GridBackgroundColor);

            if (_levelData == null)
            {
                GUI.Label(rect, "Assign a LevelData asset to begin editing", EditorStyles.centeredGreyMiniLabel);
                return;
            }

            GUI.BeginClip(rect);

            Vector2 gridSize = new(_levelData.GridSize.x, _levelData.GridSize.y);
            Vector2 offset = _gridOffset + new Vector2(rect.width * 0.5f, rect.height * 0.5f);

            // Draw grid cells
            DrawGridCells(gridSize, offset);

            // Draw walls
            DrawWalls(offset);

            // Draw player spawn
            DrawPlayerSpawn(offset);

            // Draw enemy spawns
            DrawEnemySpawns(offset);

            // Draw hover highlight
            Vector2 mousePos = Event.current.mousePosition;
            if (rect.Contains(mousePos + new Vector2(rect.x, rect.y)))
            {
                Vector2Int gridPos = ScreenToGrid(mousePos, offset);
                if (_levelData.IsValidGridPosition(gridPos))
                {
                    DrawCellHighlight(gridPos, offset, HoverColor);
                }
            }

            GUI.EndClip();
        }

        private void DrawGridCells(Vector2 gridSize, Vector2 offset)
        {
            Handles.color = GridColor;

            // Draw vertical lines
            for (int x = 0; x <= gridSize.x; x++)
            {
                Vector2 start = GridToScreen(new Vector2Int(x, 0), offset);
                Vector2 end = GridToScreen(new Vector2Int(x, (int)gridSize.y), offset);
                Handles.DrawLine(new Vector3(start.x, start.y, 0f), new Vector3(end.x, end.y, 0f));
            }

            // Draw horizontal lines
            for (int y = 0; y <= gridSize.y; y++)
            {
                Vector2 start = GridToScreen(new Vector2Int(0, y), offset);
                Vector2 end = GridToScreen(new Vector2Int((int)gridSize.x, y), offset);
                Handles.DrawLine(new Vector3(start.x, start.y, 0f), new Vector3(end.x, end.y, 0f));
            }
        }

        private void DrawWalls(Vector2 offset)
        {
            foreach (var wallPos in _levelData.WallPositions)
            {
                DrawFilledCell(wallPos, offset, WallColor);
            }
        }

        private void DrawPlayerSpawn(Vector2 offset)
        {
            if (!_levelData.HasPlayerSpawn)
            {
                return;
            }

            Vector2 center = GridToScreen(_levelData.PlayerSpawnPosition, offset) + Vector2.one * _zoom * 0.5f;
            float radius = _zoom * 0.3f;

            Handles.color = PlayerSpawnColor;
            Handles.DrawSolidDisc(new Vector3(center.x, center.y, 0f), Vector3.forward, radius);

            // Draw "P" label
            GUI.color = Color.white;
            GUI.Label(new Rect(center.x - 5f, center.y - 8f, 20f, 20f), "P", EditorStyles.boldLabel);
            GUI.color = Color.white;
        }

        private void DrawEnemySpawns(Vector2 offset)
        {
            for (int i = 0; i < _levelData.EnemySpawns.Count; i++)
            {
                var spawn = _levelData.EnemySpawns[i];
                bool isSelected = i == _selectedEnemyIndex;
                Color color = isSelected ? SelectedEnemyColor : EnemySpawnColor;

                Vector2 center = GridToScreen(spawn.SpawnPosition, offset) + Vector2.one * _zoom * 0.5f;
                float radius = _zoom * 0.25f;

                Handles.color = color;
                Handles.DrawSolidDisc(new Vector3(center.x, center.y, 0f), Vector3.forward, radius);

                // Draw direction indicator
                float angle = -spawn.InitialRotation * Mathf.Deg2Rad + Mathf.PI * 0.5f;
                Vector2 dir = new(Mathf.Cos(angle), -Mathf.Sin(angle));
                Vector2 arrowEnd = center + dir * _zoom * 0.4f;
                Handles.DrawLine(new Vector3(center.x, center.y, 0f), new Vector3(arrowEnd.x, arrowEnd.y, 0f));

                // Draw index
                GUI.color = Color.white;
                GUI.Label(new Rect(center.x - 5f, center.y - 8f, 20f, 20f), $"{i}", EditorStyles.boldLabel);

                // Draw patrol path if selected
                if (isSelected && spawn.PatrolPath.Count > 0)
                {
                    DrawPatrolPath(spawn, offset, center);
                }
            }

            GUI.color = Color.white;
        }

        private void DrawPatrolPath(EnemySpawnData spawn, Vector2 offset, Vector2 startPos)
        {
            Handles.color = PatrolPathColor;
            Vector2 prevPos = startPos;

            for (int i = 0; i < spawn.PatrolPath.Count; i++)
            {
                var waypoint = spawn.PatrolPath[i];
                Vector2 waypointCenter = GridToScreen(waypoint.GridPosition, offset) + Vector2.one * _zoom * 0.5f;

                // Draw line
                Handles.DrawDottedLine(
                    new Vector3(prevPos.x, prevPos.y, 0f),
                    new Vector3(waypointCenter.x, waypointCenter.y, 0f),
                    2f
                );

                // Draw waypoint marker
                Handles.color = WaypointColor;
                Handles.DrawSolidDisc(new Vector3(waypointCenter.x, waypointCenter.y, 0f), Vector3.forward, _zoom * 0.15f);

                // Draw waypoint index
                GUI.color = Color.black;
                GUI.Label(new Rect(waypointCenter.x - 4f, waypointCenter.y - 7f, 20f, 20f), $"{i + 1}", EditorStyles.miniLabel);

                prevPos = waypointCenter;
                Handles.color = PatrolPathColor;
            }

            // Draw line back to start
            Handles.DrawDottedLine(
                new Vector3(prevPos.x, prevPos.y, 0f),
                new Vector3(startPos.x, startPos.y, 0f),
                2f
            );

            GUI.color = Color.white;
        }

        private void DrawFilledCell(Vector2Int gridPos, Vector2 offset, Color color)
        {
            Vector2 screenPos = GridToScreen(gridPos, offset);
            Rect cellRect = new(screenPos.x + 1f, screenPos.y + 1f, _zoom - 2f, _zoom - 2f);
            EditorGUI.DrawRect(cellRect, color);
        }

        private void DrawCellHighlight(Vector2Int gridPos, Vector2 offset, Color color)
        {
            Vector2 screenPos = GridToScreen(gridPos, offset);
            Rect cellRect = new(screenPos.x, screenPos.y, _zoom, _zoom);
            EditorGUI.DrawRect(cellRect, color);
        }

        private void DrawSidebar(Rect rect)
        {
            GUILayout.BeginArea(rect);
            _sidebarScroll = EditorGUILayout.BeginScrollView(_sidebarScroll);

            EditorGUILayout.Space(5f);

            if (_levelData == null)
            {
                EditorGUILayout.HelpBox("No LevelData selected", MessageType.Info);
                EditorGUILayout.EndScrollView();
                GUILayout.EndArea();
                return;
            }

            // Grid settings
            EditorGUILayout.LabelField("Grid Settings", EditorStyles.boldLabel);

            EditorGUI.BeginChangeCheck();
            var gridSize = EditorGUILayout.Vector2IntField("Grid Size", _levelData.GridSize);
            if (EditorGUI.EndChangeCheck())
            {
                Undo.RecordObject(_levelData, "Change Grid Size");
                _levelData.SetGridSize(gridSize);
            }

            EditorGUI.BeginChangeCheck();
            var cellSize = EditorGUILayout.FloatField("Cell Size", _levelData.CellSize);
            if (EditorGUI.EndChangeCheck())
            {
                Undo.RecordObject(_levelData, "Change Cell Size");
                _levelData.SetCellSize(cellSize);
            }

            EditorGUI.BeginChangeCheck();
            var origin = EditorGUILayout.Vector3Field("Grid Origin", _levelData.GridOrigin);
            if (EditorGUI.EndChangeCheck())
            {
                Undo.RecordObject(_levelData, "Change Grid Origin");
                _levelData.SetGridOrigin(origin);
            }

            EditorGUILayout.Space(10f);

            // Enemy list
            EditorGUILayout.LabelField("Enemies", EditorStyles.boldLabel);

            for (int i = 0; i < _levelData.EnemySpawns.Count; i++)
            {
                var spawn = _levelData.EnemySpawns[i];
                bool isSelected = i == _selectedEnemyIndex;

                EditorGUILayout.BeginHorizontal();

                string prefabName = GetAssetReferenceName(spawn.EnemyPrefab);
                bool clicked = GUILayout.Toggle(isSelected, $"{i}: {prefabName}", EditorStyles.miniButton);

                // Only change selection if toggle state changed
                if (clicked && !isSelected)
                {
                    _selectedEnemyIndex = i;
                    RebuildWaypointList();
                }
                else if (!clicked && isSelected)
                {
                    _selectedEnemyIndex = -1;
                    RebuildWaypointList();
                }

                if (GUILayout.Button("X", GUILayout.Width(20f)))
                {
                    Undo.RecordObject(_levelData, "Remove Enemy");
                    _levelData.RemoveEnemySpawnAt(i);
                    if (_selectedEnemyIndex >= _levelData.EnemySpawns.Count)
                    {
                        _selectedEnemyIndex = _levelData.EnemySpawns.Count - 1;
                    }

                    RebuildWaypointList();
                    break;
                }

                EditorGUILayout.EndHorizontal();
            }

            EditorGUILayout.Space(10f);

            // Selected enemy patrol editor
            if (_selectedEnemyIndex >= 0 && _selectedEnemyIndex < _levelData.EnemySpawns.Count)
            {
                DrawPatrolEditor();
            }

            EditorGUILayout.EndScrollView();
            GUILayout.EndArea();
        }

        private void DrawPatrolEditor()
        {
            var spawn = _levelData.EnemySpawns[_selectedEnemyIndex];

            EditorGUILayout.LabelField("Selected Enemy", EditorStyles.boldLabel);

            // Enemy prefab reference using SerializedProperty for proper AssetReference drawer
            if (_serializedLevelData == null)
            {
                _serializedLevelData = new SerializedObject(_levelData);
            }

            _serializedLevelData.Update();

            var enemySpawnsProperty = _serializedLevelData.FindProperty("_enemySpawns");
            if (enemySpawnsProperty != null && _selectedEnemyIndex < enemySpawnsProperty.arraySize)
            {
                var enemyProperty = enemySpawnsProperty.GetArrayElementAtIndex(_selectedEnemyIndex);
                var prefabProperty = enemyProperty.FindPropertyRelative("_enemyPrefab");

                if (prefabProperty != null)
                {
                    EditorGUILayout.PropertyField(prefabProperty, new GUIContent("Enemy Prefab"));
                }
                else
                {
                    EditorGUILayout.HelpBox("Could not find EnemyPrefab property", MessageType.Warning);
                }
            }

            _serializedLevelData.ApplyModifiedProperties();

            EditorGUI.BeginChangeCheck();
            var rotation = EditorGUILayout.Slider("Initial Rotation", spawn.InitialRotation, 0f, 360f);
            if (EditorGUI.EndChangeCheck())
            {
                Undo.RecordObject(_levelData, "Change Enemy Rotation");
                spawn.InitialRotation = rotation;
                EditorUtility.SetDirty(_levelData);
            }

            EditorGUILayout.Space(10f);
            EditorGUILayout.LabelField("Patrol Path", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox("Select Patrol tool and click on grid to add waypoints", MessageType.Info);

            EditorGUILayout.Space(5f);

            if (_waypointList != null)
            {
                _waypointList.DoLayoutList();
            }

            EditorGUILayout.Space(5f);

            if (GUILayout.Button("Clear Patrol Path"))
            {
                Undo.RecordObject(_levelData, "Clear Patrol Path");
                spawn.ClearPatrolPath();
                RebuildWaypointList();
            }
        }

        private static string GetAssetReferenceName(AssetReferenceGameObject assetRef)
        {
            if (assetRef == null || !assetRef.RuntimeKeyIsValid())
            {
                return "(None)";
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

            return assetRef.AssetGUID;
        }

        private void RebuildWaypointList()
        {
            if (_selectedEnemyIndex < 0 || _selectedEnemyIndex >= _levelData.EnemySpawns.Count)
            {
                _waypointList = null;
                return;
            }

            var spawn = _levelData.EnemySpawns[_selectedEnemyIndex];

            _waypointList = new ReorderableList(spawn.PatrolPath, typeof(PatrolWaypoint), true, true, false, true)
            {
                drawHeaderCallback = rect => EditorGUI.LabelField(rect, "Waypoints"),
                drawElementCallback = (rect, index, _, _) =>
                {
                    if (index >= spawn.PatrolPath.Count)
                    {
                        return;
                    }

                    var waypoint = spawn.PatrolPath[index];
                    float lineHeight = EditorGUIUtility.singleLineHeight;
                    float spacing = 2f;

                    // Position
                    Rect posRect = new(rect.x, rect.y, rect.width, lineHeight);
                    EditorGUI.BeginChangeCheck();
                    var pos = EditorGUI.Vector2IntField(posRect, $"#{index + 1} Position", waypoint.GridPosition);
                    if (EditorGUI.EndChangeCheck())
                    {
                        Undo.RecordObject(_levelData, "Change Waypoint Position");
                        waypoint.GridPosition = pos;
                        EditorUtility.SetDirty(_levelData);
                    }

                    // Delay
                    Rect delayRect = new(rect.x, rect.y + lineHeight + spacing, rect.width * 0.5f - 5f, lineHeight);
                    EditorGUI.BeginChangeCheck();
                    var delay = EditorGUI.FloatField(delayRect, "Delay", waypoint.WaitDelay);
                    if (EditorGUI.EndChangeCheck())
                    {
                        Undo.RecordObject(_levelData, "Change Waypoint Delay");
                        waypoint.WaitDelay = Mathf.Max(0f, delay);
                        EditorUtility.SetDirty(_levelData);
                    }

                    // Animator param name
                    Rect paramRect = new(rect.x, rect.y + (lineHeight + spacing) * 2f, rect.width * 0.6f - 5f, lineHeight);
                    EditorGUI.BeginChangeCheck();
                    var paramName = EditorGUI.TextField(paramRect, "Anim Param", waypoint.AnimatorParameterName);
                    if (EditorGUI.EndChangeCheck())
                    {
                        Undo.RecordObject(_levelData, "Change Waypoint Animator Param");
                        waypoint.AnimatorParameterName = paramName;
                        EditorUtility.SetDirty(_levelData);
                    }

                    // Animator param value
                    Rect valueRect = new(rect.x + rect.width * 0.6f, rect.y + (lineHeight + spacing) * 2f, rect.width * 0.4f, lineHeight);
                    EditorGUI.BeginChangeCheck();
                    var paramValue = EditorGUI.Toggle(valueRect, "Value", waypoint.AnimatorParameterValue);
                    if (EditorGUI.EndChangeCheck())
                    {
                        Undo.RecordObject(_levelData, "Change Waypoint Animator Value");
                        waypoint.AnimatorParameterValue = paramValue;
                        EditorUtility.SetDirty(_levelData);
                    }
                },
                elementHeightCallback = _ => EditorGUIUtility.singleLineHeight * 3f + 8f,
                onRemoveCallback = list =>
                {
                    Undo.RecordObject(_levelData, "Remove Waypoint");
                    spawn.RemoveWaypoint(list.index);
                    EditorUtility.SetDirty(_levelData);
                },
                onReorderCallback = _ =>
                {
                    EditorUtility.SetDirty(_levelData);
                }
            };
        }

        private void HandleInput()
        {
            Event e = Event.current;

            Rect gridRect = new(0f, ToolbarHeight, position.width - SidebarWidth, position.height - ToolbarHeight);
            Vector2 mousePos = e.mousePosition;

            if (!gridRect.Contains(mousePos))
            {
                return;
            }

            Vector2 localMousePos = mousePos - new Vector2(gridRect.x, gridRect.y);
            Vector2 offset = _gridOffset + new Vector2(gridRect.width * 0.5f, gridRect.height * 0.5f);

            // Zoom with scroll wheel
            if (e.type == EventType.ScrollWheel)
            {
                float zoomDelta = -e.delta.y * 2f;
                _zoom = Mathf.Clamp(_zoom + zoomDelta, MinZoom, MaxZoom);
                e.Use();
                Repaint();
            }

            // Pan with middle mouse
            if (e.type == EventType.MouseDown && e.button == 2)
            {
                _isPanning = true;
                _lastMousePos = mousePos;
                e.Use();
            }

            if (e.type == EventType.MouseUp && e.button == 2)
            {
                _isPanning = false;
                e.Use();
            }

            if (_isPanning && e.type == EventType.MouseDrag)
            {
                _gridOffset += mousePos - _lastMousePos;
                _lastMousePos = mousePos;
                e.Use();
                Repaint();
            }

            // Tool actions
            if (_levelData == null)
            {
                return;
            }

            Vector2Int gridPos = ScreenToGrid(localMousePos, offset);

            if (!_levelData.IsValidGridPosition(gridPos))
            {
                return;
            }

            // Left click actions
            if (e.type == EventType.MouseDown && e.button == 0)
            {
                HandleToolClick(gridPos);
                e.Use();
            }

            // Drag for painting
            if (e.type == EventType.MouseDrag && e.button == 0)
            {
                if (_currentTool == EditorTool.PaintWall)
                {
                    Undo.RecordObject(_levelData, "Paint Wall");
                    _levelData.AddWall(gridPos);
                    e.Use();
                }
                else if (_currentTool == EditorTool.EraseWall)
                {
                    Undo.RecordObject(_levelData, "Erase Wall");
                    _levelData.RemoveWall(gridPos);
                    e.Use();
                }
            }

            // Right click to erase
            if (e.type == EventType.MouseDown && e.button == 1)
            {
                Undo.RecordObject(_levelData, "Erase Wall");
                _levelData.RemoveWall(gridPos);
                e.Use();
            }

            Repaint();
        }

        private void HandleToolClick(Vector2Int gridPos)
        {
            switch (_currentTool)
            {
                case EditorTool.PaintWall:
                    Undo.RecordObject(_levelData, "Paint Wall");
                    _levelData.AddWall(gridPos);
                    break;

                case EditorTool.EraseWall:
                    Undo.RecordObject(_levelData, "Erase Wall");
                    _levelData.RemoveWall(gridPos);
                    break;

                case EditorTool.PlacePlayer:
                    Undo.RecordObject(_levelData, "Place Player Spawn");
                    _levelData.SetPlayerSpawn(gridPos);
                    break;

                case EditorTool.PlaceEnemy:
                    Undo.RecordObject(_levelData, "Place Enemy Spawn");
                    var spawn = _levelData.AddEnemySpawn(gridPos);
                    if (spawn != null)
                    {
                        _selectedEnemyIndex = _levelData.EnemySpawns.Count - 1;
                        RebuildWaypointList();
                    }

                    break;

                case EditorTool.EditPatrol:
                    if (_selectedEnemyIndex >= 0 && _selectedEnemyIndex < _levelData.EnemySpawns.Count)
                    {
                        Undo.RecordObject(_levelData, "Add Waypoint");
                        var selectedSpawn = _levelData.EnemySpawns[_selectedEnemyIndex];
                        selectedSpawn.AddWaypoint(new PatrolWaypoint(gridPos));
                        RebuildWaypointList();
                    }

                    break;
            }
        }

        private Vector2 GridToScreen(Vector2Int gridPos, Vector2 offset)
        {
            return new Vector2(gridPos.x * _zoom, gridPos.y * _zoom) + offset;
        }

        private Vector2Int ScreenToGrid(Vector2 screenPos, Vector2 offset)
        {
            Vector2 gridSpace = (screenPos - offset) / _zoom;
            return new Vector2Int(Mathf.FloorToInt(gridSpace.x), Mathf.FloorToInt(gridSpace.y));
        }

        private void CenterGrid()
        {
            _gridOffset = Vector2.zero;

            if (_levelData != null)
            {
                _gridOffset = new Vector2(
                    -_levelData.GridSize.x * _zoom * 0.5f,
                    -_levelData.GridSize.y * _zoom * 0.5f
                );
            }
        }

        private void OnSceneGUI(SceneView sceneView)
        {
            // Scene view interaction can be added here if needed
        }
    }
}
