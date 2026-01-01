using UnityEngine;

[CreateAssetMenu(fileName = "NewBlockShape", menuName = "Building/Block Shape")]
public class BlockShape : ScriptableObject
{
    [Header("Shape Settings")]
    [Tooltip("The name identifier for this shape (e.g., 'cube', 'sphere', 'cylinder')")]
    public string shapeName = "cube";

    [Tooltip("The prefab to spawn for this shape (must have a Collider and MeshRenderer)")]
    public GameObject shapePrefab;

    void OnValidate()
    {
        // Ensure shape name is lowercase and has no spaces
        if (!string.IsNullOrEmpty(shapeName))
        {
            shapeName = shapeName.ToLower().Replace(" ", "_");
        }
    }
}
