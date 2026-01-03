using UnityEngine;
using SUPERCharacter;

public class BuildModeController : MonoBehaviour
{
    [Header("Settings")]
    public KeyCode exitBuildModeKey = KeyCode.Space;
    public float blockPlacementDelay = 0.15f;

    private bool isInBuildMode = false;
    public bool IsInBuildMode => isInBuildMode;

    private GameObject currentSelectedBlock = null;
    private SUPERCharacterAIO playerController;
    private float lastPlacementTime = 0f;
    private float gridSize = 1.0f; // Block size

    [Header("Platform Settings")]
    public Vector3 platformLocalDimensions = new Vector3(1f, 0.25f, 1f); // X, Y, Z
    public int thinAxisIndex = 1; // 0=X, 1=Y, 2=Z (Y-axis is thin)

    private Vector3 savedPlayerPosition;
    private float savedCameraDistance;

    void Awake()
    {
        // Ensure build mode is exited on game start
        isInBuildMode = false;
        currentSelectedBlock = null;
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

        // Update arrows continuously as camera rotates
        if (currentSelectedBlock != null && GameManager.Instance != null &&
            GameManager.Instance.arrowSystem != null && playerController != null)
        {
            GameManager.Instance.arrowSystem.UpdateArrowPositions(currentSelectedBlock, playerController.playerCamera);
        }

        // Exit build mode
        if (Input.GetKeyDown(exitBuildModeKey))
        {
            ExitBuildMode();
            return;
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

    public void EnterBuildMode(GameObject firstBlock)
    {
        if (isInBuildMode) return;

        isInBuildMode = true;
        currentSelectedBlock = firstBlock;

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
            playerController.buildModeOrbitCenter = firstBlock.transform.position;

            // Rotate player avatar to face the first block (instant snap)
            Vector3 directionToBlock = firstBlock.transform.position - playerController.transform.position;
            directionToBlock.y = 0; // Keep rotation on horizontal plane only

            if (directionToBlock != Vector3.zero)
            {
                // Calculate target rotation and instantly apply to player avatar
                Quaternion targetRotation = Quaternion.LookRotation(directionToBlock);
                playerController.transform.rotation = targetRotation;
            }

            // Set increased orbit distance for better view
            playerController.maxCameraDistInternal = playerController.buildModeOrbitDistance;
            playerController.currentCameraZ = -playerController.buildModeOrbitDistance;

            // Force third-person perspective for build mode orbit
            playerController.ChangePerspective(SUPERCharacter.PerspectiveModes._3rdPerson);
        }

        // Show arrows
        if (GameManager.Instance != null && GameManager.Instance.arrowSystem != null && playerController != null)
            GameManager.Instance.arrowSystem.ShowArrows(firstBlock, playerController.playerCamera);

        Debug.Log("BuildModeController: Entered Build Mode - Press SPACE to exit, WASD to place blocks");
    }

    public void ExitBuildMode()
    {
        if (!isInBuildMode) return;

        isInBuildMode = false;
        currentSelectedBlock = null;

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

    /// <summary>
    /// Gets the world-space direction of the platform's thin axis
    /// </summary>
    private Vector3 GetPlatformThinAxis(GameObject platform)
    {
        Vector3 localThinAxis = Vector3.zero;
        localThinAxis[thinAxisIndex] = 1f; // e.g., Vector3.up for Y-axis
        return platform.transform.rotation * localThinAxis;
    }

    /// <summary>
    /// Gets the half-extent of a platform in a given direction
    /// </summary>
    private float GetPlatformExtentInDirection(GameObject platform, Vector3 direction)
    {
        Vector3 thinAxis = GetPlatformThinAxis(platform);
        float parallelComponent = Mathf.Abs(Vector3.Dot(direction.normalized, thinAxis.normalized));

        if (parallelComponent > 0.9f) // Parallel to thin axis (within ~25 degrees)
        {
            return platformLocalDimensions[thinAxisIndex] / 2f; // Half of thin dimension (0.125)
        }
        else // Perpendicular to thin axis
        {
            return 0.5f; // Half of the 1.0 wide dimension
        }
    }

    /// <summary>
    /// Calculates center-to-center distance for edge-hinged placement
    /// New platform hinges from the edge of the current platform
    /// </summary>
    private float GetPlacementDistance(GameObject currentPlatform, Vector3 placementDirection)
    {
        // For edge-hinged placement where platforms share an edge:
        // Distance = half of current platform's depth + half of new platform's depth
        // Since both platforms face their placement direction with LookRotation,
        // the depth is along their local Z axis (1.0 for our 1×0.25×1 platform)

        // Current platform's extent in placement direction (half-depth)
        float currentExtent = 0.5f; // Half of 1.0 depth

        // New platform's extent (half-depth from its center to hinge edge)
        float newExtent = 0.5f; // Half of 1.0 depth

        // Total center-to-center distance
        return currentExtent + newExtent; // = 1.0
    }

    private void PlaceBlockInDirection(KeyCode key)
    {
        if (GameManager.Instance == null) return;
        if (currentSelectedBlock == null || GameManager.Instance.arrowSystem == null) return;

        Vector3 direction = GameManager.Instance.arrowSystem.GetPlacementDirection(key);
        if (direction == Vector3.zero) return;

        float placementDistance = GetPlacementDistance(currentSelectedBlock, direction);
        Vector3 newPosition = currentSelectedBlock.transform.position + direction * placementDistance;

        // Round to nearest 0.25 increment for consistent grid alignment
        newPosition = new Vector3(
            Mathf.Round(newPosition.x * 4f) / 4f,
            Mathf.Round(newPosition.y * 4f) / 4f,
            Mathf.Round(newPosition.z * 4f) / 4f
        );

        // Check if position occupied
        if (GameManager.Instance.adjacencyGrid != null && GameManager.Instance.adjacencyGrid.IsPositionOccupied(newPosition))
        {
            Debug.Log("BuildModeController: Position already occupied!");
            return;
        }

        // Check block limit
        if (GameManager.Instance.buildingSystem.CurrentBlockCount >= GameManager.Instance.buildingSystem.maxBlocks)
        {
            Debug.Log($"BuildModeController: Block limit ({GameManager.Instance.buildingSystem.maxBlocks}) reached!");
            return;
        }

        // Rotate platform to face the placement direction (like a wall facing you)
        // Platform's forward (Z) points toward placement direction, Y stays mostly up
        Quaternion blockRotation = Quaternion.LookRotation(direction, Vector3.up);

        // Place new block with rotation
        GameObject newBlock = Instantiate(GameManager.Instance.buildingSystem.cubePrefab, newPosition, blockRotation);
        newBlock.transform.localScale = Vector3.one;

        // Enable collider (check both parent and children since collider might be on child)
        Collider blockCollider = newBlock.GetComponentInChildren<Collider>();
        if (blockCollider != null)
            blockCollider.enabled = true;

        // Register with adjacency grid
        if (GameManager.Instance.adjacencyGrid != null)
            GameManager.Instance.adjacencyGrid.RegisterBlock(newBlock, newPosition);

        // Add to building system's block tracking
        if (GameManager.Instance.buildingSystem != null)
            GameManager.Instance.buildingSystem.AddBlock(newBlock);

        // Select this block and update camera/arrows
        SelectBlock(newBlock);
        lastPlacementTime = Time.time;
    }

    private void SelectBlock(GameObject newBlock)
    {
        currentSelectedBlock = newBlock;

        // Update orbit center for camera
        if (playerController != null)
        {
            playerController.buildModeOrbitCenter = newBlock.transform.position;
        }

        // Update arrows
        if (GameManager.Instance != null && GameManager.Instance.arrowSystem != null && playerController != null)
            GameManager.Instance.arrowSystem.ShowArrows(newBlock, playerController.playerCamera);
    }
}
