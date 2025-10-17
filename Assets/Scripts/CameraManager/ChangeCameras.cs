using UnityEngine;
using System.Collections;

// This script swaps between a main camera and a third-person camera when the player
// enters/exits the trigger attached to the same GameObject. It supports Cinemachine
// VirtualCameras by changing their Priority, and falls back to activating/deactivating
// camera GameObjects if Cinemachine is not used.
public class ChangeCameras : MonoBehaviour
{
    [Tooltip("Reference to the main camera GameObject (usually your Cinemachine Brain camera or Main Camera).")]
    public GameObject mainCamera;

    [Tooltip("Reference to the third person camera GameObject or Cinemachine VirtualCamera.")]
    public GameObject thirdPersonCamera;

    [Tooltip("Optional: additional third-person aim camera (e.g., for aiming mode).")]
    public GameObject thirdPersonAimCamera;

    [Tooltip("Tag used to identify the player GameObject that will trigger the camera swap.")]
    public string playerTag = "Player";

    // Priority values used if objects are Cinemachine VirtualCameras (have a CinemachineVirtualCamera component).
    public int mainPriority = 10;
    public int thirdPersonPriority = 20;

    void Reset()
    {
        // Try to auto-assign the main camera
        if (mainCamera == null && Camera.main != null)
            mainCamera = Camera.main.gameObject;
    }

    void Start()
    {
        // basic safety checks
        if (mainCamera == null)
            Debug.LogWarning("ChangeCameras: mainCamera is not assigned.", this);
        if (thirdPersonCamera == null)
            Debug.LogWarning("ChangeCameras: thirdPersonCamera is not assigned.", this);
    }

    void OnTriggerEnter(Collider other)
    {
        if (!IsPlayer(other))
            return;

        SwitchToThirdPerson();
    }

    void OnTriggerExit(Collider other)
    {
        if (!IsPlayer(other))
            return;

        SwitchToMainCamera();
    }

    bool IsPlayer(Collider other)
    {
        return other != null && (other.CompareTag(playerTag) || other.transform.root.CompareTag(playerTag));
    }

    void SwitchToThirdPerson()
    {
        // Try Cinemachine first
        if (TrySetCinemachinePriority(thirdPersonCamera, thirdPersonPriority) | TrySetCinemachinePriority(mainCamera, mainPriority))
        {
            // Cinemachine handled it (we still fall through to GameObject activation in case the cameras are regular cameras)
        }

        // Fallback: enable/disable GameObjects
        if (mainCamera != null)
            mainCamera.SetActive(false);
        if (thirdPersonCamera != null)
            thirdPersonCamera.SetActive(true);
        if (thirdPersonAimCamera != null)
            thirdPersonAimCamera.SetActive(false);
    }

    void SwitchToMainCamera()
    {
        if (TrySetCinemachinePriority(mainCamera, thirdPersonPriority) | TrySetCinemachinePriority(thirdPersonCamera, mainPriority))
        {
            // intentionally left blank
        }

        if (mainCamera != null)
            mainCamera.SetActive(true);
        if (thirdPersonCamera != null)
            thirdPersonCamera.SetActive(false);
        if (thirdPersonAimCamera != null)
            thirdPersonAimCamera.SetActive(false);
    }

    // Attempts to set Cinemachine VirtualCamera priority if the GameObject has one.
    // Returns true if a Cinemachine VirtualCamera component was found and updated.
    bool TrySetCinemachinePriority(GameObject go, int priority)
    {
        if (go == null)
            return false;

        // Use reflection so the project doesn't need a compile-time dependency on Cinemachine
        var type = System.Type.GetType("Cinemachine.CinemachineVirtualCamera, Cinemachine");
        if (type == null)
            return false;

        var vcam = go.GetComponent(type);
        if (vcam == null)
            return false;

        var prop = type.GetProperty("Priority");
        if (prop != null && prop.CanWrite)
        {
            prop.SetValue(vcam, priority, null);
            return true;
        }

        return false;
    }
}
