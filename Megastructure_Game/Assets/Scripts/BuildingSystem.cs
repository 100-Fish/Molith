using System.Collections.Generic;
using UnityEngine;
using SUPERCharacter;
using DG.Tweening;

public class BuildingSystem : MonoBehaviour
{
    [Header("References")]
    [Tooltip("Reference to the SUPERCharacterAIO script on the player")]
    public SUPERCharacterAIO playerController;

    [Tooltip("The block prefab to spawn (must have a Collider and MeshRenderer)")]
    public GameObject blockPrefab;

    [Tooltip("Material to use for previews (should use FX/Hologram shader)")]
    public Material hologramMaterial;

    [Tooltip("Color for placement preview")]
    public Color placementColor = new Color(0, 1, 1, 0.5f); // Cyan

    [Tooltip("Color for destruction preview")]
    public Color destructionColor = new Color(1, 0, 0, 0.5f); // Red

    [Header("Placement Settings")]
    [Tooltip("Distance from camera to place blocks")]
    public float placementDistance = 5f;

    [Tooltip("Key to hold for placing blocks")]
    public KeyCode placeKey = KeyCode.E;

    [Header("Destruction Settings")]
    [Tooltip("Key to hold for destroying blocks")]
    public KeyCode destroyKey = KeyCode.Q;

    [Tooltip("Radius of the destruction sphere")]
    public float destructionRadius = 2f;

    [Header("Block Scaling Settings")]
    [Tooltip("Minimum scale multiplier for blocks")]
    public float minScale = 0.5f;

    [Tooltip("Maximum scale multiplier for blocks")]
    public float maxScale = 2f;

    [Tooltip("Speed of scale oscillation")]
    public float scaleOscillationSpeed = 2f;

    [Header("Block Merging Settings")]
    [Tooltip("Radius to check for nearby blocks to merge")]
    public float mergeRadius = 1.5f;

    [Tooltip("Enable block merging when placing")]
    public bool enableMerging = true;

    [Tooltip("Vertex merge tolerance (lower = more detail, higher = smoother)")]
    [Range(0.001f, 0.5f)]
    public float meshSimplificationTolerance = 0.05f;

    [Tooltip("Enable mesh smoothing after merge")]
    public bool smoothMergedMesh = true;

    [Tooltip("Number of smoothing iterations")]
    [Range(1, 5)]
    public int smoothingIterations = 2;

    [Header("Animation Settings")]
    [Tooltip("Duration of preview spawn animation")]
    public float previewSpawnDuration = 0.2f;

    [Tooltip("Duration of block placement animation")]
    public float placementDuration = 0.15f;

    [Tooltip("Duration of block destruction animation")]
    public float destructionDuration = 0.2f;

    // Private variables
    private GameObject currentPreview;
    private GameObject destructionSpherePreview;
    private bool isPlacementMode = false;
    private bool isDestructionMode = false;
    private Vector3 previewPosition;
    private readonly HashSet<GameObject> placedBlocks = new();
    private Material previewMaterialInstance;
    private Material destructionMaterialInstance;

    // Block scaling
    private float currentScaleMultiplier = 1f;
    private float scaleTime = 0f;

    // Destruction preview tracking
    private readonly Dictionary<GameObject, Material[]> originalMaterials = new();
    private readonly HashSet<GameObject> blocksInDestructionRadius = new();

    void Start()
    {
        // Validate references
        if (playerController == null)
        {
            Debug.LogError("BuildingSystem: PlayerController reference is missing! Please assign it in the inspector.");
        }

        if (blockPrefab == null)
        {
            Debug.LogError("BuildingSystem: BlockPrefab reference is missing! Please assign it in the inspector.");
        }

        // Create material instances
        if (hologramMaterial == null)
        {
            Debug.LogWarning("BuildingSystem: HologramMaterial is missing. Creating a default hologram material.");
            CreateDefaultHologramMaterial();
        }

        // Create instances for placement and destruction with different colors
        previewMaterialInstance = new Material(hologramMaterial);
        previewMaterialInstance.SetColor("_Color", placementColor);

        destructionMaterialInstance = new Material(hologramMaterial);
        destructionMaterialInstance.SetColor("_Color", destructionColor);
    }

    void Update()
    {
        if (playerController == null || playerController.playerCamera == null)
            return;

        HandlePlacementInput();
        HandleDestructionInput();
    }

    void HandlePlacementInput()
    {
        // Can't use placement if destruction is active
        if (isDestructionMode)
            return;

        // Check if placement key is being held
        if (Input.GetKeyDown(placeKey))
        {
            isPlacementMode = true;
            scaleTime = 0f;
            CreatePreviewBlock();
        }

        if (Input.GetKey(placeKey) && isPlacementMode)
        {
            // Update scale oscillation
            scaleTime += Time.deltaTime * scaleOscillationSpeed;
            currentScaleMultiplier = Mathf.Lerp(minScale, maxScale, (Mathf.Sin(scaleTime) + 1f) / 2f);

            UpdatePreviewPosition();
            UpdatePreviewScale();
        }

        if (Input.GetKeyUp(placeKey) && isPlacementMode)
        {
            PlaceBlock();
            isPlacementMode = false;
        }
    }

    void HandleDestructionInput()
    {
        // Can't use destruction if placement is active
        if (isPlacementMode)
            return;

        // Check if destruction key is being held
        if (Input.GetKeyDown(destroyKey))
        {
            isDestructionMode = true;
            CreateDestructionSpherePreview();
        }

        if (Input.GetKey(destroyKey) && isDestructionMode)
        {
            UpdatePreviewPosition();
            UpdateDestructionPreview();
        }

        if (Input.GetKeyUp(destroyKey) && isDestructionMode)
        {
            DestroyBlocksInRadius();
            ResetBlockMaterials();
            isDestructionMode = false;
        }
    }

    void CreatePreviewBlock()
    {
        if (blockPrefab == null)
            return;

        // Instantiate preview
        currentPreview = Instantiate(blockPrefab);

        // Disable collider on preview
        Collider previewCollider = currentPreview.GetComponent<Collider>();
        if (previewCollider != null)
        {
            previewCollider.enabled = false;
        }

        // Apply preview material
        MeshRenderer renderer = currentPreview.GetComponent<MeshRenderer>();
        if (renderer != null && previewMaterialInstance != null)
        {
            // Create array of preview materials
            Material[] previewMaterials = new Material[renderer.materials.Length];
            for (int i = 0; i < previewMaterials.Length; i++)
            {
                previewMaterials[i] = previewMaterialInstance;
            }
            renderer.materials = previewMaterials;
        }

        // Start at scale 1 (no initial animation to avoid conflict with oscillation)
        currentPreview.transform.localScale = Vector3.one;
        currentScaleMultiplier = 1f;

        UpdatePreviewPosition();
    }

    void CreateDestructionSpherePreview()
    {
        // Create a sphere for destruction preview
        destructionSpherePreview = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        destructionSpherePreview.transform.localScale = Vector3.one * destructionRadius * 2f;

        // Remove collider from preview
        Collider sphereCollider = destructionSpherePreview.GetComponent<Collider>();
        if (sphereCollider != null)
        {
            Destroy(sphereCollider);
        }

        // Apply destruction preview material
        MeshRenderer renderer = destructionSpherePreview.GetComponent<MeshRenderer>();
        if (renderer != null && destructionMaterialInstance != null)
        {
            renderer.material = destructionMaterialInstance;
        }

        // Animate sphere appearance with DOTween
        destructionSpherePreview.transform.localScale = Vector3.zero;
        destructionSpherePreview.transform.DOScale(Vector3.one * destructionRadius * 2f, previewSpawnDuration).SetEase(Ease.OutBack);

        UpdatePreviewPosition();
    }

    void UpdatePreviewPosition()
    {
        // Calculate position based on camera forward direction
        Camera cam = playerController.playerCamera;
        previewPosition = cam.transform.position + cam.transform.forward * placementDistance;

        if (isPlacementMode && currentPreview != null)
        {
            currentPreview.transform.position = previewPosition;
        }

        if (isDestructionMode && destructionSpherePreview != null)
        {
            destructionSpherePreview.transform.position = previewPosition;
        }
    }

    void UpdatePreviewScale()
    {
        if (currentPreview != null)
        {
            currentPreview.transform.localScale = Vector3.one * currentScaleMultiplier;
        }
    }

    void UpdateDestructionPreview()
    {
        // Check all placed blocks to see if they're in the destruction radius
        HashSet<GameObject> currentBlocksInRadius = new HashSet<GameObject>();

        foreach (GameObject block in placedBlocks)
        {
            if (block == null) continue;

            float distance = Vector3.Distance(block.transform.position, previewPosition);
            if (distance <= destructionRadius)
            {
                currentBlocksInRadius.Add(block);

                // If this block wasn't in radius before, save its material and apply destruction material
                if (!blocksInDestructionRadius.Contains(block))
                {
                    MeshRenderer renderer = block.GetComponent<MeshRenderer>();
                    if (renderer != null)
                    {
                        // Save original materials
                        if (!originalMaterials.ContainsKey(block))
                        {
                            originalMaterials[block] = renderer.materials;
                        }

                        // Apply destruction preview material
                        Material[] destructionMaterials = new Material[renderer.materials.Length];
                        for (int i = 0; i < destructionMaterials.Length; i++)
                        {
                            destructionMaterials[i] = destructionMaterialInstance;
                        }
                        renderer.materials = destructionMaterials;
                    }
                }
            }
        }

        // Reset materials for blocks that left the radius
        foreach (GameObject block in blocksInDestructionRadius)
        {
            if (block != null && !currentBlocksInRadius.Contains(block))
            {
                RestoreBlockMaterial(block);
            }
        }

        blocksInDestructionRadius.Clear();
        foreach (GameObject block in currentBlocksInRadius)
        {
            blocksInDestructionRadius.Add(block);
        }
    }

    void RestoreBlockMaterial(GameObject block)
    {
        if (originalMaterials.ContainsKey(block))
        {
            MeshRenderer renderer = block.GetComponent<MeshRenderer>();
            if (renderer != null)
            {
                renderer.materials = originalMaterials[block];
            }
            originalMaterials.Remove(block);
        }
    }

    void ResetBlockMaterials()
    {
        // Restore all blocks that had their materials changed
        foreach (GameObject block in blocksInDestructionRadius)
        {
            if (block != null)
            {
                RestoreBlockMaterial(block);
            }
        }
        blocksInDestructionRadius.Clear();
    }

    void PlaceBlock()
    {
        if (currentPreview == null || blockPrefab == null)
            return;

        // Create the actual block at the preview position
        GameObject newBlock = Instantiate(blockPrefab, previewPosition, Quaternion.identity);

        // Apply the current scale
        newBlock.transform.localScale = Vector3.one * currentScaleMultiplier;

        // Ensure collider is enabled
        Collider blockCollider = newBlock.GetComponent<Collider>();
        if (blockCollider != null)
        {
            blockCollider.enabled = true;
        }

        // Ensure the block has its original material (from prefab)
        MeshRenderer renderer = newBlock.GetComponent<MeshRenderer>();
        if (renderer != null)
        {
            // Get the original materials from the prefab
            MeshRenderer prefabRenderer = blockPrefab.GetComponent<MeshRenderer>();
            if (prefabRenderer != null)
            {
                renderer.materials = prefabRenderer.sharedMaterials;
            }
        }

        // Add BlockMerger component if merging is enabled
        if (enableMerging)
        {
            BlockMerger merger = newBlock.AddComponent<BlockMerger>();
            merger.mergeRadius = mergeRadius;
            merger.buildingSystem = this;
            merger.simplificationTolerance = meshSimplificationTolerance;
            merger.enableSmoothing = smoothMergedMesh;
            merger.smoothingIterations = smoothingIterations;
        }

        // Animate block placement with DOTween - subtle pop (starting from current scale)
        Vector3 targetScale = newBlock.transform.localScale;
        newBlock.transform.localScale = Vector3.zero;
        newBlock.transform.DOScale(targetScale, placementDuration).SetEase(Ease.OutBack, 1.2f);

        // Track this block as player-placed
        placedBlocks.Add(newBlock);

        // Check for merging after placement
        if (enableMerging)
        {
            CheckAndMergeBlocks(newBlock);
        }

        // Destroy the preview
        Destroy(currentPreview);
        currentPreview = null;
    }

    void CheckAndMergeBlocks(GameObject newBlock)
    {
        BlockMerger newMerger = newBlock.GetComponent<BlockMerger>();
        if (newMerger != null)
        {
            newMerger.CheckForMerge(placedBlocks);
        }
    }

    void DestroyBlocksInRadius()
    {
        List<GameObject> blocksToDestroy = new List<GameObject>();

        // Find all player-placed blocks within the destruction radius
        // Use a copy of the set to avoid modification during iteration
        List<GameObject> blocksList = new List<GameObject>(placedBlocks);

        foreach (GameObject block in blocksList)
        {
            if (block == null) continue;

            // Check distance from block's bounds center for better accuracy
            Renderer blockRenderer = block.GetComponent<Renderer>();
            Vector3 checkPosition = blockRenderer != null ? blockRenderer.bounds.center : block.transform.position;

            float distance = Vector3.Distance(checkPosition, previewPosition);

            if (distance <= destructionRadius)
            {
                blocksToDestroy.Add(block);
            }
        }

        // Destroy the blocks with animation
        foreach (GameObject block in blocksToDestroy)
        {
            // Remove from tracking immediately
            placedBlocks.Remove(block);

            // Clean up material tracking
            if (originalMaterials.ContainsKey(block))
            {
                originalMaterials.Remove(block);
            }
            blocksInDestructionRadius.Remove(block);

            // Capture the block reference for the callback
            GameObject blockToDestroy = block;

            // Kill any existing tweens on this object
            block.transform.DOKill();

            // Animate destruction - subtle shrink
            block.transform.DOScale(Vector3.zero, destructionDuration).SetEase(Ease.InBack).OnComplete(() =>
            {
                if (blockToDestroy != null)
                    Destroy(blockToDestroy);
            });
        }

        // Always destroy the destruction sphere preview
        if (destructionSpherePreview != null)
        {
            GameObject sphereToDestroy = destructionSpherePreview;

            destructionSpherePreview.transform.DOScale(Vector3.zero, destructionDuration * 0.5f).SetEase(Ease.InBack).OnComplete(() =>
            {
                if (sphereToDestroy != null)
                    Destroy(sphereToDestroy);
            });

            // Clear the reference immediately so we don't try to update its position
            destructionSpherePreview = null;
        }
    }

    void CreateDefaultHologramMaterial()
    {
        Shader hologramShader = Shader.Find("FX/Hologram");
        if (hologramShader != null)
        {
            hologramMaterial = new Material(hologramShader);
            hologramMaterial.SetFloat("_GlowIntensity", 0.3f);
            hologramMaterial.SetFloat("_ScrollSpeedV", 0.5f);
            hologramMaterial.SetFloat("_Scale", 5f);
            hologramMaterial.SetTexture("_AlphaTexture", CreateStripedTexture());
        }
        else
        {
            // Fallback to standard transparent material
            hologramMaterial = new Material(Shader.Find("Standard"));
            hologramMaterial.color = new Color(0.5f, 0.5f, 1f, 0.5f);
            SetupTransparentMaterial(hologramMaterial);
        }
    }

    void SetupTransparentMaterial(Material mat)
    {
        // Set rendering mode to transparent
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
                // Create horizontal stripes
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

        // Also remove from original materials tracking if present
        if (originalMaterials.ContainsKey(block))
        {
            originalMaterials.Remove(block);
        }

        blocksInDestructionRadius.Remove(block);
    }

    public void AddBlock(GameObject block)
    {
        placedBlocks.Add(block);
    }

    void OnDestroy()
    {
        // Clean up material instances
        if (previewMaterialInstance != null)
            Destroy(previewMaterialInstance);

        if (destructionMaterialInstance != null)
            Destroy(destructionMaterialInstance);
    }
}
