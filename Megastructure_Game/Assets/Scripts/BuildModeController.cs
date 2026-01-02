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

    private Vector3 savedPlayerPosition;
    private float savedCameraDistance;

    void Start()
    {
        if (GameManager.Instance != null)
        {
            playerController = GameManager.Instance.playerController;
        }
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

    private void PlaceBlockInDirection(KeyCode key)
    {
        if (GameManager.Instance == null) return;
        if (currentSelectedBlock == null || GameManager.Instance.arrowSystem == null) return;

        Vector3 direction = GameManager.Instance.arrowSystem.GetPlacementDirection(key);
        if (direction == Vector3.zero) return;

        Vector3 newPosition = currentSelectedBlock.transform.position + direction * gridSize;

        // Round to grid (1.0 increments)
        newPosition = new Vector3(
            Mathf.Round(newPosition.x),
            Mathf.Round(newPosition.y),
            Mathf.Round(newPosition.z)
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

        // Place new block
        GameObject newBlock = Instantiate(GameManager.Instance.buildingSystem.cubePrefab, newPosition, Quaternion.identity);
        newBlock.transform.localScale = Vector3.one;

        // Enable collider
        Collider blockCollider = newBlock.GetComponent<Collider>();
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
            playerController.buildModeOrbitCenter = newBlock.transform.position;

        // Update arrows
        if (GameManager.Instance != null && GameManager.Instance.arrowSystem != null && playerController != null)
            GameManager.Instance.arrowSystem.ShowArrows(newBlock, playerController.playerCamera);
    }
}
