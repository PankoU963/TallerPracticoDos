using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(CharacterController))]
public class PlayerController : MonoBehaviour
{
    public enum ControlMode { TopDown, FirstPerson }

    [Header("Movement")]
    public float walkSpeed = 3f;
    public float runMultiplier = 1.8f;
    public float gravity = -9.81f;

    [Header("References")]
    public CameraController cameraController;
    [Header("TopDown Options")]
    public bool rotateInTopDown = true; // if true, player will rotate toward movement; otherwise keep facing original rotation
    [Header("Mode transition")]
    [Tooltip("Time in seconds to blend the player's movement direction when switching control modes.")]
    public float controlBlendTime = 0.2f;
    float blendTimer = 0f;
    Vector3 blendStartDir = Vector3.zero;
    Vector3 blendTargetDir = Vector3.zero;
    [Header("Zone settings")]
    [Tooltip("Cooldown after exiting a zone before the same zone can trigger again.")]
    public float zoneReentryCooldown = 0.6f;
    [Tooltip("Minimum distance from last exit position before a zone can trigger again.")]
    public float zoneMinExitDistance = 1.0f;
    [Tooltip("Seconds to pause movement when entering a zone.")]
    public float zonePauseOnEnter = 2f;
    [Tooltip("Seconds to pause movement when exiting a zone.")]
    public float zonePauseOnExit = 2f;

    // zone state (per-player)
    float lastZoneExitTime = -999f;
    Vector3 lastZoneExitPos = Vector3.positiveInfinity;
    Coroutine exitMonitorCoroutine = null;

    CharacterController cc;
    Vector3 velocity;
    bool isPaused = false;
    Coroutine pauseCoroutine = null;

    // Input System
    PlayerMovement inputActions;

    public ControlMode controlMode = ControlMode.TopDown;

    [Header("UI / Cursor")]
    [Tooltip("If true, hide and lock the cursor while in TopDown mode.")]
    public bool hideCursorInTopDown = true;

    void Awake()
    {
        cc = GetComponent<CharacterController>();
        if (cc == null) cc = gameObject.AddComponent<CharacterController>();

        inputActions = new PlayerMovement();
    }

    void Start()
    {
        // ensure cursor state matches starting mode
        UpdateCursorState();
    }

    void OnEnable()
    {
        inputActions.Enable();
    }

    void OnDisable()
    {
        inputActions.Disable();
        inputActions.Dispose();
    }

    void Update()
    {
    Vector2 moveInput = inputActions.HorizontalMovement.Movement.ReadValue<Vector2>();

        // If paused, zero out movement input
        if (isPaused)
        {
            moveInput = Vector2.zero;
        }

        // movement vector in world space
        Vector3 move = Vector3.zero;

        if (controlMode == ControlMode.TopDown)
        {
            // TopDown movement relative to camera: W = camera forward, A = camera left, etc.
            Transform camT = null;
            if (Camera.main != null) camT = Camera.main.transform;
            else if (cameraController != null && cameraController.cinemachineTopDown != null)
                camT = cameraController.cinemachineTopDown.transform;

            if (camT != null)
            {
                Vector3 camForward = camT.forward;
                Vector3 camRight = camT.right;
                camForward.y = 0f; camRight.y = 0f;
                camForward.Normalize(); camRight.Normalize();
                move = camRight * moveInput.x + camForward * moveInput.y;
            }
            else
            {
                // fallback to world axes if no camera available
                move = new Vector3(moveInput.x, 0f, moveInput.y);
            }
        }
        else // FirstPerson
        {
            // Movement relative to the active camera's forward (so forward is where the camera looks)
            Transform camT = null;
            if (Camera.main != null) camT = Camera.main.transform;
            else if (cameraController != null && cameraController.cinemachineFirstPerson != null)
            {
                // try to use the vcam's transform (the vcam GameObject often holds orientation)
                camT = cameraController.cinemachineFirstPerson.transform;
            }

            if (camT != null)
            {
                Vector3 camForward = camT.forward;
                Vector3 camRight = camT.right;
                camForward.y = 0; camRight.y = 0;
                camForward.Normalize(); camRight.Normalize();
                move = camRight * moveInput.x + camForward * moveInput.y;
            }
            else
            {
                // fallback to player forward
                Vector3 forward = transform.forward;
                Vector3 right = transform.right;
                forward.y = 0; right.y = 0;
                forward.Normalize(); right.Normalize();
                move = right * moveInput.x + forward * moveInput.y;
            }
        }

        float speed = walkSpeed;
        // If you had a Run action, you could multiply here. For now keep walkSpeed.

        // If we're blending due to a mode switch, interpolate the movement vector horizontally
        if (blendTimer > 0f)
        {
            float t = 1f - (blendTimer / controlBlendTime);
            Vector3 blended = Vector3.Slerp(blendStartDir, blendTargetDir, t);
            // keep magnitude of intended move
            move = blended * move.magnitude;
            blendTimer -= Time.deltaTime;
            if (blendTimer <= 0f) blendTimer = 0f;
        }

        Vector3 horizontalVelocity = move * speed;

        // Apply gravity
        if (cc.isGrounded && velocity.y < 0)
            velocity.y = -1f; // small negative to keep grounded

        velocity.x = horizontalVelocity.x;
        velocity.z = horizontalVelocity.z;
        velocity.y += gravity * Time.deltaTime;

        cc.Move(velocity * Time.deltaTime);

        // Rotate player to movement direction in TopDown when there is input
        if (controlMode == ControlMode.TopDown && rotateInTopDown)
        {
            Vector3 inputDir = new Vector3(move.x, 0f, move.z);
            if (inputDir.sqrMagnitude > 0.0001f)
            {
                transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(inputDir.normalized), 10f * Time.deltaTime);
            }
        }

        // Mouse look (only in first person)
        if (controlMode == ControlMode.FirstPerson)
        {
            // If there's a CinemachineInputBridge on the FP vcam, try to read its yaw and sync the player yaw to it
            float targetYaw = float.NaN;
            if (cameraController != null && cameraController.cinemachineFirstPerson != null)
            {
                var bridge = cameraController.cinemachineFirstPerson.GetComponent("CinemachineInputBridge");
                if (bridge != null)
                {
                    var prop = bridge.GetType().GetProperty("CurrentYaw");
                    if (prop != null)
                    {
                        var val = prop.GetValue(bridge);
                        if (val is float f) targetYaw = f;
                    }
                }
            }

            if (!float.IsNaN(targetYaw))
            {
                Vector3 euler = transform.rotation.eulerAngles;
                float newYaw = Mathf.LerpAngle(euler.y, targetYaw, 10f * Time.deltaTime);
                transform.rotation = Quaternion.Euler(0f, newYaw, 0f);
            }
        }
    }

    /// <summary>
    /// Pause horizontal movement for a number of seconds. Gravity still applies so the
    /// player will remain grounded normally. Multiple calls will reset the timer.
    /// </summary>
    public void PauseMovement(float seconds)
    {
        if (pauseCoroutine != null) StopCoroutine(pauseCoroutine);
        pauseCoroutine = StartCoroutine(PauseCoroutine(seconds));
    }

    IEnumerator PauseCoroutine(float seconds)
    {
        isPaused = true;
        yield return new WaitForSeconds(seconds);
        isPaused = false;
        pauseCoroutine = null;
    }

    /// <summary>
    /// Switch movement mode (and inform camera controller)
    /// </summary>
    public void SetMode(ControlMode mode)
    {
        controlMode = mode;
        if (cameraController != null)
            cameraController.SetMode(mode == ControlMode.TopDown ? CameraController.CameraMode.TopDown : CameraController.CameraMode.FirstPerson);

        // If switching to FirstPerson, snap player yaw to the active camera's yaw so movement forward matches view immediately
        // Start a small blend so control direction doesn't jump abruptly
        StartControlBlend();

        if (mode == ControlMode.FirstPerson)
        {
            // Do not force-snap the player's yaw to the camera. The CameraController will initialize the FP camera
            // from the player's FP anchor (or vice versa). We keep the control blend so movement direction transitions smoothly.
        }

        // update cursor according to new mode
        UpdateCursorState();
    }

    void UpdateCursorState()
    {
        // When the user enabled cursor hiding option, hide cursor in both TopDown and FirstPerson
        if (hideCursorInTopDown)
        {
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }
        else
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }
    }

    /// <summary>
    /// Called by a ModeZone when the player enters it. Returns true if the mode change was accepted.
    /// This method enforces per-player cooldown and minimum exit distance logic.
    /// </summary>
    public bool TrySetModeFromZone(ControlMode mode)
    {
        // cooldown check
        if (Time.time < lastZoneExitTime + zoneReentryCooldown) return false;

        // min exit distance check
        if (lastZoneExitPos != Vector3.positiveInfinity)
        {
            float dist = Vector3.Distance(lastZoneExitPos, transform.position);
            if (dist < zoneMinExitDistance) return false;
        }

        // Accept mode change
        SetMode(mode);
        if (zonePauseOnEnter > 0f) PauseMovement(zonePauseOnEnter);
        return true;
    }

    /// <summary>
    /// Called by a ModeZone when the player exits it.
    /// Records exit pos/time and optionally pauses movement. The player will be rotated to look outward
    /// from the zone center so that when returning to TopDown the model faces away from the zone.
    /// </summary>
    public void NotifyZoneExit(Vector3 zoneCenter)
    {
        SetMode(ControlMode.TopDown);

        // Rotate player to face outward from the zone center
        Vector3 dirOut = transform.position - zoneCenter;
        dirOut.y = 0f;
        if (dirOut.sqrMagnitude > 0.0001f)
        {
            transform.rotation = Quaternion.LookRotation(dirOut.normalized);
        }

        if (zonePauseOnExit > 0f) PauseMovement(zonePauseOnExit);
        // Record exit position; start monitoring until the player moves away far enough
        lastZoneExitPos = transform.position;
        // Reset the cooldown marker until the player has moved away
        lastZoneExitTime = -999f;
        if (exitMonitorCoroutine != null) StopCoroutine(exitMonitorCoroutine);
        exitMonitorCoroutine = StartCoroutine(ExitDistanceMonitor());
    }

    IEnumerator ExitDistanceMonitor()
    {
        float start = Time.time;
        float maxWait = 10f; // safety timeout: after this we'll set the cooldown anyway
        while (Time.time - start < maxWait)
        {
            float dist = Vector3.Distance(lastZoneExitPos, transform.position);
            if (dist >= zoneMinExitDistance)
            {
                lastZoneExitTime = Time.time;
                exitMonitorCoroutine = null;
                yield break;
            }
            yield return null;
        }
        // timeout reached
        lastZoneExitTime = Time.time;
        exitMonitorCoroutine = null;
    }

    void StartControlBlend()
    {
        // capture current horizontal forward and target forward immediately after mode switch
        Vector3 currentDir = transform.forward;
        currentDir.y = 0f; currentDir.Normalize();
        blendStartDir = currentDir;

        // target direction tries to align with camera forward (so pushing forward moves where camera looks)
        Transform camT = null;
        if (Camera.main != null) camT = Camera.main.transform;
        else if (cameraController != null && cameraController.cinemachineFirstPerson != null)
            camT = cameraController.cinemachineFirstPerson.transform;

        if (camT != null)
        {
            Vector3 target = camT.forward; target.y = 0f; target.Normalize();
            blendTargetDir = target;
        }
        else
        {
            blendTargetDir = blendStartDir;
        }

        blendTimer = controlBlendTime;
    }
}
