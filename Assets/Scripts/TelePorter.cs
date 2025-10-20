using System;
using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// TelePorter: Teleports GameObjects (players or other objects) to a target Transform or linked TelePorter.
/// Features:
/// - Configurable target Transform (or link to another TelePorter)
/// - Cooldown to avoid immediate re-teleport
/// - Optional tag filter to only allow objects with certain tags
/// - Supports CharacterController and Rigidbody (kinematic or non-kinematic)
/// - Public Teleport(GameObject obj) method so UI can call it
/// </summary>
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

    // internal cooldown tracker per object (using instanceID)
    private System.Collections.Generic.Dictionary<int, float> _lastTeleportedTime = new System.Collections.Generic.Dictionary<int, float>();

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
        if (_lastTeleportedTime.TryGetValue(id, out float t))
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
            _lastTeleportedTime[id] = now;
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

            _lastTeleportedTime[id] = now;
            OnTeleported?.Invoke(obj);
            return true;
        }

        // 3) Fallback: just move transform
        MoveTransform(obj.transform, dest);
        _lastTeleportedTime[id] = now;
        OnTeleported?.Invoke(obj);
        return true;
    }

    // Convenience: teleport the player (this GameObject) when something enters trigger
    private void OnTriggerEnter(Collider other)
    {
        // Try teleporting the collided object automatically
        Teleport(other.gameObject);
    }

    // Calculate destination world position based on destination transform and offset
    private Vector3 CalcDestinationPosition(Transform dest)
    {
        return dest.TransformPoint(DestinationOffset);
    }

    private void MoveTransform(Transform src, Transform dest)
    {
        src.position = CalcDestinationPosition(dest);
        if (MatchRotation)
            src.rotation = dest.rotation;
    }

    // Draw a gizmo line to the destination for editor convenience
    private void OnDrawGizmos()
    {
        var dest = GetDestination();
        if (dest != null)
        {
            Gizmos.color = Color.cyan;
            Gizmos.DrawLine(transform.position, dest.position);
            Gizmos.DrawSphere(dest.position, 0.1f);
        }
    }
}
