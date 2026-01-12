using System.Collections.Generic;
using UnityEngine;

public class RoadMeshGenerator : MonoBehaviour
{
    [Header("Road Settings")]
    [Tooltip("Fixed width for all roads")]
    public float roadWidth = 1.0f;

    [Tooltip("Depth (thickness) of road platforms - 0.25 per unit width")]
    public float roadDepth = 0.25f;

    [Tooltip("Number of subdivisions per unit length along spline")]
    public int segmentsPerUnit = 4;

    [Tooltip("Default material for roads")]
    public Material defaultRoadMaterial;

    /// <summary>
    /// Generates a road mesh from a list of spline control points
    /// </summary>
    public Mesh GenerateRoadMesh(List<Vector3> splinePoints)
    {
        if (splinePoints == null || splinePoints.Count < 2)
        {
            // For single point, create a small square platform
            if (splinePoints != null && splinePoints.Count == 1)
                return CreateSinglePointMesh(splinePoints[0]);

            return new Mesh();
        }

        Mesh mesh = new Mesh();
        mesh.name = "Road Mesh";

        // 1. Generate interpolated points along Catmull-Rom spline
        List<Vector3> interpolatedPoints = InterpolateCatmullRomSpline(splinePoints);

        // 2. Generate vertices and triangles for road surface
        GenerateRoadGeometry(interpolatedPoints, mesh);

        // 3. Finalize mesh
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();

        return mesh;
    }

    /// <summary>
    /// Creates a small cubic platform mesh for a single point
    /// </summary>
    private Mesh CreateSinglePointMesh(Vector3 point)
    {
        Mesh mesh = new Mesh();
        mesh.name = "Single Point Mesh";

        float halfWidth = roadWidth * 0.5f;
        float halfDepth = roadDepth * 0.5f;

        // Create a box (8 vertices for a cube)
        Vector3[] vertices = new Vector3[8]
        {
            // Top face (y = +halfDepth)
            point + new Vector3(-halfWidth, halfDepth, -halfWidth),  // 0
            point + new Vector3(halfWidth, halfDepth, -halfWidth),   // 1
            point + new Vector3(halfWidth, halfDepth, halfWidth),    // 2
            point + new Vector3(-halfWidth, halfDepth, halfWidth),   // 3
            // Bottom face (y = -halfDepth)
            point + new Vector3(-halfWidth, -halfDepth, -halfWidth), // 4
            point + new Vector3(halfWidth, -halfDepth, -halfWidth),  // 5
            point + new Vector3(halfWidth, -halfDepth, halfWidth),   // 6
            point + new Vector3(-halfWidth, -halfDepth, halfWidth)   // 7
        };

        // 6 faces, 2 triangles each = 36 indices
        int[] triangles = new int[36]
        {
            // Top face
            0, 2, 1, 0, 3, 2,
            // Bottom face
            4, 5, 6, 4, 6, 7,
            // Front face
            0, 1, 5, 0, 5, 4,
            // Back face
            2, 3, 7, 2, 7, 6,
            // Left face
            3, 0, 4, 3, 4, 7,
            // Right face
            1, 2, 6, 1, 6, 5
        };

        Vector2[] uvs = new Vector2[8]
        {
            new Vector2(0, 0),
            new Vector2(1, 0),
            new Vector2(1, 1),
            new Vector2(0, 1),
            new Vector2(0, 0),
            new Vector2(1, 0),
            new Vector2(1, 1),
            new Vector2(0, 1)
        };

        mesh.vertices = vertices;
        mesh.triangles = triangles;
        mesh.uv = uvs;
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();

        return mesh;
    }

    /// <summary>
    /// Interpolates points along a Catmull-Rom spline through the control points
    /// </summary>
    private List<Vector3> InterpolateCatmullRomSpline(List<Vector3> controlPoints)
    {
        List<Vector3> result = new List<Vector3>();

        if (controlPoints.Count < 2)
        {
            result.AddRange(controlPoints);
            return result;
        }

        // For each segment between control points
        for (int i = 0; i < controlPoints.Count - 1; i++)
        {
            // Get 4 points for Catmull-Rom (P0, P1, P2, P3)
            // P0 and P3 are used to control curve shape
            // The curve actually passes through P1 and P2
            Vector3 p0 = (i == 0) ? controlPoints[i] : controlPoints[i - 1];
            Vector3 p1 = controlPoints[i];
            Vector3 p2 = controlPoints[i + 1];
            Vector3 p3 = (i + 2 < controlPoints.Count) ? controlPoints[i + 2] : controlPoints[i + 1];

            // Calculate segment length to determine subdivision
            float segmentLength = Vector3.Distance(p1, p2);
            int subdivisions = Mathf.Max(2, Mathf.CeilToInt(segmentLength * segmentsPerUnit));

            // Interpolate along this segment
            for (int j = 0; j < subdivisions; j++)
            {
                float t = j / (float)subdivisions;
                Vector3 point = CatmullRomPoint(p0, p1, p2, p3, t);
                result.Add(point);
            }
        }

        // Add final point
        result.Add(controlPoints[controlPoints.Count - 1]);

        return result;
    }

    /// <summary>
    /// Calculates a point on a Catmull-Rom spline given 4 control points and parameter t
    /// </summary>
    private Vector3 CatmullRomPoint(Vector3 p0, Vector3 p1, Vector3 p2, Vector3 p3, float t)
    {
        // Catmull-Rom formula with tension = 0.5
        float t2 = t * t;
        float t3 = t2 * t;

        Vector3 result = 0.5f * (
            (2f * p1) +
            (-p0 + p2) * t +
            (2f * p0 - 5f * p1 + 4f * p2 - p3) * t2 +
            (-p0 + 3f * p1 - 3f * p2 + p3) * t3
        );

        return result;
    }

    /// <summary>
    /// Generates road geometry (vertices, triangles, UVs) from interpolated spline points
    /// Creates a 3D platform with depth (like the original block platforms)
    /// </summary>
    private void GenerateRoadGeometry(List<Vector3> splinePoints, Mesh mesh)
    {
        int pointCount = splinePoints.Count;
        if (pointCount < 2) return;

        float halfWidth = roadWidth * 0.5f;
        float halfDepth = roadDepth * 0.5f;

        // 4 vertices per cross-section (top-left, top-right, bottom-left, bottom-right)
        Vector3[] vertices = new Vector3[pointCount * 4];
        Vector2[] uvs = new Vector2[vertices.Length];

        // Calculate perpendicular offsets for road width
        for (int i = 0; i < pointCount; i++)
        {
            Vector3 forward;

            // Calculate tangent direction
            if (i == 0)
                forward = (splinePoints[1] - splinePoints[0]).normalized;
            else if (i == pointCount - 1)
                forward = (splinePoints[i] - splinePoints[i - 1]).normalized;
            else
                forward = (splinePoints[i + 1] - splinePoints[i - 1]).normalized;

            // Calculate perpendicular direction (right vector)
            Vector3 right;
            if (Mathf.Abs(Vector3.Dot(forward, Vector3.up)) > 0.99f)
            {
                right = Vector3.Cross(forward, Vector3.right).normalized;
            }
            else
            {
                right = Vector3.Cross(Vector3.up, forward).normalized;
            }

            // Calculate up vector
            Vector3 up = Vector3.Cross(right, forward).normalized;

            // Place 4 vertices per cross-section (forming a rectangular cross-section)
            int baseIdx = i * 4;
            Vector3 center = splinePoints[i];

            vertices[baseIdx + 0] = center - right * halfWidth + up * halfDepth;  // Top-left
            vertices[baseIdx + 1] = center + right * halfWidth + up * halfDepth;  // Top-right
            vertices[baseIdx + 2] = center - right * halfWidth - up * halfDepth;  // Bottom-left
            vertices[baseIdx + 3] = center + right * halfWidth - up * halfDepth;  // Bottom-right

            // UV mapping
            float vCoord = i / (float)(pointCount - 1);
            uvs[baseIdx + 0] = new Vector2(0, vCoord);
            uvs[baseIdx + 1] = new Vector2(1, vCoord);
            uvs[baseIdx + 2] = new Vector2(0, vCoord);
            uvs[baseIdx + 3] = new Vector2(1, vCoord);
        }

        // Generate triangles for all 5 faces (top, bottom, left, right, front/back are caps)
        List<int> triangleList = new List<int>();

        for (int i = 0; i < pointCount - 1; i++)
        {
            int curr = i * 4;
            int next = (i + 1) * 4;

            // Top face (two triangles)
            triangleList.Add(curr + 0);
            triangleList.Add(next + 0);
            triangleList.Add(curr + 1);

            triangleList.Add(curr + 1);
            triangleList.Add(next + 0);
            triangleList.Add(next + 1);

            // Bottom face (two triangles)
            triangleList.Add(curr + 2);
            triangleList.Add(curr + 3);
            triangleList.Add(next + 2);

            triangleList.Add(curr + 3);
            triangleList.Add(next + 3);
            triangleList.Add(next + 2);

            // Left side face
            triangleList.Add(curr + 0);
            triangleList.Add(curr + 2);
            triangleList.Add(next + 0);

            triangleList.Add(curr + 2);
            triangleList.Add(next + 2);
            triangleList.Add(next + 0);

            // Right side face
            triangleList.Add(curr + 1);
            triangleList.Add(next + 1);
            triangleList.Add(curr + 3);

            triangleList.Add(curr + 3);
            triangleList.Add(next + 1);
            triangleList.Add(next + 3);
        }

        // Add front cap (first cross-section)
        triangleList.Add(0);
        triangleList.Add(1);
        triangleList.Add(2);

        triangleList.Add(1);
        triangleList.Add(3);
        triangleList.Add(2);

        // Add back cap (last cross-section)
        int lastIdx = (pointCount - 1) * 4;
        triangleList.Add(lastIdx + 0);
        triangleList.Add(lastIdx + 2);
        triangleList.Add(lastIdx + 1);

        triangleList.Add(lastIdx + 1);
        triangleList.Add(lastIdx + 2);
        triangleList.Add(lastIdx + 3);

        mesh.vertices = vertices;
        mesh.triangles = triangleList.ToArray();
        mesh.uv = uvs;
    }
}
