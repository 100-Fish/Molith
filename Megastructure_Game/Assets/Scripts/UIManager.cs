using UnityEngine;
using UnityEngine.UI;
using TMPro;
using DG.Tweening;
using System.Collections.Generic;

[System.Serializable]
public class KeyIndicator
{
    [Tooltip("The keyboard key to monitor")]
    public KeyCode key;

    [Tooltip("UI Image that represents this key")]
    public Image keyImage;

    [HideInInspector]
    public bool wasPressed;
}

public class UIManager : MonoBehaviour
{
    [Header("Color Transition Settings")]
    [Tooltip("Duration of color transition animation")]
    public float colorTransitionDuration = 0.3f;

    [Header("UI Sprites")]
    [Tooltip("Sprite shown for inactive state (key not pressed, block unavailable)")]
    public Sprite inactiveSprite;

    [Tooltip("Sprite shown for active state (key pressed, block available)")]
    public Sprite activeSprite;

    [Header("Key Indicators")]
    [Tooltip("List of keyboard key UI indicators")]
    public List<KeyIndicator> keyIndicators = new List<KeyIndicator>();

    [Header("Telemetry UI")]
    [Tooltip("Text field for player telemetry (position and rotation)")]
    public TextMeshProUGUI playerTelemetryText;

    [Tooltip("Text field for camera telemetry (position and rotation)")]
    public TextMeshProUGUI cameraTelemetryText;


    [Tooltip("Transform of the block counter parent (must have BlockCounterUI component)")]
    public Transform blockCounterTransform;

    private BlockCounterUI blockCounterUI;

    [Header("Mode Indicator")]
    [Tooltip("Single image that shows mode color (hidden when in default mode)")]
    public Image modeIndicatorImage;

    [Header("Crosshair Settings")]
    [Tooltip("The main crosshair Image element")]
    public Image crosshairImage;

    [Tooltip("The crosshair box Image that frames placed blocks")]
    public Image crosshairBoxImage;

    [Tooltip("Duration for crosshair position/size transitions")]
    public float crosshairTransitionDuration = 0.3f;

    [Tooltip("Minimum size for crosshair box")]
    public Vector2 crosshairBoxMinSize = new Vector2(50f, 50f);

    [Tooltip("Padding around block bounds for crosshair box")]
    public float crosshairBoxPadding = 20f;

    [Tooltip("Minimum margin from canvas edges for crosshair box")]
    public float crosshairBoxEdgeMargin = 50f;

    private Color currentTargetColor;
    private enum UIState { Default, Placement, Destruction, BuildMode }
    private UIState currentState = UIState.Default;

    // Crosshair state tracking (cached at start)
    private Vector2 originalCrosshairPosition;
    private Vector2 originalCrosshairBoxSize;
    private Vector2 originalCrosshairBoxPosition;
    private Tweener crosshairPositionTween;
    private Tweener crosshairBoxSizeTween;
    private Tweener crosshairBoxPositionTween;

    void Start()
    {
        if (GameManager.Instance == null)
        {
            Debug.LogError("UIManager: GameManager instance is missing!");
            return;
        }

        currentTargetColor = GameManager.Instance.defaultColor;

        // Hide mode indicator on start (default mode)
        if (modeIndicatorImage != null)
            modeIndicatorImage.gameObject.SetActive(false);

        // Cache original crosshair positions at start
        if (crosshairImage != null)
            originalCrosshairPosition = crosshairImage.rectTransform.anchoredPosition;

        if (crosshairBoxImage != null)
        {
            originalCrosshairBoxSize = crosshairBoxImage.rectTransform.sizeDelta;
            originalCrosshairBoxPosition = crosshairBoxImage.rectTransform.anchoredPosition;
        }

        // Get BlockCounterUI component and sync sprites
        if (blockCounterTransform != null)
        {
            blockCounterUI = blockCounterTransform.GetComponent<BlockCounterUI>();
        }

        // Initialize block counter with BuildingSystem values
        InitializeBlockCounter();
    }


    void InitializeBlockCounter()
    {
        if (blockCounterUI == null) return;

        // Sync sprites from UIManager (inverted: available=inactive, used=active)
        blockCounterUI.availableSprite = inactiveSprite;
        blockCounterUI.usedSprite = activeSprite;

        // Sync maxBlocks from BuildingSystem
        var buildingSystem = GameManager.Instance?.buildingSystem;
        if (buildingSystem != null)
        {
            blockCounterUI.SetMaxBlocks(buildingSystem.maxBlocks);
        }
    }

    void Update()
    {
        if (GameManager.Instance == null)
            return;

        UpdateTelemetry();
        UpdateUIState();
        UpdateKeyIndicators();

        if (currentState == UIState.BuildMode)
            UpdateBuildModeCrosshair();
    }

    void UpdateTelemetry()
    {
        var playerController = GameManager.Instance.playerController;
        if (playerController == null)
            return;

        // Player telemetry (position and rotation in one text)
        if (playerTelemetryText != null)
        {
            Vector3 playerPos = playerController.transform.position;
            Vector3 playerRot = playerController.transform.eulerAngles;
            playerTelemetryText.text = $"POS {playerPos.x:F2}, {playerPos.y:F2}, {playerPos.z:F2}\n" +
                                       $"ROT {playerRot.x:F1}, {playerRot.y:F1}, {playerRot.z:F1}";
        }

        // Camera telemetry (position and rotation in one text)
        if (cameraTelemetryText != null && playerController.playerCamera != null)
        {
            Vector3 camPos = playerController.playerCamera.transform.position;
            Vector3 camRot = playerController.playerCamera.transform.eulerAngles;
            cameraTelemetryText.text = $"CAM.POS {camPos.x:F2}, {camPos.y:F2}, {camPos.z:F2}\n" +
                                       $"CAM.ROT {camRot.x:F1}, {camRot.y:F1}, {camRot.z:F1}";
        }

        // Block count display
        var buildingSystem = GameManager.Instance.buildingSystem;
        if (blockCounterUI != null && buildingSystem != null)
        {
            blockCounterUI.UpdateBlockCount(buildingSystem.CurrentBlockCount);
        }
    }

    void UpdateKeyIndicators()
    {
        foreach (var indicator in keyIndicators)
        {
            if (indicator.keyImage == null)
                continue;

            bool isPressed = Input.GetKey(indicator.key);

            // Only update sprite if state changed
            if (isPressed != indicator.wasPressed)
            {
                indicator.wasPressed = isPressed;
                indicator.keyImage.sprite = isPressed ? activeSprite : inactiveSprite;
            }
        }
    }

    void RestoreCrosshairState()
    {
        // Kill any active tweens
        crosshairPositionTween?.Kill();
        crosshairBoxSizeTween?.Kill();
        crosshairBoxPositionTween?.Kill();

        // Animate crosshair back to original position
        if (crosshairImage != null)
        {
            crosshairPositionTween = crosshairImage.rectTransform
                .DOAnchorPos(originalCrosshairPosition, crosshairTransitionDuration)
                .SetEase(Ease.OutCubic);
        }

        // Animate box back to original size and position
        if (crosshairBoxImage != null)
        {
            crosshairBoxSizeTween = crosshairBoxImage.rectTransform
                .DOSizeDelta(originalCrosshairBoxSize, crosshairTransitionDuration)
                .SetEase(Ease.OutCubic);

            crosshairBoxPositionTween = crosshairBoxImage.rectTransform
                .DOAnchorPos(originalCrosshairBoxPosition, crosshairTransitionDuration)
                .SetEase(Ease.OutCubic);
        }
    }

    Vector2 CalculateWASDBoxCenter()
    {
        var arrowSystem = GameManager.Instance?.arrowSystem;
        if (arrowSystem == null || arrowSystem.arrowTextElements == null)
            return Vector2.zero;

        Vector2 sum = Vector2.zero;
        int activeCount = 0;

        for (int i = 0; i < arrowSystem.arrowTextElements.Length; i++)
        {
            var arrow = arrowSystem.arrowTextElements[i];
            if (arrow != null && arrow.gameObject.activeInHierarchy)
            {
                sum += (Vector2)arrow.rectTransform.position;
                activeCount++;
            }
        }

        if (activeCount == 0)
            return Vector2.zero;

        return sum / activeCount;
    }

    Vector2 CalculateWeightedBlockBoundsSize()
    {
        var buildingSystem = GameManager.Instance?.buildingSystem;
        var playerController = GameManager.Instance?.playerController;
        var buildModeController = GameManager.Instance?.buildModeController;

        if (buildingSystem == null || playerController?.playerCamera == null)
            return crosshairBoxMinSize;

        List<GameObject> blockList = buildingSystem.GetChainBlockOrder();
        if (blockList == null || blockList.Count == 0)
            return crosshairBoxMinSize;

        Camera cam = playerController.playerCamera;
        float decayFactor = buildModeController?.orbitWeightDecayFactor ?? 0.5f;

        List<Vector2> screenPositions = new List<Vector2>();
        List<float> weights = new List<float>();
        int n = blockList.Count;

        for (int i = 0; i < n; i++)
        {
            GameObject block = blockList[i];
            if (block == null) continue;

            Vector3 viewportPos = cam.WorldToViewportPoint(block.transform.position);
            if (viewportPos.z <= 0) continue;

            screenPositions.Add(new Vector2(
                viewportPos.x * Screen.width,
                viewportPos.y * Screen.height
            ));
            weights.Add(Mathf.Pow(decayFactor, n - 1 - i));
        }

        if (screenPositions.Count == 0)
            return crosshairBoxMinSize;

        // Calculate weighted center
        Vector2 weightedCenter = Vector2.zero;
        float totalWeight = 0f;
        for (int i = 0; i < screenPositions.Count; i++)
        {
            weightedCenter += screenPositions[i] * weights[i];
            totalWeight += weights[i];
        }
        weightedCenter /= totalWeight;

        // Calculate weighted max distance from center
        float weightedMaxDistX = 0f;
        float weightedMaxDistY = 0f;
        for (int i = 0; i < screenPositions.Count; i++)
        {
            float distX = Mathf.Abs(screenPositions[i].x - weightedCenter.x);
            float distY = Mathf.Abs(screenPositions[i].y - weightedCenter.y);
            weightedMaxDistX = Mathf.Max(weightedMaxDistX, distX * weights[i] / totalWeight * screenPositions.Count);
            weightedMaxDistY = Mathf.Max(weightedMaxDistY, distY * weights[i] / totalWeight * screenPositions.Count);
        }

        // Calculate size with padding
        float width = Mathf.Max((weightedMaxDistX * 2f) + crosshairBoxPadding * 2f, crosshairBoxMinSize.x);
        float height = Mathf.Max((weightedMaxDistY * 2f) + crosshairBoxPadding * 2f, crosshairBoxMinSize.y);

        return new Vector2(width, height);
    }

    void UpdateBuildModeCrosshair()
    {
        Vector2 wasdCenter = CalculateWASDBoxCenter();
        if (wasdCenter == Vector2.zero) return;

        // Move crosshair to WASD center
        if (crosshairImage != null)
        {
            RectTransform canvasRect = crosshairImage.canvas?.GetComponent<RectTransform>();
            if (canvasRect != null)
            {
                Vector2 localPoint;
                RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    canvasRect, wasdCenter, null, out localPoint);

                crosshairPositionTween?.Kill();
                crosshairPositionTween = crosshairImage.rectTransform
                    .DOAnchorPos(localPoint, crosshairTransitionDuration)
                    .SetEase(Ease.OutCubic);
            }
        }

        // Scale and position crosshair box
        if (crosshairBoxImage != null)
        {
            Vector2 boxSize = CalculateWeightedBlockBoundsSize();

            // Compensate for the box's localScale (e.g., 0.5 scale means sizeDelta needs to be 2x larger)
            Vector3 boxScale = crosshairBoxImage.rectTransform.localScale;
            if (boxScale.x != 0f) boxSize.x /= boxScale.x;
            if (boxScale.y != 0f) boxSize.y /= boxScale.y;

            RectTransform canvasRect = crosshairBoxImage.canvas?.GetComponent<RectTransform>();
            if (canvasRect != null)
            {
                Vector2 canvasSize = canvasRect.rect.size;

                // Clamp box size so it doesn't exceed canvas minus margins
                float maxWidth = (canvasSize.x - crosshairBoxEdgeMargin * 2f) / Mathf.Abs(boxScale.x);
                float maxHeight = (canvasSize.y - crosshairBoxEdgeMargin * 2f) / Mathf.Abs(boxScale.y);
                boxSize.x = Mathf.Min(boxSize.x, maxWidth);
                boxSize.y = Mathf.Min(boxSize.y, maxHeight);

                // Calculate the visual half-size of the box (accounting for scale)
                float visualHalfWidth = (boxSize.x * Mathf.Abs(boxScale.x)) / 2f;
                float visualHalfHeight = (boxSize.y * Mathf.Abs(boxScale.y)) / 2f;

                // Convert WASD center to local canvas position
                Vector2 localPoint;
                RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    canvasRect, wasdCenter, null, out localPoint);

                // Clamp position so box stays within canvas bounds (with margin)
                float minX = -canvasSize.x / 2f + crosshairBoxEdgeMargin + visualHalfWidth;
                float maxX = canvasSize.x / 2f - crosshairBoxEdgeMargin - visualHalfWidth;
                float minY = -canvasSize.y / 2f + crosshairBoxEdgeMargin + visualHalfHeight;
                float maxY = canvasSize.y / 2f - crosshairBoxEdgeMargin - visualHalfHeight;

                localPoint.x = Mathf.Clamp(localPoint.x, minX, maxX);
                localPoint.y = Mathf.Clamp(localPoint.y, minY, maxY);

                crosshairBoxImage.rectTransform.anchoredPosition = localPoint;
            }

            crosshairBoxSizeTween?.Kill();
            crosshairBoxSizeTween = crosshairBoxImage.rectTransform
                .DOSizeDelta(boxSize, crosshairTransitionDuration)
                .SetEase(Ease.OutCubic);
        }
    }

    void UpdateUIState()
    {
        var buildingSystem = GameManager.Instance.buildingSystem;
        if (buildingSystem == null)
            return;

        UIState newState = UIState.Default;

        // Check for build mode first (highest priority)
        var buildModeController = FindObjectOfType<BuildModeController>();
        if (buildModeController != null && buildModeController.IsInBuildMode)
        {
            newState = UIState.BuildMode;
        }
        // Check if placement key is held
        else if (Input.GetKey(buildingSystem.placeKey))
        {
            newState = UIState.Placement;
        }
        // Check if destruction key is held
        else if (Input.GetKey(buildingSystem.destroyKey))
        {
            newState = UIState.Destruction;
        }

        // If state changed, transition colors
        if (newState != currentState)
        {
            currentState = newState;
            TransitionToState(newState);
        }
    }

    void TransitionToState(UIState state)
    {
        // Restore crosshair when returning to default mode
        if (state == UIState.Default)
        {
            RestoreCrosshairState();
        }

        Color targetColor = GameManager.Instance.defaultColor;

        switch (state)
        {
            case UIState.BuildMode:
                targetColor = GameManager.Instance.buildModeColor;
                targetColor.a = 1f; // Full opacity for UI
                break;
            case UIState.Placement:
                targetColor = GameManager.Instance.placementColor;
                targetColor.a = 1f; // Full opacity for UI
                break;
            case UIState.Destruction:
                targetColor = GameManager.Instance.destructionColor;
                targetColor.a = 1f; // Full opacity for UI
                break;
            case UIState.Default:
                targetColor = GameManager.Instance.defaultColor;
                break;
        }

        if (targetColor != currentTargetColor)
        {
            currentTargetColor = targetColor;
            UpdateModeIndicator(targetColor);
        }
    }

    void UpdateModeIndicator(Color targetColor)
    {
        if (modeIndicatorImage == null) return;

        // Show/hide based on whether we're in default mode (white color)
        bool isDefaultMode = (targetColor == GameManager.Instance.defaultColor);

        if (isDefaultMode)
        {
            // Fade out and disable
            modeIndicatorImage.DOKill();
            modeIndicatorImage.DOFade(0f, colorTransitionDuration)
                .SetEase(Ease.OutQuad)
                .OnComplete(() => modeIndicatorImage.gameObject.SetActive(false));
        }
        else
        {
            // Enable and fade in with color
            modeIndicatorImage.gameObject.SetActive(true);
            modeIndicatorImage.DOKill();
            targetColor.a = 1f;
            modeIndicatorImage.DOColor(targetColor, colorTransitionDuration).SetEase(Ease.OutQuad);
        }
    }

    void OnDestroy()
    {
        // Kill mode indicator tween
        if (modeIndicatorImage != null)
            modeIndicatorImage.DOKill();

        // Kill crosshair tweens
        crosshairPositionTween?.Kill();
        crosshairBoxSizeTween?.Kill();
        crosshairBoxPositionTween?.Kill();
    }
}
