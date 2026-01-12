using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public class BlockData
{
    public Vector3 position;
    public float scale;

    public BlockData(Vector3 pos, float scl)
    {
        position = pos;
        scale = scl;
    }

    public override string ToString()
    {
        return $"cube,{position.x:F1},{position.y:F1},{position.z:F1},{scale:F1} EOL";
    }
}

public class BuildingSystem : MonoBehaviour
{
    [Header("References")]
    [Tooltip("Default material for roads")]
    public Material defaultRoadMaterial;

    [Header("Road Dimensions")]
    [Tooltip("Width of roads")]
    [Range(0.1f, 5.0f)]
    public float roadWidth = 1.0f;

    [Tooltip("Depth (thickness) of roads")]
    [Range(0.1f, 2.0f)]
    public float roadDepth = 0.25f;

    [Header("Placement Settings")]
    [Tooltip("Distance from camera to place first point")]
    public float placementDistance = 5f;

    [Tooltip("Key to start new road")]
    public KeyCode placeKey = KeyCode.E;

    [Header("Destruction Settings")]
    [Tooltip("Key to remove oldest point")]
    public KeyCode destroyKey = KeyCode.Q;

    [Header("Point Limit Settings")]
    [Tooltip("Maximum number of points that can be placed at once")]
    public int maxBlocks = 50;

    [Header("Road System")]
    private readonly List<Road> roads = new List<Road>();
    private Road currentRoad = null;
    private RoadMeshGenerator roadMeshGenerator;
    private int nextRoadID = 0;

    private GameObject previewRoad = null;
    private bool isPlacementMode = false;
    private Vector3 previewPosition;

    public int CurrentBlockCount
    {
        get
        {
            int count = 0;
            foreach (Road road in roads)
                count += road.splinePoints.Count;
            if (currentRoad != null)
                count += currentRoad.splinePoints.Count;
            return count;
        }
    }

    public int RemainingBlocks => maxBlocks - CurrentBlockCount;

    void Start()
    {
        if (GameManager.Instance == null)
        {
            Debug.LogError("BuildingSystem: GameManager instance is missing!");
            return;
        }

        // Initialize road mesh generator
        roadMeshGenerator = gameObject.AddComponent<RoadMeshGenerator>();
        roadMeshGenerator.roadWidth = roadWidth;
        roadMeshGenerator.roadDepth = roadDepth;
        roadMeshGenerator.segmentsPerUnit = 4;
        roadMeshGenerator.defaultRoadMaterial = defaultRoadMaterial;
    }

    void Update()
    {
        if (GameManager.Instance == null || GameManager.Instance.playerController == null)
            return;

        if (GameManager.Instance.playerController.playerCamera == null)
            return;

        HandlePlacementInput();
        HandleDestructionInput();
    }

    void HandlePlacementInput()
    {
        // Check if already in build mode - if so, E exits instead
        if (GameManager.Instance != null && GameManager.Instance.buildModeController != null)
        {
            if (GameManager.Instance.buildModeController.IsInBuildMode)
            {
                // E key exits build mode
                if (Input.GetKeyDown(placeKey))
                {
                    GameManager.Instance.buildModeController.ExitBuildMode();
                }
                return;
            }
        }

        // E key held - show preview
        if (Input.GetKeyDown(placeKey))
        {
            isPlacementMode = true;
            CreatePreviewRoad();
        }

        if (Input.GetKey(placeKey) && isPlacementMode)
        {
            UpdatePreviewPosition();
        }

        // E key released - place road and enter build mode
        if (Input.GetKeyUp(placeKey) && isPlacementMode)
        {
            PlaceFirstPoint();
            isPlacementMode = false;
        }
    }

    void CreatePreviewRoad()
    {
        if (previewRoad == null)
        {
            previewRoad = new GameObject("RoadPreview");
            previewRoad.AddComponent<MeshFilter>();
            MeshRenderer renderer = previewRoad.AddComponent<MeshRenderer>();

            // Use hologram material with placement color
            if (GameManager.Instance != null && GameManager.Instance.hologramMaterial != null)
            {
                Material previewMat = new Material(GameManager.Instance.hologramMaterial);
                previewMat.SetColor("_Color", GameManager.Instance.placementColor);
                renderer.material = previewMat;
            }
        }

        UpdatePreviewPosition();
    }

    void UpdatePreviewPosition()
    {
        if (previewRoad == null) return;
        if (GameManager.Instance == null || GameManager.Instance.playerController == null) return;

        Camera cam = GameManager.Instance.playerController.playerCamera;
        if (cam == null) return;

        // Calculate position from camera
        Vector3 rawPosition = cam.transform.position + cam.transform.forward * placementDistance;

        // Get platform scale for grid snapping
        float platformScale = 1.0f;
        if (GameManager.Instance.buildModeController != null)
        {
            platformScale = GameManager.Instance.buildModeController.platformScale;
        }

        // Round to grid (same calculation as actual placement)
        float roundingFactor = 4f / platformScale;
        previewPosition = new Vector3(
            Mathf.Round(rawPosition.x * roundingFactor) / roundingFactor,
            Mathf.Round(rawPosition.y * roundingFactor) / roundingFactor,
            Mathf.Round(rawPosition.z * roundingFactor) / roundingFactor
        );

        previewRoad.transform.position = previewPosition;

        // Generate preview mesh (single point)
        Mesh previewMesh = roadMeshGenerator.GenerateRoadMesh(new System.Collections.Generic.List<Vector3> { previewPosition });
        previewRoad.GetComponent<MeshFilter>().mesh = previewMesh;
    }

    void PlaceFirstPoint()
    {
        if (previewRoad != null)
        {
            Destroy(previewRoad);
            previewRoad = null;
        }

        StartNewRoad(previewPosition);

        // Enter build mode
        if (GameManager.Instance.buildModeController != null)
        {
            GameManager.Instance.buildModeController.EnterBuildMode(previewPosition);
        }
    }

    private float lastDestructionTime = 0f;
    private float destructionDelay = 0.15f;

    void HandleDestructionInput()
    {
        // Don't allow destruction in build mode
        if (GameManager.Instance != null && GameManager.Instance.buildModeController != null)
        {
            if (GameManager.Instance.buildModeController.IsInBuildMode)
                return;
        }

        // Q key removes oldest point - continuous when held
        if (Input.GetKey(destroyKey))
        {
            // First removal is instant
            if (Input.GetKeyDown(destroyKey))
            {
                RemoveOldestPoint();
                lastDestructionTime = Time.time;
            }
            // Subsequent removals have a delay
            else if (Time.time - lastDestructionTime >= destructionDelay)
            {
                RemoveOldestPoint();
                lastDestructionTime = Time.time;
            }
        }
    }

    void RemoveOldestPoint()
    {
        // Find oldest road with points
        Road oldestRoad = null;
        foreach (Road road in roads)
        {
            if (road.splinePoints.Count > 0)
            {
                oldestRoad = road;
                break;
            }
        }

        if (oldestRoad == null)
        {
            Debug.Log("No points to remove");
            return;
        }

        // Remove oldest point
        oldestRoad.RemoveOldestPoint();

        // Destroy road if it has less than 2 points remaining
        if (oldestRoad.splinePoints.Count < 2)
        {
            DestroyRoad(oldestRoad);
            roads.Remove(oldestRoad);
        }
        else
        {
            // Regenerate mesh with remaining points
            oldestRoad.roadMesh = roadMeshGenerator.GenerateRoadMesh(oldestRoad.splinePoints);
            if (oldestRoad.roadMeshObject != null)
            {
                oldestRoad.roadMeshObject.GetComponent<MeshFilter>().mesh = oldestRoad.roadMesh;
            }
        }
    }

    void DestroyRoad(Road road)
    {
        if (road.roadMeshObject != null)
            Destroy(road.roadMeshObject);
    }

    public void StartNewRoad(Vector3 firstPoint)
    {
        currentRoad = new Road(nextRoadID++);
        currentRoad.AddPoint(firstPoint);
        roads.Add(currentRoad);

        Debug.Log($"Started new road {currentRoad.roadID} at {firstPoint}");
    }

    public void AddPointToCurrentRoad(Vector3 point)
    {
        if (currentRoad == null)
        {
            Debug.LogError("No current road! Call StartNewRoad first.");
            return;
        }

        currentRoad.AddPoint(point);
        RegenerateCurrentRoadMesh();
    }

    public void FinalizeCurrentRoad()
    {
        if (currentRoad == null) return;

        // Discard roads with less than 2 points
        if (currentRoad.splinePoints.Count < 2)
        {
            Debug.Log($"Road {currentRoad.roadID} has only {currentRoad.splinePoints.Count} point(s) - discarding");
            roads.Remove(currentRoad);
            if (currentRoad.roadMeshObject != null)
                Destroy(currentRoad.roadMeshObject);
        }
        else
        {
            Debug.Log($"Finalized road {currentRoad.roadID} with {currentRoad.splinePoints.Count} points");
        }

        currentRoad = null;
    }

    private void RegenerateCurrentRoadMesh()
    {
        if (currentRoad == null) return;

        // Generate mesh
        Mesh newMesh = roadMeshGenerator.GenerateRoadMesh(currentRoad.splinePoints);
        currentRoad.roadMesh = newMesh;

        // Create or update GameObject
        if (currentRoad.roadMeshObject == null)
        {
            currentRoad.roadMeshObject = new GameObject($"Road_{currentRoad.roadID}");
            currentRoad.roadMeshObject.AddComponent<MeshFilter>();
            MeshRenderer renderer = currentRoad.roadMeshObject.AddComponent<MeshRenderer>();

            // Use hologram material with build mode color
            if (GameManager.Instance != null && GameManager.Instance.hologramMaterial != null)
            {
                Material roadMat = new Material(GameManager.Instance.hologramMaterial);
                roadMat.SetColor("_Color", GameManager.Instance.buildModeColor);
                renderer.material = roadMat;
            }
            else
            {
                renderer.material = defaultRoadMaterial != null ? defaultRoadMaterial : new Material(Shader.Find("Standard"));
            }

            // Add MeshCollider for walkability
            MeshCollider collider = currentRoad.roadMeshObject.AddComponent<MeshCollider>();
            collider.sharedMesh = newMesh;
        }

        // Update mesh
        currentRoad.roadMeshObject.GetComponent<MeshFilter>().mesh = newMesh;

        // Update collider mesh
        MeshCollider meshCollider = currentRoad.roadMeshObject.GetComponent<MeshCollider>();
        if (meshCollider != null)
        {
            meshCollider.sharedMesh = newMesh;
        }
    }

    public Road GetCurrentRoad()
    {
        return currentRoad;
    }
}
