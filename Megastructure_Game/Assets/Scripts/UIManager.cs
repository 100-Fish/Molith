using UnityEngine;
using UnityEngine.UI;
using TMPro;
using DG.Tweening;
using System.Collections.Generic;

public class UIManager : MonoBehaviour
{
    [Header("Color Transition Settings")]
    [Tooltip("Duration of color transition animation")]
    public float colorTransitionDuration = 0.3f;

    [Header("Telemetry UI")]
    [Tooltip("Text field for player telemetry (position and rotation)")]
    public TextMeshProUGUI playerTelemetryText;

    [Tooltip("Text field for camera telemetry (position and rotation)")]
    public TextMeshProUGUI cameraTelemetryText;

    [Tooltip("Text field for block count display")]
    public TextMeshProUGUI blockCountText;

    [Header("UI Panel")]
    [Tooltip("Root panel transform - all child UI elements will be tinted")]
    public Transform uiPanelRoot;

    private Color currentTargetColor;
    private enum UIState { Default, Placement, Destruction, BuildMode }
    private UIState currentState = UIState.Default;
    private List<Image> cachedImages = new List<Image>();
    private List<TextMeshProUGUI> cachedTexts = new List<TextMeshProUGUI>();

    void Start()
    {
        if (GameManager.Instance == null)
        {
            Debug.LogError("UIManager: GameManager instance is missing!");
            return;
        }

        // Cache all UI elements from panel root
        if (uiPanelRoot != null)
        {
            cachedImages.AddRange(uiPanelRoot.GetComponentsInChildren<Image>(true));
            cachedTexts.AddRange(uiPanelRoot.GetComponentsInChildren<TextMeshProUGUI>(true));
        }

        currentTargetColor = GameManager.Instance.defaultColor;
        ApplyColorImmediate(GameManager.Instance.defaultColor);
    }

    void Update()
    {
        if (GameManager.Instance == null)
            return;

        UpdateTelemetry();
        UpdateUIState();
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
            playerTelemetryText.text = $"POS [{playerPos.x:F2}, {playerPos.y:F2}, {playerPos.z:F2}]\n" +
                                       $"ROT [{playerRot.x:F1}, {playerRot.y:F1}, {playerRot.z:F1}]";
        }

        // Camera telemetry (position and rotation in one text)
        if (cameraTelemetryText != null && playerController.playerCamera != null)
        {
            Vector3 camPos = playerController.playerCamera.transform.position;
            Vector3 camRot = playerController.playerCamera.transform.eulerAngles;
            cameraTelemetryText.text = $"CAM.POS [{camPos.x:F2}, {camPos.y:F2}, {camPos.z:F2}]\n" +
                                       $"CAM.ROT [{camRot.x:F1}, {camRot.y:F1}, {camRot.z:F1}]";
        }

        // Block count display
        var buildingSystem = GameManager.Instance.buildingSystem;
        if (blockCountText != null && buildingSystem != null)
        {
            blockCountText.text = $"BLOCKS: {buildingSystem.CurrentBlockCount}/{buildingSystem.maxBlocks}";
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
            AnimateColorTransition(targetColor);
        }
    }

    void AnimateColorTransition(Color targetColor)
    {
        // Animate all cached images
        foreach (Image img in cachedImages)
        {
            if (img != null)
            {
                img.DOKill();
                img.DOColor(targetColor, colorTransitionDuration).SetEase(Ease.OutQuad);
            }
        }

        // Animate all cached texts
        foreach (TextMeshProUGUI txt in cachedTexts)
        {
            if (txt != null)
            {
                txt.DOKill();
                txt.DOColor(targetColor, colorTransitionDuration).SetEase(Ease.OutQuad);
            }
        }
    }

    void ApplyColorImmediate(Color color)
    {
        // Apply to all cached images
        foreach (Image img in cachedImages)
        {
            if (img != null)
            {
                img.color = color;
            }
        }

        // Apply to all cached texts
        foreach (TextMeshProUGUI txt in cachedTexts)
        {
            if (txt != null)
            {
                txt.color = color;
            }
        }
    }

    void OnDestroy()
    {
        // Kill all DOTween animations on cached UI elements
        foreach (Image img in cachedImages)
        {
            if (img != null)
                img.DOKill();
        }

        foreach (TextMeshProUGUI txt in cachedTexts)
        {
            if (txt != null)
                txt.DOKill();
        }
    }
}
