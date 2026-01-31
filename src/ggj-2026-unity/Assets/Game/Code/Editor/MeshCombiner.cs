using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Game.Editor
{
    public class MeshCombiner : EditorWindow
    {
        private const string CombinedMeshName = "CombinedMesh";
        private const float VertexTolerance = 0.0001f;
        private const float NormalTolerance = 0.001f;

        private bool _removeOverlaps = true;

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

            _removeOverlaps = EditorGUILayout.Toggle("Remove Overlapping Triangles", _removeOverlaps);

            EditorGUILayout.Space();

            var selectedCount = GetSelectedMeshFilters().Count;
            EditorGUILayout.LabelField($"Selected meshes: {selectedCount}");

            EditorGUILayout.Space();

            EditorGUI.BeginDisabledGroup(selectedCount < 2);
            if (GUILayout.Button("Combine Selected Meshes"))
            {
                CombineSelectedMeshes(_removeOverlaps);
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

        private static void CombineSelectedMeshes(bool removeOverlaps)
        {
            var meshFilters = GetSelectedMeshFilters();

            if (meshFilters.Count < 2)
            {
                EditorUtility.DisplayDialog("Mesh Combiner", "Select at least 2 GameObjects with MeshFilter components.", "OK");
                return;
            }

            var firstRenderer = meshFilters[0].GetComponent<MeshRenderer>();
            var sharedMaterial = firstRenderer != null ? firstRenderer.sharedMaterial : null;

            Mesh combinedMesh;

            if (removeOverlaps)
            {
                combinedMesh = CombineMeshesWithOverlapRemoval(meshFilters);
            }
            else
            {
                combinedMesh = CombineMeshesSimple(meshFilters);
            }

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

        private static Mesh CombineMeshesSimple(List<MeshFilter> meshFilters)
        {
            var combineInstances = new CombineInstance[meshFilters.Count];

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

            return combinedMesh;
        }

        private static Mesh CombineMeshesWithOverlapRemoval(List<MeshFilter> meshFilters)
        {
            // Collect all triangles from all meshes in world space
            var allTriangles = new List<Triangle>();

            foreach (var mf in meshFilters)
            {
                var mesh = mf.sharedMesh;
                var transform = mf.transform.localToWorldMatrix;

                var vertices = mesh.vertices;
                var normals = mesh.normals;
                var triangles = mesh.triangles;
                var uvs = mesh.uv;

                for (var i = 0; i < triangles.Length; i += 3)
                {
                    var v0 = transform.MultiplyPoint3x4(vertices[triangles[i]]);
                    var v1 = transform.MultiplyPoint3x4(vertices[triangles[i + 1]]);
                    var v2 = transform.MultiplyPoint3x4(vertices[triangles[i + 2]]);

                    var n0 = normals.Length > triangles[i] ? transform.MultiplyVector(normals[triangles[i]]).normalized : Vector3.up;
                    var n1 = normals.Length > triangles[i + 1] ? transform.MultiplyVector(normals[triangles[i + 1]]).normalized : Vector3.up;
                    var n2 = normals.Length > triangles[i + 2] ? transform.MultiplyVector(normals[triangles[i + 2]]).normalized : Vector3.up;

                    var uv0 = uvs.Length > triangles[i] ? uvs[triangles[i]] : Vector2.zero;
                    var uv1 = uvs.Length > triangles[i + 1] ? uvs[triangles[i + 1]] : Vector2.zero;
                    var uv2 = uvs.Length > triangles[i + 2] ? uvs[triangles[i + 2]] : Vector2.zero;

                    allTriangles.Add(new Triangle(v0, v1, v2, n0, n1, n2, uv0, uv1, uv2));
                }
            }

            // Remove overlapping triangles
            var filteredTriangles = RemoveOverlappingTriangles(allTriangles);

            // Build the final mesh
            return BuildMeshFromTriangles(filteredTriangles);
        }

        private static List<Triangle> RemoveOverlappingTriangles(List<Triangle> triangles)
        {
            var result = new List<Triangle>();
            var removed = new HashSet<int>();

            for (var i = 0; i < triangles.Count; i++)
            {
                if (removed.Contains(i))
                {
                    continue;
                }

                var triA = triangles[i];
                var isDuplicate = false;

                for (var j = i + 1; j < triangles.Count; j++)
                {
                    if (removed.Contains(j))
                    {
                        continue;
                    }

                    var triB = triangles[j];

                    if (AreTrianglesOverlapping(triA, triB))
                    {
                        // Mark the second one as removed (keep the first)
                        removed.Add(j);
                        isDuplicate = true;
                    }
                }

                if (!isDuplicate || !removed.Contains(i))
                {
                    result.Add(triA);
                }
            }

            var removedCount = triangles.Count - result.Count;

            if (removedCount > 0)
            {
                Debug.Log($"Removed {removedCount} overlapping triangles");
            }

            return result;
        }

        private static bool AreTrianglesOverlapping(Triangle a, Triangle b)
        {
            // Check if triangles are coplanar (same plane)
            var normalA = Vector3.Cross(a.V1 - a.V0, a.V2 - a.V0).normalized;
            var normalB = Vector3.Cross(b.V1 - b.V0, b.V2 - b.V0).normalized;

            // Check if normals are parallel (same or opposite direction)
            var normalDot = Mathf.Abs(Vector3.Dot(normalA, normalB));

            if (normalDot < 1f - NormalTolerance)
            {
                return false; // Not coplanar
            }

            // Check if both triangles are on the same plane
            var centerA = (a.V0 + a.V1 + a.V2) / 3f;
            var centerB = (b.V0 + b.V1 + b.V2) / 3f;
            var planeDistance = Mathf.Abs(Vector3.Dot(normalA, centerB - centerA));

            if (planeDistance > VertexTolerance)
            {
                return false; // Not on the same plane
            }

            // Check if triangles share the same vertices (within tolerance)
            var matchCount = 0;

            if (VerticesMatch(a.V0, b.V0) || VerticesMatch(a.V0, b.V1) || VerticesMatch(a.V0, b.V2))
            {
                matchCount++;
            }

            if (VerticesMatch(a.V1, b.V0) || VerticesMatch(a.V1, b.V1) || VerticesMatch(a.V1, b.V2))
            {
                matchCount++;
            }

            if (VerticesMatch(a.V2, b.V0) || VerticesMatch(a.V2, b.V1) || VerticesMatch(a.V2, b.V2))
            {
                matchCount++;
            }

            // If all 3 vertices match, triangles are identical
            if (matchCount >= 3)
            {
                return true;
            }

            // Check for 2D overlap on the plane
            if (matchCount >= 2)
            {
                // Triangles share an edge, check if they overlap
                return DoTrianglesOverlap2D(a, b, normalA);
            }

            return false;
        }

        private static bool VerticesMatch(Vector3 a, Vector3 b)
        {
            return (a - b).sqrMagnitude < VertexTolerance * VertexTolerance;
        }

        private static bool DoTrianglesOverlap2D(Triangle a, Triangle b, Vector3 planeNormal)
        {
            // Project triangles onto 2D plane and check for overlap
            Vector3 u, v;
            GetPlaneBasis(planeNormal, out u, out v);

            var a0 = ProjectToPlane(a.V0, u, v);
            var a1 = ProjectToPlane(a.V1, u, v);
            var a2 = ProjectToPlane(a.V2, u, v);

            var b0 = ProjectToPlane(b.V0, u, v);
            var b1 = ProjectToPlane(b.V1, u, v);
            var b2 = ProjectToPlane(b.V2, u, v);

            // Check if centers are very close (simple overlap check)
            var centerA = (a0 + a1 + a2) / 3f;
            var centerB = (b0 + b1 + b2) / 3f;

            return (centerA - centerB).sqrMagnitude < VertexTolerance * VertexTolerance;
        }

        private static void GetPlaneBasis(Vector3 normal, out Vector3 u, out Vector3 v)
        {
            // Create orthonormal basis for the plane
            if (Mathf.Abs(normal.x) < 0.9f)
            {
                u = Vector3.Cross(normal, Vector3.right).normalized;
            }
            else
            {
                u = Vector3.Cross(normal, Vector3.up).normalized;
            }

            v = Vector3.Cross(normal, u).normalized;
        }

        private static Vector2 ProjectToPlane(Vector3 point, Vector3 u, Vector3 v)
        {
            return new Vector2(Vector3.Dot(point, u), Vector3.Dot(point, v));
        }

        private static Mesh BuildMeshFromTriangles(List<Triangle> triangles)
        {
            var vertices = new List<Vector3>();
            var normals = new List<Vector3>();
            var uvs = new List<Vector2>();
            var indices = new List<int>();

            foreach (var tri in triangles)
            {
                var baseIndex = vertices.Count;

                vertices.Add(tri.V0);
                vertices.Add(tri.V1);
                vertices.Add(tri.V2);

                normals.Add(tri.N0);
                normals.Add(tri.N1);
                normals.Add(tri.N2);

                uvs.Add(tri.UV0);
                uvs.Add(tri.UV1);
                uvs.Add(tri.UV2);

                indices.Add(baseIndex);
                indices.Add(baseIndex + 1);
                indices.Add(baseIndex + 2);
            }

            var mesh = new Mesh();
            mesh.name = CombinedMeshName;
            mesh.SetVertices(vertices);
            mesh.SetNormals(normals);
            mesh.SetUVs(0, uvs);
            mesh.SetTriangles(indices, 0);
            mesh.RecalculateBounds();

            return mesh;
        }

        private struct Triangle
        {
            public Vector3 V0, V1, V2;
            public Vector3 N0, N1, N2;
            public Vector2 UV0, UV1, UV2;

            public Triangle(Vector3 v0, Vector3 v1, Vector3 v2, Vector3 n0, Vector3 n1, Vector3 n2, Vector2 uv0, Vector2 uv1, Vector2 uv2)
            {
                V0 = v0;
                V1 = v1;
                V2 = v2;
                N0 = n0;
                N1 = n1;
                N2 = n2;
                UV0 = uv0;
                UV1 = uv1;
                UV2 = uv2;
            }
        }
    }
}
