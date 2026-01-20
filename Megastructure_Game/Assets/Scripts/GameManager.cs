using UnityEngine;
using SUPERCharacter;

public class GameManager : MonoBehaviour
{
    [Header("Singleton")]
    private static GameManager instance;
    public static GameManager Instance
    {
        get
        {
            if (instance == null)
            {
                instance = FindObjectOfType<GameManager>();
                if (instance == null)
                {
                    GameObject go = new GameObject("GameManager");
                    instance = go.AddComponent<GameManager>();
                }
            }
            return instance;
        }
    }

    [Header("System References")]
    [Tooltip("Reference to the BuildingSystem")]
    public BuildingSystem buildingSystem;

    [Tooltip("Reference to the UIManager")]
    public UIManager uiManager;

    [Tooltip("Reference to the SUPERCharacterAIO script on the player")]
    public SUPERCharacterAIO playerController;

    [Tooltip("Reference to the BuildModeController")]
    public BuildModeController buildModeController;

    [Tooltip("Reference to the DirectionalArrowSystem")]
    public DirectionalArrowSystem arrowSystem;

    [Tooltip("Reference to the BuildModeCameraController")]
    public BuildModeCameraController buildCameraController;

    [Tooltip("Reference to the BlockAdjacencyGrid")]
    public BlockAdjacencyGrid adjacencyGrid;

    [Tooltip("Reference to the CameraManager")]
    public CameraManager cameraManager;

    [Tooltip("Reference to the WorldGenerator")]
    public WorldGenerator worldGenerator;

    [Header("Materials")]
    [Tooltip("Hologram material (should use FX/Hologram shader)")]
    public Material hologramMaterial;

    [Header("Color Scheme")]
    [Tooltip("Default UI color (white)")]
    public Color defaultColor = Color.white;

    [Tooltip("Placement/Build mode color (cyan) - used for both placement preview and build mode")]
    public Color placementColor = new Color(0, 1, 1, 0.5f);

    [Tooltip("Destruction mode color (red)")]
    public Color destructionColor = new Color(1, 0, 0, 0.5f);

    // Build mode uses the same color as placement mode
    public Color buildModeColor => placementColor;

    [Header("Height Tracking")]
    [Tooltip("The minimum height threshold before displaying actual height (displays xx.xx until exceeded)")]
    public float heightDisplayThreshold = 10f;

    private float maxHeightReached = 0f;

    /// <summary>
    /// The maximum height the player has reached. Returns 0 if below threshold.
    /// </summary>
    public float MaxHeightReached => maxHeightReached;

    /// <summary>
    /// Whether the player has exceeded the height threshold.
    /// </summary>
    public bool HasExceededHeightThreshold => maxHeightReached > heightDisplayThreshold;

    void Awake()
    {
        if (instance == null)
        {
            instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else if (instance != this)
        {
            Destroy(gameObject);
        }
    }

    void Start()
    {
        ValidateReferences();
    }

    void Update()
    {
        UpdateMaxHeight();
    }

    void UpdateMaxHeight()
    {
        if (playerController == null) return;

        float currentHeight = playerController.transform.position.y;
        if (currentHeight > maxHeightReached)
        {
            maxHeightReached = currentHeight;
        }
    }

    /// <summary>
    /// Resets the max height tracker (useful for respawning or level changes).
    /// </summary>
    public void ResetMaxHeight()
    {
        maxHeightReached = 0f;
    }

    void ValidateReferences()
    {
        if (buildingSystem == null)
        {
            Debug.LogWarning("GameManager: BuildingSystem reference is missing. Attempting to find it...");
            buildingSystem = FindObjectOfType<BuildingSystem>();
        }

        if (uiManager == null)
        {
            Debug.LogWarning("GameManager: UIManager reference is missing. Attempting to find it...");
            uiManager = FindObjectOfType<UIManager>();
        }

        if (playerController == null)
        {
            Debug.LogWarning("GameManager: PlayerController reference is missing. Attempting to find it...");
            playerController = FindObjectOfType<SUPERCharacterAIO>();
        }

        if (buildModeController == null)
        {
            Debug.LogWarning("GameManager: BuildModeController reference is missing. Attempting to find it...");
            buildModeController = FindObjectOfType<BuildModeController>();
        }

        if (arrowSystem == null)
        {
            Debug.LogWarning("GameManager: DirectionalArrowSystem reference is missing. Attempting to find it...");
            arrowSystem = FindObjectOfType<DirectionalArrowSystem>();
        }

        if (buildCameraController == null)
        {
            Debug.LogWarning("GameManager: BuildModeCameraController reference is missing. Attempting to find it...");
            buildCameraController = FindObjectOfType<BuildModeCameraController>();
        }

        if (adjacencyGrid == null)
        {
            Debug.LogWarning("GameManager: BlockAdjacencyGrid reference is missing. Attempting to find it...");
            adjacencyGrid = FindObjectOfType<BlockAdjacencyGrid>();
        }

        if (cameraManager == null)
        {
            Debug.LogWarning("GameManager: CameraManager reference is missing. Attempting to find it...");
            cameraManager = FindObjectOfType<CameraManager>();
        }

        if (worldGenerator == null)
        {
            Debug.LogWarning("GameManager: WorldGenerator reference is missing. Attempting to find it...");
            worldGenerator = FindObjectOfType<WorldGenerator>();
        }

        // Error logging for critical systems
        if (buildingSystem == null)
            Debug.LogError("GameManager: BuildingSystem could not be found!");

        if (uiManager == null)
            Debug.LogError("GameManager: UIManager could not be found!");

        if (playerController == null)
            Debug.LogError("GameManager: PlayerController could not be found!");

        if (cameraManager == null)
            Debug.LogError("GameManager: CameraManager could not be found!");
    }
}
