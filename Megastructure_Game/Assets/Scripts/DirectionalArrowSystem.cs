using UnityEngine;

public class DirectionalArrowSystem : MonoBehaviour
{
    [Header("Arrow Settings")]
    public GameObject arrowPrefab;
    public float arrowOffset = 0.6f;
    public Color[] wasdColors = new Color[] {
        new Color(1f, 1f, 0f, 1f),    // W - Yellow
        new Color(0f, 1f, 1f, 1f),    // A - Cyan
        new Color(0f, 1f, 0f, 1f),    // S - Green
        new Color(1f, 0f, 1f, 1f)     // D - Magenta
    };

    private GameObject[] arrows = new GameObject[4];
    private GameObject currentBlock;
    private Camera playerCamera;

    void Start()
    {
        // Create arrow prefab if not assigned
        if (arrowPrefab == null)
        {
            arrowPrefab = CreateDefaultArrowPrefab();
        }
    }

    public void ShowArrows(GameObject block, Camera cam)
    {
        currentBlock = block;
        playerCamera = cam;

        // Create arrows if needed
        for (int i = 0; i < 4; i++)
        {
            if (arrows[i] == null)
            {
                arrows[i] = Instantiate(arrowPrefab);
                arrows[i].name = $"Arrow_{i}";
            }
            arrows[i].SetActive(true);
        }

        UpdateArrowPositions();
    }

    public void HideArrows()
    {
        foreach (var arrow in arrows)
        {
            if (arrow != null)
                arrow.SetActive(false);
        }
    }

    void Update()
    {
        if (currentBlock != null && playerCamera != null)
        {
            UpdateArrowPositions();
        }
    }

    private void UpdateArrowPositions()
    {
        if (currentBlock == null || playerCamera == null) return;

        Vector3 blockCenter = currentBlock.transform.position;
        float blockSize = currentBlock.transform.localScale.x;

        // Determine camera orientation
        Vector3 camForward = playerCamera.transform.forward;
        float pitch = Vector3.Angle(camForward, Vector3.down) - 90f;
        bool showHorizontal = Mathf.Abs(pitch) < 45f;

        if (showHorizontal)
        {
            // Horizontal arrows
            Vector3 camRight = Vector3.Cross(Vector3.up, camForward).normalized;
            Vector3 camForwardFlat = Vector3.Cross(camRight, Vector3.up).normalized;

            // W = forward, A = left, S = back, D = right
            Vector3[] positions = new Vector3[] {
                blockCenter + camForwardFlat * (blockSize/2 + arrowOffset),
                blockCenter - camRight * (blockSize/2 + arrowOffset),
                blockCenter - camForwardFlat * (blockSize/2 + arrowOffset),
                blockCenter + camRight * (blockSize/2 + arrowOffset)
            };

            Quaternion[] rotations = new Quaternion[] {
                Quaternion.LookRotation(camForwardFlat, Vector3.up),
                Quaternion.LookRotation(-camRight, Vector3.up),
                Quaternion.LookRotation(-camForwardFlat, Vector3.up),
                Quaternion.LookRotation(camRight, Vector3.up)
            };

            for (int i = 0; i < 4; i++)
            {
                arrows[i].transform.position = positions[i];
                arrows[i].transform.rotation = rotations[i];
                SetArrowColor(arrows[i], wasdColors[i]);
            }
        }
        else
        {
            // Vertical arrows
            Vector3[] positions = new Vector3[] {
                blockCenter + Vector3.up * (blockSize/2 + arrowOffset),
                blockCenter + Vector3.left * (blockSize/2 + arrowOffset),
                blockCenter + Vector3.down * (blockSize/2 + arrowOffset),
                blockCenter + Vector3.right * (blockSize/2 + arrowOffset)
            };

            Quaternion[] rotations = new Quaternion[] {
                Quaternion.LookRotation(Vector3.up),
                Quaternion.LookRotation(Vector3.left),
                Quaternion.LookRotation(Vector3.down),
                Quaternion.LookRotation(Vector3.right)
            };

            for (int i = 0; i < 4; i++)
            {
                arrows[i].transform.position = positions[i];
                arrows[i].transform.rotation = rotations[i];
                SetArrowColor(arrows[i], wasdColors[i]);
            }
        }
    }

    public Vector3 GetPlacementDirection(KeyCode key)
    {
        if (currentBlock == null || playerCamera == null)
            return Vector3.zero;

        Vector3 camForward = playerCamera.transform.forward;
        float pitch = Vector3.Angle(camForward, Vector3.down) - 90f;
        bool isHorizontal = Mathf.Abs(pitch) < 45f;

        if (isHorizontal)
        {
            Vector3 camRight = Vector3.Cross(Vector3.up, camForward).normalized;
            Vector3 camForwardFlat = Vector3.Cross(camRight, Vector3.up).normalized;

            switch (key)
            {
                case KeyCode.W: return camForwardFlat;
                case KeyCode.A: return -camRight;
                case KeyCode.S: return -camForwardFlat;
                case KeyCode.D: return camRight;
                default: return Vector3.zero;
            }
        }
        else
        {
            switch (key)
            {
                case KeyCode.W: return Vector3.up;
                case KeyCode.A: return Vector3.left;
                case KeyCode.S: return Vector3.down;
                case KeyCode.D: return Vector3.right;
                default: return Vector3.zero;
            }
        }
    }

    private GameObject CreateDefaultArrowPrefab()
    {
        // Create simple cone arrow
        GameObject arrow = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        arrow.transform.localScale = new Vector3(0.2f, 0.3f, 0.2f);

        // Remove collider
        Destroy(arrow.GetComponent<Collider>());

        // Create emissive material
        Material mat = new Material(Shader.Find("Standard"));
        mat.EnableKeyword("_EMISSION");
        mat.SetColor("_EmissionColor", Color.yellow * 2f);
        arrow.GetComponent<MeshRenderer>().material = mat;

        return arrow;
    }

    private void SetArrowColor(GameObject arrow, Color color)
    {
        if (arrow == null) return;

        MeshRenderer renderer = arrow.GetComponent<MeshRenderer>();
        if (renderer != null && renderer.material != null)
        {
            renderer.material.SetColor("_Color", color);
            renderer.material.SetColor("_EmissionColor", color * 2f);
        }
    }

    void OnDestroy()
    {
        // Clean up arrows
        foreach (var arrow in arrows)
        {
            if (arrow != null)
                Destroy(arrow);
        }
    }
}
