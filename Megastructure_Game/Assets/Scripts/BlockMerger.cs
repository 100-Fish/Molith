using System.Collections.Generic;
using UnityEngine;
using DG.Tweening;

public class BlockMerger : MonoBehaviour
{
    [HideInInspector]
    public float mergeRadius = 1.5f;

    [HideInInspector]
    public BuildingSystem buildingSystem;

    [HideInInspector]
    public float simplificationTolerance = 0.05f;

    [HideInInspector]
    public bool enableSmoothing = true;

    [HideInInspector]
    public int smoothingIterations = 2;

    private bool hasCheckedForMerge = false;

    public void CheckForMerge(HashSet<GameObject> allBlocks)
    {
        if (hasCheckedForMerge)
            return;

        hasCheckedForMerge = true;

        List<GameObject> nearbyBlocks = new List<GameObject>();

        foreach (GameObject block in allBlocks)
        {
            if (block == null || block == gameObject)
                continue;

            float distance = Vector3.Distance(transform.position, block.transform.position);
            if (distance <= mergeRadius)
            {
                nearbyBlocks.Add(block);
            }
        }

        if (nearbyBlocks.Count > 0)
        {
            MergeBlocks(nearbyBlocks);
        }
    }

    void MergeBlocks(List<GameObject> blocksToMerge)
    {
        List<GameObject> allBlocksInMerge = new List<GameObject>(blocksToMerge);
        allBlocksInMerge.Add(gameObject);

        List<Vector3> allWorldVertices = new List<Vector3>();
        List<int> allTriangles = new List<int>();
        Material sharedMaterial = null;
        int vertexOffset = 0;

        foreach (GameObject block in allBlocksInMerge)
        {
            if (block == null) continue;

            MeshFilter meshFilter = block.GetComponent<MeshFilter>();
            Renderer renderer = block.GetComponent<Renderer>();

            if (meshFilter != null)
            {
                Mesh meshToUse = meshFilter.sharedMesh != null ? meshFilter.sharedMesh : meshFilter.mesh;

                if (meshToUse != null)
                {
                    Vector3[] localVerts = meshToUse.vertices;
                    int[] localTriangles = meshToUse.triangles;
                    Vector3 blockPos = block.transform.position;
                    Quaternion blockRot = block.transform.rotation;
                    Vector3 blockScale = block.transform.lossyScale;

                    // Transform and add vertices
                    foreach (Vector3 v in localVerts)
                    {
                        Vector3 scaledVert = Vector3.Scale(v, blockScale);
                        Vector3 rotatedVert = blockRot * scaledVert;
                        Vector3 worldVert = rotatedVert + blockPos;
                        allWorldVertices.Add(worldVert);
                    }

                    // Add triangles with offset
                    foreach (int tri in localTriangles)
                    {
                        allTriangles.Add(tri + vertexOffset);
                    }

                    vertexOffset += localVerts.Length;
                }

                if (sharedMaterial == null && renderer != null)
                {
                    MeshRenderer meshRenderer = renderer as MeshRenderer;
                    if (meshRenderer != null && meshRenderer.sharedMaterials.Length > 0)
                    {
                        sharedMaterial = meshRenderer.sharedMaterials[0];
                    }
                }
            }
        }

        if (allWorldVertices.Count < 3)
            return;

        // Calculate centroid
        Vector3 centroid = Vector3.zero;
        foreach (Vector3 v in allWorldVertices)
        {
            centroid += v;
        }
        centroid /= allWorldVertices.Count;

        // Convert to local space
        List<Vector3> localVertices = new List<Vector3>();
        foreach (Vector3 v in allWorldVertices)
        {
            localVertices.Add(v - centroid);
        }

        // Create merged mesh keeping all geometry
        Mesh mergedMesh = new Mesh();
        mergedMesh.name = "MergedMesh";
        mergedMesh.vertices = localVertices.ToArray();
        mergedMesh.triangles = allTriangles.ToArray();

        if (mergedMesh == null)
            return;

        // Calculate normals before smoothing
        mergedMesh.RecalculateNormals();
        mergedMesh.RecalculateBounds();

        if (enableSmoothing && smoothingIterations > 0)
        {
            SmoothMesh(mergedMesh, smoothingIterations);
            // Recalculate after smoothing
            mergedMesh.RecalculateNormals();
            mergedMesh.RecalculateBounds();
        }

        // Generate UVs
        GenerateUVs(mergedMesh);

        GameObject mergedBlock = new GameObject("MergedBlock");
        mergedBlock.transform.position = centroid;
        mergedBlock.transform.rotation = Quaternion.identity;
        mergedBlock.transform.localScale = Vector3.one;

        MeshFilter newMeshFilter = mergedBlock.AddComponent<MeshFilter>();
        MeshRenderer newRenderer = mergedBlock.AddComponent<MeshRenderer>();
        MeshCollider newCollider = mergedBlock.AddComponent<MeshCollider>();

        newMeshFilter.mesh = mergedMesh;
        newCollider.sharedMesh = mergedMesh;
        newCollider.convex = false; // Use non-convex after smoothing

        if (sharedMaterial != null)
        {
            newRenderer.material = sharedMaterial;
        }

        Vector3 originalScale = mergedBlock.transform.localScale;
        mergedBlock.transform.localScale = originalScale * 0.8f;
        mergedBlock.transform.DOScale(originalScale, 0.3f).SetEase(Ease.OutElastic);

        if (buildingSystem != null)
        {
            foreach (GameObject block in allBlocksInMerge)
            {
                if (block != null)
                {
                    buildingSystem.RemoveBlock(block);

                    GameObject blockToDestroy = block;

                    block.transform.DOScale(Vector3.zero, 0.2f).SetEase(Ease.InBack).OnComplete(() =>
                    {
                        if (blockToDestroy != null)
                            Destroy(blockToDestroy);
                    });
                }
            }

            buildingSystem.AddBlock(mergedBlock);
        }

        BlockMerger newMerger = mergedBlock.AddComponent<BlockMerger>();
        newMerger.mergeRadius = mergeRadius;
        newMerger.buildingSystem = buildingSystem;
        newMerger.simplificationTolerance = simplificationTolerance;
        newMerger.enableSmoothing = enableSmoothing;
        newMerger.smoothingIterations = smoothingIterations;
        newMerger.hasCheckedForMerge = true;
    }

    List<Vector3> MergeCloseVertices(List<Vector3> vertices, float tolerance)
    {
        List<Vector3> result = new List<Vector3>();

        foreach (Vector3 v in vertices)
        {
            bool found = false;
            for (int i = 0; i < result.Count; i++)
            {
                if (Vector3.Distance(v, result[i]) < tolerance)
                {
                    result[i] = (result[i] + v) * 0.5f;
                    found = true;
                    break;
                }
            }
            if (!found)
            {
                result.Add(v);
            }
        }

        return result;
    }

    Mesh CreateConvexHullMesh(List<Vector3> points)
    {
        if (points.Count < 4)
            return null;

        List<int> hullIndices = ComputeConvexHull3D(points);

        if (hullIndices.Count < 3)
            return null;

        Mesh mesh = new Mesh();
        mesh.name = "MergedHullMesh";
        mesh.vertices = points.ToArray();
        mesh.triangles = hullIndices.ToArray();

        return mesh;
    }

    List<int> ComputeConvexHull3D(List<Vector3> points)
    {
        List<HullFace> faces = new List<HullFace>();
        List<int> resultTriangles = new List<int>();

        if (points.Count < 4)
            return resultTriangles;

        int p0 = 0, p1 = 1, p2 = -1, p3 = -1;

        for (int i = 2; i < points.Count; i++)
        {
            Vector3 v1 = points[p1] - points[p0];
            Vector3 v2 = points[i] - points[p0];
            if (Vector3.Cross(v1, v2).sqrMagnitude > 0.0001f)
            {
                p2 = i;
                break;
            }
        }

        if (p2 == -1)
            return resultTriangles;

        Vector3 normal = Vector3.Cross(points[p1] - points[p0], points[p2] - points[p0]).normalized;

        for (int i = 0; i < points.Count; i++)
        {
            if (i == p0 || i == p1 || i == p2)
                continue;

            float dist = Vector3.Dot(points[i] - points[p0], normal);
            if (Mathf.Abs(dist) > 0.0001f)
            {
                p3 = i;
                if (dist < 0)
                {
                    int temp = p1;
                    p1 = p2;
                    p2 = temp;
                }
                break;
            }
        }

        if (p3 == -1)
        {
            return Create2DHullMesh(points, p0, p1, p2, normal);
        }

        faces.Add(new HullFace(p0, p1, p2, points));
        faces.Add(new HullFace(p0, p2, p3, points));
        faces.Add(new HullFace(p0, p3, p1, points));
        faces.Add(new HullFace(p1, p3, p2, points));

        for (int i = 0; i < points.Count; i++)
        {
            if (i == p0 || i == p1 || i == p2 || i == p3)
                continue;

            List<HullFace> visibleFaces = new List<HullFace>();

            foreach (HullFace face in faces)
            {
                if (face.IsPointAbove(points[i], points))
                {
                    visibleFaces.Add(face);
                }
            }

            if (visibleFaces.Count == 0)
                continue;

            List<HullEdge> horizon = new List<HullEdge>();

            foreach (HullFace face in visibleFaces)
            {
                HullEdge[] edges = face.GetEdges();
                foreach (HullEdge edge in edges)
                {
                    bool isShared = false;
                    foreach (HullFace otherFace in visibleFaces)
                    {
                        if (otherFace == face)
                            continue;
                        if (otherFace.HasEdge(edge))
                        {
                            isShared = true;
                            break;
                        }
                    }
                    if (!isShared)
                    {
                        horizon.Add(edge);
                    }
                }
            }

            foreach (HullFace face in visibleFaces)
            {
                faces.Remove(face);
            }

            foreach (HullEdge edge in horizon)
            {
                faces.Add(new HullFace(edge.v0, edge.v1, i, points));
            }
        }

        foreach (HullFace face in faces)
        {
            resultTriangles.Add(face.i0);
            resultTriangles.Add(face.i1);
            resultTriangles.Add(face.i2);
        }

        return resultTriangles;
    }

    List<int> Create2DHullMesh(List<Vector3> points, int p0, int p1, int p2, Vector3 normal)
    {
        List<int> result = new List<int>();

        Vector3 right = (points[p1] - points[p0]).normalized;
        Vector3 up = Vector3.Cross(normal, right).normalized;
        Vector3 origin = points[p0];

        List<Vector2> points2D = new List<Vector2>();
        for (int i = 0; i < points.Count; i++)
        {
            Vector3 rel = points[i] - origin;
            points2D.Add(new Vector2(Vector3.Dot(rel, right), Vector3.Dot(rel, up)));
        }

        List<int> hull2D = ComputeConvexHull2D(points2D);

        if (hull2D.Count < 3)
            return result;

        for (int i = 1; i < hull2D.Count - 1; i++)
        {
            result.Add(hull2D[0]);
            result.Add(hull2D[i]);
            result.Add(hull2D[i + 1]);
        }

        int triCount = result.Count;
        for (int i = 0; i < triCount; i += 3)
        {
            result.Add(result[i]);
            result.Add(result[i + 2]);
            result.Add(result[i + 1]);
        }

        return result;
    }

    List<int> ComputeConvexHull2D(List<Vector2> points)
    {
        List<int> hull = new List<int>();

        if (points.Count < 3)
            return hull;

        int startIdx = 0;
        for (int i = 1; i < points.Count; i++)
        {
            if (points[i].x < points[startIdx].x ||
                (points[i].x == points[startIdx].x && points[i].y < points[startIdx].y))
            {
                startIdx = i;
            }
        }

        int current = startIdx;
        do
        {
            hull.Add(current);
            int next = 0;

            for (int i = 0; i < points.Count; i++)
            {
                if (i == current)
                    continue;

                if (next == current)
                {
                    next = i;
                    continue;
                }

                float cross = Cross2D(points[current], points[next], points[i]);

                if (cross > 0 || (cross == 0 && Vector2.Distance(points[current], points[i]) >
                    Vector2.Distance(points[current], points[next])))
                {
                    next = i;
                }
            }

            current = next;

        } while (current != startIdx && hull.Count < points.Count);

        return hull;
    }

    float Cross2D(Vector2 o, Vector2 a, Vector2 b)
    {
        return (a.x - o.x) * (b.y - o.y) - (a.y - o.y) * (b.x - o.x);
    }

    void SmoothMesh(Mesh mesh, int iterations)
    {
        Vector3[] vertices = mesh.vertices;
        int[] triangles = mesh.triangles;

        Dictionary<int, List<int>> vertexNeighbors = new Dictionary<int, List<int>>();
        for (int i = 0; i < vertices.Length; i++)
        {
            vertexNeighbors[i] = new List<int>();
        }

        for (int i = 0; i < triangles.Length; i += 3)
        {
            int v0 = triangles[i];
            int v1 = triangles[i + 1];
            int v2 = triangles[i + 2];

            if (!vertexNeighbors[v0].Contains(v1)) vertexNeighbors[v0].Add(v1);
            if (!vertexNeighbors[v0].Contains(v2)) vertexNeighbors[v0].Add(v2);

            if (!vertexNeighbors[v1].Contains(v0)) vertexNeighbors[v1].Add(v0);
            if (!vertexNeighbors[v1].Contains(v2)) vertexNeighbors[v1].Add(v2);

            if (!vertexNeighbors[v2].Contains(v0)) vertexNeighbors[v2].Add(v0);
            if (!vertexNeighbors[v2].Contains(v1)) vertexNeighbors[v2].Add(v1);
        }

        for (int iteration = 0; iteration < iterations; iteration++)
        {
            Vector3[] smoothedVertices = new Vector3[vertices.Length];

            for (int i = 0; i < vertices.Length; i++)
            {
                if (vertexNeighbors[i].Count == 0)
                {
                    smoothedVertices[i] = vertices[i];
                    continue;
                }

                Vector3 average = Vector3.zero;
                foreach (int neighborIndex in vertexNeighbors[i])
                {
                    average += vertices[neighborIndex];
                }
                average /= vertexNeighbors[i].Count;

                smoothedVertices[i] = Vector3.Lerp(vertices[i], average, 0.5f);
            }

            vertices = smoothedVertices;
        }

        mesh.vertices = vertices;
    }

    private class HullFace
    {
        public int i0, i1, i2;
        public Vector3 normal;
        public float d;

        public HullFace(int a, int b, int c, List<Vector3> points)
        {
            i0 = a;
            i1 = b;
            i2 = c;

            Vector3 v0 = points[a];
            Vector3 v1 = points[b];
            Vector3 v2 = points[c];

            normal = Vector3.Cross(v1 - v0, v2 - v0).normalized;
            d = -Vector3.Dot(normal, v0);
        }

        public bool IsPointAbove(Vector3 point, List<Vector3> points)
        {
            float dist = Vector3.Dot(normal, point) + d;
            return dist > 0.0001f;
        }

        public HullEdge[] GetEdges()
        {
            return new HullEdge[]
            {
                new HullEdge(i0, i1),
                new HullEdge(i1, i2),
                new HullEdge(i2, i0)
            };
        }

        public bool HasEdge(HullEdge edge)
        {
            return (i0 == edge.v0 && i1 == edge.v1) || (i0 == edge.v1 && i1 == edge.v0) ||
                   (i1 == edge.v0 && i2 == edge.v1) || (i1 == edge.v1 && i2 == edge.v0) ||
                   (i2 == edge.v0 && i0 == edge.v1) || (i2 == edge.v1 && i0 == edge.v0);
        }
    }

    private struct HullEdge
    {
        public int v0, v1;

        public HullEdge(int a, int b)
        {
            v0 = a;
            v1 = b;
        }
    }

    void GenerateUVs(Mesh mesh)
    {
        Vector3[] vertices = mesh.vertices;
        Vector2[] uvs = new Vector2[vertices.Length];

        // Simple planar UV mapping based on vertex positions
        Bounds bounds = mesh.bounds;

        for (int i = 0; i < vertices.Length; i++)
        {
            Vector3 v = vertices[i];

            // Use XZ plane for UV mapping
            float u = (v.x - bounds.min.x) / (bounds.size.x + 0.001f);
            float v2 = (v.z - bounds.min.z) / (bounds.size.z + 0.001f);

            uvs[i] = new Vector2(u, v2);
        }

        mesh.uv = uvs;
    }
}