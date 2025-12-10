using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// Marca una escultura como 'colocable' y almacena el índice del prefab original.
/// Otros sistemas (p. ej. `ProyectorSlot`) comprobarán este componente para aceptar/colocar.
/// </summary>
public class PlaceableSculpture : MonoBehaviour
{
    [Tooltip("Índice del prefab original en SculptureSpawner.prefabs")]
    [SerializeField] private int prefabIndex = -1;

    public int PrefabIndex => prefabIndex;
    public bool IsPlaced { get; private set; }

    public UnityEvent OnPlaced;

    public void Initialize(int index)
    {
        prefabIndex = index;
    }

    public void MarkPlaced()
    {
        if (IsPlaced) return;
        IsPlaced = true;
        OnPlaced?.Invoke();
        // Optional: disable physics/grab components here if present
        var rb = GetComponent<Rigidbody>();
        if (rb != null) rb.isKinematic = true;
    }
}
