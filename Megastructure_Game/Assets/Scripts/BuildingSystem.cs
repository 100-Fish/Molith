using System;
using System.Collections.Generic;
using UnityEngine;
using DG.Tweening;

[Serializable]
public class BlockData
{
    public Vector3 position;
    public float scale;

    public BlockData(Vector3 pos, float scl)
    {
        position = pos;
        scale = scl;
    }

    public override string ToString()
    {
        return $"cube,{position.x:F1},{position.y:F1},{position.z:F1},{scale:F1} EOL";
    }
}

public class BuildingSystem : MonoBehaviour
{
    [Header("References")]
    [Tooltip("The cube prefab to place")]
    public GameObject cubePrefab;

    [Tooltip("Material to use for previews (should use FX/Hologram shader)")]
    public Material hologramMaterial;

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
    private GameObject destructionSpherePreview;
    private bool isPlacementMode = false;
    private bool isDestructionMode = false;
    private Vector3 previewPosition;
    private readonly HashSet<GameObject> placedBlocks = new();
    private Material previewMaterialInstance;
    private Material destructionMaterialInstance;

    private float currentScaleMultiplier = 1f;
    private float scaleTime = 0f;

    private readonly Dictionary<GameObject, Material[]> originalMaterials = new();
    private readonly HashSet<GameObject> blocksInDestructionRadius = new();

    private readonly List<BlockData> blockHistory = new();
    private readonly Dictionary<GameObject, BlockData> blockToData = new();

    public int CurrentBlockCount => placedBlocks.Count;
    public int RemainingBlocks => maxBlocks - CurrentBlockCount;
    public List<BlockData> BlockHistory => new List<BlockData>(blockHistory);

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

        if (hologramMaterial == null)
        {
            Debug.LogWarning("BuildingSystem: HologramMaterial is missing. Creating a default hologram material.");
            CreateDefaultHologramMaterial();
        }

        previewMaterialInstance = new Material(hologramMaterial);
        previewMaterialInstance.SetColor("_Color", GameManager.Instance.placementColor);

        destructionMaterialInstance = new Material(hologramMaterial);
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
        // Don't allow placement if in build mode
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
            scaleTime = 0f;
            CreatePreviewBlock();
        }

        if (Input.GetKey(placeKey) && isPlacementMode)
        {
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
        if (isPlacementMode)
            return;

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
        if (cubePrefab == null)
            return;

        currentPreview = Instantiate(cubePrefab);

        Collider previewCollider = currentPreview.GetComponent<Collider>();
        if (previewCollider != null)
        {
            previewCollider.enabled = false;
        }

        MeshRenderer renderer = currentPreview.GetComponent<MeshRenderer>();
        if (renderer != null && previewMaterialInstance != null)
        {
            Material[] previewMaterials = new Material[renderer.materials.Length];
            for (int i = 0; i < previewMaterials.Length; i++)
            {
                previewMaterials[i] = previewMaterialInstance;
            }
            renderer.materials = previewMaterials;
        }

        currentPreview.transform.localScale = Vector3.one;
        currentScaleMultiplier = 1f;

        UpdatePreviewPosition();
    }

    void CreateDestructionSpherePreview()
    {
        destructionSpherePreview = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        destructionSpherePreview.transform.localScale = Vector3.one * destructionRadius * 2f;

        Collider sphereCollider = destructionSpherePreview.GetComponent<Collider>();
        if (sphereCollider != null)
        {
            Destroy(sphereCollider);
        }

        MeshRenderer renderer = destructionSpherePreview.GetComponent<MeshRenderer>();
        if (renderer != null && destructionMaterialInstance != null)
        {
            renderer.material = destructionMaterialInstance;
        }

        destructionSpherePreview.transform.localScale = Vector3.zero;
        destructionSpherePreview.transform.DOScale(Vector3.one * destructionRadius * 2f, previewSpawnDuration).SetEase(Ease.OutBack);

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
        HashSet<GameObject> currentBlocksInRadius = new HashSet<GameObject>();

        foreach (GameObject block in placedBlocks)
        {
            if (block == null) continue;

            float distance = Vector3.Distance(block.transform.position, previewPosition);
            if (distance <= destructionRadius)
            {
                currentBlocksInRadius.Add(block);

                if (!blocksInDestructionRadius.Contains(block))
                {
                    MeshRenderer renderer = block.GetComponent<MeshRenderer>();
                    if (renderer != null)
                    {
                        if (!originalMaterials.ContainsKey(block))
                        {
                            originalMaterials[block] = renderer.materials;
                        }

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

        // Round position to nearest 0.5
        Vector3 roundedPosition = new Vector3(
            Mathf.Round(previewPosition.x * 2f) / 2f,
            Mathf.Round(previewPosition.y * 2f) / 2f,
            Mathf.Round(previewPosition.z * 2f) / 2f
        );

        // Round scale to nearest 0.1
        float roundedScale = Mathf.Round(currentScaleMultiplier * 10f) / 10f;

        GameObject newBlock = Instantiate(cubePrefab, roundedPosition, Quaternion.identity);

        newBlock.transform.localScale = Vector3.one * roundedScale;

        Collider blockCollider = newBlock.GetComponent<Collider>();
        if (blockCollider != null)
        {
            blockCollider.enabled = true;
        }

        MeshRenderer renderer = newBlock.GetComponent<MeshRenderer>();
        if (renderer != null)
        {
            MeshRenderer prefabRenderer = cubePrefab.GetComponent<MeshRenderer>();
            if (prefabRenderer != null)
            {
                renderer.materials = prefabRenderer.sharedMaterials;
            }
        }

        placedBlocks.Add(newBlock);

        // Create and track block data
        BlockData blockData = new BlockData(roundedPosition, roundedScale);
        blockHistory.Add(blockData);
        blockToData[newBlock] = blockData;

        Vector3 targetScale = newBlock.transform.localScale;
        newBlock.transform.localScale = Vector3.zero;

        newBlock.transform.DOScale(targetScale, placementDuration).SetEase(Ease.OutBack, 1.2f);

        Destroy(currentPreview);
        currentPreview = null;

        // Register with adjacency grid
        if (GameManager.Instance != null && GameManager.Instance.adjacencyGrid != null)
        {
            GameManager.Instance.adjacencyGrid.RegisterBlock(newBlock, roundedPosition);
        }

        // Trigger build mode after placing first block
        if (GameManager.Instance != null && GameManager.Instance.buildModeController != null)
        {
            GameManager.Instance.buildModeController.EnterBuildMode(newBlock);
        }
    }

    void DestroyBlocksInRadius()
    {
        HashSet<GameObject> blocksToDestroy = new HashSet<GameObject>();

        // Use OverlapSphere to find all colliders in the destruction radius
        Collider[] hitColliders = Physics.OverlapSphere(previewPosition, destructionRadius);

        foreach (Collider col in hitColliders)
        {
            GameObject block = col.gameObject;

            // Only destroy blocks that we've placed
            if (placedBlocks.Contains(block))
            {
                blocksToDestroy.Add(block);
            }
        }

        foreach (GameObject block in blocksToDestroy)
        {
            placedBlocks.Remove(block);

            // Remove from block history
            if (blockToData.ContainsKey(block))
            {
                BlockData data = blockToData[block];
                blockHistory.Remove(data);
                blockToData.Remove(block);
            }

            if (originalMaterials.ContainsKey(block))
            {
                originalMaterials.Remove(block);
            }
            blocksInDestructionRadius.Remove(block);

            GameObject blockToDestroy = block;

            block.transform.DOKill();

            block.transform.DOScale(Vector3.zero, destructionDuration).SetEase(Ease.InBack).OnComplete(() =>
            {
                if (blockToDestroy != null)
                    Destroy(blockToDestroy);
            });
        }

        if (destructionSpherePreview != null)
        {
            GameObject sphereToDestroy = destructionSpherePreview;

            destructionSpherePreview.transform.DOScale(Vector3.zero, destructionDuration * 0.5f).SetEase(Ease.InBack).OnComplete(() =>
            {
                if (sphereToDestroy != null)
                    Destroy(sphereToDestroy);
            });

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
            hologramMaterial = new Material(Shader.Find("Standard"));
            hologramMaterial.color = new Color(0.5f, 0.5f, 1f, 0.5f);
            SetupTransparentMaterial(hologramMaterial);
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

        // Remove from block history
        if (blockToData.ContainsKey(block))
        {
            BlockData data = blockToData[block];
            blockHistory.Remove(data);
            blockToData.Remove(block);
        }

        if (originalMaterials.ContainsKey(block))
        {
            originalMaterials.Remove(block);
        }

        blocksInDestructionRadius.Remove(block);
    }

    public void AddBlock(GameObject block)
    {
        placedBlocks.Add(block);

        // Add block data for merged blocks
        Vector3 roundedPosition = new Vector3(
            Mathf.Round(block.transform.position.x * 2f) / 2f,
            Mathf.Round(block.transform.position.y * 2f) / 2f,
            Mathf.Round(block.transform.position.z * 2f) / 2f
        );
        float roundedScale = Mathf.Round(block.transform.localScale.x * 10f) / 10f;

        // For merged blocks, track position and scale
        BlockData blockData = new BlockData(roundedPosition, roundedScale);
        blockHistory.Add(blockData);
        blockToData[block] = blockData;
    }

    void OnDestroy()
    {
        if (previewMaterialInstance != null)
            Destroy(previewMaterialInstance);

        if (destructionMaterialInstance != null)
            Destroy(destructionMaterialInstance);
    }
}