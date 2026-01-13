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
    public string[] wasdLabels = new string[] { "W", "A", "S", "D" };

    [Header("Platform Configuration")]
    [Tooltip("Scale multiplier for platforms (should match BuildModeController)")]
    [Range(0.1f, 5.0f)]
    public float platformScale = 1.0f;

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
        // textMesh.color = wasdColors[index];
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

        // Determine camera orientation
        Vector3 camForward = playerCamera.transform.forward;
        float pitch = Vector3.Angle(camForward, Vector3.down) - 90f;
        bool showHorizontal = Mathf.Abs(pitch) < 45f;

        Vector3[] newCardinalDirections = new Vector3[4];

        if (showHorizontal)
        {
            // Horizontal mode - quantize to 8 diagonal directions
            Vector3 camForwardFlat = new Vector3(camForward.x, 0, camForward.z).normalized;
            Vector3 camRightFlat = new Vector3(playerCamera.transform.right.x, 0, playerCamera.transform.right.z).normalized;

            // W = forward, A = left, S = back, D = right (relative to camera)
            newCardinalDirections[0] = QuantizeTo8Directions(camForwardFlat);
            newCardinalDirections[1] = QuantizeTo8Directions(-camRightFlat);
            newCardinalDirections[2] = QuantizeTo8Directions(-camForwardFlat);
            newCardinalDirections[3] = QuantizeTo8Directions(camRightFlat);
        }
        else
        {
            // Vertical mode - use 3D diagonal quantization
            Vector3 camForwardFull = playerCamera.transform.forward;

            newCardinalDirections[0] = QuantizeTo3DDiagonals(camForwardFull);
            newCardinalDirections[1] = QuantizeTo3DDiagonals(Quaternion.Euler(0, -90, 0) * camForwardFull);
            newCardinalDirections[2] = QuantizeTo3DDiagonals(-camForwardFull);
            newCardinalDirections[3] = QuantizeTo3DDiagonals(Quaternion.Euler(0, 90, 0) * camForwardFull);
        }

        // Calculate target world positions based on cardinal directions
        for (int i = 0; i < 4; i++)
        {
            // Platforms are 1x1x1 cubes, so extent is always 0.5 * scale
            float platformExtent = 0.5f * platformScale;
            Vector3 newTargetWorldPos = blockCenter + newCardinalDirections[i] * (platformExtent + arrowOffset);

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
            // Horizontal mode - quantize camera yaw to nearest 45-degree direction
            Vector3 camForwardFlat = new Vector3(camForward.x, 0, camForward.z).normalized;

            switch (key)
            {
                case KeyCode.W: return QuantizeTo8Directions(camForwardFlat);
                case KeyCode.A: return QuantizeTo8Directions(Quaternion.Euler(0, -90, 0) * camForwardFlat);
                case KeyCode.S: return QuantizeTo8Directions(-camForwardFlat);
                case KeyCode.D: return QuantizeTo8Directions(Quaternion.Euler(0, 90, 0) * camForwardFlat);
                default: return Vector3.zero;
            }
        }
        else
        {
            // Vertical mode - use 3D diagonal quantization
            Vector3 camForwardFull = playerCamera.transform.forward;

            switch (key)
            {
                case KeyCode.W: return QuantizeTo3DDiagonals(camForwardFull);
                case KeyCode.A: return QuantizeTo3DDiagonals(Quaternion.Euler(0, -90, 0) * camForwardFull);
                case KeyCode.S: return QuantizeTo3DDiagonals(-camForwardFull);
                case KeyCode.D: return QuantizeTo3DDiagonals(Quaternion.Euler(0, 90, 0) * camForwardFull);
                default: return Vector3.zero;
            }
        }
    }

    private Vector3 QuantizeTo8Directions(Vector3 direction)
    {
        // Normalize to horizontal plane
        direction.y = 0;
        if (direction == Vector3.zero) return Vector3.forward;

        direction.Normalize();

        // Calculate angle from forward (0° = North)
        float angle = Mathf.Atan2(direction.x, direction.z) * Mathf.Rad2Deg;
        if (angle < 0) angle += 360;

        // Snap to nearest 45-degree angle
        // 0° = N, 45° = NE, 90° = E, 135° = SE, etc.
        int sector = Mathf.RoundToInt(angle / 45f) % 8;

        switch (sector)
        {
            case 0: return Vector3.forward;              // N
            case 1: return (Vector3.forward + Vector3.right).normalized;  // NE
            case 2: return Vector3.right;                // E
            case 3: return (Vector3.back + Vector3.right).normalized;     // SE
            case 4: return Vector3.back;                 // S
            case 5: return (Vector3.back + Vector3.left).normalized;      // SW
            case 6: return Vector3.left;                 // W
            case 7: return (Vector3.forward + Vector3.left).normalized;   // NW
            default: return Vector3.forward;
        }
    }

    private Vector3 QuantizeTo3DDiagonals(Vector3 direction)
    {
        if (direction == Vector3.zero) return Vector3.forward;

        direction.Normalize();

        // Separate into vertical and horizontal components
        float verticalComponent = direction.y;
        Vector3 horizontalDir = new Vector3(direction.x, 0, direction.z);
        float horizontalMagnitude = horizontalDir.magnitude;

        // Quantize horizontal to 8 directions (or zero if negligible)
        Vector3 quantizedHorizontal = Vector3.zero;
        if (horizontalMagnitude > 0.1f)
        {
            horizontalDir.Normalize();
            float angle = Mathf.Atan2(horizontalDir.x, horizontalDir.z) * Mathf.Rad2Deg;
            if (angle < 0) angle += 360;
            int sector = Mathf.RoundToInt(angle / 45f) % 8;

            switch (sector)
            {
                case 0: quantizedHorizontal = Vector3.forward; break;
                case 1: quantizedHorizontal = (Vector3.forward + Vector3.right).normalized; break;
                case 2: quantizedHorizontal = Vector3.right; break;
                case 3: quantizedHorizontal = (Vector3.back + Vector3.right).normalized; break;
                case 4: quantizedHorizontal = Vector3.back; break;
                case 5: quantizedHorizontal = (Vector3.back + Vector3.left).normalized; break;
                case 6: quantizedHorizontal = Vector3.left; break;
                case 7: quantizedHorizontal = (Vector3.forward + Vector3.left).normalized; break;
            }
        }

        // Quantize vertical to -1, 0, or +1
        int verticalQuantized = 0;
        if (Mathf.Abs(verticalComponent) > 0.3f)
        {
            verticalQuantized = verticalComponent > 0 ? 1 : -1;
        }

        // Combine horizontal and vertical
        Vector3 result = quantizedHorizontal + Vector3.up * verticalQuantized;
        return result.normalized;
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
