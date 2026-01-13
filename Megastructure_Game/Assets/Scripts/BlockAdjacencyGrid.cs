using UnityEngine;
using System.Collections.Generic;

public class BlockAdjacencyGrid : MonoBehaviour
{
    private Dictionary<Vector3Int, GameObject> blockGrid = new Dictionary<Vector3Int, GameObject>();
    private float gridCellSize = 0.25f; // Changed from 1.0f to support platform thickness

    public void SetGridCellSize(float scale)
    {
        gridCellSize = 1.0f * scale; // Changed from 0.25f to 1.0f for cube-based grid
    }

    public void RegisterBlock(GameObject block, Vector3 worldPosition)
    {
        Vector3Int gridPos = WorldToGrid(worldPosition);

        if (blockGrid.ContainsKey(gridPos))
        {
            Debug.LogWarning($"BlockAdjacencyGrid: Block already registered at grid position {gridPos}");
            return;
        }

        blockGrid[gridPos] = block;
    }

    public void UnregisterBlock(Vector3 worldPosition)
    {
        Vector3Int gridPos = WorldToGrid(worldPosition);
        blockGrid.Remove(gridPos);
    }

    public bool IsPositionOccupied(Vector3 worldPosition)
    {
        Vector3Int gridPos = WorldToGrid(worldPosition);
        return blockGrid.ContainsKey(gridPos);
    }

    public GameObject GetBlockAtPosition(Vector3 worldPosition)
    {
        Vector3Int gridPos = WorldToGrid(worldPosition);
        return blockGrid.ContainsKey(gridPos) ? blockGrid[gridPos] : null;
    }

    /// <summary>
    /// Get platform at XZ coordinate (any height)
    /// Used for platform replacement logic - only one platform per XZ allowed
    /// </summary>
    public GameObject GetPlatformAtXZ(float worldX, float worldZ)
    {
        int gridX = Mathf.RoundToInt(worldX / gridCellSize);
        int gridZ = Mathf.RoundToInt(worldZ / gridCellSize);

        foreach (var kvp in blockGrid)
        {
            if (kvp.Key.x == gridX && kvp.Key.z == gridZ)
            {
                return kvp.Value;
            }
        }
        return null;
    }

    private Vector3Int WorldToGrid(Vector3 worldPos)
    {
        return new Vector3Int(
            Mathf.RoundToInt(worldPos.x / gridCellSize),
            Mathf.RoundToInt(worldPos.y / gridCellSize),
            Mathf.RoundToInt(worldPos.z / gridCellSize)
        );
    }

    public void Clear()
    {
        blockGrid.Clear();
    }
}
