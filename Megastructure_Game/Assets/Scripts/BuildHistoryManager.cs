using System.Collections.Generic;
using System.Text;
using UnityEngine;

public class BuildHistoryManager : MonoBehaviour
{
    [Header("References")]
    [Tooltip("Reference to the BuildingSystem")]
    public BuildingSystem buildingSystem;

    [Tooltip("List of available block shapes for importing")]
    public List<BlockShape> availableShapes = new List<BlockShape>();

    [Header("Legacy Support")]
    [Tooltip("(Legacy) Direct block prefab reference")]
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
    /// Format: START on first line, each block line contains: shapeName,x,y,z,scale EOL, END on last line
    /// Example:
    /// START
    /// cube,5.0,2.5,10.0,1.2 EOL
    /// sphere,3.5,1.0,8.5,0.8 EOL
    /// END
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
        sb.AppendLine("START");

        foreach (BlockData blockData in history)
        {
            sb.AppendLine(blockData.ToString());
        }

        sb.AppendLine("END");

        string exportData = sb.ToString();
        Debug.Log($"BuildHistoryManager: Exported {history.Count} blocks:\n{exportData}");
        return exportData;
    }

    /// <summary>
    /// Imports blocks from a formatted string and spawns them in the world.
    /// Format: START on first line, each block line contains: shapeName,x,y,z,scale EOL, END on last line
    /// Example:
    /// START
    /// cube,5.0,2.5,10.0,1.2 EOL
    /// sphere,3.5,1.0,8.5,0.8 EOL
    /// END
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
        bool inDataBlock = false;

        foreach (string line in lines)
        {
            string trimmedLine = line.Trim();
            if (string.IsNullOrEmpty(trimmedLine))
                continue;

            // Check for START marker
            if (trimmedLine == "START")
            {
                inDataBlock = true;
                continue;
            }

            // Check for END marker
            if (trimmedLine == "END")
            {
                inDataBlock = false;
                break;
            }

            // Only process lines within START/END block
            if (!inDataBlock)
                continue;

            // Remove EOL marker if present
            trimmedLine = trimmedLine.Replace(" EOL", "").Trim();

            string[] parts = trimmedLine.Split(',');
            if (parts.Length != 5)
            {
                Debug.LogWarning($"BuildHistoryManager: Invalid line format (expected 5 parts): {trimmedLine}");
                failCount++;
                continue;
            }

            string shapeName = parts[0].Trim();

            if (float.TryParse(parts[1], out float x) &&
                float.TryParse(parts[2], out float y) &&
                float.TryParse(parts[3], out float z) &&
                float.TryParse(parts[4], out float scale))
            {
                // Check if we've reached the block limit
                if (buildingSystem.CurrentBlockCount >= buildingSystem.maxBlocks)
                {
                    Debug.LogWarning($"BuildHistoryManager: Block limit reached. Imported {successCount} blocks, {lines.Length - successCount - failCount} remaining.");
                    break;
                }

                Vector3 position = new Vector3(x, y, z);
                SpawnBlock(shapeName, position, scale);
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
    void SpawnBlock(string shapeName, Vector3 position, float scale)
    {
        GameObject prefabToSpawn = GetPrefabForShape(shapeName);

        if (prefabToSpawn == null)
        {
            Debug.LogWarning($"BuildHistoryManager: No prefab found for shape '{shapeName}'. Skipping block.");
            return;
        }

        GameObject newBlock = Instantiate(prefabToSpawn, position, Quaternion.identity);
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
    /// Gets the prefab for a given shape name.
    /// </summary>
    GameObject GetPrefabForShape(string shapeName)
    {
        // First, try to find in availableShapes list
        foreach (BlockShape shape in availableShapes)
        {
            if (shape != null && shape.shapeName == shapeName)
            {
                return shape.shapePrefab;
            }
        }

        // Fallback to legacy blockPrefab if shape name is "cube"
        if (shapeName == "cube" && blockPrefab != null)
        {
            return blockPrefab;
        }

        return null;
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

        // Find all placed blocks by checking for any object that might be a block
        GameObject[] allObjects = FindObjectsOfType<GameObject>();
        foreach (GameObject obj in allObjects)
        {
            // Check if this object is tracked by the building system
            if (obj.GetComponent<MeshRenderer>() != null && obj.GetComponent<Collider>() != null)
            {
                // Check against all available shape prefabs
                bool isBlock = false;

                // Check available shapes
                foreach (BlockShape shape in availableShapes)
                {
                    if (shape != null && shape.shapePrefab != null && obj.name.Contains(shape.shapePrefab.name))
                    {
                        isBlock = true;
                        break;
                    }
                }

                // Check legacy prefab
                if (!isBlock && blockPrefab != null && obj.name.Contains(blockPrefab.name))
                {
                    isBlock = true;
                }

                if (isBlock)
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
