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
    private readonly HashSet<GameObject> placedBlocks = new();
    private Material previewMaterialInstance;
    private Material destructionMaterialInstance;

    private readonly Dictionary<GameObject, Material[]> originalMaterials = new();

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
                        Destroy(blockToDestroy);
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
        previewPosition = cam.transform.position + cam.transform.forward * placementDistance;

        if (isPlacementMode && currentPreview != null)
        {
            currentPreview.transform.position = previewPosition;
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
    }
}