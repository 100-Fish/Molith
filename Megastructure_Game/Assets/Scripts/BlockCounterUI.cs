using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;

#if UNITY_EDITOR
using UnityEditor;
#endif

/// <summary>
/// Manages a grid of block indicator UI elements that show available/used blocks.
/// Automatically generates child block images and flips every other one.
/// Works in editor and runtime.
/// </summary>
[ExecuteAlways]
public class BlockCounterUI : MonoBehaviour
{
    [Header("Block Indicator Settings")]
    [Tooltip("Prefab for individual block indicator (must have Image component)")]
    public GameObject blockIndicatorPrefab;

    [Header("Auto-Flip Settings")]
    [Tooltip("Flip every other block indicator 180 degrees on Z axis")]
    public bool flipAlternating = true;

    // Sprites (set by UIManager at runtime - inverted: available=inactive, used=active)
    [HideInInspector] public Sprite availableSprite;
    [HideInInspector] public Sprite usedSprite;

    // Max blocks (synced from BuildingSystem)
    private int maxBlocks = 0;

    // Runtime tracking
    private List<Image> blockIndicators = new List<Image>();
    private int lastMaxBlocks = -1;
    private bool lastFlipAlternating = true;

    void OnEnable()
    {
        RegenerateIfNeeded();
    }

    void Update()
    {
        // In editor, check if settings changed and regenerate
#if UNITY_EDITOR
        if (!Application.isPlaying)
        {
            if (maxBlocks != lastMaxBlocks || flipAlternating != lastFlipAlternating)
            {
                RegenerateIndicators();
            }
        }
#endif
    }

    void RegenerateIfNeeded()
    {
        if (maxBlocks != lastMaxBlocks || flipAlternating != lastFlipAlternating || blockIndicators.Count != maxBlocks)
        {
            RegenerateIndicators();
        }
    }

    [ContextMenu("Regenerate Indicators")]
    public void RegenerateIndicators()
    {
        if (blockIndicatorPrefab == null)
        {
            Debug.LogWarning("BlockCounterUI: Block indicator prefab is not assigned!");
            return;
        }

        // Clear existing children
        ClearChildren();

        blockIndicators.Clear();

        // Generate new indicators
        for (int i = 0; i < maxBlocks; i++)
        {
            GameObject indicator;

#if UNITY_EDITOR
            if (!Application.isPlaying)
            {
                indicator = (GameObject)PrefabUtility.InstantiatePrefab(blockIndicatorPrefab, transform);
            }
            else
            {
                indicator = Instantiate(blockIndicatorPrefab, transform);
            }
#else
            indicator = Instantiate(blockIndicatorPrefab, transform);
#endif

            indicator.name = $"BlockIndicator_{i}";

            // Flip every other indicator
            if (flipAlternating && i % 2 == 1)
            {
                indicator.transform.localRotation = Quaternion.Euler(0f, 0f, 180f);
            }
            else
            {
                indicator.transform.localRotation = Quaternion.identity;
            }

            Image img = indicator.GetComponent<Image>();
            if (img != null)
            {
                blockIndicators.Add(img);
                // Default to available sprite
                if (availableSprite != null)
                    img.sprite = availableSprite;
            }
        }

        lastMaxBlocks = maxBlocks;
        lastFlipAlternating = flipAlternating;

#if UNITY_EDITOR
        if (!Application.isPlaying)
        {
            EditorUtility.SetDirty(gameObject);
        }
#endif
    }

    void ClearChildren()
    {
        // Collect children to destroy (can't modify collection while iterating)
        List<GameObject> toDestroy = new List<GameObject>();
        for (int i = transform.childCount - 1; i >= 0; i--)
        {
            toDestroy.Add(transform.GetChild(i).gameObject);
        }

        foreach (var child in toDestroy)
        {
#if UNITY_EDITOR
            if (!Application.isPlaying)
            {
                DestroyImmediate(child);
            }
            else
            {
                Destroy(child);
            }
#else
            Destroy(child);
#endif
        }
    }

    /// <summary>
    /// Updates the block indicators to show current vs max blocks.
    /// Blocks are shown as "available" (active sprite) when index >= usedCount.
    /// </summary>
    public void UpdateBlockCount(int usedCount)
    {
        for (int i = 0; i < blockIndicators.Count; i++)
        {
            if (blockIndicators[i] == null) continue;

            // Blocks are available if their index is >= usedCount
            // e.g., if usedCount=3, blocks 0,1,2 are used, blocks 3+ are available
            bool isAvailable = i >= usedCount;
            blockIndicators[i].sprite = isAvailable ? availableSprite : usedSprite;
        }
    }

    /// <summary>
    /// Refreshes the indicator list from current children (useful after scene load)
    /// </summary>
    public void RefreshIndicatorList()
    {
        blockIndicators.Clear();
        for (int i = 0; i < transform.childCount; i++)
        {
            Image img = transform.GetChild(i).GetComponent<Image>();
            if (img != null)
            {
                blockIndicators.Add(img);
            }
        }
    }

    /// <summary>
    /// Sets the max blocks and regenerates indicators if needed
    /// </summary>
    public void SetMaxBlocks(int count)
    {
        maxBlocks = Mathf.Max(0, count);
        RegenerateIfNeeded();
    }
}
