using System;
using System.Collections;
using UnityEngine;
using UnityEngine.Events;

public class TelePorter : MonoBehaviour
{
    [Header("Target")]
    [Tooltip("Direct target transform to teleport to. If left null and LinkedTeleporter is set, that will be used.")]
    public Transform Target;

    [Tooltip("Optional linked teleporter. If set, this teleporter will send to the linked teleporter's transform (useful for pairs).")]
    public TelePorter LinkedTeleporter;

    [Header("Filtering & Cooldown")]
    [Tooltip("If set, only objects with one of these tags will be allowed to teleport. Leave empty to allow any tag.")]
    public string[] AllowTags = new string[0];

    [Tooltip("Seconds before the same object can be teleported again (prevents immediate bounce-back).")]
    public float Cooldown = 0.5f;

    [Tooltip("Optional offset applied to destination position after teleporting (local space of destination transform).")]
    public Vector3 DestinationOffset = Vector3.zero;

    [Header("Behavior")]
    [Tooltip("If true, object rotation will be matched to the destination's rotation. If false, object's rotation remains unchanged.")]
    public bool MatchRotation = true;

    [Tooltip("Optional event invoked after a successful teleport. Passes the teleported GameObject.")]
    public UnityEvent<GameObject> OnTeleported;

    [Header("Effects")]
    [Tooltip("Delay in seconds before the actual teleport occurs. While waiting, the object's movement can be locked to prevent it from leaving.")]
    public float TeleportDelay = 0f;

    [Tooltip("If true, temporarily lock CharacterController or Rigidbody movement during TeleportDelay so the object cannot exit before teleport.")]
    public bool LockMovementDuringDelay = true;

    [Tooltip("Optional particle systems on this teleporter to accelerate while the player is about to be teleported.")]
    public ParticleSystem[] TeleportParticles;

    [Tooltip("If true, also accelerate particle systems on the linked teleporter (if any).")]
    public bool AccelerateLinkedTeleporter = true;

    [Tooltip("Multiplier applied to particle system simulation speed during the delay (e.g., 2 = twice as fast).")]
    public float ParticleSpeedMultiplier = 2f;

    [Header("Mission Activation")]
    [Tooltip("If false, this teleporter will ignore teleport attempts until re-enabled by mission completion.")]
    public bool TeleportEnabled = true;

    // internal cooldown tracker per object (using instanceID)
    // Use a static dictionary so all TelePorter instances share cooldown state and avoid immediate bounce-back
    private static System.Collections.Generic.Dictionary<int, float> s_lastTeleportedTime = new System.Collections.Generic.Dictionary<int, float>();

    // Track objects currently pending teleport to avoid duplicate coroutines
    private static System.Collections.Generic.HashSet<int> s_pendingTeleports = new System.Collections.Generic.HashSet<int>();

    // Helper: get the effective destination transform
    private Transform GetDestination()
    {
        if (LinkedTeleporter != null)
            return LinkedTeleporter.transform;

        if (Target != null)
            return Target;

        return null;
    }

    // Public method so UI or other scripts can trigger teleport explicitly
    public bool Teleport(GameObject obj)
    {
        if (!TeleportEnabled) return false;

        if (obj == null) return false;

        // Tag filtering
        if (AllowTags != null && AllowTags.Length > 0)
        {
            bool ok = false;
            for (int i = 0; i < AllowTags.Length; i++)
            {
                if (obj.CompareTag(AllowTags[i])) { ok = true; break; }
            }

            if (!ok) return false;
        }

        var dest = GetDestination();
        if (dest == null) return false;

        int id = obj.GetInstanceID();
        float now = Time.time;
        if (s_lastTeleportedTime.TryGetValue(id, out float t))
        {
            if (now - t < Cooldown) return false; // still cooling down
        }

        // Perform teleport while trying to preserve physics where sensible
        // 1) If the object has a CharacterController, disable it, move transform, then re-enable
        var controller = obj.GetComponent<CharacterController>();
        if (controller != null)
        {
            controller.enabled = false;
            MoveTransform(obj.transform, dest);
            controller.enabled = true;
            s_lastTeleportedTime[id] = now;
            OnTeleported?.Invoke(obj);
            return true;
        }

        // 2) If it has a Rigidbody, try setting position appropriately
        var rb = obj.GetComponent<Rigidbody>();
        if (rb != null)
        {
            // if non-kinematic, use MovePosition to respect interpolation; otherwise set transform
            if (!rb.isKinematic)
            {
#if UNITY_2022_2_OR_NEWER
                rb.linearVelocity = Vector3.zero; // stop residual velocity (newer API)
                rb.angularVelocity = Vector3.zero;
#else
                rb.velocity = Vector3.zero; // stop residual velocity
                rb.angularVelocity = Vector3.zero;
#endif
                rb.MovePosition(CalcDestinationPosition(dest));
                if (MatchRotation)
                    rb.MoveRotation(dest.rotation);
            }
            else
            {
                MoveTransform(obj.transform, dest);
            }

            s_lastTeleportedTime[id] = now;
            OnTeleported?.Invoke(obj);
            return true;
        }

        // 3) Fallback: just move transform
        MoveTransform(obj.transform, dest);
        s_lastTeleportedTime[id] = now;
        OnTeleported?.Invoke(obj);

        // If the player used this teleporter, disable it until mission completion
        if (obj.CompareTag("Player"))
        {
            TeleportEnabled = false;
            // optional: stop particle effects / visuals to indicate disabled state
            if (TeleportParticles != null)
            {
                foreach (var ps in TeleportParticles) if (ps != null) ps.Stop(true, ParticleSystemStopBehavior.StopEmitting);
            }
        }
        return true;
    }

    private void Awake()
    {
        // subscribe to mission completion so teleporters can be re-enabled
        MisionBotero.OnMissionCompleted += HandleMissionCompleted;
    }

    private void OnDestroy()
    {
        MisionBotero.OnMissionCompleted -= HandleMissionCompleted;
    }

    private void HandleMissionCompleted()
    {
        TeleportEnabled = true;
        // restore particles/visuals if any
        if (TeleportParticles != null)
        {
            foreach (var ps in TeleportParticles) if (ps != null) ps.Play(true);
        }
    }

    // Convenience: teleport the player (this GameObject) when something enters trigger
    private void OnTriggerEnter(Collider other)
    {
        // Try teleporting the collided object automatically
        var obj = other.gameObject;
        if (TeleportDelay > 0f)
        {
            // Start a delayed teleport coroutine on this TelePorter instance
            StartTeleportWithDelay(obj);
        }
        else
        {
            Teleport(obj);
        }
    }

    // Public helper: start delayed teleport (safe to call multiple places)
    public void StartTeleportWithDelay(GameObject obj)
    {
        if (obj == null) return;
        int id = obj.GetInstanceID();
        if (s_pendingTeleports.Contains(id)) return; // already pending
        StartCoroutine(TeleportDelayCoroutine(obj));
    }

    private IEnumerator TeleportDelayCoroutine(GameObject obj)
    {
        if (obj == null) yield break;
        int id = obj.GetInstanceID();
        s_pendingTeleports.Add(id);

        CharacterController controller = obj.GetComponent<CharacterController>();
        Rigidbody rb = obj.GetComponent<Rigidbody>();

        // Store original physics states
        bool hasRb = rb != null;
        var origConstraints = hasRb ? rb.constraints : RigidbodyConstraints.None;
#if UNITY_2022_2_OR_NEWER
        var origLinearVelocity = hasRb ? rb.linearVelocity : Vector3.zero;
#else
        var origLinearVelocity = hasRb ? rb.velocity : Vector3.zero;
#endif
        var origAngularVelocity = hasRb ? rb.angularVelocity : Vector3.zero;

        TemporaryMovementBlocker blocker = null;

        // Start particle acceleration if any
        System.Collections.Generic.List<float> originalSpeeds = new System.Collections.Generic.List<float>();
        System.Collections.Generic.List<ParticleSystem> modifiedSystems = new System.Collections.Generic.List<ParticleSystem>();
        System.Collections.Generic.List<bool> originalPlaying = new System.Collections.Generic.List<bool>();

        AccelerateParticles(this, originalSpeeds, modifiedSystems, originalPlaying);
        if (AccelerateLinkedTeleporter && LinkedTeleporter != null)
            LinkedTeleporter.AccelerateParticles(LinkedTeleporter, originalSpeeds, modifiedSystems, originalPlaying);

        if (LockMovementDuringDelay)
        {
            if (controller != null)
            {
                // Add a temporary blocker that keeps position fixed but allows rotation
                blocker = obj.AddComponent<TemporaryMovementBlocker>();
                blocker.LockPosition = obj.transform.position;
            }

            if (rb != null)
            {
                // zero velocities and freeze constraints to prevent physical motion (do NOT toggle isKinematic)
#if UNITY_2022_2_OR_NEWER
                rb.linearVelocity = Vector3.zero;
                rb.angularVelocity = Vector3.zero;
#else
                rb.velocity = Vector3.zero;
                rb.angularVelocity = Vector3.zero;
#endif
                rb.constraints = RigidbodyConstraints.FreezeAll;
            }
        }

        // Wait for the effect/delay
        if (TeleportDelay > 0f)
            yield return new WaitForSeconds(TeleportDelay);

        // Perform the actual teleport (this will set cooldown timestamp)
        Teleport(obj);

    // After teleport, restore rigidbody states if they were changed
        if (hasRb)
        {
            rb.constraints = origConstraints;
#if UNITY_2022_2_OR_NEWER
            rb.linearVelocity = origLinearVelocity;
            rb.angularVelocity = origAngularVelocity;
#else
            rb.velocity = origLinearVelocity;
            rb.angularVelocity = origAngularVelocity;
#endif
        }

    // Restore particle speeds
    RestoreParticleSpeeds(modifiedSystems, originalSpeeds, originalPlaying);

        // Remove temporary blocker if we added one
        if (blocker != null)
        {
            Destroy(blocker);
        }

        s_pendingTeleports.Remove(id);
    }

    // Accelerate particle systems on a teleporter instance and record original speeds
    private void AccelerateParticles(TelePorter tp, System.Collections.Generic.List<float> originalSpeeds, System.Collections.Generic.List<ParticleSystem> modifiedSystems, System.Collections.Generic.List<bool> originalPlaying)
    {
        if (tp == null || tp.TeleportParticles == null) return;

        foreach (var ps in tp.TeleportParticles)
        {
            if (ps == null) continue;
            var main = ps.main;
            // store original simulation speed and playing state
            originalSpeeds.Add(main.simulationSpeed);
            modifiedSystems.Add(ps);
            originalPlaying.Add(ps.isPlaying);

            // increase simulation speed
            main.simulationSpeed = main.simulationSpeed * ParticleSpeedMultiplier;

            // Ensure the particle system is playing so the faster simulation is visible
            if (!ps.isPlaying)
            {
                ps.Play(true);
            }
        }
    }

    // Restore particle system speeds using the lists filled by AccelerateParticles
    private void RestoreParticleSpeeds(System.Collections.Generic.List<ParticleSystem> modifiedSystems, System.Collections.Generic.List<float> originalSpeeds, System.Collections.Generic.List<bool> originalPlaying)
    {
        if (modifiedSystems == null || originalSpeeds == null) return;
        int n = System.Math.Min(modifiedSystems.Count, originalSpeeds.Count);
        n = System.Math.Min(n, originalPlaying != null ? originalPlaying.Count : n);
        for (int i = 0; i < n; i++)
        {
            var ps = modifiedSystems[i];
            if (ps == null) continue;
            var main = ps.main;
            // restore original simulation speed
            main.simulationSpeed = originalSpeeds[i];

            // Restore playing state
            if (originalPlaying[i])
            {
                ps.Play(true);
            }
            else
            {
                ps.Stop(true, ParticleSystemStopBehavior.StopEmitting);
            }
        }
    }

    // Move the transform of an object to a destination, applying the optional offset
    private void MoveTransform(Transform objTransform, Transform destTransform)
    {
        if (objTransform == null || destTransform == null) return;

        // Compute the destination position with the optional offset
        Vector3 destinationPosition = destTransform.position + destTransform.TransformVector(DestinationOffset);

        // Snap the position immediately
        objTransform.position = destinationPosition;

        if (MatchRotation)
        {
            // Optionally match the rotation to the destination
            objTransform.rotation = destTransform.rotation;
        }
    }

    // Calculate the correct destination position considering the destination's rotation and the offset
    private Vector3 CalcDestinationPosition(Transform dest)
    {
        if (dest == null) return Vector3.zero;
        // Apply the inverse of the destination's rotation to the offset, then add to the destination position
        return dest.position + Quaternion.Inverse(dest.rotation) * DestinationOffset;
    }
}
