using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// Manages scaffolding cubes for a single platform.
/// Each platform has scaffolding extending down to Y=0 (ground level).
/// </summary>
public class ScaffoldingManager : MonoBehaviour
{
    [Header("Scaffolding Settings")]
    [Tooltip("Prefab for individual scaffolding cubes")]
    public GameObject scaffoldingCubePrefab;

    [Tooltip("Ground level Y-coordinate where scaffolding stops")]
    public float groundLevel = 0f;

    [Tooltip("Platform scale multiplier")]
    public float platformScale = 1.0f;

    [Header("References")]
    private GameObject parentPlatform;
    private List<GameObject> scaffoldingCubes = new List<GameObject>();
    private Material platformMaterial;

    /// <summary>
    /// Initialize scaffolding for this platform
    /// </summary>
    public void Initialize(GameObject platform, GameObject scaffoldingPrefab, float scale, float ground)
    {
        parentPlatform = platform;
        scaffoldingCubePrefab = scaffoldingPrefab;
        platformScale = scale;
        groundLevel = ground;

        // Get platform's material to apply to scaffolding (handles child structure)
        MeshRenderer[] renderers = platform.GetComponentsInChildren<MeshRenderer>();
        if (renderers != null && renderers.Length > 0 && renderers[0].materials.Length > 0)
        {
            platformMaterial = renderers[0].materials[0];
        }

        GenerateScaffolding();
    }

    /// <summary>
    /// Generate scaffolding cubes from platform down to ground
    /// </summary>
    private void GenerateScaffolding()
    {
        if (parentPlatform == null || scaffoldingCubePrefab == null)
        {
            Debug.LogError($"ScaffoldingManager: Missing references! Platform={parentPlatform != null}, Prefab={scaffoldingCubePrefab != null}");
            return;
        }

        Vector3 platformPos = parentPlatform.transform.position;
        int platformYLevel = Mathf.RoundToInt(platformPos.y / platformScale);
        int groundYLevel = Mathf.RoundToInt(groundLevel / platformScale);

        Debug.Log($"ScaffoldingManager: Platform at {platformPos}, Y level={platformYLevel}, Ground level={groundYLevel}, Scale={platformScale}");
        Debug.Log($"ScaffoldingManager: Will generate {platformYLevel - groundYLevel} scaffolding cubes (from Y={groundYLevel} to Y={platformYLevel - 1})");

        // Generate scaffolding from ground up to (but not including) platform level
        for (int y = groundYLevel; y < platformYLevel; y++)
        {
            Vector3 scaffoldPos = new Vector3(
                platformPos.x,
                y * platformScale,
                platformPos.z
            );

            GameObject scaffoldCube = Instantiate(scaffoldingCubePrefab, scaffoldPos, Quaternion.identity);

            // IMPORTANT: Do NOT parent to platform - keep independent to avoid DOTween scale issues
            // Scaffolding should not be affected by platform's scale animations
            scaffoldCube.transform.localScale = Vector3.one * platformScale;

            scaffoldCube.name = $"Scaffolding_Y{y}";

            Debug.Log($"ScaffoldingManager: Created scaffolding at world pos {scaffoldPos}, scale {scaffoldCube.transform.localScale}");

            // Apply platform material to scaffolding (handles child structure)
            if (platformMaterial != null)
            {
                // Apply to all renderers in children (handles empty container with child model)
                MeshRenderer[] scaffoldRenderers = scaffoldCube.GetComponentsInChildren<MeshRenderer>();
                foreach (MeshRenderer renderer in scaffoldRenderers)
                {
                    if (renderer != null)
                    {
                        renderer.material = platformMaterial;
                    }
                }
            }

            // Keep collider enabled (allows player to climb scaffolding)
            // Uncomment to disable colliders if needed:
            // Collider[] scaffoldColliders = scaffoldCube.GetComponentsInChildren<Collider>();
            // foreach (Collider collider in scaffoldColliders)
            // {
            //     if (collider != null)
            //         collider.enabled = false;
            // }

            scaffoldingCubes.Add(scaffoldCube);
        }

        Debug.Log($"ScaffoldingManager: Generated {scaffoldingCubes.Count} scaffolding cubes for platform at {platformPos}");
    }

    /// <summary>
    /// Destroy all scaffolding cubes
    /// </summary>
    public void DestroyScaffolding()
    {
        foreach (GameObject cube in scaffoldingCubes)
        {
            if (cube != null)
            {
                Destroy(cube);
            }
        }
        scaffoldingCubes.Clear();
    }

    /// <summary>
    /// Update scaffolding material when platform material changes
    /// </summary>
    public void UpdateMaterial(Material newMaterial)
    {
        platformMaterial = newMaterial;

        int updateCount = 0;
        foreach (GameObject cube in scaffoldingCubes)
        {
            if (cube != null)
            {
                // Apply to all renderers in children (handles empty container with child model)
                MeshRenderer[] renderers = cube.GetComponentsInChildren<MeshRenderer>();
                foreach (MeshRenderer renderer in renderers)
                {
                    if (renderer != null)
                    {
                        renderer.material = newMaterial;
                        updateCount++;
                    }
                }
            }
        }

        Debug.Log($"ScaffoldingManager: Updated material on {updateCount} scaffold renderers to {newMaterial.name}");
    }

    void OnDestroy()
    {
        DestroyScaffolding();
    }
}
