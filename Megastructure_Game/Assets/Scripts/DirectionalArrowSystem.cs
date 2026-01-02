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

    public void UpdateArrowPositions(GameObject block, Camera cam)
    {
        currentBlock = block;
        playerCamera = cam;
        UpdateArrowPositions();
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
            // Horizontal mode - quantize camera yaw to nearest cardinal direction
            Vector3 camForwardFlat = new Vector3(camForward.x, 0, camForward.z).normalized;

            switch (key)
            {
                case KeyCode.W: return QuantizeToCardinal(camForwardFlat);
                case KeyCode.A: return QuantizeToCardinal(Quaternion.Euler(0, -90, 0) * camForwardFlat);
                case KeyCode.S: return QuantizeToCardinal(-camForwardFlat);
                case KeyCode.D: return QuantizeToCardinal(Quaternion.Euler(0, 90, 0) * camForwardFlat);
                default: return Vector3.zero;
            }
        }
        else
        {
            // Vertical mode - already cardinal
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

    private Vector3 QuantizeToCardinal(Vector3 direction)
    {
        // Snap to nearest world axis (forward/back/left/right)
        float absX = Mathf.Abs(direction.x);
        float absZ = Mathf.Abs(direction.z);

        if (absX > absZ)
        {
            // Closer to X axis
            return direction.x > 0 ? Vector3.right : Vector3.left;
        }
        else
        {
            // Closer to Z axis
            return direction.z > 0 ? Vector3.forward : Vector3.back;
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
