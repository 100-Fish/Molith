using UnityEngine;
using SUPERCharacter;
using DG.Tweening;

public class BuildModeController : MonoBehaviour
{
    [Header("Settings")]
    public float blockPlacementDelay = 0.15f;

    private bool isInBuildMode = false;
    public bool IsInBuildMode => isInBuildMode;

    private SUPERCharacterAIO playerController;
    private float lastPlacementTime = 0f;

    [Header("Platform Settings")]
    [Tooltip("Scale multiplier for platforms and grid system (1.0 = normal size)")]
    [Range(0.1f, 5.0f)]
    public float platformScale = 1.0f;

    private Vector3 savedPlayerPosition;
    private float savedCameraDistance;

    void Awake()
    {
        // Ensure build mode is exited on game start
        isInBuildMode = false;
    }

    void Start()
    {
        if (GameManager.Instance != null)
        {
            playerController = GameManager.Instance.playerController;
        }

        // Ensure player controller is in normal mode on game start
        if (playerController != null)
        {
            // Explicitly enable all normal mode settings
            playerController.controllerPaused = false;
            playerController.buildModeOverride = false;
            playerController.enableCameraControl = true;

            // Set camera perspective directly instead of calling ChangePerspective
            // (ChangePerspective may fail if SUPERCharacterAIO hasn't fully initialized)
            playerController.cameraPerspective = SUPERCharacter.PerspectiveModes._3rdPerson;

            Debug.Log("BuildModeController: Initialized - Camera control enabled, third-person mode set");
        }

        // Hide arrows on start
        if (GameManager.Instance != null && GameManager.Instance.arrowSystem != null)
            GameManager.Instance.arrowSystem.HideArrows();
    }

    void Update()
    {
        // Only process build mode input when in build mode
        if (!isInBuildMode) return;

        // Freeze player position in build mode
        if (playerController != null)
        {
            playerController.transform.position = savedPlayerPosition;
        }

        // Handle WASD placement with delay
        if (Time.time - lastPlacementTime > blockPlacementDelay)
        {
            if (Input.GetKeyDown(KeyCode.W))
                PlaceBlockInDirection(KeyCode.W);
            else if (Input.GetKeyDown(KeyCode.A))
                PlaceBlockInDirection(KeyCode.A);
            else if (Input.GetKeyDown(KeyCode.S))
                PlaceBlockInDirection(KeyCode.S);
            else if (Input.GetKeyDown(KeyCode.D))
                PlaceBlockInDirection(KeyCode.D);
        }
    }

    public void EnterBuildMode(Vector3 firstPoint)
    {
        if (isInBuildMode) return;

        isInBuildMode = true;

        // Disable player movement but keep camera control enabled
        if (playerController != null)
        {
            // Save player position to restore later
            savedPlayerPosition = playerController.transform.position;

            // Save current camera distance
            savedCameraDistance = playerController.maxCameraDistInternal;

            playerController.controllerPaused = true;
            playerController.enableCameraControl = true; // Allow camera orbit
            playerController.buildModeOverride = true;
            playerController.buildModeOrbitCenter = firstPoint;

            // Smoothly transition camera distance for better view
            DOVirtual.Float(
                savedCameraDistance,
                playerController.buildModeOrbitDistance,
                0.5f,
                value =>
                {
                    if (playerController != null)
                    {
                        playerController.maxCameraDistInternal = value;
                        playerController.currentCameraZ = -value;
                    }
                }
            ).SetEase(Ease.OutCubic);

            // Force third-person perspective for build mode orbit
            playerController.ChangePerspective(SUPERCharacter.PerspectiveModes._3rdPerson);
        }

        // Sync platformScale to arrow system and show arrows
        if (GameManager.Instance != null && GameManager.Instance.arrowSystem != null && playerController != null)
        {
            GameManager.Instance.arrowSystem.platformScale = platformScale;
            GameManager.Instance.arrowSystem.ShowArrowsAtPosition(firstPoint, playerController.playerCamera);
        }

        Debug.Log("BuildModeController: Entered Build Mode - Press E to exit, WASD to place blocks");
    }

    public void ExitBuildMode()
    {
        if (!isInBuildMode) return;

        isInBuildMode = false;

        // Finalize current road
        if (GameManager.Instance != null && GameManager.Instance.buildingSystem != null)
        {
            GameManager.Instance.buildingSystem.FinalizeCurrentRoad();
        }

        // Re-enable player movement and disable build mode override
        if (playerController != null)
        {
            playerController.controllerPaused = false;
            playerController.buildModeOverride = false;

            // Restore camera distance
            playerController.maxCameraDistInternal = savedCameraDistance;
            playerController.currentCameraZ = -savedCameraDistance;
        }

        // Hide arrows
        if (GameManager.Instance != null && GameManager.Instance.arrowSystem != null)
            GameManager.Instance.arrowSystem.HideArrows();

        Debug.Log("BuildModeController: Exited Build Mode");
    }

    private void PlaceBlockInDirection(KeyCode key)
    {
        if (GameManager.Instance == null) return;
        if (GameManager.Instance.arrowSystem == null) return;
        if (GameManager.Instance.buildingSystem == null) return;

        Vector3 direction = GameManager.Instance.arrowSystem.GetPlacementDirection(key);
        if (direction == Vector3.zero) return;

        // Get current road's latest point
        Road currentRoad = GameManager.Instance.buildingSystem.GetCurrentRoad();
        if (currentRoad == null || currentRoad.splinePoints.Count == 0)
        {
            Debug.LogError("No current road or no points in road!");
            return;
        }

        Vector3 lastPoint = currentRoad.splinePoints[currentRoad.splinePoints.Count - 1];

        float placementDistance = 1.0f * platformScale;  // Fixed distance between points
        Vector3 newPosition = lastPoint + direction * placementDistance;

        // Round to grid
        float roundingFactor = 4f / platformScale;
        newPosition = new Vector3(
            Mathf.Round(newPosition.x * roundingFactor) / roundingFactor,
            Mathf.Round(newPosition.y * roundingFactor) / roundingFactor,
            Mathf.Round(newPosition.z * roundingFactor) / roundingFactor
        );

        // Check point limit
        if (GameManager.Instance.buildingSystem.CurrentBlockCount >= GameManager.Instance.buildingSystem.maxBlocks)
        {
            Debug.Log($"Point limit ({GameManager.Instance.buildingSystem.maxBlocks}) reached!");
            return;
        }

        // Add point to current road
        GameManager.Instance.buildingSystem.AddPointToCurrentRoad(newPosition);

        // Update orbit center and arrows
        UpdateOrbitToPoint(newPosition);
        lastPlacementTime = Time.time;
    }

    private void UpdateOrbitToPoint(Vector3 point)
    {
        // Update orbit center
        if (playerController != null)
        {
            playerController.buildModeOrbitCenter = point;
        }

        // Update arrows
        if (GameManager.Instance != null && GameManager.Instance.arrowSystem != null && playerController != null)
        {
            GameManager.Instance.arrowSystem.platformScale = platformScale;
            GameManager.Instance.arrowSystem.ShowArrowsAtPosition(point, playerController.playerCamera);
        }
    }

}
