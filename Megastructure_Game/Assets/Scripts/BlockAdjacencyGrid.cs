using UnityEngine;
using System.Collections.Generic;

public class BlockAdjacencyGrid : MonoBehaviour
{
    private Dictionary<Vector3Int, GameObject> blockGrid = new Dictionary<Vector3Int, GameObject>();
    private float gridCellSize = 1.0f;

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
