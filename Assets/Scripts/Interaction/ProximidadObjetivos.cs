using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

// Note: this class will optionally update minimap icons (MinimapIcon component)
// and a UI text field showing the distance to the nearest objective.

public class ProximidadObjetivos : MonoBehaviour
{
    [Header("Jugador")]
    [Tooltip("Asignar el transform del jugador (usado para calcular distancias)")]
    public Transform jugador; // Asignar en el Inspector

    [Header("Objetivos (opcional prellenado)")]
    [Tooltip("Lista inicial de objetivos. Se usa solo para poblar la colección interna al iniciar.")]
    [SerializeField] private List<Transform> initialObjetivos;

    [Header("Parametros")]
    [Tooltip("Distancia mínima para considerar atrapado")] public float distanciaAtrapar = 1f;
    [Tooltip("Intervalo en segundos para mostrar logs de distancia (0 = desactivar logs)")]
    [SerializeField] private float logInterval = 0.5f;
    
    [Header("Minimap / UI")]
    [Tooltip("Texto UI (UnityEngine.UI.Text) opcional para mostrar la distancia al objetivo más cercano")]
    public Text distanceUIText;
    [Tooltip("Texto UI (TextMeshPro) opcional para mostrar la distancia al objetivo más cercano")]
    public TMP_Text distanceTMPText;
    [Tooltip("Texto UI (UnityEngine.UI.Text) opcional para mostrar cuántos se han recogido")]
    public Text collectedUIText;
    [Tooltip("Texto UI (TextMeshPro) opcional para mostrar cuántos se han recogido")]
    public TMP_Text collectedTMPText;
    [Tooltip("Formato del texto de distancia. Use {0} para el número (entero)")]
    public string distanceFormat = "Tu objetivo está a {0} m";
    [Tooltip("Formato para mostrar cuántos se han recogido (use {0}=recogidos, {1}=total)")]
    public string collectedFormat = "Recogidos: {0}/{1}";

    // Internal collection with O(1) contains/remove
    private HashSet<Transform> objetivosSet = new HashSet<Transform>();
    private float logTimer = 0f;
    // Total number of objectives that have been added (used to compute collected count)
    private int totalAssigned = 0;
    // Number of objectives actually collected/removed
    private int collectedSoFar = 0;

    // Events for external systems
    public event Action<Transform> OnObjectiveCollected;
    public event Action OnAllObjectivesCollected;

    private void Awake()
    {
        if (initialObjetivos != null)
        {
            foreach (var t in initialObjetivos)
                if (t != null) objetivosSet.Add(t);
            totalAssigned = objetivosSet.Count;
        }

        // If a MisionBotero exists in scene, subscribe to its progress events and use it as authoritative
        try
        {
            var mission = UnityEngine.Object.FindAnyObjectByType<MisionBotero>();
            if (mission != null)
            {
                mission.OnMissionProgressChanged += (collected, total) =>
                {
                    // adopt mission counts as authoritative for UI display
                    totalAssigned = Mathf.Max(0, total);
                    collectedSoFar = Mathf.Clamp(collected, 0, totalAssigned);
                };
            }
        }
        catch { }

        // Subscribe to global mission-completed notification so this component
        // reacts even if mission completion happens elsewhere (e.g. holograms activation)
        try
        {
            MisionBotero.OnMissionCompleted += OnMissionCompletedStatic;
        }
        catch { }
    }

    private void OnDestroy()
    {
        try { MisionBotero.OnMissionCompleted -= OnMissionCompletedStatic; } catch { }
    }

    private void OnMissionCompletedStatic()
    {
        // mark everything collected and clear active objectives so Update() will refresh UI accordingly
        collectedSoFar = totalAssigned;
        try { objetivosSet.Clear(); } catch { }
        // immediately clear distance UI as mission is done
        if (distanceUIText != null) distanceUIText.text = string.Empty;
        if (distanceTMPText != null) distanceTMPText.text = string.Empty;
        if (collectedUIText != null) collectedUIText.text = string.Empty;
        if (collectedTMPText != null) collectedTMPText.text = string.Empty;
    }

    /// <summary>
    /// Añade un objetivo dinámicamente (por ejemplo, un objeto instanciado en runtime).
    /// Evita duplicados.
    /// </summary>
    public void AddObjective(Transform t)
    {
        if (t == null) return;
        // Only increment totalAssigned when the objective was actually added (avoid duplicates)
        if (objetivosSet.Add(t))
            totalAssigned++;
    }

    /// <summary>
    /// Añade varios objetivos de una vez.
    /// </summary>
    public void AddObjectives(IEnumerable<Transform> list)
    {
        if (list == null) return;
        int added = 0;
        foreach (var t in list)
        {
            if (t == null) continue;
            if (objetivosSet.Add(t)) added++;
        }
        // account for actually added items (avoid counting duplicates)
        totalAssigned += added;
    }

    /// <summary>
    /// Called by external systems to notify that an objective was collected/placed.
    /// This removes it from the internal set, increments the collected counter and fires events.
    /// </summary>
    public void NotifyCollected(Transform t)
    {
        if (t == null) return;
        bool removed = RemoveObjective(t);
        if (removed)
        {
            OnObjectiveCollected?.Invoke(t);
            if (objetivosSet.Count == 0)
                OnAllObjectivesCollected?.Invoke();
        }
    }

    /// <summary>
    /// Quita un objetivo (por ejemplo, si se destruye fuera de aquí).
    /// </summary>
    public bool RemoveObjective(Transform t)
    {
        if (t == null) return false;
        bool removed = objetivosSet.Remove(t);
        if (removed) collectedSoFar = Mathf.Clamp(collectedSoFar + 1, 0, totalAssigned);
        return removed;
    }

    private void Update()
    {
        if (jugador == null) return; // nothing to do without a player

        int collectedCount = Mathf.Clamp(collectedSoFar, 0, totalAssigned);

        if (objetivosSet == null || objetivosSet.Count == 0)
        {
            // If there are no tracked objectives, still clear distance UI when
            // the mission is effectively complete (prevents showing "0 m").
            collectedCount = Mathf.Clamp(collectedSoFar, 0, totalAssigned);
            bool missionCompletedEarly = (totalAssigned > 0 && collectedCount >= totalAssigned) || (totalAssigned > 0 && (objetivosSet == null || objetivosSet.Count == 0));
            if (missionCompletedEarly)
            {
                if (distanceUIText != null) distanceUIText.text = string.Empty;
                if (distanceTMPText != null) distanceTMPText.text = string.Empty;
                // keep collected UI visible — only hide distance here
            }
            // avoid spamming logs every frame
            return;
        }

        // If any objectives were picked up by the player via another system (parented under the player),
        // remove them and count them as collected so the UI stays in sync.
        if (jugador != null)
        {
            var toRemove = new List<Transform>();
            foreach (var obj in objetivosSet)
            {
                if (obj == null) { toRemove.Add(obj); continue; }
                // If the objective has been parented to the player (picked up), remove it
                if (obj.IsChildOf(jugador))
                {
                    toRemove.Add(obj);
                    continue;
                }
                // If the objective has a PlaceableSculpture and it was marked placed elsewhere,
                // treat it as collected as well.
                var placeable = obj.GetComponentInChildren<PlaceableSculpture>(true);
                if (placeable != null && placeable.IsPlaced)
                {
                    toRemove.Add(obj);
                }
            }
            if (toRemove.Count > 0)
            {
                foreach (var r in toRemove)
                {
                    if (r == null)
                    {
                        // removed/destroyed externally
                        RemoveObjective(r);
                        continue;
                    }
                    // Use the public notifier so events are fired consistently
                    NotifyCollected(r);
                }
                if (objetivosSet.Count == 0)
                    OnAllObjectivesCollected?.Invoke();
            }
        }

        Transform objetivoMasCercano = null;
        float distanciaMin = Mathf.Infinity;

        // Buscar el objetivo más cercano
        foreach (Transform objetivo in objetivosSet)
        {
            if (objetivo == null) continue;
            float distancia = Vector3.Distance(jugador.position, objetivo.position);
            if (distancia < distanciaMin)
            {
                distanciaMin = distancia;
                objetivoMasCercano = objetivo;
            }
        }

        // Throttled logging
        if (logInterval > 0f)
        {
            logTimer -= Time.deltaTime;
            if (logTimer <= 0f)
            {
                logTimer = logInterval;
                //Debug.Log($"Objetivo más cercano a: {distanciaMin:F2} metros (quedan {objetivosSet.Count})");
            }
        }

        // Update minimap icons of all registered objectives (if they have MinimapIcon)
        foreach (var objetivo in objetivosSet)
        {
            if (objetivo == null) continue;
            var icon = objetivo.GetComponentInChildren<MinimapIcon>(true);
            if (icon != null)
            {
                float d = Vector3.Distance(jugador.position, objetivo.position);
                icon.UpdateScaleByDistance(d);
            }
        }

        // Update on-screen distance UI for nearest objective and collected count

        // Distance text (separate field)
        if (distanceUIText != null || distanceTMPText != null)
        {
            if (objetivoMasCercano != null)
            {
                int distInt = Mathf.RoundToInt(distanciaMin);
                string distTxt = string.Format(distanceFormat, distInt);
                if (distanceUIText != null) distanceUIText.text = distTxt;
                if (distanceTMPText != null) distanceTMPText.text = distTxt;
            }
            else
            {
                if (distanceUIText != null) distanceUIText.text = string.Empty;
                if (distanceTMPText != null) distanceTMPText.text = string.Empty;
            }
        }

        // If mission completed (collected all), show completed message and clear progress.
        bool missionCompleted = (totalAssigned > 0 && collectedCount >= totalAssigned) || (totalAssigned > 0 && objetivosSet.Count == 0);

        if (missionCompleted)
        {
            // Si es la misión de las 4 miniaturas, ocultar la distancia al completarse
            if (totalAssigned == 4)
            {
                if (distanceUIText != null) distanceUIText.text = string.Empty;
                if (distanceTMPText != null) distanceTMPText.text = string.Empty;
            }
            else
            {
                string doneMsg = "Completado, activa los hologramas.";
                if (distanceUIText != null) distanceUIText.text = doneMsg;
                if (distanceTMPText != null) distanceTMPText.text = doneMsg;
            }

            if (collectedUIText != null) collectedUIText.text = string.Empty;
            if (collectedTMPText != null) collectedTMPText.text = string.Empty;
        }
        else
        {
            // Collected/progress text (separate field). If not provided, fall back to placing it under distance fields as before.
            if (collectedUIText != null || collectedTMPText != null)
            {
                string collTxt = string.Format(collectedFormat, collectedCount, totalAssigned);
                if (collectedUIText != null) collectedUIText.text = collTxt;
                if (collectedTMPText != null) collectedTMPText.text = collTxt;
            }
            else
            {
                // Fallback: if no collected-specific fields assigned, inject progress below distance text (compatibility)
                if ((distanceUIText != null || distanceTMPText != null))
                {
                    if (objetivoMasCercano != null)
                    {
                        string distTxt = string.Format(distanceFormat, Mathf.RoundToInt(distanciaMin));
                        string collTxt = string.Format(collectedFormat, collectedCount, totalAssigned);
                        string combined = distTxt + "\n" + collTxt;
                        if (distanceUIText != null) distanceUIText.text = combined;
                        if (distanceTMPText != null) distanceTMPText.text = combined;
                    }
                    else
                    {
                        if (distanceUIText != null) distanceUIText.text = string.Empty;
                        if (distanceTMPText != null) distanceTMPText.text = string.Empty;
                    }
                }
            }
        }

        // Verificar si el jugador atrapó ese objetivo
        if (objetivoMasCercano != null && distanciaMin <= distanciaAtrapar)
        {
            // Si el objetivo está parentado al jugador (por ejemplo, el jugador lo sostiene),
            // no lo consideramos "capturado" para evitar destruir objetos que el jugador tiene en mano.
            if (jugador != null && objetivoMasCercano.IsChildOf(jugador))
            {
                // Si el objetivo fue parentado al jugador (se recogió), tratarlo como recogido
                NotifyCollected(objetivoMasCercano);
                if (distanceUIText != null) distanceUIText.text = string.Empty;
                if (distanceTMPText != null) distanceTMPText.text = string.Empty;
                return;
            }

            // Debug log removed for build cleanliness

            // Intentar transferir el objeto al jugador (pickup) en lugar de destruirlo.
            bool collected = false;

            // Buscar un componente PlaceableSculpture en el objetivo
            var placeable = objetivoMasCercano.GetComponentInParent<PlaceableSculpture>() ?? objetivoMasCercano.GetComponentInChildren<PlaceableSculpture>(true);

            if (jugador != null)
            {
                // Preferir usar PlayerPickupController si existe (realiza parenting y desactiva colliders correctamente)
                var pickupController = jugador.GetComponent<PlayerPickupController>();
                if (pickupController != null && placeable != null && !placeable.IsPlaced)
                {
                    pickupController.Pickup(placeable);
                    collected = true;
                }
                else
                {
                    // Fallback: intentar usar solo PlayerPickupInventory (sin parenting especializado)
                    var inv = jugador.GetComponent<PlayerPickupInventory>();
                    if (inv != null && placeable != null && !placeable.IsPlaced)
                    {
                        // Registrar en inventario. No parentear directamente al jugador (evita que desactivar la raíz
                        // del objeto al colocarlo desactive al propio jugador).
                        inv.Pickup(placeable);
                        collected = true;
                    }
                }
            }

            // Si no pudimos hacer pickup (no hay componentes de pickup o no era Placeable), proceder con el comportamiento anterior (destruir)
            if (collected)
            {
                // Use RemoveObjective helper to ensure collected counter increments consistently
                RemoveObjective(objetivoMasCercano);
                OnObjectiveCollected?.Invoke(objetivoMasCercano);
                if (objetivosSet.Count == 0)
                    OnAllObjectivesCollected?.Invoke();
            }
            else
            {
                // Comportamiento legacy: eliminar el objeto si no hay sistema de pickup disponible
                RemoveObjective(objetivoMasCercano);
                try { Destroy(objetivoMasCercano.gameObject); } catch { }
                OnObjectiveCollected?.Invoke(objetivoMasCercano);
                if (objetivosSet.Count == 0)
                    OnAllObjectivesCollected?.Invoke();
            }
        }
        else
        {
            // If nearest objective exists, ensure UI shows the distance; else clear it
            if (objetivoMasCercano == null)
            {
                if (distanceUIText != null) distanceUIText.text = string.Empty;
                if (distanceTMPText != null) distanceTMPText.text = string.Empty;
            }
        }
    }

    /// <summary>
    /// Devuelve los objetivos actuales (solo lectura).
    /// </summary>
    public IReadOnlyCollection<Transform> CurrentObjectives => objetivosSet;
}