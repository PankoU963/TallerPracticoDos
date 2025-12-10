using UnityEngine;

/// <summary>
/// Simple pickup controller for testing: usa raycast desde la cámara para recoger
/// y soltar `PlaceableSculpture`. Integra con `PlayerPickupInventory`.
/// - Presiona la tecla `interactKey` para recoger/soltar.
/// - Al recoger, el objeto se parenta a `holdPoint`, colliders se desactivan y Rigidbody se hace kinematic.
/// - Al soltar, se restauran colliders/rigidbody y se limpia el inventario.
/// Diseñado como un helper plug-and-play para pruebas; reemplaza o integra tu sistema de grab real.
/// </summary>
public class PlayerPickupController : MonoBehaviour
{
    [Tooltip("Transform donde se colocará el objeto sostenido (ej. un Empty delante de la cámara)")]
    public Transform holdPoint;

    [Tooltip("Distancia máxima para recoger objetos")] public float pickupRange = 3f;
    [Tooltip("Capa(s) que contienen objetos placeables (opcional)")] public LayerMask placeableLayer = Physics.DefaultRaycastLayers;
    [Tooltip("Tecla de interacción")]
    public KeyCode interactKey = KeyCode.E;

    private Camera cam;
    private PlayerPickupInventory inventory;

    private void Awake()
    {
        cam = Camera.main;
        inventory = GetComponent<PlayerPickupInventory>() ?? gameObject.AddComponent<PlayerPickupInventory>();
        if (holdPoint == null && cam != null)
        {
            // crear un holdPoint por defecto 1.0m delante de la cámara
            var go = new GameObject("HoldPoint");
            go.transform.SetParent(cam.transform, false);
            go.transform.localPosition = new Vector3(0f, -0.2f, 1.0f);
            holdPoint = go.transform;
        }
    }

    private void Update()
    {
        if (Input.GetKeyDown(interactKey))
        {
            // If the player is inside any ProyectorSlot that requires interaction,
            // do not treat the key as pickup/drop. Let the ProyectorSlot handle it.
            try
            {
                var slots = UnityEngine.Object.FindObjectsByType<ProyectorSlot>(UnityEngine.FindObjectsSortMode.None);
                foreach (var s in slots)
                {
                    if (s == null) continue;
                    if (s.PlayerInside && s.RequiresInteraction)
                    {
                        // consume the input here (do nothing in pickup controller)
                        return;
                    }
                }
            }
            catch { }

            if (inventory.HasHeld())
            {
                DropHeld();
            }
            else
            {
                TryPickupFromView();
            }
        }
    }

    private void TryPickupFromView()
    {
        if (cam == null) cam = Camera.main;
        if (cam == null) return;

        Ray ray = cam.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f));
        if (Physics.Raycast(ray, out var hit, pickupRange, placeableLayer))
        {
            // buscar PlaceableSculpture en el objeto golpeado
            var p = hit.collider.GetComponentInParent<PlaceableSculpture>() ?? hit.collider.GetComponentInChildren<PlaceableSculpture>(true);
                if (p != null && !p.IsPlaced)
                {
                    Pickup(p);
                }
        }
    }

    public void Pickup(PlaceableSculpture p)
    {
        if (p == null) return;
        // First register in inventory (hides renderers and stores collider/rigidbody state)
        inventory.Pickup(p);

        // Then parent to the holdPoint and disable colliders/physics to avoid interference
        var root = p.transform.root;
        // Ensure root is active (in case it was inactive)
        try { root.gameObject.SetActive(true); } catch { }
        root.SetParent(holdPoint, true);
        root.localPosition = Vector3.zero;
        root.localRotation = Quaternion.identity;

        // Desactivar colliders y freeze física para evitar interferencias
        var cols = root.GetComponentsInChildren<Collider>(true);
        foreach (var c in cols) c.enabled = false;
        var rbs = root.GetComponentsInChildren<Rigidbody>(true);
        foreach (var r in rbs)
        {
            r.isKinematic = true;
            r.detectCollisions = false;
        }
    }

    public void DropHeld()
    {
        if (!inventory.HasHeld()) return;
        var p = inventory.Held;
        if (p == null)
        {
            inventory.ClearHeld();
            return;
        }

        var root = p.transform.root;
        // despegar del holdPoint y dejar frente al jugador
        root.SetParent(null, true);
        root.position = holdPoint != null ? holdPoint.position : root.position;

        // restaurar colliders y rigidbodies
        var cols = root.GetComponentsInChildren<Collider>(true);
        foreach (var c in cols) c.enabled = true;
        var rbs = root.GetComponentsInChildren<Rigidbody>(true);
        foreach (var r in rbs)
        {
            r.isKinematic = false;
            r.detectCollisions = true;
        }

        inventory.Drop();
    }
}
