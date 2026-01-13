using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class WorldGenerator : MonoBehaviour
{
    [Header("Generation Settings")]
    [SerializeField] private List<GameObject> structurePrefabs = new();
    [SerializeField] private int numberOfCircles = 3;
    [SerializeField] private float baseRadius = 20f;
    [SerializeField] private float radiusExponent = 1.8f;

    [Header("Structure Scaling")]
    [SerializeField] private float baseScale = 2f;
    [SerializeField] private float scaleGrowthRate = 1.3f;
    [SerializeField] private float positionRandomness = 0.4f; // How much structures can deviate from circle (0-1 where 1 = full radius)

    [Header("Terrain")]
    [SerializeField] private MeshFilter groundPlaneMeshFilter;
    [SerializeField] private float terrainSize = 100f; // Size of the terrain (square: X and Z)
    [SerializeField] private int terrainSubdivisions = 80; // Number of subdivisions per axis for terrain mesh

    [Header("Terrain Noise Settings")]
    [SerializeField] private float noiseScale = 0.02f; // Base frequency of the noise
    [SerializeField] private float noiseStrength = 15f; // Overall height multiplier
    [SerializeField] private int octaves = 4; // Number of noise layers (more = more detail)
    [SerializeField] private float persistence = 0.5f; // How much each octave contributes (0-1)
    [SerializeField] private float lacunarity = 2.0f; // Frequency multiplier for each octave
    [SerializeField] private Vector2 noiseOffset = Vector2.zero; // Offset for noise sampling

    [Header("Generation")]
    [SerializeField] private bool autoGenerate = false; // Automatically generate when settings change in editor
    [SerializeField] private int generationSeed = 12345; // Set a specific seed for consistent generation (0 = random each time)
    [SerializeField] private bool clearOldStructures = true; // Clear old structures before generating new ones

    [Header("Debug")]
    [SerializeField] private bool drawGizmos = true;
    [SerializeField] private bool drawTerrainNoise = false;
    [SerializeField] private int terrainDebugSamples = 20; // Number of samples per axis for terrain visualization

    private List<Vector3> generatedPositions = new List<Vector3>();
    private List<float> generatedScales = new List<float>();
    private Mesh previewMesh;
    private Mesh originalMesh;
    private bool isPreviewActive = false;
    private List<GameObject> editorSpawnedStructures = new List<GameObject>();

    void Start()
    {
        // Everything is already set up in the editor - terrain mesh and structures are spawned
        // No need to do anything at runtime!
        Debug.Log("World already generated in editor - ready to play!");
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

                // Add random angle offset based on how many structures are in this circle
                float angleOffset = Random.Range(-180f / structureCount, 180f / structureCount) * positionRandomness;
                float angleRad = (angle + angleOffset) * Mathf.Deg2Rad;

                // Add random radius offset
                float radiusOffset = Random.Range(-radius * positionRandomness, radius * positionRandomness);
                float actualRadius = radius + radiusOffset;

                // Calculate position on circle (using XZ plane) with randomization
                Vector3 position = new Vector3(
                    Mathf.Cos(angleRad) * actualRadius,
                    0f,
                    Mathf.Sin(angleRad) * actualRadius
                );

                generatedPositions.Add(position);
                generatedScales.Add(scale);
            }
        }

        Debug.Log($"Calculated {generatedPositions.Count} structure positions across {numberOfCircles} circles");
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

    void OnValidate()
    {
        // Auto-generate when settings change in editor (but not during play mode)
        // Use delayCall to avoid issues with OnValidate restrictions
        if (autoGenerate && !Application.isPlaying)
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.delayCall += () =>
            {
                if (this != null && !Application.isPlaying)
                {
                    GenerateInEditor();
                }
            };
#endif
        }
    }

    [ContextMenu("Generate World in Editor")]
    public void GenerateInEditor()
    {
        if (groundPlaneMeshFilter == null)
        {
            Debug.LogWarning("Ground Plane MeshFilter is not assigned!");
            return;
        }

        // Set seed for consistent generation
        if (generationSeed != 0)
        {
            Random.InitState(generationSeed);
        }

        // Clear previously spawned editor structures
        if (clearOldStructures)
        {
            ClearEditorStructures();
        }

        // Generate terrain mesh with subdivisions
        if (!isPreviewActive)
        {
            // Store original mesh reference
            originalMesh = groundPlaneMeshFilter.sharedMesh;
            isPreviewActive = true;
        }

        // Create a new subdivided mesh
        previewMesh = CreateSubdividedTerrainMesh();
        groundPlaneMeshFilter.sharedMesh = previewMesh;

        // Generate positions for structures
        GenerateStructurePositions();

        // Spawn structures in editor
        SpawnStructuresInEditor();

        Debug.Log($"Generated world with {generatedPositions.Count} structures and {terrainSubdivisions}x{terrainSubdivisions} terrain");
    }

    [ContextMenu("Clear All Generated Structures")]
    public void ClearAllStructures()
    {
        ClearEditorStructures();
        Debug.Log("Cleared all generated structures");
    }

    private Mesh CreateSubdividedTerrainMesh()
    {
        // Use the terrain size variable for both X and Z (square terrain)
        float size = terrainSize;

        Mesh mesh = new Mesh();
        mesh.name = "Generated Terrain";

        // Ensure we have at least 2 subdivisions
        int subdivs = Mathf.Max(2, terrainSubdivisions);
        int vertCountX = subdivs + 1;
        int vertCountZ = subdivs + 1;
        Vector3[] vertices = new Vector3[vertCountX * vertCountZ];
        Vector2[] uvs = new Vector2[vertices.Length];

        // Generate vertices in a grid
        for (int z = 0; z < vertCountZ; z++)
        {
            for (int x = 0; x < vertCountX; x++)
            {
                int index = z * vertCountX + x;

                // Position vertices from -size/2 to size/2 (in local space)
                float xPos = (x / (float)subdivs - 0.5f) * size;
                float zPos = (z / (float)subdivs - 0.5f) * size;

                vertices[index] = new Vector3(xPos, 0, zPos);
                uvs[index] = new Vector2(x / (float)subdivs, z / (float)subdivs);
            }
        }

        // Generate triangles
        int[] triangles = new int[subdivs * subdivs * 6];
        int triIndex = 0;

        for (int z = 0; z < subdivs; z++)
        {
            for (int x = 0; x < subdivs; x++)
            {
                int vertIndex = z * vertCountX + x;

                // First triangle
                triangles[triIndex++] = vertIndex;
                triangles[triIndex++] = vertIndex + vertCountX;
                triangles[triIndex++] = vertIndex + 1;

                // Second triangle
                triangles[triIndex++] = vertIndex + 1;
                triangles[triIndex++] = vertIndex + vertCountX;
                triangles[triIndex++] = vertIndex + vertCountX + 1;
            }
        }

        mesh.vertices = vertices;
        mesh.triangles = triangles;
        mesh.uv = uvs;

        // Apply noise to the mesh
        ApplyNoiseToMesh(mesh);

        Debug.Log($"Created terrain mesh: {vertices.Length} vertices, {triangles.Length / 3} triangles, size: {size}x{size}");

        return mesh;
    }

    private void ClearEditorStructures()
    {
        // Clear any previously spawned structures in editor
        // First, try to clear tracked structures
        for (int i = editorSpawnedStructures.Count - 1; i >= 0; i--)
        {
            if (editorSpawnedStructures[i] != null)
            {
#if UNITY_EDITOR
                DestroyImmediate(editorSpawnedStructures[i]);
#else
                Destroy(editorSpawnedStructures[i]);
#endif
            }
        }
        editorSpawnedStructures.Clear();

        // Also clear any child objects that might be structures
        // (in case the list lost track of them)
        Transform[] children = GetComponentsInChildren<Transform>();
        foreach (Transform child in children)
        {
            if (child != transform && child.parent == transform)
            {
#if UNITY_EDITOR
                DestroyImmediate(child.gameObject);
#else
                Destroy(child.gameObject);
#endif
            }
        }
    }

    private void SpawnStructuresInEditor()
    {
        if (structurePrefabs.Count == 0) return;

        for (int i = 0; i < generatedPositions.Count; i++)
        {
            Vector3 position = generatedPositions[i];
            float scale = generatedScales[i];

            float randomScale = Random.Range(0.25f, 2f) * scale;

#if UNITY_EDITOR
            GameObject structure = (GameObject)UnityEditor.PrefabUtility.InstantiatePrefab(structurePrefabs[Random.Range(0, structurePrefabs.Count)], transform);
            structure.transform.position = position;
            structure.transform.rotation = Random.rotation;
            structure.transform.localScale = randomScale * Vector3.one;
#else
            GameObject structure = Instantiate(structurePrefabs[Random.Range(0, structurePrefabs.Count)], position, Random.rotation, transform);
            structure.transform.localScale = randomScale * Vector3.one;
#endif

            // Adjust Y position so bottom touches the ground
            Bounds bounds = GetTotalBounds(structure);
            if (bounds.size.magnitude > 0)
            {
                float yOffset = -bounds.min.y;
                structure.transform.position += Vector3.up * yOffset;
            }
            structure.transform.position -= Vector3.up * Random.Range(2f, 10f);

            editorSpawnedStructures.Add(structure);
        }

        Debug.Log($"Spawned {editorSpawnedStructures.Count} structures in editor");
    }

    private void ApplyNoiseToMesh(Mesh mesh)
    {
        if (mesh == null || mesh.vertices.Length < 3)
        {
            Debug.LogError("Invalid mesh for noise application");
            return;
        }

        Vector3[] vertices = mesh.vertices;

        for (int i = 0; i < vertices.Length; i++)
        {
            // Use world position for noise sampling
            Vector3 worldPos = groundPlaneMeshFilter.transform.TransformPoint(vertices[i]);

            // Generate fractal Brownian motion (layered noise) for more dramatic terrain
            float noiseValue = GenerateFractalNoise(worldPos.x, worldPos.z);

            // Apply height with the noise value
            float height = noiseValue * noiseStrength;

            // Set the Y position in local space
            vertices[i].y = height;
        }

        mesh.vertices = vertices;
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();

        // Update mesh collider if present (only if we have enough vertices)
        if (mesh.vertexCount >= 3)
        {
            MeshCollider meshCollider = groundPlaneMeshFilter.GetComponent<MeshCollider>();
            if (meshCollider != null)
            {
                meshCollider.sharedMesh = null; // Clear first to force update
                meshCollider.sharedMesh = mesh;
            }
        }
    }

    private float GenerateFractalNoise(float x, float z)
    {
        float total = 0f;
        float amplitude = 1f;
        float frequency = noiseScale;
        float maxValue = 0f; // Used for normalizing

        // Layer multiple octaves of noise
        for (int i = 0; i < octaves; i++)
        {
            // Sample Perlin noise at this octave's frequency
            float sampleX = (x + noiseOffset.x) * frequency;
            float sampleZ = (z + noiseOffset.y) * frequency;

            // Get noise value in range [0, 1]
            float perlinValue = Mathf.PerlinNoise(sampleX, sampleZ);

            // Map to [-1, 1] for more dramatic terrain
            perlinValue = perlinValue * 2f - 1f;

            total += perlinValue * amplitude;
            maxValue += amplitude;

            // Adjust amplitude and frequency for next octave
            amplitude *= persistence;
            frequency *= lacunarity;
        }

        // Normalize to [-1, 1] range
        return total / maxValue;
    }

    private void GenerateStructurePositions()
    {
        generatedPositions.Clear();
        generatedScales.Clear();

        for (int circleIndex = 0; circleIndex < numberOfCircles; circleIndex++)
        {
            float radius = baseRadius * Mathf.Pow(radiusExponent, circleIndex);
            float scale = baseScale * Mathf.Pow(scaleGrowthRate, circleIndex);
            int structureCount = 3 + (circleIndex * 2);

            for (int i = 0; i < structureCount; i++)
            {
                float angle = 360f / structureCount * i;
                float angleOffset = Random.Range(-180f / structureCount, 180f / structureCount) * positionRandomness;
                float angleRad = (angle + angleOffset) * Mathf.Deg2Rad;

                float radiusOffset = Random.Range(-radius * positionRandomness, radius * positionRandomness);
                float actualRadius = radius + radiusOffset;

                Vector3 position = new Vector3(
                    Mathf.Cos(angleRad) * actualRadius,
                    0f,
                    Mathf.Sin(angleRad) * actualRadius
                );

                generatedPositions.Add(position);
                generatedScales.Add(scale);
            }
        }
    }

    void OnDrawGizmos()
    {
        if (!drawGizmos) return;

        // Draw guideline circles (semi-transparent to show they're just guides)
        Gizmos.color = new Color(1f, 0f, 0f, 0.2f);
        for (int circleIndex = 0; circleIndex < numberOfCircles; circleIndex++)
        {
            float radius = baseRadius * Mathf.Pow(radiusExponent, circleIndex);
            DrawCircle(Vector3.zero, radius, 64);
        }

        // Draw terrain noise visualization (only when not auto-generating, to avoid clutter)
        if (drawTerrainNoise && groundPlaneMeshFilter != null && !autoGenerate)
        {
            DrawTerrainNoiseDebug();
        }
    }

    private void DrawTerrainNoiseDebug()
    {
        if (terrainDebugSamples <= 0) return;

        // Get the bounds of the ground plane
        Bounds bounds = groundPlaneMeshFilter.GetComponent<Renderer>().bounds;
        Vector3 min = bounds.min;
        Vector3 max = bounds.max;

        float stepX = (max.x - min.x) / terrainDebugSamples;
        float stepZ = (max.z - min.z) / terrainDebugSamples;

        // Sample the noise at a grid of points
        for (int x = 0; x < terrainDebugSamples; x++)
        {
            for (int z = 0; z < terrainDebugSamples; z++)
            {
                float worldX = min.x + x * stepX;
                float worldZ = min.z + z * stepZ;

                // Sample noise
                float noiseValue = Mathf.PerlinNoise(worldX * noiseScale, worldZ * noiseScale);
                float height = (noiseValue * 2f - 1f) * noiseStrength;

                Vector3 position = new Vector3(worldX, height, worldZ);

                // Color based on height (blue = low, red = high)
                float normalizedHeight = (height + noiseStrength) / (2f * noiseStrength);
                Gizmos.color = Color.Lerp(Color.blue, Color.red, normalizedHeight);
                Gizmos.DrawSphere(position, 0.2f);

                // Draw vertical line to show height
                Gizmos.color = new Color(1f, 1f, 1f, 0.3f);
                Gizmos.DrawLine(new Vector3(worldX, 0, worldZ), position);
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
