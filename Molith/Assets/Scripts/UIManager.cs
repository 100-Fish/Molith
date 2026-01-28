using UnityEngine;
using UnityEngine.UI;
using TMPro;
using DG.Tweening;
using System.Collections.Generic;

#if UNITY_EDITOR
using UnityEditor;
#endif

[System.Serializable]
public class KeyIndicator
{
    [Tooltip("The keyboard key to monitor")]
    public KeyCode key;

    [Tooltip("UI Image that represents this key")]
    public Image keyImage;

    [HideInInspector]
    public bool wasPressed;

    [HideInInspector]
    public Tweener scaleTween;

    [HideInInspector]
    public Tweener alphaTween;

    [HideInInspector]
    public bool isAvailable = true;
}

[ExecuteAlways]
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

    [Header("Key Indicator Animation")]
    [Tooltip("Duration of bounce animation when key is pressed")]
    public float keyPressBounceDuration = 0.25f;
    [Tooltip("Duration of bounce animation when key is released")]
    public float keyReleaseBounceDuration = 0.15f;
    [Tooltip("Easing for key press animation")]
    public Ease keyPressEase = Ease.OutBack;
    [Tooltip("Easing for key release animation")]
    public Ease keyReleaseEase = Ease.InBack;
    [Tooltip("Scale when key is pressed")]
    public float keyPressedScale = 1.2f;
    [Tooltip("Scale when key is released")]
    public float keyReleasedScale = 1.0f;

    [Tooltip("Duration of key fade in/out when availability changes")]
    public float keyFadeDuration = 0.2f;

    [Tooltip("Alpha value for unavailable keys (0 = fully hidden)")]
    [Range(0f, 1f)]
    public float unavailableKeyAlpha = 0f;

    [Header("Telemetry UI")]
    [Tooltip("Text field for player telemetry (position and rotation)")]
    public TextMeshProUGUI playerTelemetryText;

    [Tooltip("Text field for camera telemetry (position and rotation)")]
    public TextMeshProUGUI cameraTelemetryText;

    [Tooltip("Text field for max height reached display")]
    public TextMeshProUGUI maxHeightText;

    [Tooltip("Font for text labels (letters) - set as main font on TMP components")]
    public TMP_FontAsset labelFont;

    [Tooltip("Font for numbers and decimal points - added as fallback at runtime")]
    public TMP_FontAsset numberFont;

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

        // Setup dual font system - add numberFont as fallback to labelFont so <font> tags work
        SetupDualFonts();

        // Initialize block counter with BuildingSystem values
        InitializeBlockCounter();

        // Set initial key visibility (e.g. Q starts hidden since no crystals exist yet)
        InitializeKeyVisibility();
    }

    void InitializeKeyVisibility()
    {
        var buildingSystem = GameManager.Instance?.buildingSystem;
        if (buildingSystem == null) return;

        bool hasCrystals = buildingSystem.CurrentBlockCount > 0;

        foreach (var indicator in keyIndicators)
        {
            if (indicator.keyImage == null) continue;

            bool startAvailable = true;

            if (indicator.key == buildingSystem.destroyKey)
                startAvailable = hasCrystals; // Q hidden at start (no crystals)
            else if (indicator.key == buildingSystem.placeKey)
                startAvailable = false; // E hidden until looking at placeable surface

            indicator.isAvailable = startAvailable;
            Color c = indicator.keyImage.color;
            c.a = startAvailable ? 1f : unavailableKeyAlpha;
            indicator.keyImage.color = c;
        }
    }

    void OnValidate()
    {
        // Apply dual fonts in editor when values change
        SetupDualFonts();

#if UNITY_EDITOR
        // Update preview text in editor
        if (!Application.isPlaying)
        {
            ApplyEditorPreviewText();
        }
#endif
    }

    void SetupDualFonts()
    {
        if (labelFont == null || numberFont == null) return;

        // Set the main font on telemetry text components
        if (playerTelemetryText != null)
        {
            playerTelemetryText.font = labelFont;
            playerTelemetryText.richText = true;
        }
        if (cameraTelemetryText != null)
        {
            cameraTelemetryText.font = labelFont;
            cameraTelemetryText.richText = true;
        }
        if (maxHeightText != null)
        {
            maxHeightText.font = labelFont;
            maxHeightText.richText = true;
        }

        // Add numberFont as a fallback to labelFont if not already present
        // This allows the <font="..."> tag to find it at runtime
        if (labelFont.fallbackFontAssetTable == null)
            labelFont.fallbackFontAssetTable = new List<TMP_FontAsset>();

        if (!labelFont.fallbackFontAssetTable.Contains(numberFont))
            labelFont.fallbackFontAssetTable.Add(numberFont);
    }

#if UNITY_EDITOR
    void ApplyEditorPreviewText()
    {
        if (labelFont == null || numberFont == null) return;

        // Sample preview text for editor
        string playerPreview = "POS 0.00, 0.00, 0.00\nROT 0.0, 0.0, 0.0";
        string cameraPreview = "CAM.POS 0.00, 0.00, 0.00\nCAM.ROT 0.0, 0.0, 0.0";

        if (playerTelemetryText != null)
            SetTextWithDualFonts(playerTelemetryText, playerPreview);

        if (cameraTelemetryText != null)
            SetTextWithDualFonts(cameraTelemetryText, cameraPreview);

        // Mark dirty so changes are saved
        if (playerTelemetryText != null)
            EditorUtility.SetDirty(playerTelemetryText);
        if (cameraTelemetryText != null)
            EditorUtility.SetDirty(cameraTelemetryText);
    }
#endif

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
        UpdateMaxHeightDisplay();
        UpdateUIState();
        UpdateKeyIndicators();
        UpdateKeyVisibility();

        if (currentState == UIState.BuildMode)
            UpdateBuildModeCrosshair();
    }

    void UpdateMaxHeightDisplay()
    {
        if (maxHeightText == null) return;

        string displayText;
        if (GameManager.Instance.HasExceededHeightThreshold)
        {
            displayText = $"{GameManager.Instance.MaxHeightReached:F2}";
        }
        else
        {
            displayText = "xx.xx";
        }
        SetTextWithDualFonts(maxHeightText, displayText);
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
            string text = $"{playerPos.x:F2},x{playerPos.y:F2}y{playerPos.z:F2}z\n" +
                          $"{playerRot.x:F1},x{playerRot.y:F1}y{playerRot.z:F1}z";
            SetTextWithDualFonts(playerTelemetryText, text);
        }

        // Camera telemetry (position and rotation in one text)
        if (cameraTelemetryText != null && playerController.playerCamera != null)
        {
            Vector3 camPos = playerController.playerCamera.transform.position;
            Vector3 camRot = playerController.playerCamera.transform.eulerAngles;
            string text = $"{camPos.x:F2}x{camPos.y:F2}y{camPos.z:F2}z\n" +
                          $"{camRot.x:F1}x{camRot.y:F1}y{camRot.z:F1}z";
            SetTextWithDualFonts(cameraTelemetryText, text);
        }

        // Block count display
        var buildingSystem = GameManager.Instance.buildingSystem;
        if (blockCounterUI != null && buildingSystem != null)
        {
            blockCounterUI.UpdateBlockCount(buildingSystem.CurrentBlockCount);
        }
    }

    /// <summary>
    /// Sets text on a TMP component. If dual fonts are configured, labels use labelFont
    /// and numbers use numberFont (via fallback system).
    /// </summary>
    void SetTextWithDualFonts(TextMeshProUGUI textComponent, string text)
    {
        if (textComponent == null) return;

        // Just set plain text - the fallback font system handles number rendering
        // The labelFont should NOT contain number glyphs, so they fall back to numberFont
        textComponent.text = text;
    }

    void UpdateKeyIndicators()
    {
        foreach (var indicator in keyIndicators)
        {
            if (indicator.keyImage == null)
                continue;

            bool isPressed = Input.GetKey(indicator.key);

            if (isPressed != indicator.wasPressed)
            {
                indicator.wasPressed = isPressed;
                indicator.keyImage.sprite = isPressed ? activeSprite : inactiveSprite;
            }
        }
    }

    void UpdateKeyVisibility()
    {
        var buildingSystem = GameManager.Instance?.buildingSystem;
        if (buildingSystem == null) return;

        bool isBuilding = buildingSystem.IsPlacementActive;
        bool isDestroying = buildingSystem.IsDestructionActive;
        bool hasCrystals = buildingSystem.CurrentBlockCount > 0;
        bool lookingAtPlaceable = buildingSystem.IsLookingAtPlaceableSurface();

        foreach (var indicator in keyIndicators)
        {
            if (indicator.keyImage == null) continue;

            bool shouldBeAvailable;

            if (isBuilding)
            {
                // Building mode: only E visible
                shouldBeAvailable = (indicator.key == buildingSystem.placeKey);
            }
            else if (isDestroying)
            {
                // Destroy mode: only Q visible
                shouldBeAvailable = (indicator.key == buildingSystem.destroyKey);
            }
            else
            {
                // Default mode
                if (indicator.key == buildingSystem.destroyKey)
                {
                    // Q only visible if crystals are placed
                    shouldBeAvailable = hasCrystals;
                }
                else if (indicator.key == buildingSystem.placeKey)
                {
                    // E only visible if looking at placeable surface
                    shouldBeAvailable = lookingAtPlaceable;
                }
                else
                {
                    // All other keys visible in default
                    shouldBeAvailable = true;
                }
            }

            // Fade if availability changed
            if (shouldBeAvailable != indicator.isAvailable)
            {
                indicator.isAvailable = shouldBeAvailable;
                float targetAlpha = shouldBeAvailable ? 1f : unavailableKeyAlpha;

                indicator.alphaTween?.Kill();
                indicator.alphaTween = indicator.keyImage
                    .DOFade(targetAlpha, keyFadeDuration)
                    .SetEase(Ease.OutQuad);
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

        // Kill key alpha tweens
        foreach (var indicator in keyIndicators)
            indicator.alphaTween?.Kill();
    }
}
