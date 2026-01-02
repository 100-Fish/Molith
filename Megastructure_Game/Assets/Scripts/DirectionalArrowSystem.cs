using UnityEngine;
using TMPro;
using DG.Tweening;

public class DirectionalArrowSystem : MonoBehaviour
{
    [Header("UI References")]
    public Canvas uiCanvas;
    public TextMeshProUGUI[] arrowTextElements = new TextMeshProUGUI[4]; // W, A, S, D

    [Header("Arrow Settings")]
    public float arrowOffset = 0.6f;
    public float snapDuration = 0.3f; // DOTween duration for smooth snapping
    public Color[] wasdColors = new Color[] {
        new Color(1f, 1f, 0f, 1f),    // W - Yellow
        new Color(0f, 1f, 1f, 1f),    // A - Cyan
        new Color(0f, 1f, 0f, 1f),    // S - Green
        new Color(1f, 0f, 1f, 1f)     // D - Magenta
    };
    public string[] wasdLabels = new string[] { "W", "A", "S", "D" };

    private GameObject currentBlock;
    private Camera playerCamera;
    private Vector3[] currentCardinalDirections = new Vector3[4];

    // Arrow position data - using class wrapper for proper DOTween reference
    private ArrowPositionData[] arrowData = new ArrowPositionData[4];

    // Helper class to wrap position for DOTween
    private class ArrowPositionData
    {
        public Vector3 worldPosition;
        public Tweener tween;
    }

    void Start()
    {
        // Initialize arrow data
        for (int i = 0; i < 4; i++)
        {
            arrowData[i] = new ArrowPositionData();
        }

        // Initialize UI canvas if not assigned
        if (uiCanvas == null)
        {
            uiCanvas = FindObjectOfType<Canvas>();
        }

        // Ensure canvas is in Screen Space - Overlay mode for proper positioning
        if (uiCanvas != null && uiCanvas.renderMode != RenderMode.ScreenSpaceOverlay)
        {
            Debug.LogWarning("DirectionalArrowSystem: Canvas should be in Screen Space - Overlay mode for correct positioning with render textures.");
        }

        // Create UI elements if not assigned
        for (int i = 0; i < 4; i++)
        {
            if (arrowTextElements[i] == null)
            {
                arrowTextElements[i] = CreateUIElement(i);
            }
            arrowTextElements[i].gameObject.SetActive(false);
        }
    }

    private TextMeshProUGUI CreateUIElement(int index)
    {
        GameObject uiObj = new GameObject($"Arrow_UI_{wasdLabels[index]}");
        uiObj.transform.SetParent(uiCanvas.transform, false);

        TextMeshProUGUI textMesh = uiObj.AddComponent<TextMeshProUGUI>();
        textMesh.text = wasdLabels[index];
        textMesh.fontSize = 36;
        textMesh.alignment = TextAlignmentOptions.Center;
        textMesh.color = wasdColors[index];
        textMesh.fontStyle = FontStyles.Bold;

        // Add outline for visibility
        textMesh.outlineWidth = 0.2f;
        textMesh.outlineColor = Color.black;

        RectTransform rectTransform = uiObj.GetComponent<RectTransform>();
        rectTransform.sizeDelta = new Vector2(100, 100);

        return textMesh;
    }

    public void ShowArrows(GameObject block, Camera cam)
    {
        currentBlock = block;
        playerCamera = cam;

        for (int i = 0; i < 4; i++)
        {
            if (arrowTextElements[i] != null)
                arrowTextElements[i].gameObject.SetActive(true);
        }

        UpdateArrowPositions();
    }

    public void HideArrows()
    {
        // Clear current block reference to stop Update loop
        currentBlock = null;
        playerCamera = null;

        // Kill all active tweens
        for (int i = 0; i < 4; i++)
        {
            if (arrowData[i] != null && arrowData[i].tween != null && arrowData[i].tween.IsActive())
            {
                arrowData[i].tween.Kill();
            }
        }

        // Hide all arrow UI elements
        foreach (var arrow in arrowTextElements)
        {
            if (arrow != null)
                arrow.gameObject.SetActive(false);
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

        Vector3[] newCardinalDirections = new Vector3[4];

        if (showHorizontal)
        {
            // Horizontal mode - quantize to cardinal directions
            Vector3 camForwardFlat = new Vector3(camForward.x, 0, camForward.z).normalized;
            Vector3 camRightFlat = new Vector3(playerCamera.transform.right.x, 0, playerCamera.transform.right.z).normalized;

            // W = forward, A = left, S = back, D = right (relative to camera)
            newCardinalDirections[0] = QuantizeToCardinal(camForwardFlat);
            newCardinalDirections[1] = QuantizeToCardinal(-camRightFlat);
            newCardinalDirections[2] = QuantizeToCardinal(-camForwardFlat);
            newCardinalDirections[3] = QuantizeToCardinal(camRightFlat);
        }
        else
        {
            // Vertical mode - already cardinal
            newCardinalDirections[0] = Vector3.up;
            newCardinalDirections[1] = Vector3.left;
            newCardinalDirections[2] = Vector3.down;
            newCardinalDirections[3] = Vector3.right;
        }

        // Calculate target world positions based on cardinal directions
        for (int i = 0; i < 4; i++)
        {
            Vector3 newTargetWorldPos = blockCenter + newCardinalDirections[i] * (blockSize / 2 + arrowOffset);

            // Check if cardinal direction changed - if so, use DOTween
            if (currentCardinalDirections[i] != newCardinalDirections[i])
            {
                currentCardinalDirections[i] = newCardinalDirections[i];

                // Kill existing tween
                if (arrowData[i].tween != null && arrowData[i].tween.IsActive())
                {
                    arrowData[i].tween.Kill();
                }

                // Animate the world position smoothly
                Vector3 startPos = arrowData[i].worldPosition != Vector3.zero ? arrowData[i].worldPosition : newTargetWorldPos;

                arrowData[i].tween = DOTween.To(
                    () => startPos,
                    x => arrowData[i].worldPosition = x,
                    newTargetWorldPos,
                    snapDuration
                ).SetEase(Ease.OutCubic);
            }
            else
            {
                // Same cardinal direction, just update position directly (for block movement)
                arrowData[i].worldPosition = newTargetWorldPos;
            }

            // Convert world position to viewport, then to screen space
            // This handles render texture scenarios correctly
            Vector3 viewportPos = playerCamera.WorldToViewportPoint(arrowData[i].worldPosition);

            // Check if behind camera
            if (viewportPos.z > 0)
            {
                // Convert viewport to actual screen space for UI
                // Viewport is 0-1, screen is pixel-based
                Vector2 screenPos = new Vector2(
                    viewportPos.x * Screen.width,
                    viewportPos.y * Screen.height
                );

                arrowTextElements[i].rectTransform.position = screenPos;
                arrowTextElements[i].gameObject.SetActive(true);
            }
            else
            {
                arrowTextElements[i].gameObject.SetActive(false);
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

    void OnDestroy()
    {
        // Clean up tweens
        for (int i = 0; i < 4; i++)
        {
            if (arrowData[i] != null && arrowData[i].tween != null && arrowData[i].tween.IsActive())
            {
                arrowData[i].tween.Kill();
            }
        }

        // Clean up UI elements
        foreach (var arrow in arrowTextElements)
        {
            if (arrow != null)
                Destroy(arrow.gameObject);
        }
    }
}
