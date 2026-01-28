using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using DG.Tweening;
using SUPERCharacter;

public class BuildingSystem : MonoBehaviour
{
    [Header("References")]
    [Tooltip("The cube prefab to place")]
    public GameObject cubePrefab;

    [Tooltip("The scaffolding cube prefab (supports platforms)")]
    public GameObject scaffoldingPrefab;

    [Header("Placement Settings")]
    [Tooltip("Distance from camera to place blocks")]
    public float placementDistance = 5f;

    [Tooltip("Key to hold for placing blocks")]
    public KeyCode placeKey = KeyCode.E;

    [Tooltip("Maximum raycast distance for preview placement")]
    public float maxRaycastDistance = 100f;

    [Tooltip("LayerMask for raycast (should include ground and blocks)")]
    public LayerMask placementRaycastLayers = -1; // Default: all layers

    [Header("Visual Settings")]
    [Tooltip("Optional LineRenderer prefab for raycast visualization. If not assigned, one will be created automatically.")]
    public GameObject raycastLinePrefab;

    [Tooltip("Reference to the player's backpack transform (line will be drawn from here)")]
    public Transform playerBackpack;

    [Tooltip("Show raycast line visualization")]
    public bool showRaycastLine = true;

    [Tooltip("Width of raycast line (used when no prefab is assigned)")]
    public float raycastLineWidth = 0.02f;

    [Tooltip("Number of segments in the parabola curve")]
    public int parabolaSegments = 20;

    [Tooltip("Horizontal curvature of the parabola arc (sideways displacement)")]
    public float parabolaHorizontalCurve = 2f;

    [Tooltip("Duration of line endpoint transition between blocks")]
    public float lineTransitionDuration = 0.3f;

    [Header("Destruction Settings")]
    [Tooltip("Key to hold for destroying blocks")]
    public KeyCode destroyKey = KeyCode.Q;

    [Header("Animation Settings")]
    [Tooltip("Duration of preview spawn animation")]
    public float previewSpawnDuration = 0.2f;

    [Tooltip("Duration of block placement animation")]
    public float placementDuration = 0.15f;

    [Tooltip("Duration of block destruction animation")]
    public float destructionDuration = 0.2f;

    [Header("Block Limit Settings")]
    [Tooltip("Maximum number of blocks that can be placed at once")]
    public int maxBlocks = 50;

    private GameObject currentPreview;
    private bool isPlacementMode = false;
    private bool isDestructionMode = false;
    private Vector3 previewPosition;
    private bool lastRaycastHit = false; // Track if raycast hit something for placement validation
    private readonly HashSet<GameObject> placedBlocks = new();
    private Material previewMaterialInstance;
    private Material destructionMaterialInstance;
    private LineRenderer raycastLineRenderer;
    private Vector3 currentLineEndpoint; // Current endpoint for lerping
    private Tweener lineEndpointTween; // Active tween for line endpoint

    private readonly Dictionary<GameObject, Material[]> originalMaterials = new();
    private readonly Dictionary<GameObject, LineRenderer> destructionLines = new(); // Lines for each block being destroyed

    // FIFO Destruction System
    private readonly List<GameObject> fifoBlockList = new(); // Maintains insertion order
    private readonly List<GameObject> highlightedBlocks = new(); // Currently highlighted blocks
    private Coroutine highlightCoroutine = null;
    private float currentHighlightDelay;

    // Chain line system for build mode
    private readonly List<LineRenderer> chainLineRenderers = new();
    private readonly List<GameObject> chainBlockOrder = new();
    private LineRenderer activeChainSegment = null;
    private Vector3 activeSegmentStartPos;
    private Vector3 activeSegmentEndpoint;
    private Tweener activeSegmentTween;

    [Header("Crystal Growth Settings")]
    [Tooltip("List of crystal prefabs to randomly select from when building")]
    public List<GameObject> crystalPrefabs = new();

    [Tooltip("Proportion of hit-to-player distance used as max crystal length (0-1)")]
    [Range(0f, 1f)]
    public float maxLengthRatio = 0.75f;

    [Tooltip("Minimum crystal radius")]
    public float minCrystalRadius = 0.1f;

    [Tooltip("Maximum crystal radius")]
    public float maxCrystalRadius = 0.5f;

    [Tooltip("Number of rays to cast when calculating available space for crystal radius")]
    public int radiusProbeCount = 8;

    [Tooltip("Maximum scale of the crystal at full growth (Y=length axis)")]
    public float crystalMaxScale = 5f;

    [Tooltip("Base growth speed in units per second (at maximum distance)")]
    public float crystalGrowthSpeed = 8f;

    [Tooltip("Crystal stops growing when its tip gets this close to the player")]
    public float crystalStopDistance = 1.5f;

    [Tooltip("Growth speed curve based on proximity (X=0 means at stop distance, X=1 means far away, Y=speed multiplier)")]
    public AnimationCurve crystalGrowthCurve = new AnimationCurve(
        new Keyframe(0f, 0f, 0f, 0.5f),     // Stopped at stop distance
        new Keyframe(0.3f, 0.4f, 1f, 1f),   // Slow when close
        new Keyframe(1f, 1f, 0.5f, 0f)      // Full speed when far
    );

    // Crystal state
    private Vector3 crystalBasePosition;
    private Vector3 crystalSurfaceNormal;     // Normal of the surface at contact point
    private Vector3 crystalGrowthDirection;   // Direction from hit point toward player
    private Quaternion crystalRotation;
    private bool isCrystalGrowing = false;
    private float currentCrystalRadius;      // Calculated radius based on available space
    private float currentMaxCrystalLength;   // Max length before hitting obstacle (min of 75% distance and collision)
    private float currentCrystalLength;      // Current growth length (stops if collision detected)
    private bool crystalCollisionStopped = false; // Whether growth stopped due to collision
    private float initialDistanceToPlayer;   // Distance from base to player at time of placement
    private GameObject selectedCrystalPrefab; // Randomly chosen prefab for this crystal

    [Header("FIFO Destruction Settings")]
    [Tooltip("Initial delay before first block starts highlighting (seconds)")]
    public float initialHighlightDelay = 0.5f;

    [Tooltip("Minimum delay between block highlights as speed increases (seconds)")]
    public float minHighlightDelay = 0.05f;

    [Tooltip("Rate at which highlighting accelerates (multiply delay by this each time)")]
    public float accelerationRate = 0.95f;

    [Tooltip("Delay between each block destruction in the sequence (seconds)")]
    public float destructionSequenceDelay = 0.1f;

    public int CurrentBlockCount => placedBlocks.Count;
    public int RemainingBlocks => maxBlocks - CurrentBlockCount;
    public bool IsPlacementActive => isPlacementMode || isCrystalGrowing;
    public bool IsDestructionActive => isDestructionMode;

    void Start()
    {
        if (GameManager.Instance == null)
        {
            Debug.LogError("BuildingSystem: GameManager instance is missing!");
            return;
        }

        if (cubePrefab == null)
        {
            Debug.LogError("BuildingSystem: CubePrefab is not assigned! Please assign it in the inspector.");
        }

        if (GameManager.Instance.hologramMaterial == null)
        {
            Debug.LogWarning("BuildingSystem: HologramMaterial is missing on GameManager. Creating a default hologram material.");
            CreateDefaultHologramMaterial();
        }

        previewMaterialInstance = new Material(GameManager.Instance.hologramMaterial);
        previewMaterialInstance.SetColor("_Color", GameManager.Instance.placementColor);

        destructionMaterialInstance = new Material(GameManager.Instance.hologramMaterial);
        destructionMaterialInstance.SetColor("_Color", GameManager.Instance.destructionColor);

        // Initialize LineRenderer for raycast visualization
        InitializeRaycastLineRenderer();
    }

    void InitializeRaycastLineRenderer()
    {
        GameObject lineObj;

        // Use prefab if assigned, otherwise create programmatically
        if (raycastLinePrefab != null)
        {
            lineObj = Instantiate(raycastLinePrefab, transform);
            lineObj.name = "RaycastLine";
            raycastLineRenderer = lineObj.GetComponent<LineRenderer>();

            if (raycastLineRenderer == null)
            {
                Debug.LogWarning("BuildingSystem: Assigned raycast line prefab does not have a LineRenderer component! Creating one programmatically instead.");
                raycastLineRenderer = lineObj.AddComponent<LineRenderer>();
            }
        }
        else
        {
            // Fallback: create LineRenderer programmatically
            lineObj = new GameObject("RaycastLine");
            lineObj.transform.SetParent(transform);
            raycastLineRenderer = lineObj.AddComponent<LineRenderer>();
        }

        // Configure LineRenderer for parabola (prefab settings will override these if prefab is used)
        raycastLineRenderer.positionCount = parabolaSegments + 1;

        // Only apply these settings if creating programmatically (no prefab)
        if (raycastLinePrefab == null)
        {
            raycastLineRenderer.startWidth = raycastLineWidth;
            raycastLineRenderer.endWidth = raycastLineWidth;
            raycastLineRenderer.material = new Material(Shader.Find("Sprites/Default"));
        }

        // Set initial color from GameManager (for both prefab and programmatic)
        Color initialColor = GameManager.Instance != null ? GameManager.Instance.placementColor : Color.cyan;
        SetLineRendererColor(raycastLineRenderer, initialColor);

        raycastLineRenderer.enabled = false;
    }

    void Update()
    {
        if (GameManager.Instance == null || GameManager.Instance.playerController == null)
            return;

        if (GameManager.Instance.playerController.playerCamera == null)
            return;

        HandlePlacementInput();
        HandleDestructionInput();
        UpdatePersistentDestructionLines();
    }

    void HandlePlacementInput()
    {
        // Don't allow placement if in build mode or destruction mode
        if (GameManager.Instance != null && GameManager.Instance.buildModeController != null)
        {
            if (GameManager.Instance.buildModeController.IsInBuildMode)
                return;

            // Check cooldown to prevent immediate re-entry after exiting build mode
            if (!GameManager.Instance.buildModeController.CanToggleBuildMode())
                return;
        }

        if (isDestructionMode)
            return;

        // === CRYSTAL GROWTH FLOW ===

        // KeyDown: Start crystal at raycast hit point
        if (Input.GetKeyDown(placeKey))
        {
            Camera cam = GameManager.Instance.playerController.playerCamera;
            Vector3 rayOrigin = cam.transform.position;
            Vector3 rayDirection = cam.transform.forward;

            if (Physics.Raycast(rayOrigin, rayDirection, out RaycastHit hit,
                maxRaycastDistance, placementRaycastLayers))
            {
                isPlacementMode = true;
                isCrystalGrowing = true;

                // Freeze player movement — building and moving are mutually exclusive
                SetPlayerMovementLocked(true);

                // Store crystal anchor and orientation
                crystalBasePosition = hit.point;
                crystalSurfaceNormal = hit.normal;

                // Growth direction points directly from hit point toward the player
                crystalGrowthDirection = (rayOrigin - hit.point).normalized;

                // Store initial distance — max length is 75% of this
                initialDistanceToPlayer = Vector3.Distance(hit.point, rayOrigin);

                // Rotation: align cylinder's Y-axis (up) with growth direction
                crystalRotation = Quaternion.FromToRotation(Vector3.up, crystalGrowthDirection);

                // Randomly select a crystal prefab
                if (crystalPrefabs != null && crystalPrefabs.Count > 0)
                    selectedCrystalPrefab = crystalPrefabs[Random.Range(0, crystalPrefabs.Count)];
                else
                    selectedCrystalPrefab = null;

                CreateCrystalPreview();
                lastRaycastHit = true;
            }
        }

        // Key Held: Grow crystal
        if (Input.GetKey(placeKey) && isCrystalGrowing)
        {
            UpdateCrystalGrowth();
        }

        // KeyUp: Finalize crystal
        if (Input.GetKeyUp(placeKey) && isCrystalGrowing)
        {
            PlaceCrystal();
            isCrystalGrowing = false;
            isPlacementMode = false;

            // Restore player movement
            SetPlayerMovementLocked(false);
        }
    }

    void HandleDestructionInput()
    {
        // Don't allow destruction if building
        if (isPlacementMode || isCrystalGrowing)
            return;

        // Don't allow destruction if in build mode
        if (GameManager.Instance != null && GameManager.Instance.buildModeController != null)
        {
            if (GameManager.Instance.buildModeController.IsInBuildMode)
            {
                // Clean up if we were in destruction mode
                if (isDestructionMode)
                {
                    CancelDestructionMode();
                }
                return;
            }

            // Also check if already in destruction mode (handled by BuildModeController)
            if (GameManager.Instance.buildModeController.IsInDestructionMode)
            {
                return; // Input is handled by BuildModeController.Update()
            }
        }

        // Key pressed - start highlighting
        if (Input.GetKeyDown(destroyKey))
        {
            if (fifoBlockList.Count == 0)
                return; // No blocks to destroy

            isDestructionMode = true;
            currentHighlightDelay = initialHighlightDelay;
            highlightedBlocks.Clear();

            // Freeze player movement — destroying and moving are mutually exclusive
            SetPlayerMovementLocked(true);

            // Start highlighting coroutine
            if (highlightCoroutine != null)
                StopCoroutine(highlightCoroutine);

            highlightCoroutine = StartCoroutine(HighlightBlocksSequentially());
        }

        // Key released - trigger destruction (only if not in orbit mode yet, or if orbit mode handles it)
        if (Input.GetKeyUp(destroyKey) && isDestructionMode)
        {
            ConfirmAndTriggerDestruction();
        }
    }

    /// <summary>
    /// Called when Q is released to confirm destruction
    /// </summary>
    private void ConfirmAndTriggerDestruction()
    {
        isDestructionMode = false;

        // Stop highlighting
        if (highlightCoroutine != null)
        {
            StopCoroutine(highlightCoroutine);
            highlightCoroutine = null;
        }

        // Reset line color back to build mode color
        if (GameManager.Instance != null)
        {
            SetLineColor(GameManager.Instance.placementColor);
        }

        // Hide the line when releasing destroy key
        HideRaycastLine();

        // Restore player movement (must happen regardless of path)
        SetPlayerMovementLocked(false);

        // If no blocks highlighted (quick release), just exit destruction mode
        if (highlightedBlocks.Count == 0)
        {
            // Tell BuildModeController to exit destruction mode if it was entered
            if (GameManager.Instance != null && GameManager.Instance.buildModeController != null &&
                GameManager.Instance.buildModeController.IsInDestructionMode)
            {
                GameManager.Instance.buildModeController.ConfirmDestruction();
            }
            return;
        }

        // Tell BuildModeController to exit destruction orbit mode
        if (GameManager.Instance != null && GameManager.Instance.buildModeController != null &&
            GameManager.Instance.buildModeController.IsInDestructionMode)
        {
            GameManager.Instance.buildModeController.ConfirmDestruction();
        }

        // Trigger destruction sequence
        StartCoroutine(DestroyBlocksSequentially());
    }

    /// <summary>
    /// Called by BuildModeController when Q is released during destruction orbit mode
    /// </summary>
    public void OnDestructionConfirmed()
    {
        ConfirmAndTriggerDestruction();
    }

    IEnumerator HighlightBlocksSequentially()
    {
        // Set line to destruction color immediately when entering destroy mode
        if (GameManager.Instance != null)
        {
            SetLineColor(GameManager.Instance.destructionColor);
        }

        int blockIndex = 0;
        bool enteredOrbitMode = false;

        while (isDestructionMode && blockIndex < fifoBlockList.Count)
        {
            GameObject block = fifoBlockList[blockIndex];

            // Safety check - block might have been destroyed
            if (block != null && placedBlocks.Contains(block))
            {
                // Apply destruction material to ALL renderers in platform children
                MeshRenderer[] renderers = block.GetComponentsInChildren<MeshRenderer>();
                if (renderers != null && renderers.Length > 0)
                {
                    // Store original materials if not already stored (only save first for reference)
                    if (!originalMaterials.ContainsKey(block))
                    {
                        originalMaterials[block] = renderers[0].materials;
                    }

                    // Apply destruction material to ALL children with renderers
                    foreach (MeshRenderer renderer in renderers)
                    {
                        if (renderer != null)
                        {
                            Material[] destructionMaterials = new Material[renderer.materials.Length];
                            for (int i = 0; i < destructionMaterials.Length; i++)
                            {
                                destructionMaterials[i] = destructionMaterialInstance;
                            }
                            renderer.materials = destructionMaterials;
                        }
                    }

                    // Apply destruction material to scaffolding
                    ScaffoldingManager scaffolding = block.GetComponent<ScaffoldingManager>();
                    if (scaffolding != null)
                    {
                        scaffolding.UpdateMaterial(destructionMaterialInstance);
                    }
                }

                // Add to highlighted list
                highlightedBlocks.Add(block);

                // Create persistent line for this block
                CreatePersistentDestructionLine(block);

                // Update main line to point at this block in destroy mode
                UpdateLineToBlock(block, true);

                // Enter orbit mode on first block, update orbit target on subsequent blocks
                if (GameManager.Instance != null && GameManager.Instance.buildModeController != null)
                {
                    if (!enteredOrbitMode)
                    {
                        // Enter destruction orbit mode with camera following this block
                        GameManager.Instance.buildModeController.EnterDestructionMode(block);
                        enteredOrbitMode = true;
                    }
                    else
                    {
                        // Update orbit target to the latest highlighted block
                        GameManager.Instance.buildModeController.UpdateOrbitTarget(block);
                    }
                }
            }

            blockIndex++;

            // Accelerate highlighting speed (multiply delay by accelerationRate)
            currentHighlightDelay = Mathf.Max(minHighlightDelay, currentHighlightDelay * accelerationRate);

            // Wait before highlighting next block
            yield return new WaitForSeconds(currentHighlightDelay);
        }

        // All blocks highlighted
        highlightCoroutine = null;
    }

    IEnumerator DestroyBlocksSequentially()
    {
        // Create a copy since we'll modify during iteration
        List<GameObject> blocksToDestroy = new List<GameObject>(highlightedBlocks);

        foreach (GameObject block in blocksToDestroy)
        {
            if (block == null || !placedBlocks.Contains(block))
                continue; // Block already destroyed

            // Remove from all tracking structures
            placedBlocks.Remove(block);
            fifoBlockList.Remove(block);

            // Unregister from adjacency grid
            if (GameManager.Instance != null && GameManager.Instance.adjacencyGrid != null)
            {
                GameManager.Instance.adjacencyGrid.UnregisterBlock(block.transform.position);
            }

            // Clean up material tracking
            if (originalMaterials.ContainsKey(block))
            {
                originalMaterials.Remove(block);
            }

            // Destroy scaffolding
            ScaffoldingManager scaffolding = block.GetComponent<ScaffoldingManager>();
            if (scaffolding != null)
            {
                scaffolding.DestroyScaffolding();
            }

            // Capture reference for closure
            GameObject blockToDestroy = block;

            // Kill existing tweens
            block.transform.DOKill();

            // Animate destruction (reuse existing animation)
            block.transform.DOScale(Vector3.zero, destructionDuration)
                .SetEase(Ease.InBack)
                .OnStart(() => AudioEventDispatcher.PlaySound(SoundID.BlockRemove))
                .OnComplete(() =>
                {
                    if (blockToDestroy != null)
                    {
                        // Destroy the persistent line for this block
                        if (destructionLines.ContainsKey(blockToDestroy))
                        {
                            LineRenderer lineToDestroy = destructionLines[blockToDestroy];
                            if (lineToDestroy != null)
                            {
                                Destroy(lineToDestroy.gameObject);
                            }
                            destructionLines.Remove(blockToDestroy);
                        }

                        Destroy(blockToDestroy);
                    }
                });

            // Wait before destroying next block
            yield return new WaitForSeconds(destructionSequenceDelay);
        }

        // Clear highlighted list
        highlightedBlocks.Clear();
    }

    public void CancelDestructionMode()
    {
        isDestructionMode = false;

        // Stop highlighting coroutine
        if (highlightCoroutine != null)
        {
            StopCoroutine(highlightCoroutine);
            highlightCoroutine = null;
        }

        // Restore materials for all highlighted blocks
        foreach (GameObject block in highlightedBlocks)
        {
            if (block != null)
            {
                RestoreBlockMaterial(block);
            }
        }

        highlightedBlocks.Clear();

        // Clean up all persistent destruction lines
        foreach (var kvp in destructionLines)
        {
            if (kvp.Value != null)
            {
                Destroy(kvp.Value.gameObject);
            }
        }
        destructionLines.Clear();

        // Reset line color back to build mode color
        if (GameManager.Instance != null)
        {
            SetLineColor(GameManager.Instance.placementColor);
        }

        // Hide the line when exiting destruction mode
        HideRaycastLine();

        // Restore player movement
        SetPlayerMovementLocked(false);
    }

    void CreatePreviewBlock()
    {
        if (cubePrefab == null)
            return;

        currentPreview = Instantiate(cubePrefab);

        // Disable colliders on preview
        Collider[] previewColliders = currentPreview.GetComponentsInChildren<Collider>();
        foreach (Collider collider in previewColliders)
        {
            if (collider != null)
            {
                collider.enabled = false;
            }
        }

        // Apply preview material to ALL renderers in children (handles empty container structure)
        if (previewMaterialInstance != null)
        {
            MeshRenderer[] renderers = currentPreview.GetComponentsInChildren<MeshRenderer>();
            foreach (MeshRenderer renderer in renderers)
            {
                if (renderer != null)
                {
                    Material[] previewMaterials = new Material[renderer.materials.Length];
                    for (int i = 0; i < previewMaterials.Length; i++)
                    {
                        previewMaterials[i] = previewMaterialInstance;
                    }
                    renderer.materials = previewMaterials;
                }
            }
        }

        // Get platform scale from BuildModeController
        float platformScale = 1.0f;
        if (GameManager.Instance != null && GameManager.Instance.buildModeController != null)
        {
            platformScale = GameManager.Instance.buildModeController.platformScale;
        }

        currentPreview.transform.localScale = Vector3.one * platformScale;

        UpdatePreviewPosition();
    }

    void UpdatePreviewPosition()
    {
        Camera cam = GameManager.Instance.playerController.playerCamera;
        Vector3 rayOrigin = cam.transform.position;
        Vector3 rayDirection = cam.transform.forward;

        RaycastHit hit;
        bool hitSomething = Physics.Raycast(rayOrigin, rayDirection, out hit,
                                            maxRaycastDistance, placementRaycastLayers);

        // Store hit state for placement validation
        lastRaycastHit = hitSomething;

        if (hitSomething)
        {
            previewPosition = hit.point;
        }
        else
        {
            previewPosition = rayOrigin + rayDirection * maxRaycastDistance;
        }

        // Only show preview if raycast hit something
        if (isPlacementMode && currentPreview != null)
        {
            currentPreview.SetActive(hitSomething);

            if (hitSomething)
            {
                currentPreview.transform.position = previewPosition;
            }
        }

        // Update LineRenderer visualization with parabola
        if (showRaycastLine && raycastLineRenderer != null)
        {
            // Only show line in placement mode and if raycast hit something
            raycastLineRenderer.enabled = isPlacementMode && hitSomething;

            if (isPlacementMode && hitSomething)
            {
                // Use backpack position if assigned, otherwise fall back to camera position
                Vector3 startPos = playerBackpack != null ? playerBackpack.position : rayOrigin;
                Vector3 endPos = hit.point;

                // Draw parabola from backpack to hit point with horizontal curve for preview mode
                DrawParabola(startPos, endPos, true);
            }
        }
    }

    void CreateCrystalPreview()
    {
        // Calculate available radius at contact point (uses surface normal for radial probing)
        currentCrystalRadius = CalculateAvailableRadius(crystalBasePosition, crystalSurfaceNormal);

        // Max length = 75% of distance to player, capped by obstacle collision
        float distanceBasedMax = initialDistanceToPlayer * maxLengthRatio;
        float obstacleMax = CalculateMaxGrowthLength(crystalBasePosition, crystalGrowthDirection);
        currentMaxCrystalLength = Mathf.Min(distanceBasedMax, obstacleMax);

        // Reset growth state
        currentCrystalLength = 0.1f;
        crystalCollisionStopped = false;

        // Create preview from prefab or fallback to primitive cylinder
        if (selectedCrystalPrefab != null)
        {
            currentPreview = Instantiate(selectedCrystalPrefab);
            currentPreview.name = "CrystalPreview";
        }
        else
        {
            currentPreview = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            currentPreview.name = "CrystalPreview";
        }

        // Disable colliders on preview
        Collider[] colliders = currentPreview.GetComponentsInChildren<Collider>();
        foreach (Collider col in colliders)
        {
            if (col != null) col.enabled = false;
        }

        // Apply preview material to all renderers
        if (previewMaterialInstance != null)
        {
            MeshRenderer[] renderers = currentPreview.GetComponentsInChildren<MeshRenderer>();
            foreach (MeshRenderer rend in renderers)
            {
                if (rend != null)
                {
                    Material[] mats = new Material[rend.materials.Length];
                    for (int i = 0; i < mats.Length; i++)
                        mats[i] = previewMaterialInstance;
                    rend.materials = mats;
                }
            }
        }

        // Initialize at base position with minimal scale
        currentPreview.transform.position = crystalBasePosition;
        currentPreview.transform.rotation = crystalRotation;
        currentPreview.transform.localScale = new Vector3(0.01f, 0.01f, 0.01f);

        // Show and position line renderer
        if (showRaycastLine && raycastLineRenderer != null)
        {
            raycastLineRenderer.enabled = true;
            SetLineRendererColor(raycastLineRenderer, GameManager.Instance.placementColor);
        }
    }

    /// <summary>
    /// Calculate available radius at contact point by casting rays radially along the surface.
    /// Returns the maximum radius that fits without overlapping other geometry.
    /// </summary>
    float CalculateAvailableRadius(Vector3 contactPoint, Vector3 surfaceNormal)
    {
        // Create a coordinate system on the surface plane
        Vector3 tangent = Vector3.Cross(surfaceNormal, Vector3.up);
        if (tangent.sqrMagnitude < 0.001f)
        {
            // Surface is horizontal, use forward instead
            tangent = Vector3.Cross(surfaceNormal, Vector3.forward);
        }
        tangent.Normalize();
        Vector3 bitangent = Vector3.Cross(surfaceNormal, tangent).normalized;

        float minDistance = maxCrystalRadius;

        // Cast rays radially from contact point along the surface
        for (int i = 0; i < radiusProbeCount; i++)
        {
            float angle = (i / (float)radiusProbeCount) * 360f * Mathf.Deg2Rad;
            Vector3 probeDirection = tangent * Mathf.Cos(angle) + bitangent * Mathf.Sin(angle);

            // Offset slightly along normal to avoid self-intersection
            Vector3 probeOrigin = contactPoint + surfaceNormal * 0.01f;

            if (Physics.Raycast(probeOrigin, probeDirection, out RaycastHit hit, maxCrystalRadius, placementRaycastLayers))
            {
                minDistance = Mathf.Min(minDistance, hit.distance);
            }
        }

        // Clamp between min and max radius
        return Mathf.Clamp(minDistance, minCrystalRadius, maxCrystalRadius);
    }

    /// <summary>
    /// Calculate maximum growth length before hitting an obstacle.
    /// Uses the full initial distance as the raycast range.
    /// </summary>
    float CalculateMaxGrowthLength(Vector3 basePosition, Vector3 growthDirection)
    {
        float maxRayDistance = initialDistanceToPlayer;
        if (Physics.Raycast(basePosition + growthDirection * 0.01f, growthDirection, out RaycastHit hit, maxRayDistance, placementRaycastLayers))
        {
            return hit.distance;
        }
        return maxRayDistance;
    }

    void UpdateCrystalGrowth()
    {
        if (currentPreview == null) return;

        // If already stopped due to collision or proximity, don't update length
        if (!crystalCollisionStopped)
        {
            // Calculate distance from crystal's visual top to player
            Camera cam = GameManager.Instance.playerController.playerCamera;
            Vector3 playerPos = cam.transform.position;

            // The visual top of the crystal extends beyond currentCrystalLength due to scaling.
            // Visual half-height = scaleY = growthFraction * crystalMaxScale
            // Visual top = base + growthDirection * (scaleY * 2)
            float growthFrac = currentMaxCrystalLength > 0f
                ? Mathf.Clamp01(currentCrystalLength / currentMaxCrystalLength)
                : 0f;
            float visualHeight = growthFrac * crystalMaxScale * 2f;
            Vector3 visualTop = crystalBasePosition + crystalGrowthDirection * visualHeight;
            float tipToPlayerDistance = Vector3.Distance(visualTop, playerPos);

            // Stop if visual top is within stop distance of player
            if (tipToPlayerDistance <= crystalStopDistance)
            {
                crystalCollisionStopped = true;
            }
            else
            {
                // Proximity factor: 0 at stop distance, 1 at initial distance (far away)
                float proximityRange = initialDistanceToPlayer - crystalStopDistance;
                float proximity = proximityRange > 0f
                    ? Mathf.Clamp01((tipToPlayerDistance - crystalStopDistance) / proximityRange)
                    : 0f;

                // Evaluate growth curve based on proximity (slows as crystal approaches player)
                float speedMultiplier = crystalGrowthCurve.Evaluate(proximity);
                float growthDelta = crystalGrowthSpeed * speedMultiplier * Time.deltaTime;

                if (growthDelta > 0f)
                {
                    // Raycast from current growth front to check for collision
                    Vector3 growthFront = crystalBasePosition + crystalGrowthDirection * currentCrystalLength;
                    if (Physics.Raycast(growthFront, crystalGrowthDirection, out RaycastHit hit, growthDelta, placementRaycastLayers))
                    {
                        // Hit something - stop at the collision point
                        currentCrystalLength += hit.distance;
                        crystalCollisionStopped = true;
                    }
                    else
                    {
                        // No collision - grow by delta (capped by pre-calculated max)
                        currentCrystalLength = Mathf.Min(currentCrystalLength + growthDelta, currentMaxCrystalLength);

                        if (currentCrystalLength >= currentMaxCrystalLength)
                        {
                            crystalCollisionStopped = true;
                        }
                    }
                }
            }
        }

        // Calculate scale based on growth progress toward max
        // growthFraction: 0 at start, 1 at full length
        float growthFraction = currentMaxCrystalLength > 0f
            ? Mathf.Clamp01(currentCrystalLength / currentMaxCrystalLength)
            : 0f;

        // Length axis (Y) scales up to crystalMaxScale
        float scaleY = Mathf.Max(growthFraction * crystalMaxScale, 0.01f);

        // Width axes (X/Z) scale proportionally, floored at half the length scale (1:2 ratio min)
        float scaleXZ = Mathf.Max(growthFraction * crystalMaxScale, scaleY * 0.5f);

        currentPreview.transform.localScale = new Vector3(scaleXZ, scaleY, scaleXZ);

        // Position so that the base (bottom) of the crystal is pinned at crystalBasePosition.
        // The crystal's local Y-axis is aligned with crystalGrowthDirection via crystalRotation.
        // A Unity cylinder has height=2 units centered at pivot, so half-height in world = scaleY * 1.0.
        // For a generic prefab, scaleY * 1.0 is the half-extent in the Y direction.
        // Offset the center from the base by half the world-space height along growth direction.
        float halfWorldHeight = scaleY; // scaleY * 1.0 (unity cylinder half-height = 1 unit * scaleY)
        currentPreview.transform.position = crystalBasePosition + crystalGrowthDirection * halfWorldHeight;

        // Update line renderer (from backpack to crystal tip)
        if (showRaycastLine && raycastLineRenderer != null && playerBackpack != null)
        {
            Vector3 crystalTip = crystalBasePosition + crystalGrowthDirection * (halfWorldHeight * 2f);
            DrawParabola(playerBackpack.position, crystalTip, true);
        }
    }

    /// <summary>
    /// Returns true if the player's crosshair is currently over a surface
    /// that crystals can be placed on (layer-based check via placementRaycastLayers).
    /// Used by UIManager for key visibility.
    /// </summary>
    public bool IsLookingAtPlaceableSurface()
    {
        if (GameManager.Instance == null || GameManager.Instance.playerController == null)
            return false;

        Camera cam = GameManager.Instance.playerController.playerCamera;
        if (cam == null) return false;

        return Physics.Raycast(cam.transform.position, cam.transform.forward,
            maxRaycastDistance, placementRaycastLayers);
    }

    void SetPlayerMovementLocked(bool locked)
    {
        var pc = GameManager.Instance != null ? GameManager.Instance.playerController : null;
        if (pc == null) return;

        if (locked)
            pc.PausePlayer(PauseModes.FreezeInPlace);
        else
            pc.UnpausePlayer();
    }

    void CancelCrystalBuilding()
    {
        if (currentPreview != null)
        {
            Destroy(currentPreview);
            currentPreview = null;
        }
        if (raycastLineRenderer != null)
            raycastLineRenderer.enabled = false;

        isCrystalGrowing = false;
        isPlacementMode = false;

        SetPlayerMovementLocked(false);
    }

    void PlaceCrystal()
    {
        if (currentPreview == null || !lastRaycastHit)
        {
            if (currentPreview != null)
            {
                Destroy(currentPreview);
                currentPreview = null;
            }
            if (raycastLineRenderer != null)
                raycastLineRenderer.enabled = false;
            return;
        }

        // Check block limit
        if (placedBlocks.Count >= maxBlocks)
        {
            Debug.LogWarning("Maximum block limit reached!");
            Destroy(currentPreview);
            currentPreview = null;
            if (raycastLineRenderer != null)
                raycastLineRenderer.enabled = false;
            return;
        }

        // Create final crystal from prefab or fallback to primitive
        GameObject crystal;
        if (selectedCrystalPrefab != null)
        {
            crystal = Instantiate(selectedCrystalPrefab);
            crystal.name = "Crystal";
        }
        else
        {
            crystal = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            crystal.name = "Crystal";
        }

        crystal.transform.position = currentPreview.transform.position;
        crystal.transform.rotation = currentPreview.transform.rotation;
        crystal.transform.localScale = currentPreview.transform.localScale;

        // Enable colliders
        Collider[] colliders = crystal.GetComponentsInChildren<Collider>();
        foreach (Collider col in colliders)
        {
            if (col != null) col.enabled = true;
        }

        // Track the crystal
        placedBlocks.Add(crystal);
        fifoBlockList.Add(crystal);

        // Play placement animation (punch scale effect)
        Vector3 targetScale = crystal.transform.localScale;
        crystal.transform.DOPunchScale(targetScale * 0.1f, placementDuration, 1, 0.5f);

        // Play sound
        AudioEventDispatcher.PlaySound(SoundID.BlockPlace);

        // Clean up preview
        Destroy(currentPreview);
        currentPreview = null;

        // Hide line renderer
        if (raycastLineRenderer != null)
            raycastLineRenderer.enabled = false;
    }

    void DrawParabola(Vector3 start, Vector3 end, bool useHorizontalCurve = false)
    {
        if (raycastLineRenderer == null) return;

        // Calculate right direction from backpack (perpendicular to forward direction)
        Vector3 rightDirection = Vector3.zero;
        if (useHorizontalCurve && playerBackpack != null)
        {
            // Get camera forward direction to determine "right"
            Camera cam = GameManager.Instance?.playerController?.playerCamera;
            if (cam != null)
            {
                rightDirection = cam.transform.right;
            }
        }

        // Calculate parabola points
        for (int i = 0; i <= parabolaSegments; i++)
        {
            float t = i / (float)parabolaSegments;

            // Linear interpolation between start and end
            Vector3 linearPoint = Vector3.Lerp(start, end, t);

            // Add parabolic offset (peaks at the middle)
            // Using formula: curve * 4 * t * (1 - t) which creates a parabola peaking at t=0.5
            float parabolaOffset = 4f * t * (1f - t);

            if (useHorizontalCurve)
            {
                // Horizontal curvature - offset to the right
                linearPoint += rightDirection * (parabolaHorizontalCurve * parabolaOffset);
            }
            else
            {
                // Vertical curvature - offset upward (keep old parabolaHeight for backward compatibility)
                linearPoint.y += 2f * parabolaOffset; // Using 2f as default vertical height
            }

            raycastLineRenderer.SetPosition(i, linearPoint);
        }
    }

    public void HideRaycastLine()
    {
        if (raycastLineRenderer != null)
        {
            raycastLineRenderer.enabled = false;

            // Kill active tween if exists
            if (lineEndpointTween != null)
            {
                lineEndpointTween.Kill();
                lineEndpointTween = null;
            }
        }
    }

    public void SetLineColor(Color color)
    {
        SetLineRendererColor(raycastLineRenderer, color);
    }

    private void SetLineRendererColor(LineRenderer lineRenderer, Color color)
    {
        if (lineRenderer != null)
        {
            lineRenderer.startColor = color;
            lineRenderer.endColor = color;

            // Also update shader property for hologram material
            if (lineRenderer.material != null)
            {
                lineRenderer.material.SetColor("_Color", color);
            }
        }
    }

    public void UpdateLineToBlock(GameObject targetBlock, bool isDestroyMode = false)
    {
        if (raycastLineRenderer == null || targetBlock == null || playerBackpack == null)
            return;

        // Get color from GameManager based on mode
        Color lineColor = Color.cyan; // Fallback
        if (GameManager.Instance != null)
        {
            lineColor = isDestroyMode ? GameManager.Instance.destructionColor : GameManager.Instance.placementColor;
        }
        SetLineColor(lineColor);

        // Use backpack position as start
        Vector3 startPos = playerBackpack.position;

        // Calculate target end position at the top of the platform block
        Vector3 targetEndPos = targetBlock.transform.position;
        float halfBlockHeight = targetBlock.transform.localScale.y * 0.5f;
        targetEndPos.y += halfBlockHeight; // Move to top of block

        // Show the line
        raycastLineRenderer.enabled = true;

        // Kill existing tween if active
        if (lineEndpointTween != null)
        {
            lineEndpointTween.Kill();
        }

        // Lerp the endpoint to the new target position
        lineEndpointTween = DOTween.To(
            () => currentLineEndpoint,
            x =>
            {
                currentLineEndpoint = x;
                DrawParabola(startPos, currentLineEndpoint, false);
            },
            targetEndPos,
            lineTransitionDuration
        ).SetEase(Ease.OutCubic);
    }

    void CreatePersistentDestructionLine(GameObject targetBlock)
    {
        if (targetBlock == null || playerBackpack == null)
            return;

        // Don't create duplicate lines
        if (destructionLines.ContainsKey(targetBlock))
            return;

        // Create a new LineRenderer for this block
        GameObject lineObj;
        LineRenderer lineRenderer;

        if (raycastLinePrefab != null)
        {
            lineObj = Instantiate(raycastLinePrefab, transform);
            lineObj.name = $"DestructionLine_{targetBlock.name}";
            lineRenderer = lineObj.GetComponent<LineRenderer>();

            if (lineRenderer == null)
            {
                lineRenderer = lineObj.AddComponent<LineRenderer>();
            }
        }
        else
        {
            lineObj = new GameObject($"DestructionLine_{targetBlock.name}");
            lineObj.transform.SetParent(transform);
            lineRenderer = lineObj.AddComponent<LineRenderer>();

            // Configure LineRenderer
            lineRenderer.startWidth = raycastLineWidth;
            lineRenderer.endWidth = raycastLineWidth;
            lineRenderer.material = new Material(Shader.Find("Sprites/Default"));
        }

        // Set position count and color from GameManager
        lineRenderer.positionCount = parabolaSegments + 1;
        Color destroyColor = GameManager.Instance != null ? GameManager.Instance.destructionColor : Color.red;
        SetLineRendererColor(lineRenderer, destroyColor);

        // Calculate positions
        Vector3 startPos = playerBackpack.position;
        Vector3 endPos = targetBlock.transform.position;
        float halfBlockHeight = targetBlock.transform.localScale.y * 0.5f;
        endPos.y += halfBlockHeight;

        // Draw the line
        DrawParabolaForRenderer(lineRenderer, startPos, endPos, false);

        // Store in dictionary
        destructionLines[targetBlock] = lineRenderer;
    }

    void DrawParabolaForRenderer(LineRenderer renderer, Vector3 start, Vector3 end, bool useHorizontalCurve = false)
    {
        if (renderer == null) return;

        // Calculate right direction from backpack (perpendicular to forward direction)
        Vector3 rightDirection = Vector3.zero;
        if (useHorizontalCurve && playerBackpack != null)
        {
            Camera cam = GameManager.Instance?.playerController?.playerCamera;
            if (cam != null)
            {
                rightDirection = cam.transform.right;
            }
        }

        // Calculate parabola points
        for (int i = 0; i <= parabolaSegments; i++)
        {
            float t = i / (float)parabolaSegments;
            Vector3 linearPoint = Vector3.Lerp(start, end, t);
            float parabolaOffset = 4f * t * (1f - t);

            if (useHorizontalCurve)
            {
                linearPoint += rightDirection * (parabolaHorizontalCurve * parabolaOffset);
            }
            else
            {
                linearPoint.y += 2f * parabolaOffset;
            }

            renderer.SetPosition(i, linearPoint);
        }
    }

    public void UpdatePersistentDestructionLines()
    {
        // Only update if player backpack exists and there are lines to update
        if (playerBackpack == null || destructionLines.Count == 0)
            return;

        // Update all persistent destruction lines with current backpack position
        foreach (var kvp in destructionLines)
        {
            GameObject targetBlock = kvp.Key;
            LineRenderer lineRenderer = kvp.Value;

            if (targetBlock == null || lineRenderer == null)
                continue;

            // Recalculate positions from current backpack position
            Vector3 startPos = playerBackpack.position;
            Vector3 endPos = targetBlock.transform.position;
            float halfBlockHeight = targetBlock.transform.localScale.y * 0.5f;
            endPos.y += halfBlockHeight;

            // Redraw the line
            DrawParabolaForRenderer(lineRenderer, startPos, endPos, false);
        }
    }

    void RestoreBlockMaterial(GameObject block)
    {
        if (originalMaterials.ContainsKey(block))
        {
            Material[] savedMaterials = originalMaterials[block];

            // Restore materials to ALL renderers in children
            MeshRenderer[] renderers = block.GetComponentsInChildren<MeshRenderer>();
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

            originalMaterials.Remove(block);
        }
    }

    void PlaceBlock()
    {
        if (currentPreview == null || cubePrefab == null)
            return;

        // Don't place if raycast didn't hit anything
        if (!lastRaycastHit)
        {
            Debug.Log("Cannot place block: Raycast did not hit any surface");
            return;
        }

        // Check if we've reached the block limit
        if (CurrentBlockCount >= maxBlocks)
        {
            Debug.Log($"Cannot place block: Maximum block limit ({maxBlocks}) reached!");
            Destroy(currentPreview);
            currentPreview = null;
            HideRaycastLine();
            return;
        }

        // Get platform scale from BuildModeController
        float platformScale = 1.0f;
        if (GameManager.Instance != null && GameManager.Instance.buildModeController != null)
        {
            platformScale = GameManager.Instance.buildModeController.platformScale;
        }

        // Round position to (1.0 * platformScale) increments
        float gridIncrement = 1.0f * platformScale;
        Vector3 roundedPosition = new Vector3(
            Mathf.Round(previewPosition.x / gridIncrement) * gridIncrement,
            Mathf.Round(previewPosition.y / gridIncrement) * gridIncrement,
            Mathf.Round(previewPosition.z / gridIncrement) * gridIncrement
        );

        // If block would be below ground level, try placing one level higher
        if (GameManager.Instance != null && GameManager.Instance.worldGenerator != null)
        {
            float groundHeight = GameManager.Instance.worldGenerator.GetGroundHeight(roundedPosition.x, roundedPosition.z);
            if (roundedPosition.y < groundHeight)
            {
                // Try one grid level higher
                float adjustedY = roundedPosition.y + gridIncrement;
                Debug.Log($"BuildingSystem: Block Y={roundedPosition.y} below ground ({groundHeight}), trying Y={adjustedY}");

                if (adjustedY >= groundHeight)
                {
                    roundedPosition.y = adjustedY;
                }
                else
                {
                    // Still below ground, cannot place
                    Debug.Log($"BuildingSystem: Cannot place block - still below ground after adjustment");
                    Destroy(currentPreview);
                    currentPreview = null;
                    HideRaycastLine();
                    return;
                }
            }
        }

        // Keep scale at 1.0 (constant size)
        float roundedScale = 1.0f;

        GameObject newBlock = Instantiate(cubePrefab, roundedPosition, Quaternion.identity);

        newBlock.transform.localScale = Vector3.one * roundedScale * platformScale;

        Collider blockCollider = newBlock.GetComponentInChildren<Collider>();
        if (blockCollider != null)
        {
            blockCollider.enabled = true;
        }

        // Apply materials to ALL renderers in children (handles empty container with 2 children: top + bottom)
        MeshRenderer[] renderers = newBlock.GetComponentsInChildren<MeshRenderer>();
        MeshRenderer[] prefabRenderers = cubePrefab.GetComponentsInChildren<MeshRenderer>();

        if (renderers != null && prefabRenderers != null && renderers.Length == prefabRenderers.Length)
        {
            for (int i = 0; i < renderers.Length; i++)
            {
                if (renderers[i] != null && prefabRenderers[i] != null)
                {
                    renderers[i].materials = prefabRenderers[i].sharedMaterials;
                }
            }
        }

        placedBlocks.Add(newBlock);
        fifoBlockList.Add(newBlock); // Track FIFO order

        Vector3 targetScale = newBlock.transform.localScale;
        newBlock.transform.localScale = Vector3.zero;

        newBlock.transform.DOScale(targetScale, placementDuration)
            .SetEase(Ease.OutBack, 1.2f)
            .OnStart(() => AudioEventDispatcher.PlaySound(SoundID.BlockPlace));

        Destroy(currentPreview);
        currentPreview = null;

        // Hide raycast line
        if (raycastLineRenderer != null)
        {
            raycastLineRenderer.enabled = false;
        }

        // Register with adjacency grid
        if (GameManager.Instance != null && GameManager.Instance.adjacencyGrid != null)
        {
            GameManager.Instance.adjacencyGrid.RegisterBlock(newBlock, roundedPosition);
        }

        // DISABLED FOR CRYSTAL MODE - No scaffolding
        // ScaffoldingManager scaffoldingManager = newBlock.AddComponent<ScaffoldingManager>();
        // GameObject scaffoldPrefab = scaffoldingPrefab != null ? scaffoldingPrefab : cubePrefab;
        // scaffoldingManager.Initialize(newBlock, scaffoldPrefab, platformScale, 0f);

        // DISABLED FOR CRYSTAL MODE - No build mode after placement
        // if (GameManager.Instance != null && GameManager.Instance.buildModeController != null)
        // {
        //     GameManager.Instance.buildModeController.EnterBuildMode(newBlock);
        // }
    }

    void CreateDefaultHologramMaterial()
    {
        Shader hologramShader = Shader.Find("FX/Hologram");
        if (hologramShader != null)
        {
            GameManager.Instance.hologramMaterial = new Material(hologramShader);
            GameManager.Instance.hologramMaterial.SetFloat("_GlowIntensity", 0.3f);
            GameManager.Instance.hologramMaterial.SetFloat("_ScrollSpeedV", 0.5f);
            GameManager.Instance.hologramMaterial.SetFloat("_Scale", 5f);
            GameManager.Instance.hologramMaterial.SetTexture("_AlphaTexture", CreateStripedTexture());
        }
        else
        {
            GameManager.Instance.hologramMaterial = new Material(Shader.Find("Standard"));
            GameManager.Instance.hologramMaterial.color = new Color(0.5f, 0.5f, 1f, 0.5f);
            SetupTransparentMaterial(GameManager.Instance.hologramMaterial);
        }
    }

    void SetupTransparentMaterial(Material mat)
    {
        mat.SetFloat("_Mode", 3);
        mat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
        mat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
        mat.SetInt("_ZWrite", 0);
        mat.DisableKeyword("_ALPHATEST_ON");
        mat.EnableKeyword("_ALPHABLEND_ON");
        mat.DisableKeyword("_ALPHAPREMULTIPLY_ON");
        mat.renderQueue = 3000;
    }

    Texture2D CreateStripedTexture()
    {
        int width = 64;
        int height = 64;
        Texture2D texture = new Texture2D(width, height, TextureFormat.RGBA32, false);

        Color[] pixels = new Color[width * height];
        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                float alpha = (y % 8 < 4) ? 1f : 0.3f;
                pixels[y * width + x] = new Color(1, 1, 1, alpha);
            }
        }

        texture.SetPixels(pixels);
        texture.Apply();
        texture.filterMode = FilterMode.Point;
        texture.wrapMode = TextureWrapMode.Repeat;

        return texture;
    }

    public void RemoveBlock(GameObject block)
    {
        placedBlocks.Remove(block);
        fifoBlockList.Remove(block); // Remove from FIFO

        // Unregister from adjacency grid
        if (GameManager.Instance != null && GameManager.Instance.adjacencyGrid != null)
        {
            GameManager.Instance.adjacencyGrid.UnregisterBlock(block.transform.position);
        }

        // Destroy scaffolding
        ScaffoldingManager scaffolding = block.GetComponent<ScaffoldingManager>();
        if (scaffolding != null)
        {
            scaffolding.DestroyScaffolding();
        }

        if (originalMaterials.ContainsKey(block))
        {
            originalMaterials.Remove(block);
        }
    }

    public void AddBlock(GameObject block)
    {
        placedBlocks.Add(block);
        fifoBlockList.Add(block); // Track FIFO order

        // Register with adjacency grid
        if (GameManager.Instance != null && GameManager.Instance.adjacencyGrid != null)
        {
            Vector3 roundedPosition = new Vector3(
                Mathf.Round(block.transform.position.x),
                Mathf.Round(block.transform.position.y),
                Mathf.Round(block.transform.position.z)
            );
            GameManager.Instance.adjacencyGrid.RegisterBlock(block, roundedPosition);
        }
    }

    /// <summary>
    /// Returns the list of blocks in placement order (for weighted orbit center calculation)
    /// </summary>
    public List<GameObject> GetChainBlockOrder()
    {
        return chainBlockOrder;
    }

    /// <summary>
    /// Returns highlighted blocks for destruction mode weighted orbit
    /// </summary>
    public List<GameObject> GetHighlightedBlocks()
    {
        return highlightedBlocks;
    }

    #region Chain Line System

    /// <summary>
    /// Initializes the chain line system when entering build mode
    /// </summary>
    public void InitializeChainLine(GameObject firstBlock)
    {
        // Clear any existing chain
        ClearChainLines();

        if (firstBlock == null || playerBackpack == null)
            return;

        // Add the first block to the chain
        chainBlockOrder.Add(firstBlock);

        // Create the first segment from backpack to first block (animated)
        CreateAnimatedChainSegment(null, firstBlock);
    }

    /// <summary>
    /// Adds a new segment to the chain when a block is placed
    /// </summary>
    public void AddChainSegment(GameObject previousBlock, GameObject newBlock)
    {
        if (previousBlock == null || newBlock == null || playerBackpack == null)
            return;

        // Finalize the active animating segment (make it static)
        FinalizeActiveSegment();

        // Add new block to chain order
        chainBlockOrder.Add(newBlock);

        // Create new animating segment from previous block to new block
        CreateAnimatedChainSegment(previousBlock, newBlock);
    }

    /// <summary>
    /// Finalizes the currently animating segment (makes it static)
    /// </summary>
    private void FinalizeActiveSegment()
    {
        if (activeChainSegment != null)
        {
            // Kill the tween if still active
            if (activeSegmentTween != null)
            {
                activeSegmentTween.Kill();
                activeSegmentTween = null;
            }

            // Add to the static chain list
            chainLineRenderers.Add(activeChainSegment);
            activeChainSegment = null;
        }
    }

    /// <summary>
    /// Creates an animated chain segment between two points
    /// </summary>
    private void CreateAnimatedChainSegment(GameObject fromBlock, GameObject toBlock)
    {
        if (playerBackpack == null || toBlock == null)
            return;

        // Determine start position
        Vector3 startPos;
        if (fromBlock == null)
        {
            // First segment starts from backpack
            startPos = playerBackpack.position;
        }
        else
        {
            // Subsequent segments start from top of previous block
            startPos = fromBlock.transform.position;
            startPos.y += fromBlock.transform.localScale.y * 0.5f;
        }

        // Target end position (top of target block)
        Vector3 targetEndPos = toBlock.transform.position;
        targetEndPos.y += toBlock.transform.localScale.y * 0.5f;

        // Create new LineRenderer
        activeChainSegment = CreateChainLineRenderer($"ChainSegment_{chainBlockOrder.Count}");
        activeChainSegment.enabled = true;

        // Set color from GameManager
        Color lineColor = GameManager.Instance != null ? GameManager.Instance.placementColor : Color.cyan;
        SetLineRendererColor(activeChainSegment, lineColor);

        // Store the fixed start position for this segment
        activeSegmentStartPos = startPos;

        // Start endpoint at the start position (will animate to target)
        activeSegmentEndpoint = startPos;

        // Kill any existing tween
        if (activeSegmentTween != null)
        {
            activeSegmentTween.Kill();
        }

        // Animate the endpoint to the target
        activeSegmentTween = DOTween.To(
            () => activeSegmentEndpoint,
            x =>
            {
                activeSegmentEndpoint = x;
                DrawParabolaForRenderer(activeChainSegment, activeSegmentStartPos, activeSegmentEndpoint, false);
            },
            targetEndPos,
            lineTransitionDuration
        ).SetEase(Ease.OutCubic);
    }

    /// <summary>
    /// Creates a LineRenderer for a chain segment
    /// </summary>
    private LineRenderer CreateChainLineRenderer(string name)
    {
        GameObject lineObj;
        LineRenderer lineRenderer;

        if (raycastLinePrefab != null)
        {
            lineObj = Instantiate(raycastLinePrefab, transform);
            lineObj.name = name;
            lineRenderer = lineObj.GetComponent<LineRenderer>();

            if (lineRenderer == null)
            {
                lineRenderer = lineObj.AddComponent<LineRenderer>();
            }
        }
        else
        {
            lineObj = new GameObject(name);
            lineObj.transform.SetParent(transform);
            lineRenderer = lineObj.AddComponent<LineRenderer>();

            // Configure LineRenderer
            lineRenderer.startWidth = raycastLineWidth;
            lineRenderer.endWidth = raycastLineWidth;
            lineRenderer.material = new Material(Shader.Find("Sprites/Default"));
        }

        lineRenderer.positionCount = parabolaSegments + 1;
        return lineRenderer;
    }

    /// <summary>
    /// Updates all chain line segments (called every frame in build mode)
    /// </summary>
    public void UpdateChainLines()
    {
        if (playerBackpack == null || chainBlockOrder.Count == 0)
            return;

        // Update static segments - first segment starts from backpack
        Vector3 currentStart = playerBackpack.position;

        for (int i = 0; i < chainLineRenderers.Count && i < chainBlockOrder.Count; i++)
        {
            LineRenderer lr = chainLineRenderers[i];
            GameObject block = chainBlockOrder[i];

            if (lr == null || block == null)
                continue;

            // Determine end position (top of block)
            Vector3 endPos = block.transform.position;
            endPos.y += block.transform.localScale.y * 0.5f;

            // Redraw the segment
            DrawParabolaForRenderer(lr, currentStart, endPos, false);

            // Next segment starts from this block's top
            currentStart = endPos;
        }

        // Update the active segment's start position if it exists
        // (the animation handles the endpoint, but start may need updating if previous block moved)
        if (activeChainSegment != null && chainBlockOrder.Count > 0)
        {
            // For the active segment, recalculate start position
            if (chainBlockOrder.Count == 1)
            {
                // First segment starts from backpack
                activeSegmentStartPos = playerBackpack.position;
            }
            else if (chainBlockOrder.Count > chainLineRenderers.Count)
            {
                // Active segment starts from the last finalized block
                GameObject prevBlock = chainBlockOrder[chainBlockOrder.Count - 2];
                if (prevBlock != null)
                {
                    activeSegmentStartPos = prevBlock.transform.position;
                    activeSegmentStartPos.y += prevBlock.transform.localScale.y * 0.5f;
                }
            }

            // Redraw the active segment with updated start
            DrawParabolaForRenderer(activeChainSegment, activeSegmentStartPos, activeSegmentEndpoint, false);
        }
    }

    /// <summary>
    /// Clears all chain line renderers
    /// </summary>
    public void ClearChainLines()
    {
        // Kill active tween
        if (activeSegmentTween != null)
        {
            activeSegmentTween.Kill();
            activeSegmentTween = null;
        }

        // Destroy active segment
        if (activeChainSegment != null)
        {
            Destroy(activeChainSegment.gameObject);
            activeChainSegment = null;
        }

        // Destroy all static chain segments
        foreach (LineRenderer lr in chainLineRenderers)
        {
            if (lr != null)
            {
                Destroy(lr.gameObject);
            }
        }
        chainLineRenderers.Clear();

        // Clear block order
        chainBlockOrder.Clear();
    }

    #endregion

    void OnDestroy()
    {
        // Stop any running coroutines
        if (highlightCoroutine != null)
        {
            StopCoroutine(highlightCoroutine);
        }

        // Clean up material instances
        if (previewMaterialInstance != null)
            Destroy(previewMaterialInstance);

        if (destructionMaterialInstance != null)
            Destroy(destructionMaterialInstance);

        // Clean up LineRenderer
        if (raycastLineRenderer != null)
            Destroy(raycastLineRenderer.gameObject);

        // Clean up chain lines
        ClearChainLines();
    }
}