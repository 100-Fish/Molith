using UnityEngine;
using DG.Tweening;

public class CameraManager : MonoBehaviour
{
    [Header("Camera Settings")]
    public Camera mainCamera;
    public float defaultFOV = 60f;

    [Header("Movement Settings")]
    public float movementSpeed = 5f;
    public float rotationSpeed = 3f;

    private Vector3 targetPosition;
    private Quaternion targetRotation;
    private bool isLerping = false;

    // Reference to check if build/destruction mode is active
    private SUPERCharacter.SUPERCharacterAIO playerController;

    void Start()
    {
        // Find main camera if not assigned
        if (mainCamera == null)
        {
            mainCamera = Camera.main;
            if (mainCamera == null)
            {
                Debug.LogError("CameraManager: Main camera not found!");
            }
        }

        // Initialize target values
        if (mainCamera != null)
        {
            targetPosition = mainCamera.transform.position;
            targetRotation = mainCamera.transform.rotation;
        }

        // Get player controller reference
        if (GameManager.Instance != null)
        {
            playerController = GameManager.Instance.playerController;
        }
    }

    void LateUpdate()
    {
        if (mainCamera == null) return;

        // IMPORTANT: Don't interfere with camera when SUPERCharacterAIO is in build mode override
        // SUPERCharacterAIO handles all camera positioning during build/destruction mode
        if (playerController != null && playerController.buildModeOverride)
        {
            isLerping = false;
            return;
        }

        // Smooth interpolation if lerping
        if (isLerping)
        {
            mainCamera.transform.position = Vector3.Lerp(mainCamera.transform.position, targetPosition, Time.deltaTime * movementSpeed);
            mainCamera.transform.rotation = Quaternion.Slerp(mainCamera.transform.rotation, targetRotation, Time.deltaTime * rotationSpeed);

            // Stop lerping when close enough
            if (Vector3.Distance(mainCamera.transform.position, targetPosition) < 0.01f &&
                Quaternion.Angle(mainCamera.transform.rotation, targetRotation) < 0.1f)
            {
                isLerping = false;
            }
        }
    }

    /// <summary>
    /// Returns true if camera control should be blocked (build mode active)
    /// </summary>
    private bool IsBuildModeActive()
    {
        return playerController != null && playerController.buildModeOverride;
    }

    /// <summary>
    /// Move camera to a position with smooth interpolation
    /// </summary>
    public void MoveTo(Vector3 position, float duration = 0.5f)
    {
        if (mainCamera == null || IsBuildModeActive()) return;

        mainCamera.transform.DOMove(position, duration).SetEase(Ease.InOutQuad);
    }

    /// <summary>
    /// Rotate camera to look at a target position
    /// </summary>
    public void LookAt(Vector3 targetPos, float duration = 0.5f)
    {
        if (mainCamera == null || IsBuildModeActive()) return;

        Vector3 direction = (targetPos - mainCamera.transform.position).normalized;
        Quaternion lookRotation = Quaternion.LookRotation(direction);
        mainCamera.transform.DORotateQuaternion(lookRotation, duration).SetEase(Ease.InOutQuad);
    }

    /// <summary>
    /// Move and rotate camera to focus on a target
    /// </summary>
    public void FocusOn(Vector3 targetPos, Vector3 cameraOffset, float duration = 0.5f)
    {
        if (mainCamera == null || IsBuildModeActive()) return;

        Vector3 finalPosition = targetPos + cameraOffset;
        mainCamera.transform.DOMove(finalPosition, duration).SetEase(Ease.InOutQuad);

        Vector3 direction = (targetPos - finalPosition).normalized;
        Quaternion lookRotation = Quaternion.LookRotation(direction);
        mainCamera.transform.DORotateQuaternion(lookRotation, duration).SetEase(Ease.InOutQuad);
    }

    /// <summary>
    /// Set camera position and rotation directly (no animation)
    /// </summary>
    public void SetCameraTransform(Vector3 position, Quaternion rotation)
    {
        if (mainCamera == null || IsBuildModeActive()) return;

        mainCamera.transform.position = position;
        mainCamera.transform.rotation = rotation;
        targetPosition = position;
        targetRotation = rotation;
    }

    /// <summary>
    /// Enable/disable smooth lerping
    /// </summary>
    public void SetLerping(bool enabled)
    {
        isLerping = enabled;
        if (enabled && mainCamera != null)
        {
            targetPosition = mainCamera.transform.position;
            targetRotation = mainCamera.transform.rotation;
        }
    }

    /// <summary>
    /// Set target position for lerping
    /// </summary>
    public void SetTargetPosition(Vector3 position)
    {
        targetPosition = position;
        isLerping = true;
    }

    /// <summary>
    /// Set target rotation for lerping
    /// </summary>
    public void SetTargetRotation(Quaternion rotation)
    {
        targetRotation = rotation;
        isLerping = true;
    }

    /// <summary>
    /// Get current camera position
    /// </summary>
    public Vector3 GetPosition()
    {
        return mainCamera != null ? mainCamera.transform.position : Vector3.zero;
    }

    /// <summary>
    /// Get current camera rotation
    /// </summary>
    public Quaternion GetRotation()
    {
        return mainCamera != null ? mainCamera.transform.rotation : Quaternion.identity;
    }

    /// <summary>
    /// Get camera forward direction
    /// </summary>
    public Vector3 GetForward()
    {
        return mainCamera != null ? mainCamera.transform.forward : Vector3.forward;
    }
}
