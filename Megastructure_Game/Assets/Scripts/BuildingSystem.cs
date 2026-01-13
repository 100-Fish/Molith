using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using DG.Tweening;

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

            // Set initial color from GameManager
            Color initialColor = GameManager.Instance != null ? GameManager.Instance.placementColor : Color.cyan;
            raycastLineRenderer.startColor = initialColor;
            raycastLineRenderer.endColor = initialColor;
        }

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
        }

        if (isDestructionMode)
            return;

        if (Input.GetKeyDown(placeKey))
        {
            isPlacementMode = true;
            CreatePreviewBlock();
        }

        if (Input.GetKey(placeKey) && isPlacementMode)
        {
            UpdatePreviewPosition();
        }

        if (Input.GetKeyUp(placeKey) && isPlacementMode)
        {
            PlaceBlock();
            isPlacementMode = false;
        }
    }

    void HandleDestructionInput()
    {
        // Don't allow destruction if in build mode or placement mode
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
        }

        if (isPlacementMode)
            return;

        // Key pressed - start highlighting
        if (Input.GetKeyDown(destroyKey))
        {
            if (fifoBlockList.Count == 0)
                return; // No blocks to destroy

            isDestructionMode = true;
            currentHighlightDelay = initialHighlightDelay;
            highlightedBlocks.Clear();

            // Start highlighting coroutine
            if (highlightCoroutine != null)
                StopCoroutine(highlightCoroutine);

            highlightCoroutine = StartCoroutine(HighlightBlocksSequentially());
        }

        // Key released - trigger destruction
        if (Input.GetKeyUp(destroyKey) && isDestructionMode)
        {
            isDestructionMode = false;

            // Stop highlighting
            if (highlightCoroutine != null)
            {
                StopCoroutine(highlightCoroutine);
                highlightCoroutine = null;
            }

            // Hide the line when releasing destroy key
            HideRaycastLine();

            // If no blocks highlighted (quick release), do nothing
            if (highlightedBlocks.Count == 0)
                return;

            // Trigger destruction sequence
            StartCoroutine(DestroyBlocksSequentially());
        }
    }

    IEnumerator HighlightBlocksSequentially()
    {
        // Wait initial delay before first block
        yield return new WaitForSeconds(currentHighlightDelay);

        int blockIndex = 0;

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

    void CancelDestructionMode()
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

        // Hide the line when exiting destruction mode
        HideRaycastLine();
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
        if (raycastLineRenderer != null)
        {
            raycastLineRenderer.startColor = color;
            raycastLineRenderer.endColor = color;
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
        lineRenderer.startColor = destroyColor;
        lineRenderer.endColor = destroyColor;

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

    void UpdatePersistentDestructionLines()
    {
        // Only update if in destruction mode and player backpack exists
        if (!isDestructionMode || playerBackpack == null || destructionLines.Count == 0)
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

        // Generate scaffolding for this platform BEFORE entering build mode
        // This ensures scaffolding is ready when hologram materials are applied
        ScaffoldingManager scaffoldingManager = newBlock.AddComponent<ScaffoldingManager>();
        GameObject scaffoldPrefab = scaffoldingPrefab != null ? scaffoldingPrefab : cubePrefab;
        scaffoldingManager.Initialize(newBlock, scaffoldPrefab, platformScale, 0f);

        // Trigger build mode after placing first block (and after scaffolding is created)
        if (GameManager.Instance != null && GameManager.Instance.buildModeController != null)
        {
            GameManager.Instance.buildModeController.EnterBuildMode(newBlock);
        }
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
    }
}