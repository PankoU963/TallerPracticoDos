using UnityEngine;
#if CINEMACHINE_INSTALLED
using Cinemachine;
#endif

public class CameraController : MonoBehaviour
{
    public enum CameraMode { TopDown, FirstPerson }

    [Header("TopDown")]
    public Transform target; // usually the player
    public Vector3 topDownOffset = new Vector3(0f, 12f, -10f);
    public float topDownSmooth = 8f;

    [Header("Cinemachine (optional)")]
#if CINEMACHINE_INSTALLED
    [Tooltip("Assign the Virtual Camera used for TopDown (optional). If assigned, Cinemachine will control the camera and this script will change its Priority.")]
    public CinemachineVirtualCamera cinemachineTopDown;
    [Tooltip("Assign the Virtual Camera used for FirstPerson (optional). If assigned, Cinemachine will control the camera and this script will change its Priority.")]
    public CinemachineVirtualCamera cinemachineFirstPerson;
#else
    [Tooltip("If Cinemachine isn't available at compile time, assign the VCam GameObjects here (legacy).")]
    public GameObject cinemachineTopDown;
    public GameObject cinemachineFirstPerson;
#endif

    [Header("FirstPerson")]
    public Transform fpCameraAnchor; // a child transform at eye height
    public float mouseSensitivityX = 2.0f;
    public float mouseSensitivityY = 2.0f;
    public float lookSmooth = 8f;

    CameraMode mode = CameraMode.TopDown;
    Vector2 lookAngles = Vector2.zero; // x -> yaw, y -> pitch
    // CameraController is now a lightweight manager: it activates/deactivates VCams and provides non-Cinemachine fallback
    public float CurrentYaw => lookAngles.x;

    void Start()
    {
        // lock cursor in first person when switching; default none.
    }

    void LateUpdate()
    {
        // If Cinemachine virtual cameras are assigned we don't manually move/rotate the Unity camera here.
        if (cinemachineTopDown != null || cinemachineFirstPerson != null)
        {
            // Cinemachine will control the camera movement. Nothing to do every frame here.
            return;
        }

        if (mode == CameraMode.TopDown)
        {
            if (target == null) return;
            Vector3 desired = target.position + topDownOffset;
            transform.position = Vector3.Lerp(transform.position, desired, Time.deltaTime * topDownSmooth);
            transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.Euler(45f, 45f, 0f), Time.deltaTime * topDownSmooth);
        }
        else // FirstPerson
        {
            if (fpCameraAnchor == null) return;
            transform.position = Vector3.Lerp(transform.position, fpCameraAnchor.position, Time.deltaTime * lookSmooth);
            // rotation handled by input from PlayerInput/PlayerController
            Quaternion desiredRot = Quaternion.Euler(lookAngles.y, lookAngles.x, 0f);
            transform.rotation = Quaternion.Slerp(transform.rotation, desiredRot, Time.deltaTime * lookSmooth);
        }
    }

    /// <summary>
    /// Called externally to set camera mode
    /// </summary>
    public void SetMode(CameraMode newMode)
    {
        mode = newMode;
        if (mode == CameraMode.FirstPerson)
        {
            // In FirstPerson we lock and hide the cursor
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }
        else
        {
            // In TopDown we keep the cursor unlocked but hidden per user request
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = false;
        }

        // If Cinemachine is available at runtime, prefer adjusting Priority so the Brain blends smoothly.
#if CINEMACHINE_INSTALLED
        if (cinemachineTopDown != null || cinemachineFirstPerson != null)
        {
            // Initialize FP vcam via bridge before raising priority to avoid jumps
            if (mode == CameraMode.FirstPerson && cinemachineFirstPerson != null)
            {
                var bridge = cinemachineFirstPerson.GetComponent<CinemachineInputBridge>();
                if (bridge != null && Camera.main != null) bridge.InitializeFromCamera(Camera.main.transform);
            }

            if (cinemachineTopDown != null) cinemachineTopDown.Priority = (mode == CameraMode.TopDown ? 100 : 0);
            if (cinemachineFirstPerson != null) cinemachineFirstPerson.Priority = (mode == CameraMode.FirstPerson ? 100 : 0);
        }
#else
        // Fallback when Cinemachine isn't present at compile-time: toggle GameObject active state.
        if (cinemachineTopDown != null || cinemachineFirstPerson != null)
        {
            if (cinemachineTopDown != null) cinemachineTopDown.SetActive(mode == CameraMode.TopDown);
            if (cinemachineFirstPerson != null)
            {
                if (mode == CameraMode.FirstPerson)
                {
                    // Try to initialize the FP vcam to the current camera pose to avoid jumps
                    var bridgeComp = cinemachineFirstPerson.GetComponent("CinemachineInputBridge");
                    Transform camT = Camera.main != null ? Camera.main.transform : null;
                    if (bridgeComp != null && camT != null)
                    {
                        var initMethod = bridgeComp.GetType().GetMethod("InitializeFromCamera");
                        if (initMethod != null) initMethod.Invoke(bridgeComp, new object[] { camT });
                    }
                }
                cinemachineFirstPerson.SetActive(mode == CameraMode.FirstPerson);
            }
        }
#endif
    }
    // Removed POV reflection helpers; use dedicated CinemachineInputBridge component for POV control.
}
