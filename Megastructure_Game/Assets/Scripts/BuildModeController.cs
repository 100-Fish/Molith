using UnityEngine;
using SUPERCharacter;

public class BuildModeController : MonoBehaviour
{
    [Header("Settings")]
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
            playerController = GameManager.Instance.playerController;
        }
    }

    void Update()
    {
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

    public void EnterBuildMode(GameObject firstBlock)
    {
        if (isInBuildMode) return;

        isInBuildMode = true;
        currentSelectedBlock = firstBlock;

        // Disable player movement
        if (playerController != null)
            playerController.controllerPaused = true;

        // Focus camera on block
        if (GameManager.Instance != null && GameManager.Instance.buildCameraController != null)
            GameManager.Instance.buildCameraController.FocusOnBlock(firstBlock);

        // Show arrows
        if (GameManager.Instance != null && GameManager.Instance.arrowSystem != null && playerController != null)
            GameManager.Instance.arrowSystem.ShowArrows(firstBlock, playerController.playerCamera);

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
        if (GameManager.Instance != null && GameManager.Instance.buildCameraController != null)
            GameManager.Instance.buildCameraController.RestoreNormalCamera();

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

        // Round to grid
        newPosition = new Vector3(
            Mathf.Round(newPosition.x * 2f) / 2f,
            Mathf.Round(newPosition.y * 2f) / 2f,
            Mathf.Round(newPosition.z * 2f) / 2f
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

        // Select this block and update camera/arrows
        SelectBlock(newBlock);
        lastPlacementTime = Time.time;
    }

    private void SelectBlock(GameObject newBlock)
    {
        currentSelectedBlock = newBlock;

        if (GameManager.Instance != null && GameManager.Instance.buildCameraController != null)
            GameManager.Instance.buildCameraController.FocusOnBlock(newBlock);

        if (GameManager.Instance != null && GameManager.Instance.arrowSystem != null && playerController != null)
            GameManager.Instance.arrowSystem.ShowArrows(newBlock, playerController.playerCamera);
    }
}
