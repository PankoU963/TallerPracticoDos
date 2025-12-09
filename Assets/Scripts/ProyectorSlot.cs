using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using System;

/// <summary>
/// Slot de un proyector que acepta una escultura colocada (PlaceableSculpture).
/// Cuando la escultura correcta se coloca, se instancia o activa el holograma asociado.
/// </summary>
public class ProyectorSlot : MonoBehaviour
{
    [Tooltip("Lista de prefabs de holograma alineados por índice con SculptureSpawner.prefabs")]
    [SerializeField] private GameObject[] hologramPrefabs;
    [Tooltip("Donde se instanciará el holograma")]
    [SerializeField] private Transform hologramSpawnPoint;
    [Tooltip("Referencia al modelo de lupa dentro del proyector (se ocultará al activar el holograma)")]
    [SerializeField] private GameObject magnifierObject;
    [Tooltip("Si true, el objeto colocado se desactivará (consumido)" )]
    [SerializeField] private bool consumeOnPlace = true;

    [Header("Interaction")]
    [Tooltip("Si true, el jugador debe interactuar (tecla/tap) para colocar; si false, coloca automáticamente al entrar")]
    [SerializeField] private bool requireInteraction = true;
    [SerializeField, Tooltip("Tecla para interactuar en PC")] private KeyCode interactKey = KeyCode.E;
    [SerializeField, Tooltip("Tag que identifica al jugador para permitir interacción con tecla")] private string playerTag = "Player";
    [SerializeField, Tooltip("Show debug logs for trigger/interaction events")] private bool debugLogs = false;
    [SerializeField, Tooltip("Fallback search radius (meters) when no trigger candidates are found")]
    private float overlapRadius = 1.0f;

    public UnityEvent<int> OnHologramActivated; // pasa el índice del prefab

    private GameObject currentHologram;
    private Collider myCollider;
    private bool playerInside = false;
    private List<PlaceableSculpture> candidates = new List<PlaceableSculpture>();

    private void Reset()
    {
        hologramSpawnPoint = transform;
    }

    private void Awake()
    {
        myCollider = GetComponent<Collider>();
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other == null) return;

        // track player presence for keyboard interaction
        if (other.CompareTag(playerTag)) playerInside = true;

        // If the collider belongs to the player, check if the player has a held object
        if (other.CompareTag(playerTag))
        {
            var inv = other.GetComponentInParent<PlayerPickupInventory>() ?? other.GetComponent<PlayerPickupInventory>();
            if (inv != null && inv.HasHeld())
            {
                var held = inv.Held;
                if (held != null && !held.IsPlaced)
                {
                    // try to place the held object directly
                    if (TryPlacePlaceable(held, inv))
                    {
                        // successfully placed; no further processing needed
                        if (debugLogs) Debug.Log("ProyectorSlot: placed held object from player inventory");
                        return;
                    }
                }
            }
        }

        // try to discover placeable on the incoming collider (parent or children)
        var placeable = other.GetComponentInParent<PlaceableSculpture>() ?? other.GetComponentInChildren<PlaceableSculpture>(true);
        if (placeable != null && !candidates.Contains(placeable))
            candidates.Add(placeable);

        if (debugLogs)
        {
            Debug.Log($"ProyectorSlot: OnTriggerEnter by {other.gameObject.name}. foundPlaceable={(placeable!=null)}");
            if (placeable != null)
                Debug.Log($"ProyectorSlot: placeable prefabIndex={placeable.PrefabIndex}, IsPlaced={placeable.IsPlaced}, obj={placeable.gameObject.name}");
            else
            {
                var childPlaceables = other.gameObject.GetComponentsInChildren<PlaceableSculpture>(true);
                Debug.Log($"ProyectorSlot: GetComponentsInChildren found {childPlaceables.Length} PlaceableSculpture(s) on {other.gameObject.name}");
            }
            // Debug: list all PlaceableSculpture instances and their distance to this projector
            var all = UnityEngine.Resources.FindObjectsOfTypeAll<PlaceableSculpture>();
            Debug.Log($"ProyectorSlot: scene has {all.Length} PlaceableSculpture(s)");
            var refPosDbg = hologramSpawnPoint != null ? hologramSpawnPoint.position : transform.position;
            foreach (var p in all)
            {
                if (p == null) continue;
                var col = p.GetComponent<Collider>();
                var rb = p.GetComponent<Rigidbody>();
                float d = Vector3.Distance(refPosDbg, p.transform.position);
                Debug.Log($" - {p.gameObject.name}: index={p.PrefabIndex}, placed={p.IsPlaced}, active={p.gameObject.activeInHierarchy}, collider={(col!=null?col.enabled:false)}, rigidbody={(rb!=null)}, dist={d:F2}");
            }
        }

        // If auto-placement is enabled, try nearest-first placement (includes overlap fallback)
        if (!requireInteraction)
            InteractNearest();
    }

    private void OnTriggerStay(Collider other)
    {
        if (other == null) return;
        if (!requireInteraction)
            TryPlace(other.gameObject);
        else
        {
            // keep candidates up-to-date in case an object is released inside the trigger
            var placeable = other.GetComponentInParent<PlaceableSculpture>() ?? other.GetComponentInChildren<PlaceableSculpture>(true);
            if (placeable != null && !candidates.Contains(placeable)) candidates.Add(placeable);
        }
    }

    private void TryPlace(GameObject candidate)
    {
        if (candidate == null) return;
        // Ignore if candidate is the player collider
        if (candidate.CompareTag(playerTag)) return;
        var placeable = candidate.GetComponentInParent<PlaceableSculpture>() ?? candidate.GetComponentInChildren<PlaceableSculpture>(true);
        if (placeable == null || placeable.IsPlaced)
        {
            if (debugLogs)
            {
                Debug.Log($"ProyectorSlot: TryPlace failed for candidate={candidate.name}. placeableFound={(placeable!=null)} placed={(placeable!=null?placeable.IsPlaced:false)}");
            }
            return;
        }

        int idx = placeable.PrefabIndex;
        if (idx < 0 || hologramPrefabs == null || idx >= hologramPrefabs.Length)
        {
            // no hologram configured for this index
            return;
        }

        if (debugLogs) Debug.Log($"ProyectorSlot: Placing prefabIndex={idx} from object {candidate.name}");
        ActivateHologram(idx);

        if (consumeOnPlace)
        {
            placeable.MarkPlaced();
            // Try to disable the whole spawned object
            var root = candidate.transform.root.gameObject;
            root.SetActive(false);
        }
        else
        {
            placeable.MarkPlaced();
        }
    }

    /// <summary>
    /// Try to place a PlaceableSculpture directly (e.g. from player inventory).
    /// Returns true if the hologram was activated and the object processed.
    /// </summary>
    private bool TryPlacePlaceable(PlaceableSculpture placeable, PlayerPickupInventory inv = null)
    {
        if (placeable == null) return false;
        if (placeable.IsPlaced) return false;

        int idx = placeable.PrefabIndex;
        if (idx < 0 || hologramPrefabs == null || idx >= hologramPrefabs.Length)
        {
            if (debugLogs) Debug.Log($"ProyectorSlot: No hologram configured for prefabIndex={idx} on {placeable.gameObject.name}");
            return false;
        }

        if (debugLogs) Debug.Log($"ProyectorSlot: Placing (inventory) prefabIndex={idx} from object {placeable.gameObject.name}");
        ActivateHologram(idx);

        placeable.MarkPlaced();

        if (consumeOnPlace)
        {
            // disable the spawned root so it disappears from scene
            var root = placeable.transform.root.gameObject;
            root.SetActive(false);
        }

        // If the object was held by a player inventory, clear the reference
        if (inv != null && inv.HasHeld()) inv.ClearHeld();

        return true;
    }

    private void OnTriggerExit(Collider other)
    {
        if (other == null) return;
        if (other.CompareTag(playerTag)) playerInside = false;

        var placeable = other.GetComponentInParent<PlaceableSculpture>() ?? other.GetComponentInChildren<PlaceableSculpture>(true);
        if (placeable != null && candidates.Contains(placeable)) candidates.Remove(placeable);
    }

    private void Update()
    {
        // Keyboard interaction
        if (requireInteraction && playerInside && Input.GetKeyDown(interactKey))
        {
            InteractNearest();
        }

        // Touch interaction: tap on the proyector to interact
        if (requireInteraction && Input.touchCount > 0)
        {
            var t = Input.GetTouch(0);
            if (t.phase == TouchPhase.Ended)
            {
                var cam = Camera.main;
                if (cam != null)
                {
                    var ray = cam.ScreenPointToRay(t.position);
                    if (Physics.Raycast(ray, out var hit, 100f))
                    {
                        if (hit.collider == myCollider || hit.collider.transform.IsChildOf(transform))
                        {
                            InteractNearest();
                        }
                    }
                }
            }
        }
    }

    private void InteractNearest()
    {
        PlaceableSculpture best = null;
        float bestDist = float.MaxValue;
        var refPos = hologramSpawnPoint != null ? hologramSpawnPoint.position : transform.position;

        if (candidates != null && candidates.Count > 0)
        {
            for (int i = candidates.Count - 1; i >= 0; i--)
            {
                var p = candidates[i];
                if (p == null || p.IsPlaced)
                {
                    candidates.RemoveAt(i);
                    continue;
                }
                float d = (p.transform.position - refPos).sqrMagnitude;
                if (d < bestDist)
                {
                    bestDist = d;
                    best = p;
                }
            }
        }

        if (best != null)
        {
            if (debugLogs) Debug.Log($"ProyectorSlot: InteractNearest selected candidate {best.gameObject.name} (index={best.PrefabIndex})");
            TryPlace(best.gameObject);
            return;
        }

        // Fallback: if no candidates found via trigger, do an overlap search around the spawn point
        if (debugLogs) Debug.Log("ProyectorSlot: no trigger candidates, running fallback OverlapSphere search");
        refPos = hologramSpawnPoint != null ? hologramSpawnPoint.position : transform.position;
        var cols = Physics.OverlapSphere(refPos, overlapRadius);
        PlaceableSculpture fallback = null;
        float bestD = float.MaxValue;
        if (cols != null && cols.Length > 0)
        {
            foreach (var c in cols)
            {
                if (c == null) continue;
                var p = c.GetComponentInParent<PlaceableSculpture>() ?? c.GetComponentInChildren<PlaceableSculpture>(true);
                if (p == null || p.IsPlaced) continue;
                float d = (p.transform.position - refPos).sqrMagnitude;
                if (d < bestD)
                {
                    bestD = d;
                    fallback = p;
                }
            }
        }

        if (fallback == null)
        {
            // Last-resort: search all PlaceableSculpture instances in the scene (includes inactive)
            if (debugLogs) Debug.Log($"ProyectorSlot: OverlapSphere returned { (cols!=null?cols.Length:0) } colliders; running scene-wide search for PlaceableSculpture");
            var all = UnityEngine.Resources.FindObjectsOfTypeAll<PlaceableSculpture>();
            foreach (var p in all)
            {
                // ignore placeables that belong to a proyector (avoid self-detection)
                if (p == null || p.IsPlaced) continue;
                if (p.GetComponentInParent<ProyectorSlot>() != null) continue;
                float d = (p.transform.position - refPos).sqrMagnitude;
                if (d <= overlapRadius * overlapRadius && d < bestD)
                {
                    bestD = d;
                    fallback = p;
                }
            }
            if (fallback != null && debugLogs) Debug.Log($"ProyectorSlot: scene-wide fallback found {fallback.gameObject.name} (index={fallback.PrefabIndex})");
        }

        if (fallback != null)
        {
            if (debugLogs) Debug.Log($"ProyectorSlot: fallback found placeable {fallback.gameObject.name} (index={fallback.PrefabIndex})");
            TryPlace(fallback.gameObject);
        }
    }

    private void ActivateHologram(int prefabIndex)
    {
        if (currentHologram != null) return;
        var prefab = hologramPrefabs[prefabIndex];
        if (prefab == null) return;
        var spawn = hologramSpawnPoint != null ? hologramSpawnPoint : transform;
        // hide magnifier if provided
        if (magnifierObject != null)
        {
            magnifierObject.SetActive(false);
        }
        else
        {
            // try to find a child named like 'lupa' or 'magnifier' and hide it
            var child = transform.Find("lupa") ?? transform.Find("Lupa") ?? transform.Find("magnifier") ?? transform.Find("Magnifier");
            if (child != null) child.gameObject.SetActive(false);
        }
        currentHologram = Instantiate(prefab, spawn.position, spawn.rotation);
        currentHologram.SetActive(true);
        currentHologram.transform.SetParent(spawn, true);
        OnHologramActivated?.Invoke(prefabIndex);
    }

    public void ClearHologram()
    {
        if (currentHologram != null)
        {
            Destroy(currentHologram);
            currentHologram = null;
        }
    }
}
