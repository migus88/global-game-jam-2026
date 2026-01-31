using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace Game.Editor
{
    public class MeshCombiner : EditorWindow
    {
        private const string CombinedMeshName = "CombinedMesh";
        private const float Tolerance = 0.001f;

        private enum CombineMode
        {
            Simple,
            MergeCoplanarPlanes
        }

        private CombineMode _combineMode = CombineMode.MergeCoplanarPlanes;

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
                "Select multiple GameObjects with MeshFilter components (quads/planes), then click Combine.\n\n" +
                "Merge Coplanar Planes: Merges overlapping coplanar quads into a single unified shape.",
                MessageType.Info);

            EditorGUILayout.Space();

            _combineMode = (CombineMode)EditorGUILayout.EnumPopup("Combine Mode", _combineMode);

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

        private void CombineSelectedMeshes()
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

            switch (_combineMode)
            {
                case CombineMode.MergeCoplanarPlanes:
                    combinedMesh = MergeCoplanarPlanes(meshFilters);
                    break;
                default:
                    combinedMesh = CombineMeshesSimple(meshFilters);
                    break;
            }

            if (combinedMesh == null)
            {
                EditorUtility.DisplayDialog("Mesh Combiner", "Failed to combine meshes.", "OK");
                return;
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

        private static Mesh MergeCoplanarPlanes(List<MeshFilter> meshFilters)
        {
            // Extract quads from meshes (assuming each mesh is a quad/plane)
            var allQuads = new List<Quad>();

            foreach (var mf in meshFilters)
            {
                var quad = ExtractQuadFromMesh(mf);

                if (quad != null)
                {
                    allQuads.Add(quad);
                }
            }

            if (allQuads.Count < 2)
            {
                Debug.LogWarning("Need at least 2 valid quads to merge");
                return CombineMeshesSimple(meshFilters);
            }

            // Group quads by plane (coplanar quads)
            var planeGroups = GroupByPlane(allQuads);

            var allVertices = new List<Vector3>();
            var allNormals = new List<Vector3>();
            var allUvs = new List<Vector2>();
            var allIndices = new List<int>();

            foreach (var group in planeGroups)
            {
                if (group.Count == 1)
                {
                    // Single quad, just add it
                    AddQuadToMesh(group[0], allVertices, allNormals, allUvs, allIndices);
                }
                else
                {
                    // Multiple coplanar quads - merge them
                    var mergedPolygon = MergeCoplanarQuads(group);
                    AddPolygonToMesh(mergedPolygon, group[0].Normal, allVertices, allNormals, allUvs, allIndices);
                }
            }

            var mesh = new Mesh();
            mesh.name = CombinedMeshName;
            mesh.SetVertices(allVertices);
            mesh.SetNormals(allNormals);
            mesh.SetUVs(0, allUvs);
            mesh.SetTriangles(allIndices, 0);
            mesh.RecalculateBounds();

            return mesh;
        }

        private static Quad ExtractQuadFromMesh(MeshFilter mf)
        {
            var mesh = mf.sharedMesh;
            var transform = mf.transform.localToWorldMatrix;

            var vertices = mesh.vertices;

            if (vertices.Length < 3)
            {
                return null;
            }

            // Transform vertices to world space
            var worldVerts = new List<Vector3>();

            foreach (var v in vertices)
            {
                worldVerts.Add(transform.MultiplyPoint3x4(v));
            }

            // Get unique vertices (remove duplicates from triangle mesh)
            var uniqueVerts = GetUniqueVertices(worldVerts);

            if (uniqueVerts.Count < 3)
            {
                return null;
            }

            // Calculate normal
            var normal = Vector3.Cross(uniqueVerts[1] - uniqueVerts[0], uniqueVerts[2] - uniqueVerts[0]).normalized;

            // Sort vertices in winding order
            var sortedVerts = SortVerticesInWindingOrder(uniqueVerts, normal);

            return new Quad
            {
                Vertices = sortedVerts,
                Normal = normal
            };
        }

        private static List<Vector3> GetUniqueVertices(List<Vector3> vertices)
        {
            var unique = new List<Vector3>();

            foreach (var v in vertices)
            {
                var isDuplicate = false;

                foreach (var u in unique)
                {
                    if ((v - u).sqrMagnitude < Tolerance * Tolerance)
                    {
                        isDuplicate = true;
                        break;
                    }
                }

                if (!isDuplicate)
                {
                    unique.Add(v);
                }
            }

            return unique;
        }

        private static List<Vector3> SortVerticesInWindingOrder(List<Vector3> vertices, Vector3 normal)
        {
            if (vertices.Count < 3)
            {
                return vertices;
            }

            // Calculate center
            var center = Vector3.zero;

            foreach (var v1 in vertices)
            {
                center += v1;
            }

            center /= vertices.Count;

            // Create basis vectors for the plane
            GetPlaneBasis(normal, out var u, out var v);

            // Sort by angle around center
            var sorted = vertices.OrderBy(vert =>
            {
                var dir = vert - center;
                var x = Vector3.Dot(dir, u);
                var y = Vector3.Dot(dir, v);
                return Mathf.Atan2(y, x);
            }).ToList();

            return sorted;
        }

        private static List<List<Quad>> GroupByPlane(List<Quad> quads)
        {
            var groups = new List<List<Quad>>();
            var assigned = new bool[quads.Count];

            for (var i = 0; i < quads.Count; i++)
            {
                if (assigned[i])
                {
                    continue;
                }

                var group = new List<Quad> { quads[i] };
                assigned[i] = true;

                for (var j = i + 1; j < quads.Count; j++)
                {
                    if (assigned[j])
                    {
                        continue;
                    }

                    if (AreCoplanar(quads[i], quads[j]))
                    {
                        group.Add(quads[j]);
                        assigned[j] = true;
                    }
                }

                groups.Add(group);
            }

            return groups;
        }

        private static bool AreCoplanar(Quad a, Quad b)
        {
            // Check if normals are parallel
            var normalDot = Mathf.Abs(Vector3.Dot(a.Normal, b.Normal));

            if (normalDot < 1f - Tolerance)
            {
                return false;
            }

            // Check if on same plane (distance from a's plane to b's center is ~0)
            var centerA = GetCenter(a.Vertices);
            var centerB = GetCenter(b.Vertices);
            var planeDistance = Mathf.Abs(Vector3.Dot(a.Normal, centerB - centerA));

            return planeDistance < Tolerance;
        }

        private static Vector3 GetCenter(List<Vector3> vertices)
        {
            var center = Vector3.zero;

            foreach (var v in vertices)
            {
                center += v;
            }

            return center / vertices.Count;
        }

        private static List<Vector3> MergeCoplanarQuads(List<Quad> quads)
        {
            if (quads.Count == 0)
            {
                return new List<Vector3>();
            }

            var normal = quads[0].Normal;
            GetPlaneBasis(normal, out var u, out var v);

            // Project all quads to 2D
            var polygons2D = new List<List<Vector2>>();

            foreach (var quad in quads)
            {
                var poly2D = new List<Vector2>();

                foreach (var vert in quad.Vertices)
                {
                    poly2D.Add(ProjectToPlane(vert, u, v));
                }

                polygons2D.Add(poly2D);
            }

            // Compute union of all 2D polygons
            var unionPolygon = ComputePolygonUnion(polygons2D);

            // Project back to 3D
            var planeOrigin = GetCenter(quads[0].Vertices);
            var result = new List<Vector3>();

            foreach (var p in unionPolygon)
            {
                var point3D = planeOrigin + u * p.x + v * p.y;

                // Adjust to be on the actual plane
                var offset = Vector3.Dot(point3D - planeOrigin, normal);
                point3D -= normal * offset;

                result.Add(point3D);
            }

            return result;
        }

        private static List<Vector2> ComputePolygonUnion(List<List<Vector2>> polygons)
        {
            if (polygons.Count == 0)
            {
                return new List<Vector2>();
            }

            // Start with the first polygon
            var result = polygons[0];

            // Union with each subsequent polygon
            for (var i = 1; i < polygons.Count; i++)
            {
                result = UnionTwoPolygons(result, polygons[i]);
            }

            return result;
        }

        private static List<Vector2> UnionTwoPolygons(List<Vector2> polyA, List<Vector2> polyB)
        {
            // Simple convex hull union - works for overlapping convex shapes
            // For more complex cases, would need a proper polygon clipping library

            var allPoints = new List<Vector2>();
            allPoints.AddRange(polyA);
            allPoints.AddRange(polyB);

            // Compute convex hull of all points
            return ComputeConvexHull(allPoints);
        }

        private static List<Vector2> ComputeConvexHull(List<Vector2> points)
        {
            if (points.Count < 3)
            {
                return points;
            }

            // Find the lowest point (and leftmost if tied)
            var start = 0;

            for (var i = 1; i < points.Count; i++)
            {
                if (points[i].y < points[start].y ||
                    (Mathf.Approximately(points[i].y, points[start].y) && points[i].x < points[start].x))
                {
                    start = i;
                }
            }

            // Sort points by polar angle with respect to start point
            var startPoint = points[start];
            var sortedPoints = points.OrderBy(p =>
            {
                if (p == startPoint)
                {
                    return -Mathf.PI;
                }

                return Mathf.Atan2(p.y - startPoint.y, p.x - startPoint.x);
            }).ThenBy(p => (p - startPoint).sqrMagnitude).ToList();

            // Graham scan
            var hull = new List<Vector2>();

            foreach (var point in sortedPoints)
            {
                while (hull.Count > 1 && Cross(hull[hull.Count - 2], hull[hull.Count - 1], point) <= 0)
                {
                    hull.RemoveAt(hull.Count - 1);
                }

                hull.Add(point);
            }

            return hull;
        }

        private static float Cross(Vector2 o, Vector2 a, Vector2 b)
        {
            return (a.x - o.x) * (b.y - o.y) - (a.y - o.y) * (b.x - o.x);
        }

        private static void GetPlaneBasis(Vector3 normal, out Vector3 u, out Vector3 v)
        {
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

        private static void AddQuadToMesh(Quad quad, List<Vector3> vertices, List<Vector3> normals, List<Vector2> uvs, List<int> indices)
        {
            AddPolygonToMesh(quad.Vertices, quad.Normal, vertices, normals, uvs, indices);
        }

        private static void AddPolygonToMesh(List<Vector3> polygon, Vector3 normal, List<Vector3> vertices, List<Vector3> normals, List<Vector2> uvs, List<int> indices)
        {
            if (polygon.Count < 3)
            {
                return;
            }

            var baseIndex = vertices.Count;

            // Calculate UV bounds
            GetPlaneBasis(normal, out var u, out var v);
            var minU = float.MaxValue;
            var minV = float.MaxValue;
            var maxU = float.MinValue;
            var maxV = float.MinValue;

            foreach (var vert in polygon)
            {
                var projU = Vector3.Dot(vert, u);
                var projV = Vector3.Dot(vert, v);
                minU = Mathf.Min(minU, projU);
                minV = Mathf.Min(minV, projV);
                maxU = Mathf.Max(maxU, projU);
                maxV = Mathf.Max(maxV, projV);
            }

            var rangeU = maxU - minU;
            var rangeV = maxV - minV;

            if (rangeU < Tolerance)
            {
                rangeU = 1f;
            }

            if (rangeV < Tolerance)
            {
                rangeV = 1f;
            }

            // Add vertices
            foreach (var vert in polygon)
            {
                vertices.Add(vert);
                normals.Add(normal);

                // Calculate UV
                var projU = Vector3.Dot(vert, u);
                var projV = Vector3.Dot(vert, v);
                uvs.Add(new Vector2((projU - minU) / rangeU, (projV - minV) / rangeV));
            }

            // Triangulate (fan triangulation for convex polygons)
            for (var i = 1; i < polygon.Count - 1; i++)
            {
                indices.Add(baseIndex);
                indices.Add(baseIndex + i);
                indices.Add(baseIndex + i + 1);
            }
        }

        private class Quad
        {
            public List<Vector3> Vertices;
            public Vector3 Normal;
        }
    }
}
