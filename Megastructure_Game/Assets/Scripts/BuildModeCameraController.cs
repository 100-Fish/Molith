using UnityEngine;
using System.Collections;
using DG.Tweening;
using SUPERCharacter;

public class BuildModeCameraController : MonoBehaviour
{
    [Header("Settings")]
    public float transitionDuration = 0.5f;
    public float buildModeCameraDistance = 5f;
    public Vector3 cameraOffset = new Vector3(0, 2, -3);

    private SUPERCharacterAIO playerController;
    private GameObject targetBlock;
    private Vector3 savedPlayerPosition;
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
        if (playerController == null || block == null) return;

        targetBlock = block;

        // Save current state
        savedPlayerPosition = playerController.transform.position;
        wasControlEnabled = playerController.enableCameraControl;

        // Calculate camera target position
        Vector3 blockPosition = block.transform.position;
        Vector3 targetCameraPosition = blockPosition + cameraOffset;

        // Calculate rotation to look at block
        Vector3 directionToBlock = (blockPosition - targetCameraPosition).normalized;
        float targetPitch = Mathf.Asin(-directionToBlock.y) * Mathf.Rad2Deg;
        float targetYaw = Mathf.Atan2(directionToBlock.x, directionToBlock.z) * Mathf.Rad2Deg;
        Vector3 targetRotation = new Vector3(targetPitch, targetYaw, 0);

        // Disable camera control temporarily
        playerController.enableCameraControl = false;

        // Smoothly rotate camera
        playerController.RotateView(targetRotation, smooth: true);

        // Move player near block for camera orbit
        Vector3 playerTargetPosition = blockPosition + Vector3.down * 1.5f;
        playerController.transform.DOMove(playerTargetPosition, transitionDuration)
            .SetEase(Ease.InOutQuad);
    }

    public void RestoreNormalCamera()
    {
        if (playerController == null) return;

        // Restore camera control
        playerController.enableCameraControl = wasControlEnabled;

        // Move player back to original position
        if (savedPlayerPosition != Vector3.zero)
        {
            playerController.transform.DOMove(savedPlayerPosition, transitionDuration)
                .SetEase(Ease.InOutQuad);
        }

        targetBlock = null;
    }
}
