using UnityEngine;
using SUPERCharacter;

public class BuildModeController : MonoBehaviour
{
    [Header("References")]
    public BuildingSystem buildingSystem;
    public BuildModeCameraController cameraController;
    public DirectionalArrowSystem arrowSystem;
    public BlockAdjacencyGrid adjacencyGrid;

    [Header("Settings")]
    public KeyCode enterBuildModeKey = KeyCode.B;
    public KeyCode exitBuildModeKey = KeyCode.Escape;
    public float blockPlacementDelay = 0.15f;

    private bool isInBuildMode = false;
    public bool IsInBuildMode => isInBuildMode;

    private GameObject currentSelectedBlock = null;
    private SUPERCharacterAIO playerController;
    private float lastPlacementTime = 0f;
    private float gridSize = 1.0f; // Block size

    void Start()
    {
        if (GameManager.Instance != null)
        {
            buildingSystem = GameManager.Instance.buildingSystem;
            playerController = GameManager.Instance.playerController;
        }

        if (cameraController == null)
            cameraController = GetComponent<BuildModeCameraController>();
        if (arrowSystem == null)
            arrowSystem = GetComponent<DirectionalArrowSystem>();
        if (adjacencyGrid == null)
            adjacencyGrid = GetComponent<BlockAdjacencyGrid>();
    }

    void Update()
    {
        // Handle build mode entry when not in build mode
        if (!isInBuildMode && Input.GetKeyDown(enterBuildModeKey))
        {
            PlaceFirstBlockAndEnterBuildMode();
            return;
        }

        // Only process build mode input when in build mode
        if (!isInBuildMode) return;

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

    private void PlaceFirstBlockAndEnterBuildMode()
    {
        if (buildingSystem == null || playerController == null)
        {
            Debug.LogError("BuildModeController: BuildingSystem or PlayerController is missing!");
            return;
        }

        // Calculate spawn position in front of player
        Vector3 spawnPosition = playerController.transform.position +
                               playerController.transform.forward * 2f;

        // Round to grid
        spawnPosition = new Vector3(
            Mathf.Round(spawnPosition.x * 2f) / 2f,
            Mathf.Round(spawnPosition.y * 2f) / 2f,
            Mathf.Round(spawnPosition.z * 2f) / 2f
        );

        // Check if position is occupied
        if (adjacencyGrid != null && adjacencyGrid.IsPositionOccupied(spawnPosition))
        {
            Debug.Log("BuildModeController: Cannot place first block - position occupied!");
            return;
        }

        // Check block limit
        if (buildingSystem.CurrentBlockCount >= buildingSystem.maxBlocks)
        {
            Debug.Log($"BuildModeController: Block limit ({buildingSystem.maxBlocks}) reached!");
            return;
        }

        // Instantiate first block
        GameObject firstBlock = Instantiate(buildingSystem.cubePrefab, spawnPosition, Quaternion.identity);
        firstBlock.transform.localScale = Vector3.one;

        // Enable collider
        Collider blockCollider = firstBlock.GetComponent<Collider>();
        if (blockCollider != null)
            blockCollider.enabled = true;

        // Register with building system
        if (adjacencyGrid != null)
            adjacencyGrid.RegisterBlock(firstBlock, spawnPosition);

        // Enter build mode with this block
        EnterBuildMode(firstBlock);
    }

    public void EnterBuildMode(GameObject firstBlock)
    {
        if (isInBuildMode) return;

        isInBuildMode = true;
        currentSelectedBlock = firstBlock;

        // Disable player movement
        if (playerController != null)
            playerController.controllerPaused = true;

        // Focus camera on block
        if (cameraController != null)
            cameraController.FocusOnBlock(firstBlock);

        // Show arrows
        if (arrowSystem != null && playerController != null)
            arrowSystem.ShowArrows(firstBlock, playerController.playerCamera);

        Debug.Log("BuildModeController: Entered Build Mode - Press ESC to exit, WASD to place blocks");
    }

    public void ExitBuildMode()
    {
        if (!isInBuildMode) return;

        isInBuildMode = false;
        currentSelectedBlock = null;

        // Re-enable player movement
        if (playerController != null)
            playerController.controllerPaused = false;

        // Restore camera
        if (cameraController != null)
            cameraController.RestoreNormalCamera();

        // Hide arrows
        if (arrowSystem != null)
            arrowSystem.HideArrows();

        Debug.Log("BuildModeController: Exited Build Mode");
    }

    private void PlaceBlockInDirection(KeyCode key)
    {
        if (currentSelectedBlock == null || arrowSystem == null) return;

        Vector3 direction = arrowSystem.GetPlacementDirection(key);
        if (direction == Vector3.zero) return;

        Vector3 newPosition = currentSelectedBlock.transform.position + direction * gridSize;

        // Round to grid
        newPosition = new Vector3(
            Mathf.Round(newPosition.x * 2f) / 2f,
            Mathf.Round(newPosition.y * 2f) / 2f,
            Mathf.Round(newPosition.z * 2f) / 2f
        );

        // Check if position occupied
        if (adjacencyGrid != null && adjacencyGrid.IsPositionOccupied(newPosition))
        {
            Debug.Log("BuildModeController: Position already occupied!");
            return;
        }

        // Check block limit
        if (buildingSystem.CurrentBlockCount >= buildingSystem.maxBlocks)
        {
            Debug.Log($"BuildModeController: Block limit ({buildingSystem.maxBlocks}) reached!");
            return;
        }

        // Place new block
        GameObject newBlock = Instantiate(buildingSystem.cubePrefab, newPosition, Quaternion.identity);
        newBlock.transform.localScale = Vector3.one;

        // Enable collider
        Collider blockCollider = newBlock.GetComponent<Collider>();
        if (blockCollider != null)
            blockCollider.enabled = true;

        // Register with adjacency grid
        if (adjacencyGrid != null)
            adjacencyGrid.RegisterBlock(newBlock, newPosition);

        // Select this block and update camera/arrows
        SelectBlock(newBlock);
        lastPlacementTime = Time.time;
    }

    private void SelectBlock(GameObject newBlock)
    {
        currentSelectedBlock = newBlock;

        if (cameraController != null)
            cameraController.FocusOnBlock(newBlock);

        if (arrowSystem != null && playerController != null)
            arrowSystem.ShowArrows(newBlock, playerController.playerCamera);
    }
}
