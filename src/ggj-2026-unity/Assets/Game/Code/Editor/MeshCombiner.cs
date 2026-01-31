using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Game.Editor
{
    public class MeshCombiner : EditorWindow
    {
        private const string CombinedMeshName = "CombinedMesh";

        [MenuItem("Tools/Mesh Combiner")]
        public static void ShowWindow()
        {
            GetWindow<MeshCombiner>("Mesh Combiner");
        }

        private void OnGUI()
        {
            EditorGUILayout.LabelField("Mesh Combiner", EditorStyles.boldLabel);
            EditorGUILayout.Space();

            EditorGUILayout.HelpBox(
                "Select multiple GameObjects with MeshFilter components in the Hierarchy, then click Combine.",
                MessageType.Info);

            EditorGUILayout.Space();

            var selectedCount = GetSelectedMeshFilters().Count;
            EditorGUILayout.LabelField($"Selected meshes: {selectedCount}");

            EditorGUILayout.Space();

            EditorGUI.BeginDisabledGroup(selectedCount < 2);
            if (GUILayout.Button("Combine Selected Meshes"))
            {
                CombineSelectedMeshes();
            }
            EditorGUI.EndDisabledGroup();
        }

        private void OnSelectionChange()
        {
            Repaint();
        }

        private static List<MeshFilter> GetSelectedMeshFilters()
        {
            var meshFilters = new List<MeshFilter>();

            foreach (var go in Selection.gameObjects)
            {
                var meshFilter = go.GetComponent<MeshFilter>();
                if (meshFilter != null && meshFilter.sharedMesh != null)
                {
                    meshFilters.Add(meshFilter);
                }
            }

            return meshFilters;
        }

        private static void CombineSelectedMeshes()
        {
            var meshFilters = GetSelectedMeshFilters();

            if (meshFilters.Count < 2)
            {
                EditorUtility.DisplayDialog("Mesh Combiner", "Select at least 2 GameObjects with MeshFilter components.", "OK");
                return;
            }

            var combineInstances = new CombineInstance[meshFilters.Count];
            var firstRenderer = meshFilters[0].GetComponent<MeshRenderer>();
            var sharedMaterial = firstRenderer != null ? firstRenderer.sharedMaterial : null;

            for (var i = 0; i < meshFilters.Count; i++)
            {
                combineInstances[i].mesh = meshFilters[i].sharedMesh;
                combineInstances[i].transform = meshFilters[i].transform.localToWorldMatrix;
            }

            var combinedMesh = new Mesh();
            combinedMesh.name = CombinedMeshName;
            combinedMesh.CombineMeshes(combineInstances, true, true);
            combinedMesh.RecalculateBounds();
            combinedMesh.RecalculateNormals();

            var combinedObject = new GameObject(CombinedMeshName);
            var meshFilter = combinedObject.AddComponent<MeshFilter>();
            var meshRenderer = combinedObject.AddComponent<MeshRenderer>();

            meshFilter.sharedMesh = combinedMesh;

            if (sharedMaterial != null)
            {
                meshRenderer.sharedMaterial = sharedMaterial;
            }

            Undo.RegisterCreatedObjectUndo(combinedObject, "Combine Meshes");
            Selection.activeGameObject = combinedObject;

            Debug.Log($"Combined {meshFilters.Count} meshes into '{CombinedMeshName}'");
        }
    }
}
