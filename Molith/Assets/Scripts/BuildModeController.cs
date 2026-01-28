using UnityEngine;
using SUPERCharacter;
using DG.Tweening;
using System.Collections.Generic;
using System.Linq;

public class BuildModeController : MonoBehaviour
{
    [Header("Settings")]
    public float blockPlacementDelay = 0.15f;

    private bool isInBuildMode = false;
    public bool IsInBuildMode => isInBuildMode;

    private bool isInDestructionMode = false;
    public bool IsInDestructionMode => isInDestructionMode;

    // Shared state for both build and destruction modes
    private bool isInOrbitMode = false; // True when camera is orbiting blocks (either mode)
    private GameObject currentSelectedBlock = null;
    private SUPERCharacterAIO playerController;
    private float lastPlacementTime = 0f;

    // E key toggle cooldown
    private float lastBuildModeToggleTime = 0f;
    private const float BUILD_MODE_TOGGLE_COOLDOWN = 0.3f;

    [Header("Platform Settings")]
    [Tooltip("Scale multiplier for platforms and grid system (1.0 = normal size)")]
    [Range(0.1f, 5.0f)]
    public float platformScale = 1.0f;

    [Header("Camera Orbit Settings")]
    [Tooltip("Decay factor for weighted average orbit center (0.5 = more recent blocks weighted more)")]
    [Range(0.1f, 0.9f)]
    public float orbitWeightDecayFactor = 0.5f;

    [Tooltip("Speed of lerping to new orbit center")]
    public float orbitCenterLerpSpeed = 5f;

    private Vector3 targetOrbitCenter;
    private Vector3 currentOrbitCenter;

    private Vector3 savedPlayerPosition;
    private float savedCameraDistance;
    private float savedBuildModeOrbitDistance; // Original buildModeOrbitDistance to restore after exit
    private float lockedYLevel = 0f; // Stores Y-level when entering build mode
    private Dictionary<GameObject, Material[]> originalMaterials = new Dictionary<GameObject, Material[]>();

    // Tween references to prevent overlapping lerps
    private Tweener cameraDistanceTween;

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
        // Handle destruction mode separately (can be entered without being in orbit mode yet)
        if (isInDestructionMode)
        {
            UpdateDestructionMode();
            return;
        }

        // Only process build mode input when in build mode
        if (!isInBuildMode) return;

        UpdateOrbitMode();

        // Update arrows continuously as camera rotates
        if (currentSelectedBlock != null && GameManager.Instance != null &&
            GameManager.Instance.arrowSystem != null && playerController != null)
        {
            GameManager.Instance.arrowSystem.UpdateArrowPositions(currentSelectedBlock, playerController.playerCamera);
        }

        // Update chain lines
        if (currentSelectedBlock != null && GameManager.Instance != null && GameManager.Instance.buildingSystem != null)
        {
            GameManager.Instance.buildingSystem.UpdateChainLines();
        }

        // Exit build mode with E key (same key as enter, with cooldown)
        if (Input.GetKeyDown(KeyCode.E) && Time.time - lastBuildModeToggleTime > BUILD_MODE_TOGGLE_COOLDOWN)
        {
            ExitBuildMode();
            lastBuildModeToggleTime = Time.time;
            return;
        }

        // DISABLED FOR CRYSTAL MODE - No sequential WASD placement
        /*
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
        */
    }

    /// <summary>
    /// Shared orbit mode update - freezes player position and lerps orbit center
    /// </summary>
    private void UpdateOrbitMode()
    {
        if (!isInOrbitMode) return;

        // Freeze player position in orbit mode
        if (playerController != null)
        {
            playerController.transform.position = savedPlayerPosition;

            // Smoothly lerp orbit center to target
            currentOrbitCenter = Vector3.Lerp(
                currentOrbitCenter,
                targetOrbitCenter,
                orbitCenterLerpSpeed * Time.deltaTime);
            playerController.buildModeOrbitCenter = currentOrbitCenter;
        }
    }

    /// <summary>
    /// Update logic specific to destruction mode
    /// </summary>
    private void UpdateDestructionMode()
    {
        UpdateOrbitMode();

        // Update destruction lines
        if (GameManager.Instance != null && GameManager.Instance.buildingSystem != null)
        {
            GameManager.Instance.buildingSystem.UpdatePersistentDestructionLines();
        }

        // Confirm destruction on Q release
        if (Input.GetKeyUp(KeyCode.Q))
        {
            if (GameManager.Instance != null && GameManager.Instance.buildingSystem != null)
            {
                GameManager.Instance.buildingSystem.OnDestructionConfirmed();
            }
        }
    }

    public void EnterBuildMode(GameObject firstBlock)
    {
        if (isInBuildMode || isInDestructionMode) return;

        isInBuildMode = true;
        lastBuildModeToggleTime = Time.time; // Set toggle time on entry

        // Lock Y-level to first block's Y position
        lockedYLevel = firstBlock.transform.position.y;
        Debug.Log($"BuildModeController: Locked Y-level to {lockedYLevel}");

        // Enter shared orbit mode
        EnterOrbitMode(firstBlock);

        // Sync platformScale to arrow system and show arrows
        if (GameManager.Instance != null && GameManager.Instance.arrowSystem != null && playerController != null)
        {
            GameManager.Instance.arrowSystem.platformScale = platformScale;
            GameManager.Instance.arrowSystem.ShowArrows(firstBlock, playerController.playerCamera);
        }

        // Apply hologram materials to all placed blocks
        ApplyHologramMaterialsToAllBlocks();

        // Initialize chain line system
        if (GameManager.Instance != null && GameManager.Instance.buildingSystem != null)
        {
            GameManager.Instance.buildingSystem.InitializeChainLine(firstBlock);
        }

        Debug.Log("BuildModeController: Entered Build Mode - Press SPACE to exit, WASD to place blocks");
    }

    public void ExitBuildMode()
    {
        if (!isInBuildMode) return;

        isInBuildMode = false;

        // Exit shared orbit mode
        ExitOrbitMode();

        // Hide arrows
        if (GameManager.Instance != null && GameManager.Instance.arrowSystem != null)
            GameManager.Instance.arrowSystem.HideArrows();

        // Clear chain lines
        if (GameManager.Instance != null && GameManager.Instance.buildingSystem != null)
        {
            GameManager.Instance.buildingSystem.ClearChainLines();
        }

        // Restore original materials to all blocks
        RestoreOriginalMaterials();

        Debug.Log("BuildModeController: Exited Build Mode");
    }

    private void PlaceBlockInDirection(KeyCode key)
    {
        // DISABLED FOR CRYSTAL MODE
        return;

        #pragma warning disable CS0162 // Unreachable code detected
        if (GameManager.Instance == null) return;
        if (currentSelectedBlock == null || GameManager.Instance.arrowSystem == null) return;

        Vector3 direction = GameManager.Instance.arrowSystem.GetPlacementDirection(key);
        if (direction == Vector3.zero) return;

        // Force direction to be horizontal-only (ignore vertical component)
        Vector3 horizontalDirection = new Vector3(direction.x, 0, direction.z).normalized;

        // Calculate new position using locked Y-level
        float gridIncrement = 1.0f * platformScale;
        Vector3 newPosition = currentSelectedBlock.transform.position +
                             horizontalDirection * (1.0f * platformScale);

        // Round to grid increments with LOCKED Y-LEVEL
        newPosition = new Vector3(
            Mathf.Round(newPosition.x / gridIncrement) * gridIncrement,
            lockedYLevel, // ALWAYS use locked Y-level
            Mathf.Round(newPosition.z / gridIncrement) * gridIncrement
        );

        Debug.Log($"BuildModeController: Placing block at Y={newPosition.y} (locked level)");

        // If block would be below ground level, try placing one level higher
        if (GameManager.Instance != null && GameManager.Instance.worldGenerator != null)
        {
            float groundHeight = GameManager.Instance.worldGenerator.GetGroundHeight(newPosition.x, newPosition.z);
            if (newPosition.y < groundHeight)
            {
                // Try one grid level higher
                float adjustedY = newPosition.y + gridIncrement;
                Debug.Log($"BuildModeController: Block Y={newPosition.y} below ground ({groundHeight}), trying Y={adjustedY}");

                // Check if adjusted position is above ground
                if (adjustedY >= groundHeight)
                {
                    newPosition.y = adjustedY;
                }
                else
                {
                    // Still below ground, cannot place
                    Debug.Log($"BuildModeController: Cannot place block - still below ground after adjustment");
                    return;
                }
            }
        }

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

        // Capture previous block for chain line before selecting new block
        GameObject previousBlock = currentSelectedBlock;

        // Select this block and update camera/arrows
        SelectBlock(newBlock);

        // Add chain line segment from previous block to new block
        if (GameManager.Instance.buildingSystem != null)
        {
            GameManager.Instance.buildingSystem.AddChainSegment(previousBlock, newBlock);
        }

        lastPlacementTime = Time.time;
        #pragma warning restore CS0162
    }

    private void SelectBlock(GameObject newBlock)
    {
        currentSelectedBlock = newBlock;

        // Update orbit center using weighted average of all placed blocks in chain
        if (playerController != null && GameManager.Instance?.buildingSystem != null)
        {
            targetOrbitCenter = CalculateWeightedOrbitCenter(
                GameManager.Instance.buildingSystem.GetChainBlockOrder());
        }

        // Sync platformScale to arrow system and update arrows
        if (GameManager.Instance != null && GameManager.Instance.arrowSystem != null && playerController != null)
        {
            GameManager.Instance.arrowSystem.platformScale = platformScale;
            GameManager.Instance.arrowSystem.ShowArrows(newBlock, playerController.playerCamera);
        }
    }

    /// <summary>
    /// Check if build mode toggle cooldown has elapsed
    /// </summary>
    public bool CanToggleBuildMode()
    {
        return Time.time - lastBuildModeToggleTime > BUILD_MODE_TOGGLE_COOLDOWN;
    }

    /// <summary>
    /// Calculates weighted exponential average of block positions.
    /// More recent blocks have higher weights (decay factor 0.5 means newest block has weight 1.0,
    /// previous has 0.5, then 0.25, etc.)
    /// </summary>
    private Vector3 CalculateWeightedOrbitCenter(List<GameObject> blockList)
    {
        if (blockList == null || blockList.Count == 0)
            return currentOrbitCenter;

        var validBlocks = blockList.Where(b => b != null).ToList();
        if (validBlocks.Count == 0)
            return currentOrbitCenter;

        float totalWeight = 0f;
        Vector3 weightedSum = Vector3.zero;
        int n = validBlocks.Count;

        for (int i = 0; i < n; i++)
        {
            // Weight = decayFactor^(n-1-i)
            // i=0 (oldest) has weight decayFactor^(n-1), i=n-1 (newest) has weight 1.0
            float weight = Mathf.Pow(orbitWeightDecayFactor, n - 1 - i);
            weightedSum += validBlocks[i].transform.position * weight;
            totalWeight += weight;
        }

        return weightedSum / totalWeight;
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

    #region Shared Orbit Mode Helpers

    /// <summary>
    /// Enters orbit mode - freezes player and sets up camera to orbit around target block
    /// </summary>
    private void EnterOrbitMode(GameObject targetBlock)
    {
        if (targetBlock == null || playerController == null) return;

        isInOrbitMode = true;
        currentSelectedBlock = targetBlock;

        // Save player position to restore later
        savedPlayerPosition = playerController.transform.position;

        // Save current camera distance and orbit distance
        savedCameraDistance = playerController.maxCameraDistInternal;
        savedBuildModeOrbitDistance = playerController.buildModeOrbitDistance;

        // Rotate player to face the target block (Y-axis only)
        Vector3 directionToBlock = targetBlock.transform.position - playerController.transform.position;
        directionToBlock.y = 0; // Flatten to XZ plane

        if (directionToBlock != Vector3.zero)
        {
            Quaternion targetRotation = Quaternion.LookRotation(directionToBlock);
            playerController.transform.rotation = targetRotation;

            // Position camera looking towards the block - use immediate rotation (false)
            // to avoid coroutine conflicts with orbit mode camera control
            float yaw = targetRotation.eulerAngles.y;
            float pitch = 30f; // Look slightly downward

            Vector3 cameraAngles = new Vector3(pitch, yaw, 0f);
            playerController.RotateView(cameraAngles, false);
        }

        // Freeze player movement but allow camera orbit
        playerController.PausePlayer(PauseModes.FreezeInPlace);
        playerController.enableCameraControl = true;
        playerController.buildModeOverride = true;

        // Initialize orbit centers (set immediately, no lerp on initial entry)
        currentOrbitCenter = targetBlock.transform.position;
        targetOrbitCenter = targetBlock.transform.position;
        playerController.buildModeOrbitCenter = currentOrbitCenter;

        // Kill any existing camera distance tween to prevent overlapping lerps
        if (cameraDistanceTween != null)
        {
            cameraDistanceTween.Kill();
            cameraDistanceTween = null;
        }

        // Smoothly transition camera distance by animating buildModeOrbitDistance
        // This is the value that SUPERCharacterAIO uses in UpdateCameraPosition_3rdPerson
        cameraDistanceTween = DOVirtual.Float(
            savedCameraDistance,
            savedBuildModeOrbitDistance,
            0.5f,
            value =>
            {
                if (playerController != null)
                {
                    // Animate buildModeOrbitDistance - SUPERCharacterAIO will use this
                    playerController.buildModeOrbitDistance = value;
                }
            }
        ).SetEase(Ease.OutCubic);

        // Force third-person perspective
        playerController.ChangePerspective(SUPERCharacter.PerspectiveModes._3rdPerson);
    }

    /// <summary>
    /// Exits orbit mode - restores player movement and camera
    /// </summary>
    private void ExitOrbitMode()
    {
        if (!isInOrbitMode) return;

        isInOrbitMode = false;
        currentSelectedBlock = null;

        // Kill any active camera distance tween to prevent conflicts
        if (cameraDistanceTween != null)
        {
            cameraDistanceTween.Kill();
            cameraDistanceTween = null;
        }

        if (playerController != null)
        {
            // Use UnpausePlayer to properly restore rigidbody constraints
            // (controllerPaused = false alone does not restore FreezeAll → FreezeRotation)
            playerController.UnpausePlayer();
            playerController.buildModeOverride = false;
            playerController.maxCameraDistInternal = savedCameraDistance;
            playerController.currentCameraZ = -savedCameraDistance;
            playerController.buildModeOrbitDistance = savedBuildModeOrbitDistance;
        }
    }

    /// <summary>
    /// Updates the orbit center to a new block (uses weighted average for destruction mode)
    /// </summary>
    public void UpdateOrbitTarget(GameObject newBlock)
    {
        if (newBlock == null || !isInOrbitMode) return;

        currentSelectedBlock = newBlock;

        // In destruction mode, use highlighted blocks for weighted average
        if (isInDestructionMode && GameManager.Instance?.buildingSystem != null)
        {
            targetOrbitCenter = CalculateWeightedOrbitCenter(
                GameManager.Instance.buildingSystem.GetHighlightedBlocks());
        }
        else
        {
            targetOrbitCenter = newBlock.transform.position;
        }
    }

    #endregion

    #region Destruction Mode

    /// <summary>
    /// Enters destruction mode with camera orbiting the first highlighted block
    /// </summary>
    public void EnterDestructionMode(GameObject firstBlock)
    {
        if (isInDestructionMode || isInBuildMode) return;
        if (firstBlock == null) return;

        isInDestructionMode = true;

        // Enter shared orbit mode
        EnterOrbitMode(firstBlock);

        Debug.Log("BuildModeController: Entered Destruction Mode - Press SPACE to cancel, release Q to confirm destruction");
    }

    /// <summary>
    /// Exits destruction mode and cancels any pending destruction
    /// </summary>
    public void ExitDestructionMode()
    {
        if (!isInDestructionMode) return;

        isInDestructionMode = false;

        // Exit shared orbit mode
        ExitOrbitMode();

        // Cancel destruction in BuildingSystem
        if (GameManager.Instance != null && GameManager.Instance.buildingSystem != null)
        {
            GameManager.Instance.buildingSystem.CancelDestructionMode();
        }

        Debug.Log("BuildModeController: Exited Destruction Mode");
    }

    /// <summary>
    /// Called when destruction is confirmed (Q released) - exits orbit but doesn't cancel
    /// </summary>
    public void ConfirmDestruction()
    {
        if (!isInDestructionMode) return;

        isInDestructionMode = false;

        // Exit orbit mode without canceling destruction
        ExitOrbitMode();

        Debug.Log("BuildModeController: Destruction confirmed");
    }

    #endregion

    void OnDestroy()
    {
        // Kill any active tweens to prevent null reference errors
        if (cameraDistanceTween != null)
        {
            cameraDistanceTween.Kill();
            cameraDistanceTween = null;
        }
    }
}
