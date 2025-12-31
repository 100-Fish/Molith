using System.Collections.Generic;
using UnityEngine;
using DG.Tweening;

public class BuildingSystem : MonoBehaviour
{
    [Header("References")]
    [Tooltip("The block prefab to spawn (must have a Collider and MeshRenderer)")]
    public GameObject blockPrefab;

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

    void Start()
    {
        if (GameManager.Instance == null)
        {
            Debug.LogError("BuildingSystem: GameManager instance is missing!");
            return;
        }

        if (blockPrefab == null)
        {
            Debug.LogError("BuildingSystem: BlockPrefab reference is missing! Please assign it in the inspector.");
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
        if (blockPrefab == null)
            return;

        currentPreview = Instantiate(blockPrefab);

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
        if (currentPreview == null || blockPrefab == null)
            return;

        GameObject newBlock = Instantiate(blockPrefab, previewPosition, Quaternion.identity);

        newBlock.transform.localScale = Vector3.one * currentScaleMultiplier;

        Collider blockCollider = newBlock.GetComponent<Collider>();
        if (blockCollider != null)
        {
            blockCollider.enabled = true;
        }

        MeshRenderer renderer = newBlock.GetComponent<MeshRenderer>();
        if (renderer != null)
        {
            MeshRenderer prefabRenderer = blockPrefab.GetComponent<MeshRenderer>();
            if (prefabRenderer != null)
            {
                renderer.materials = prefabRenderer.sharedMaterials;
            }
        }

        placedBlocks.Add(newBlock);

        Vector3 targetScale = newBlock.transform.localScale;
        newBlock.transform.localScale = Vector3.zero;

        newBlock.transform.DOScale(targetScale, placementDuration).SetEase(Ease.OutBack, 1.2f);

        Destroy(currentPreview);
        currentPreview = null;
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
        if (previewMaterialInstance != null)
            Destroy(previewMaterialInstance);

        if (destructionMaterialInstance != null)
            Destroy(destructionMaterialInstance);
    }
}