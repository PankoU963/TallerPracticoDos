using System.Collections;
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
    // Event invoked when the camera finished transitioning/blending to a mode
    public System.Action<CameraMode> OnModeBlendComplete;
    Coroutine modeBlendCoroutine = null;
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
            // When switching to TopDown, try to position/rotate the TopDown vcam so it matches the player's direction
            if (mode == CameraMode.TopDown && cinemachineTopDown != null && target != null)
            {
                // Position the vcam relative to the player so "up" in the view corresponds to player's forward
                Vector3 desiredPos = target.position + target.rotation * topDownOffset;
                Quaternion desiredRot = Quaternion.LookRotation(target.position - desiredPos);
                try { cinemachineTopDown.transform.position = desiredPos; cinemachineTopDown.transform.rotation = desiredRot; } catch { }
            }

            // Initialize FP vcam via bridge before raising priority to avoid jumps
            if (mode == CameraMode.FirstPerson && cinemachineFirstPerson != null)
            {
                var bridge = cinemachineFirstPerson.GetComponent<CinemachineInputBridge>();
                // Prefer initializing from the player FP anchor so the vcam starts aligned with the player
                Transform initSource = fpCameraAnchor != null ? fpCameraAnchor : (Camera.main != null ? Camera.main.transform : null);
                if (bridge != null && initSource != null) bridge.InitializeFromCamera(initSource);
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
                    // Try to initialize the FP vcam to the player's FP anchor (if assigned) to avoid jumps
                    var bridgeComp = cinemachineFirstPerson.GetComponent("CinemachineInputBridge");
                    Transform camT = fpCameraAnchor != null ? fpCameraAnchor : (Camera.main != null ? Camera.main.transform : null);
                    if (bridgeComp != null && camT != null)
                    {
                        var initMethod = bridgeComp.GetType().GetMethod("InitializeFromCamera");
                        if (initMethod != null) initMethod.Invoke(bridgeComp, new object[] { camT });
                    }

                    // Also try to snap the FP vcam GameObject transform to the fpCameraAnchor to reduce visual jumps when Cinemachine isn't installed
                    if (fpCameraAnchor != null)
                    {
                        try { cinemachineFirstPerson.transform.position = fpCameraAnchor.position; cinemachineFirstPerson.transform.rotation = fpCameraAnchor.rotation; } catch { }
                    }
                }
                // When switching to TopDown, position the top-down GameObject to align to the player's facing
                if (mode == CameraMode.TopDown && cinemachineTopDown != null && target != null)
                {
                    Vector3 desiredPos = target.position + target.rotation * topDownOffset;
                    Quaternion desiredRot = Quaternion.LookRotation(target.position - desiredPos);
                    try { cinemachineTopDown.transform.position = desiredPos; cinemachineTopDown.transform.rotation = desiredRot; } catch { }
                }

                cinemachineFirstPerson.SetActive(mode == CameraMode.FirstPerson);
            }
        }
#endif
        // Start monitoring the camera blend/transition and invoke completion when done
        if (modeBlendCoroutine != null) StopCoroutine(modeBlendCoroutine);
        modeBlendCoroutine = StartCoroutine(ModeBlendMonitor(mode));
    }

    IEnumerator ModeBlendMonitor(CameraMode targetMode)
    {
        float start = Time.time;
        float timeout = 2.0f; // fallback timeout in seconds

#if CINEMACHINE_INSTALLED
        // If Cinemachine is present, use the Brain to detect when blending has finished
        CinemachineBrain brain = null;
        if (Camera.main != null) brain = Camera.main.GetComponent<CinemachineBrain>();
        if (brain != null)
        {
            // Wait until the active blend is finished
            while (brain.ActiveBlend != null && Time.time - start < timeout)
                yield return null;
            // small extra frame to ensure final camera state applied
            yield return null;
            OnModeBlendComplete?.Invoke(targetMode);
            modeBlendCoroutine = null;
            yield break;
        }
#endif

        // Fallback: wait until the manual camera transform reaches close to the target transform
        float positionThreshold = 0.05f;
        float angleThreshold = 2f; // degrees
        while (Time.time - start < timeout)
        {
            if (targetMode == CameraMode.TopDown)
            {
                // compute desired position/rotation
                if (this.target == null) break;
                Vector3 desiredPos = this.target.position + topDownOffset;
                float posDist = Vector3.Distance(transform.position, desiredPos);
                Quaternion desiredRot = Quaternion.Euler(45f, 45f, 0f);
                float angleDist = Quaternion.Angle(transform.rotation, desiredRot);
                if (posDist <= positionThreshold && angleDist <= angleThreshold) break;
            }
            else // FirstPerson
            {
                if (fpCameraAnchor == null) break;
                float posDist = Vector3.Distance(transform.position, fpCameraAnchor.position);
                float angleDist = Quaternion.Angle(transform.rotation, Quaternion.Euler(lookAngles.y, lookAngles.x, 0f));
                if (posDist <= positionThreshold && angleDist <= angleThreshold) break;
            }
            yield return null;
        }

        // Invoke even on timeout
        OnModeBlendComplete?.Invoke(targetMode);
        modeBlendCoroutine = null;
    }
    // Removed POV reflection helpers; use dedicated CinemachineInputBridge component for POV control.
}
