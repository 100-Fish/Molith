using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class WorldGenerator : MonoBehaviour
{
    [Header("Generation Settings")]
    [SerializeField] private GameObject structurePrefab;
    [SerializeField] private int numberOfCircles = 5;
    [SerializeField] private float baseRadius = 10f;
    [SerializeField] private float radiusExponent = 1.5f;

    [Header("Structure Scaling")]
    [SerializeField] private float baseScale = 1f;
    [SerializeField] private float scaleGrowthRate = 1.2f;

    [Header("Terrain")]
    [SerializeField] private MeshFilter groundPlaneMeshFilter;
    [SerializeField] private float noiseScale = 0.1f;
    [SerializeField] private float noiseStrength = 0.5f;

    [Header("Debug")]
    [SerializeField] private bool generateOnStart = true;
    [SerializeField] private bool drawGizmos = true;

    private List<Vector3> generatedPositions = new List<Vector3>();
    private List<float> generatedScales = new List<float>();

    void Start()
    {
        if (generateOnStart)
        {
            GenerateWorld();
        }
    }

    public void GenerateWorld()
    {
        generatedPositions.Clear();
        generatedScales.Clear();

        // Generate terrain topology first
        if (groundPlaneMeshFilter != null)
        {
            GenerateTerrainTopology();
        }

        for (int circleIndex = 0; circleIndex < numberOfCircles; circleIndex++)
        {
            // Calculate radius: exponentially increasing
            float radius = baseRadius * Mathf.Pow(radiusExponent, circleIndex);

            // Calculate scale: increases with distance
            float scale = baseScale * Mathf.Pow(scaleGrowthRate, circleIndex);

            // Calculate number of structures: 3, 5, 7, 9, etc.
            int structureCount = 3 + (circleIndex * 2);

            // Place structures evenly around the circle
            for (int i = 0; i < structureCount; i++)
            {
                // Calculate angle for this structure
                float angle = (360f / structureCount) * i;
                float angleRad = angle * Mathf.Deg2Rad;

                // Calculate position on circle (using XZ plane)
                Vector3 position = new Vector3(
                    Mathf.Cos(angleRad) * radius,
                    0f,
                    Mathf.Sin(angleRad) * radius
                );

                generatedPositions.Add(position);
                generatedScales.Add(scale);

                // Spawn structure if prefab is assigned
                if (structurePrefab != null)
                {
                    float randomScale = Random.Range(0.25f, 2f) * scale;
                    GameObject structure = Instantiate(structurePrefab, position, Random.rotation, transform);
                    structure.transform.localScale = randomScale * Vector3.one;


                    // Adjust Y position so bottom touches the ground
                    Bounds bounds = GetTotalBounds(structure);
                    if (bounds.size.magnitude > 0)
                    {
                        float yOffset = -bounds.min.y;
                        structure.transform.position += Vector3.up * yOffset;
                    }
                    structure.transform.position -= Vector3.up * Random.Range(2f, 10f);
                }
            }
        }

        Debug.Log($"Generated {generatedPositions.Count} structures across {numberOfCircles} circles");
    }

    private void GenerateTerrainTopology()
    {
        // Create a new mesh instance to avoid modifying the original asset
        Mesh originalMesh = groundPlaneMeshFilter.sharedMesh;
        Mesh mesh = new Mesh();
        mesh.name = "Generated Terrain";

        // Copy original mesh data
        mesh.vertices = originalMesh.vertices;
        mesh.triangles = originalMesh.triangles;
        mesh.uv = originalMesh.uv;

        // Now modify the vertices
        Vector3[] vertices = mesh.vertices;

        for (int i = 0; i < vertices.Length; i++)
        {
            // Get world position for noise sampling
            Vector3 worldPos = groundPlaneMeshFilter.transform.TransformPoint(vertices[i]);

            // Apply Perlin noise to vertex Y position
            float noiseValue = Mathf.PerlinNoise(
                worldPos.x * noiseScale,
                worldPos.z * noiseScale
            );

            // Map noise from [0,1] to [-1,1] range and apply strength
            vertices[i].y = (noiseValue * 2f - 1f) * noiseStrength;
        }

        mesh.vertices = vertices;
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();

        // Assign the new mesh
        groundPlaneMeshFilter.mesh = mesh;

        // Update mesh collider if present
        MeshCollider meshCollider = groundPlaneMeshFilter.GetComponent<MeshCollider>();
        if (meshCollider != null)
        {
            meshCollider.sharedMesh = mesh;
        }

        Debug.Log($"Generated terrain topology with {vertices.Length} vertices");
    }

    private Bounds GetTotalBounds(GameObject obj)
    {
        Renderer[] renderers = obj.GetComponentsInChildren<Renderer>();
        if (renderers.Length == 0)
            return new Bounds(obj.transform.position, Vector3.zero);

        Bounds bounds = renderers[0].bounds;
        for (int i = 1; i < renderers.Length; i++)
        {
            bounds.Encapsulate(renderers[i].bounds);
        }
        return bounds;
    }

    void OnDrawGizmos()
    {
        if (!drawGizmos) return;

        // Draw circles and structure positions
        for (int circleIndex = 0; circleIndex < numberOfCircles; circleIndex++)
        {
            float radius = baseRadius * Mathf.Pow(radiusExponent, circleIndex);
            float scale = baseScale * Mathf.Pow(scaleGrowthRate, circleIndex);
            int structureCount = 3 + (circleIndex * 2);

            // Draw circle
            Gizmos.color = new Color(1f, 0f, 0f, 0.3f);
            DrawCircle(Vector3.zero, radius, 64);

            // Draw structure positions with scaled spheres
            Gizmos.color = Color.yellow;
            for (int i = 0; i < structureCount; i++)
            {
                float angle = (360f / structureCount) * i;
                float angleRad = angle * Mathf.Deg2Rad;

                Vector3 position = new Vector3(
                    Mathf.Cos(angleRad) * radius,
                    0f,
                    Mathf.Sin(angleRad) * radius
                );

                Gizmos.DrawWireSphere(position, 1f * scale);
            }
        }
    }

    private void DrawCircle(Vector3 center, float radius, int segments)
    {
        float angleStep = 360f / segments;
        Vector3 prevPoint = center + new Vector3(radius, 0, 0);

        for (int i = 1; i <= segments; i++)
        {
            float angle = angleStep * i * Mathf.Deg2Rad;
            Vector3 newPoint = center + new Vector3(
                Mathf.Cos(angle) * radius,
                0f,
                Mathf.Sin(angle) * radius
            );

            Gizmos.DrawLine(prevPoint, newPoint);
            prevPoint = newPoint;
        }
    }
}
