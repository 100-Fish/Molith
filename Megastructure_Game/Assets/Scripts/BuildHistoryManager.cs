using System.Collections.Generic;
using System.Text;
using UnityEngine;

public class BuildHistoryManager : MonoBehaviour
{
    [Header("References")]
    [Tooltip("Reference to the BuildingSystem")]
    public BuildingSystem buildingSystem;

    [Tooltip("Reference to the block prefab for importing")]
    public GameObject blockPrefab;

    void Start()
    {
        if (buildingSystem == null && GameManager.Instance != null)
        {
            buildingSystem = GameManager.Instance.buildingSystem;
        }

        if (blockPrefab == null && buildingSystem != null)
        {
            blockPrefab = buildingSystem.blockPrefab;
        }
    }

    /// <summary>
    /// Exports the current block history to a simple readable format.
    /// Format: Each line contains: x,y,z,scale
    /// Example: 5.0,2.5,10.0,1.2
    /// </summary>
    public string ExportBuildHistory()
    {
        if (buildingSystem == null)
        {
            Debug.LogError("BuildHistoryManager: BuildingSystem reference is missing!");
            return string.Empty;
        }

        List<BlockData> history = buildingSystem.BlockHistory;
        if (history.Count == 0)
        {
            Debug.Log("BuildHistoryManager: No blocks to export.");
            return string.Empty;
        }

        StringBuilder sb = new StringBuilder();
        foreach (BlockData blockData in history)
        {
            sb.AppendLine(blockData.ToString());
        }

        string exportData = sb.ToString();
        Debug.Log($"BuildHistoryManager: Exported {history.Count} blocks:\n{exportData}");
        return exportData;
    }

    /// <summary>
    /// Imports blocks from a formatted string and spawns them in the world.
    /// Format: Each line contains: x,y,z,scale
    /// Example: 5.0,2.5,10.0,1.2
    /// </summary>
    /// <param name="buildData">The formatted build data string</param>
    /// <param name="clearExisting">If true, clears all existing blocks before importing</param>
    public void ImportBuildHistory(string buildData, bool clearExisting = false)
    {
        if (buildingSystem == null)
        {
            Debug.LogError("BuildHistoryManager: BuildingSystem reference is missing!");
            return;
        }

        if (blockPrefab == null)
        {
            Debug.LogError("BuildHistoryManager: BlockPrefab reference is missing!");
            return;
        }

        if (string.IsNullOrWhiteSpace(buildData))
        {
            Debug.LogWarning("BuildHistoryManager: No build data provided.");
            return;
        }

        // Clear existing blocks if requested
        if (clearExisting)
        {
            ClearAllBlocks();
        }

        string[] lines = buildData.Split('\n');
        int successCount = 0;
        int failCount = 0;

        foreach (string line in lines)
        {
            string trimmedLine = line.Trim();
            if (string.IsNullOrEmpty(trimmedLine))
                continue;

            string[] parts = trimmedLine.Split(',');
            if (parts.Length != 4)
            {
                Debug.LogWarning($"BuildHistoryManager: Invalid line format: {trimmedLine}");
                failCount++;
                continue;
            }

            if (float.TryParse(parts[0], out float x) &&
                float.TryParse(parts[1], out float y) &&
                float.TryParse(parts[2], out float z) &&
                float.TryParse(parts[3], out float scale))
            {
                // Check if we've reached the block limit
                if (buildingSystem.CurrentBlockCount >= buildingSystem.maxBlocks)
                {
                    Debug.LogWarning($"BuildHistoryManager: Block limit reached. Imported {successCount} blocks, {lines.Length - successCount - failCount} remaining.");
                    break;
                }

                Vector3 position = new Vector3(x, y, z);
                SpawnBlock(position, scale);
                successCount++;
            }
            else
            {
                Debug.LogWarning($"BuildHistoryManager: Failed to parse line: {trimmedLine}");
                failCount++;
            }
        }

        Debug.Log($"BuildHistoryManager: Import complete. Success: {successCount}, Failed: {failCount}");
    }

    /// <summary>
    /// Spawns a single block at the specified position with the given scale.
    /// </summary>
    void SpawnBlock(Vector3 position, float scale)
    {
        GameObject newBlock = Instantiate(blockPrefab, position, Quaternion.identity);
        newBlock.transform.localScale = Vector3.one * scale;

        // Enable collider
        Collider blockCollider = newBlock.GetComponent<Collider>();
        if (blockCollider != null)
        {
            blockCollider.enabled = true;
        }

        // Add to building system
        buildingSystem.AddBlock(newBlock);
    }

    /// <summary>
    /// Clears all blocks from the world.
    /// </summary>
    public void ClearAllBlocks()
    {
        if (buildingSystem == null)
        {
            Debug.LogError("BuildHistoryManager: BuildingSystem reference is missing!");
            return;
        }

        List<BlockData> history = buildingSystem.BlockHistory;
        int count = history.Count;

        // We need to get a copy of the blocks to destroy
        List<GameObject> blocksToDestroy = new List<GameObject>();

        // Find all GameObjects with the block tag or that are children of the building system
        GameObject[] allObjects = FindObjectsOfType<GameObject>();
        foreach (GameObject obj in allObjects)
        {
            // Check if this object is tracked by the building system
            if (obj.GetComponent<MeshRenderer>() != null && obj.GetComponent<Collider>() != null)
            {
                // This might be a placed block - try to verify by checking if it has our prefab components
                if (obj.name.Contains(blockPrefab.name))
                {
                    blocksToDestroy.Add(obj);
                }
            }
        }

        foreach (GameObject block in blocksToDestroy)
        {
            buildingSystem.RemoveBlock(block);
            Destroy(block);
        }

        Debug.Log($"BuildHistoryManager: Cleared {count} blocks from history.");
    }

    /// <summary>
    /// Copies the exported build history to the clipboard (Windows only).
    /// </summary>
    public void CopyToClipboard()
    {
        string exportData = ExportBuildHistory();
        if (!string.IsNullOrEmpty(exportData))
        {
            GUIUtility.systemCopyBuffer = exportData;
            Debug.Log("BuildHistoryManager: Build history copied to clipboard!");
        }
    }

    /// <summary>
    /// Imports build history from the clipboard (Windows only).
    /// </summary>
    public void PasteFromClipboard(bool clearExisting = false)
    {
        string clipboardData = GUIUtility.systemCopyBuffer;
        if (!string.IsNullOrEmpty(clipboardData))
        {
            ImportBuildHistory(clipboardData, clearExisting);
        }
        else
        {
            Debug.LogWarning("BuildHistoryManager: Clipboard is empty.");
        }
    }
}
