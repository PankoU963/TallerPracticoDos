using System;
using UnityEngine;

/// <summary>
/// Componente simple para que el jugador almacene el objeto que tiene en la mano.
/// El sistema de pickup/grab debe llamar a `Pickup` y `Drop` según corresponda.
/// ProyectorSlot consultará este componente para colocar directamente el objeto que el jugador tenga.
/// </summary>
public class PlayerPickupInventory : MonoBehaviour
{
    public PlaceableSculpture Held { get; private set; }
    // Renderers of the currently held object's root (used to restore state on drop)
    private Renderer[] heldRenderers;
    private bool[] heldRenderersPrevEnabled;
        // Colliders and bodies state saved while holding to restore on drop
        private MeshCollider[] heldMeshColliders;
        private bool[] heldMeshCollidersPrevConvex;
        private Rigidbody[] heldRigidbodies;
        private bool[] heldRigidbodiesPrevKinematic;

    /// <summary>
    /// Llamar al recoger el objeto.
    /// No modifica la jerarquía ni habilita/deshabilita colliders: eso lo debe hacer el sistema de pickup.
    /// </summary>
    public void Pickup(PlaceableSculpture sculpture)
    {
        if (sculpture == null) return;

        Held = sculpture;

        // Hide visual renderers of the picked object so it doesn't appear in the world.
        // Use the sculpture's own transform as the root for renderer lookup to avoid
        // accidentally including renderers from the player or other objects that may
        // be above the sculpture in the hierarchy (e.g. if it's parented while held).
        try
        {
            // Determine the visual root to hide renderers from.
            // Prefer the instantiated root (`transform.root`) if it doesn't belong to the player,
            // otherwise fall back to the sculpture transform to avoid hiding player renderers.
            var renderRoot = sculpture.transform;
            try
            {
                var candidateRoot = sculpture.transform.root;
                if (candidateRoot != null && candidateRoot != transform.root)
                {
                    // if the candidate root does not appear to be the player (no PlayerPickupInventory)
                    // and does not have the Player tag, prefer it as the render root.
                    bool belongsToPlayer = false;
                    try { belongsToPlayer = candidateRoot.CompareTag("Player"); } catch { belongsToPlayer = false; }
                    var hasPlayerInventory = candidateRoot.GetComponentInChildren<PlayerPickupInventory>(true) != null;
                    if (!belongsToPlayer && !hasPlayerInventory)
                        renderRoot = candidateRoot;
                }
            }
            catch { }
            heldRenderers = renderRoot.GetComponentsInChildren<Renderer>(true);
            if (heldRenderers != null && heldRenderers.Length > 0)
            {
                heldRenderersPrevEnabled = new bool[heldRenderers.Length];
                for (int i = 0; i < heldRenderers.Length; i++)
                {
                    heldRenderersPrevEnabled[i] = heldRenderers[i].enabled;
                    heldRenderers[i].enabled = false;
                }
            }
            // Handle MeshColliders: make them convex to avoid concave+dynamic Rigidbody errors
            heldMeshColliders = renderRoot.GetComponentsInChildren<MeshCollider>(true);
            if (heldMeshColliders != null && heldMeshColliders.Length > 0)
            {
                heldMeshCollidersPrevConvex = new bool[heldMeshColliders.Length];
                for (int i = 0; i < heldMeshColliders.Length; i++)
                {
                    var mc = heldMeshColliders[i];
                    if (mc == null) continue;
                    try
                    {
                        heldMeshCollidersPrevConvex[i] = mc.convex;
                        if (!mc.convex)
                            mc.convex = true;
                    }
                    catch (Exception) { }
                }
            }

            // Handle Rigidbodies: make them kinematic while held to avoid physics conflicts
            heldRigidbodies = renderRoot.GetComponentsInChildren<Rigidbody>(true);
            if (heldRigidbodies != null && heldRigidbodies.Length > 0)
            {
                heldRigidbodiesPrevKinematic = new bool[heldRigidbodies.Length];
                for (int i = 0; i < heldRigidbodies.Length; i++)
                {
                    var rb = heldRigidbodies[i];
                    if (rb == null) continue;
                    try
                    {
                        heldRigidbodiesPrevKinematic[i] = rb.isKinematic;
                        if (!rb.isKinematic)
                            rb.isKinematic = true;
                    }
                    catch (Exception) { }
                }
            }
        }
        catch (Exception)
        {
            // ignore rendering hide failures
        }
    }

    /// <summary>
    /// Dejar/soltar el objeto que se tenía en la mano.
    /// Devuelve la escultura que estaba sostenida (o null).
    /// </summary>
    public PlaceableSculpture Drop()
    {
        var tmp = Held;
        if (tmp != null)
        {
            // restore renderers enabled state
            try
            {
                if (heldRenderers != null && heldRenderersPrevEnabled != null && heldRenderers.Length == heldRenderersPrevEnabled.Length)
                {
                    for (int i = 0; i < heldRenderers.Length; i++)
                    {
                        if (heldRenderers[i] != null)
                            heldRenderers[i].enabled = heldRenderersPrevEnabled[i];
                    }
                }
            }
            catch (Exception)
            {
                // ignore
            }
            // Restore MeshCollider convex flags
            try
            {
                if (heldMeshColliders != null && heldMeshCollidersPrevConvex != null && heldMeshColliders.Length == heldMeshCollidersPrevConvex.Length)
                {
                    for (int i = 0; i < heldMeshColliders.Length; i++)
                    {
                        var mc = heldMeshColliders[i];
                        if (mc == null) continue;
                        try { mc.convex = heldMeshCollidersPrevConvex[i]; } catch (Exception) { }
                    }
                }
            }
            catch (Exception) { }

            // Restore Rigidbodies kinematic state
            try
            {
                if (heldRigidbodies != null && heldRigidbodiesPrevKinematic != null && heldRigidbodies.Length == heldRigidbodiesPrevKinematic.Length)
                {
                    for (int i = 0; i < heldRigidbodies.Length; i++)
                    {
                        var rb = heldRigidbodies[i];
                        if (rb == null) continue;
                        try { rb.isKinematic = heldRigidbodiesPrevKinematic[i]; } catch (Exception) { }
                    }
                }
            }
            catch (Exception) { }
        }

        Held = null;
        heldRenderers = null;
        heldRenderersPrevEnabled = null;
        heldMeshColliders = null;
        heldMeshCollidersPrevConvex = null;
        heldRigidbodies = null;
        heldRigidbodiesPrevKinematic = null;
        return tmp;
    }

    public bool HasHeld() => Held != null;

    public void ClearHeld() => Held = null;
}
