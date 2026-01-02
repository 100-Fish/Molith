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

    [Header("Color Scheme")]
    [Tooltip("Default UI color (white)")]
    public Color defaultColor = Color.white;

    [Tooltip("Placement mode color (cyan)")]
    public Color placementColor = new Color(0, 1, 1, 0.5f);

    [Tooltip("Destruction mode color (red)")]
    public Color destructionColor = new Color(1, 0, 0, 0.5f);

    [Tooltip("Build mode color (blue)")]
    public Color buildModeColor = new Color(0.3f, 0.6f, 1f, 1f);

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

        if (buildingSystem == null)
            Debug.LogError("GameManager: BuildingSystem could not be found!");

        if (uiManager == null)
            Debug.LogError("GameManager: UIManager could not be found!");

        if (playerController == null)
            Debug.LogError("GameManager: PlayerController could not be found!");
    }
}
