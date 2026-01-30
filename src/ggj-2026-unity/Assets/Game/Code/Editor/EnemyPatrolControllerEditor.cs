using Game.LevelEditor.Runtime;
using UnityEditor;
using UnityEngine;

namespace Game.Editor
{
    [CustomEditor(typeof(EnemyPatrolController))]
    public class EnemyPatrolControllerEditor : UnityEditor.Editor
    {
        private const float WaypointRadius = 0.3f;
        private const float ButtonSize = 0.15f;

        private EnemyPatrolController _controller;
        private int _selectedWaypointIndex = -1;
        private bool _isAddingWaypoint;

        private static readonly Color PathColor = new(1f, 0.5f, 0f, 1f);
        private static readonly Color WaypointColor = new(1f, 0.7f, 0.2f, 1f);
        private static readonly Color SelectedWaypointColor = new(0f, 1f, 0.5f, 1f);
        private static readonly Color ObservationWaypointColor = new(0f, 1f, 1f, 1f);
        private static readonly Color AddButtonColor = new(0.2f, 0.8f, 0.2f, 1f);
        private static readonly Color RemoveButtonColor = new(0.8f, 0.2f, 0.2f, 1f);

        private void OnEnable()
        {
            _controller = (EnemyPatrolController)target;
        }

        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();

            EditorGUILayout.Space(10);
            EditorGUILayout.LabelField("Patrol Path Editing", EditorStyles.boldLabel);

            EditorGUILayout.BeginHorizontal();

            if (GUILayout.Button(_isAddingWaypoint ? "Cancel Add" : "Add Waypoint"))
            {
                _isAddingWaypoint = !_isAddingWaypoint;
                if (_isAddingWaypoint)
                {
                    _selectedWaypointIndex = -1;
                }
                SceneView.RepaintAll();
            }

            if (GUILayout.Button("Clear All"))
            {
                if (EditorUtility.DisplayDialog("Clear Patrol Path",
                    "Are you sure you want to clear all waypoints?", "Yes", "No"))
                {
                    Undo.RecordObject(_controller, "Clear Waypoints");
                    _controller.ClearWaypoints();
                    _selectedWaypointIndex = -1;
                }
            }

            EditorGUILayout.EndHorizontal();

            if (_isAddingWaypoint)
            {
                EditorGUILayout.HelpBox("Shift+Click in Scene view to add waypoints", MessageType.Info);
            }

            // Show selected waypoint details
            if (_selectedWaypointIndex >= 0 && _selectedWaypointIndex < _controller.PatrolWaypoints.Count)
            {
                EditorGUILayout.Space(5);
                EditorGUILayout.LabelField($"Selected Waypoint: {_selectedWaypointIndex + 1}", EditorStyles.boldLabel);

                var waypoint = _controller.PatrolWaypoints[_selectedWaypointIndex];

                EditorGUI.BeginChangeCheck();

                var newPos = EditorGUILayout.Vector3Field("Position", waypoint.Position);
                var newDelay = EditorGUILayout.FloatField("Wait Delay", waypoint.WaitDelay);
                var newIsObs = EditorGUILayout.Toggle("Is Observation", waypoint.IsObservation);
                var newAnimParam = EditorGUILayout.TextField("Animator Parameter", waypoint.AnimatorParameterName);
                var newAnimValue = EditorGUILayout.Toggle("Animator Value", waypoint.AnimatorParameterValue);

                if (EditorGUI.EndChangeCheck())
                {
                    Undo.RecordObject(_controller, "Edit Waypoint");
                    waypoint.Position = newPos;
                    waypoint.WaitDelay = Mathf.Max(0, newDelay);
                    waypoint.IsObservation = newIsObs;
                    waypoint.AnimatorParameterName = newAnimParam;
                    waypoint.AnimatorParameterValue = newAnimValue;
                    EditorUtility.SetDirty(_controller);
                }

                if (GUILayout.Button("Delete Selected Waypoint"))
                {
                    Undo.RecordObject(_controller, "Delete Waypoint");
                    _controller.RemoveWaypoint(_selectedWaypointIndex);
                    _selectedWaypointIndex = -1;
                }
            }

            // Waypoint list
            EditorGUILayout.Space(5);
            EditorGUILayout.LabelField($"Waypoints ({_controller.PatrolWaypoints.Count})", EditorStyles.boldLabel);

            for (int i = 0; i < _controller.PatrolWaypoints.Count; i++)
            {
                var waypoint = _controller.PatrolWaypoints[i];
                EditorGUILayout.BeginHorizontal();

                bool isSelected = _selectedWaypointIndex == i;
                GUI.backgroundColor = isSelected ? Color.green : Color.white;

                string label = $"{i + 1}: {waypoint.Position:F1}";
                if (waypoint.IsObservation) label += " [Obs]";
                if (waypoint.WaitDelay > 0) label += $" [{waypoint.WaitDelay}s]";

                if (GUILayout.Button(label, GUILayout.Height(20)))
                {
                    _selectedWaypointIndex = isSelected ? -1 : i;
                    SceneView.RepaintAll();
                }

                GUI.backgroundColor = Color.white;
                EditorGUILayout.EndHorizontal();
            }
        }

        private void OnSceneGUI()
        {
            if (_controller == null)
            {
                return;
            }

            DrawPatrolPath();
            HandleWaypointEditing();
            HandleSceneInput();
        }

        private void DrawPatrolPath()
        {
            var waypoints = _controller.PatrolWaypoints;
            Vector3 startPos = _controller.transform.position;

            // Draw line from enemy to first waypoint
            if (waypoints.Count > 0)
            {
                Handles.color = PathColor;
                Handles.DrawDottedLine(startPos, waypoints[0].Position, 4f);
            }

            // Draw path between waypoints
            for (int i = 0; i < waypoints.Count; i++)
            {
                var waypoint = waypoints[i];
                Vector3 pos = waypoint.Position;

                // Draw line to next waypoint
                if (i < waypoints.Count - 1)
                {
                    Handles.color = PathColor;
                    Handles.DrawDottedLine(pos, waypoints[i + 1].Position, 4f);
                }

                // Draw waypoint disc
                bool isSelected = _selectedWaypointIndex == i;
                Color waypointCol = waypoint.IsObservation ? ObservationWaypointColor :
                                   isSelected ? SelectedWaypointColor : WaypointColor;

                Handles.color = waypointCol;
                Handles.DrawSolidDisc(pos, Vector3.up, WaypointRadius);

                // Draw waypoint number
                Handles.Label(pos + Vector3.up * 0.5f, $"{i + 1}",
                    new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter, fontStyle = FontStyle.Bold });

                // Draw observation marker
                if (waypoint.IsObservation)
                {
                    Handles.color = ObservationWaypointColor;
                    float size = WaypointRadius * 1.5f;
                    Vector3[] diamond = {
                        pos + Vector3.forward * size,
                        pos + Vector3.right * size,
                        pos - Vector3.forward * size,
                        pos - Vector3.right * size,
                        pos + Vector3.forward * size
                    };
                    Handles.DrawPolyLine(diamond);
                }
            }

            // Draw start position marker
            Handles.color = new Color(0.2f, 0.6f, 1f, 1f);
            Handles.DrawSolidDisc(startPos, Vector3.up, WaypointRadius * 0.7f);
            Handles.Label(startPos + Vector3.up * 0.5f, "Start",
                new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter });
        }

        private void HandleWaypointEditing()
        {
            var waypoints = _controller.PatrolWaypoints;

            for (int i = 0; i < waypoints.Count; i++)
            {
                var waypoint = waypoints[i];
                Vector3 pos = waypoint.Position;

                // Position handle for selected or all waypoints
                EditorGUI.BeginChangeCheck();

                float handleSize = HandleUtility.GetHandleSize(pos) * 0.15f;
                Vector3 newPos = Handles.FreeMoveHandle(pos, handleSize, Vector3.one * 0.5f, Handles.SphereHandleCap);

                if (EditorGUI.EndChangeCheck())
                {
                    Undo.RecordObject(_controller, "Move Waypoint");
                    _controller.SetWaypointPosition(i, newPos);
                    _selectedWaypointIndex = i;
                }

                // Click to select
                if (Handles.Button(pos, Quaternion.identity, WaypointRadius, WaypointRadius * 1.2f, Handles.SphereHandleCap))
                {
                    _selectedWaypointIndex = i;
                    Repaint();
                }
            }
        }

        private void HandleSceneInput()
        {
            Event e = Event.current;

            // Shift+Click to add waypoint
            if (_isAddingWaypoint && e.type == EventType.MouseDown && e.button == 0 && e.shift)
            {
                Ray ray = HandleUtility.GUIPointToWorldRay(e.mousePosition);

                if (Physics.Raycast(ray, out RaycastHit hit, 1000f))
                {
                    Undo.RecordObject(_controller, "Add Waypoint");
                    _controller.AddWaypoint(hit.point);
                    e.Use();
                }
                else
                {
                    // Raycast to ground plane
                    Plane groundPlane = new Plane(Vector3.up, Vector3.zero);
                    if (groundPlane.Raycast(ray, out float distance))
                    {
                        Vector3 point = ray.GetPoint(distance);
                        Undo.RecordObject(_controller, "Add Waypoint");
                        _controller.AddWaypoint(point);
                        e.Use();
                    }
                }
            }

            // Delete key to remove selected waypoint
            if (e.type == EventType.KeyDown && e.keyCode == KeyCode.Delete && _selectedWaypointIndex >= 0)
            {
                Undo.RecordObject(_controller, "Delete Waypoint");
                _controller.RemoveWaypoint(_selectedWaypointIndex);
                _selectedWaypointIndex = -1;
                e.Use();
            }

            // Escape to deselect or cancel add mode
            if (e.type == EventType.KeyDown && e.keyCode == KeyCode.Escape)
            {
                if (_isAddingWaypoint)
                {
                    _isAddingWaypoint = false;
                }
                else
                {
                    _selectedWaypointIndex = -1;
                }
                e.Use();
                Repaint();
            }
        }
    }
}
