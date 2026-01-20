using UnityEngine;
using SUPERCharacter;

public class BuildModeCameraController : MonoBehaviour
{
    [Header("Settings")]
    public float transitionDuration = 0.5f;
    public float buildModeCameraDistance = 5f;
    public Vector3 cameraOffset = new Vector3(0, 2, -3);

    private SUPERCharacterAIO playerController;
    private GameObject targetBlock;
    private Vector3 savedCameraPosition;
    private Quaternion savedCameraRotation;
    private bool wasControlEnabled;

    void Start()
    {
        if (GameManager.Instance != null)
        {
            playerController = GameManager.Instance.playerController;
        }
    }

    public void FocusOnBlock(GameObject block)
    {
        if (GameManager.Instance == null || GameManager.Instance.cameraManager == null) return;
        if (playerController == null || block == null) return;

        targetBlock = block;

        // Save current camera state
        savedCameraPosition = GameManager.Instance.cameraManager.GetPosition();
        savedCameraRotation = GameManager.Instance.cameraManager.GetRotation();
        wasControlEnabled = playerController.enableCameraControl;

        // Calculate camera target position
        Vector3 blockPosition = block.transform.position;
        Vector3 targetCameraPosition = blockPosition + cameraOffset;

        // Calculate rotation to look at block
        Vector3 directionToBlock = (blockPosition - targetCameraPosition).normalized;
        Quaternion targetRotation = Quaternion.LookRotation(directionToBlock);

        // Disable camera control temporarily
        playerController.enableCameraControl = false;

        // Use CameraManager to move and focus on block
        GameManager.Instance.cameraManager.FocusOn(blockPosition, cameraOffset, transitionDuration);
    }

    public void RestoreNormalCamera()
    {
        if (GameManager.Instance == null || GameManager.Instance.cameraManager == null) return;
        if (playerController == null) return;

        // Restore camera control
        playerController.enableCameraControl = wasControlEnabled;

        // Move camera back to original position
        if (savedCameraPosition != Vector3.zero)
        {
            GameManager.Instance.cameraManager.SetCameraTransform(savedCameraPosition, savedCameraRotation);
        }

        targetBlock = null;
    }
}
