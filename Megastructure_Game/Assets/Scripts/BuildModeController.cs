using UnityEngine;
using SUPERCharacter;
using DG.Tweening;

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

    [Header("Platform Settings")]
    [Tooltip("Scale multiplier for platforms and grid system (1.0 = normal size)")]
    [Range(0.1f, 5.0f)]
    public float platformScale = 1.0f;

    private Vector3 savedPlayerPosition;
    private float savedCameraDistance;
    private System.Collections.Generic.Dictionary<GameObject, Material[]> originalMaterials = new System.Collections.Generic.Dictionary<GameObject, Material[]>();

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

        // Sync grid cell size with adjacency grid
        if (GameManager.Instance != null && GameManager.Instance.adjacencyGrid != null)
        {
            GameManager.Instance.adjacencyGrid.SetGridCellSize(platformScale);
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
            GameManager.Instance.arrowSystem.ShowArrows(firstBlock, playerController.playerCamera);
        }

        // Apply hologram materials to all placed blocks
        ApplyHologramMaterialsToAllBlocks();

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

        // Restore original materials to all blocks
        RestoreOriginalMaterials();

        Debug.Log("BuildModeController: Exited Build Mode");
    }

    private void PlaceBlockInDirection(KeyCode key)
    {
        if (GameManager.Instance == null) return;
        if (currentSelectedBlock == null || GameManager.Instance.arrowSystem == null) return;

        Vector3 direction = GameManager.Instance.arrowSystem.GetPlacementDirection(key);
        if (direction == Vector3.zero) return;

        // Detect if placement is diagonal (both X and Z components present)
        bool isDiagonal = Mathf.Abs(direction.x) > 0.1f && Mathf.Abs(direction.z) > 0.1f;

        // Calculate base position: 1.0 * platformScale distance in horizontal direction
        Vector3 horizontalDirection = new Vector3(direction.x, 0, direction.z).normalized;
        Vector3 newPosition = currentSelectedBlock.transform.position + horizontalDirection * (1.0f * platformScale);

        // Round to 1.0 * platformScale grid increments
        float gridIncrement = 1.0f * platformScale;
        newPosition = new Vector3(
            Mathf.Round(newPosition.x / gridIncrement) * gridIncrement,
            currentSelectedBlock.transform.position.y, // Start at same Y level
            Mathf.Round(newPosition.z / gridIncrement) * gridIncrement
        );

        // If diagonal placement, add +1 height level
        if (isDiagonal)
        {
            newPosition.y += gridIncrement; // +1 level
        }

        // Round Y to integer levels
        newPosition.y = Mathf.Round(newPosition.y / gridIncrement) * gridIncrement;

        // Check if platform already exists at this XZ coordinate
        GameObject existingPlatform = null;
        if (GameManager.Instance.adjacencyGrid != null)
        {
            existingPlatform = GameManager.Instance.adjacencyGrid.GetPlatformAtXZ(newPosition.x, newPosition.z);
        }

        // If platform exists at this XZ, compare heights
        if (existingPlatform != null)
        {
            float existingY = existingPlatform.transform.position.y;

            if (newPosition.y <= existingY)
            {
                Debug.Log($"BuildModeController: Cannot place lower platform at same XZ coordinate! Existing Y={existingY}, New Y={newPosition.y}");
                return;
            }

            // Destroy existing platform (instant replacement)
            Debug.Log($"BuildModeController: Replacing lower platform at ({newPosition.x}, {newPosition.z}). Old Y={existingY}, New Y={newPosition.y}");

            // Kill any existing tweens
            existingPlatform.transform.DOKill();

            // Unregister and destroy old platform
            if (GameManager.Instance.adjacencyGrid != null)
            {
                GameManager.Instance.adjacencyGrid.UnregisterBlock(existingPlatform.transform.position);
            }
            if (GameManager.Instance.buildingSystem != null)
            {
                GameManager.Instance.buildingSystem.RemoveBlock(existingPlatform);
            }

            // Destroy scaffolding
            ScaffoldingManager oldScaffolding = existingPlatform.GetComponent<ScaffoldingManager>();
            if (oldScaffolding != null)
            {
                oldScaffolding.DestroyScaffolding();
            }

            Destroy(existingPlatform);
        }

        // Check block limit
        if (GameManager.Instance.buildingSystem.CurrentBlockCount >= GameManager.Instance.buildingSystem.maxBlocks)
        {
            Debug.Log($"BuildModeController: Block limit ({GameManager.Instance.buildingSystem.maxBlocks}) reached!");
            return;
        }

        // Place new platform with NO ROTATION (always axis-aligned)
        Quaternion blockRotation = Quaternion.identity;

        // Place new block with scale
        GameObject newBlock = Instantiate(GameManager.Instance.buildingSystem.cubePrefab, newPosition, blockRotation);

        // Animate block placement (scale from zero to target scale)
        Vector3 targetScale = Vector3.one * platformScale;
        newBlock.transform.localScale = Vector3.zero;
        newBlock.transform.DOScale(targetScale, 0.15f)
            .SetEase(Ease.OutBack, 1.2f)
            .OnStart(() => AudioEventDispatcher.PlaySound(SoundID.BlockPlace));

        // Apply hologram material immediately when instantiating in build mode
        if (GameManager.Instance != null && GameManager.Instance.hologramMaterial != null && isInBuildMode)
        {
            // Apply to ALL renderers in children (handles empty container structure)
            MeshRenderer[] renderers = newBlock.GetComponentsInChildren<MeshRenderer>();
            foreach (MeshRenderer renderer in renderers)
            {
                if (renderer != null)
                {
                    Material[] hologramMaterials = new Material[renderer.materials.Length];
                    for (int i = 0; i < hologramMaterials.Length; i++)
                    {
                        hologramMaterials[i] = new Material(GameManager.Instance.hologramMaterial);
                        hologramMaterials[i].SetColor("_Color", GameManager.Instance.buildModeColor);
                    }
                    renderer.materials = hologramMaterials;
                }
            }
        }

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

        // Generate scaffolding for this platform
        ScaffoldingManager scaffoldingManager = newBlock.AddComponent<ScaffoldingManager>();
        GameObject scaffoldPrefab = GameManager.Instance.buildingSystem.scaffoldingPrefab != null
            ? GameManager.Instance.buildingSystem.scaffoldingPrefab
            : GameManager.Instance.buildingSystem.cubePrefab;
        scaffoldingManager.Initialize(newBlock, scaffoldPrefab, platformScale, 0f);

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

        // Sync platformScale to arrow system and update arrows
        if (GameManager.Instance != null && GameManager.Instance.arrowSystem != null && playerController != null)
        {
            GameManager.Instance.arrowSystem.platformScale = platformScale;
            GameManager.Instance.arrowSystem.ShowArrows(newBlock, playerController.playerCamera);
        }
    }

    /// <summary>
    /// Applies hologram material to all placed blocks
    /// </summary>
    private void ApplyHologramMaterialsToAllBlocks()
    {
        if (GameManager.Instance == null || GameManager.Instance.hologramMaterial == null || GameManager.Instance.buildingSystem == null)
            return;

        // Get all placed blocks from BuildingSystem
        var buildingSystem = GameManager.Instance.buildingSystem;
        if (buildingSystem == null) return;

        // We need to access the placed blocks - let's use reflection or add a public property
        // For now, let's iterate through all GameObjects with the block tag or find them
        GameObject[] allBlocks = GameObject.FindGameObjectsWithTag("Block");

        foreach (GameObject block in allBlocks)
        {
            if (block != null)
                ApplyHologramMaterialToBlock(block);
        }
    }

    /// <summary>
    /// Applies hologram material to a single block
    /// </summary>
    private void ApplyHologramMaterialToBlock(GameObject block)
    {
        if (GameManager.Instance == null || GameManager.Instance.hologramMaterial == null || block == null)
            return;

        // Apply hologram material to ALL renderers in platform children (handles empty container structure)
        MeshRenderer[] renderers = block.GetComponentsInChildren<MeshRenderer>();
        if (renderers != null && renderers.Length > 0)
        {
            // Save original materials if not already saved (only save first renderer for reference)
            if (!originalMaterials.ContainsKey(block))
            {
                originalMaterials[block] = renderers[0].materials;
            }

            // Apply hologram material to ALL children with renderers
            foreach (MeshRenderer renderer in renderers)
            {
                if (renderer != null)
                {
                    Material[] hologramMaterials = new Material[renderer.materials.Length];
                    for (int i = 0; i < hologramMaterials.Length; i++)
                    {
                        hologramMaterials[i] = new Material(GameManager.Instance.hologramMaterial);
                        hologramMaterials[i].SetColor("_Color", GameManager.Instance.buildModeColor);
                    }
                    renderer.materials = hologramMaterials;
                }
            }

            // Update scaffolding materials
            ScaffoldingManager scaffolding = block.GetComponent<ScaffoldingManager>();
            if (scaffolding != null)
            {
                Material hologramMat = new Material(GameManager.Instance.hologramMaterial);
                hologramMat.SetColor("_Color", GameManager.Instance.buildModeColor);
                scaffolding.UpdateMaterial(hologramMat);
            }
        }
    }

    /// <summary>
    /// Restores original materials to all blocks
    /// </summary>
    private void RestoreOriginalMaterials()
    {
        foreach (var kvp in originalMaterials)
        {
            GameObject block = kvp.Key;
            Material[] savedMaterials = kvp.Value;

            if (block != null)
            {
                // Restore materials to ALL renderers in children (handles empty container structure)
                MeshRenderer[] renderers = block.GetComponentsInChildren<MeshRenderer>();
                if (renderers != null && renderers.Length > 0)
                {
                    // Restore original material to all children
                    foreach (MeshRenderer renderer in renderers)
                    {
                        if (renderer != null && savedMaterials.Length > 0)
                        {
                            renderer.materials = savedMaterials;
                        }
                    }

                    // Restore scaffolding materials
                    ScaffoldingManager scaffolding = block.GetComponent<ScaffoldingManager>();
                    if (scaffolding != null && savedMaterials.Length > 0)
                    {
                        scaffolding.UpdateMaterial(savedMaterials[0]);
                    }
                }
            }
        }

        originalMaterials.Clear();
    }
}
