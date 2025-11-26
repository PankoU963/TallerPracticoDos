using System;
using System.Collections.Generic;
using UnityEngine;

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

    // Internal collection with O(1) contains/remove
    private HashSet<Transform> objetivosSet = new HashSet<Transform>();
    private float logTimer = 0f;

    // Events for external systems
    public event Action<Transform> OnObjectiveCollected;
    public event Action OnAllObjectivesCollected;

    private void Awake()
    {
        if (initialObjetivos != null)
        {
            foreach (var t in initialObjetivos)
                if (t != null) objetivosSet.Add(t);
        }
    }

    /// <summary>
    /// Añade un objetivo dinámicamente (por ejemplo, un objeto instanciado en runtime).
    /// Evita duplicados.
    /// </summary>
    public void AddObjective(Transform t)
    {
        if (t == null) return;
        objetivosSet.Add(t);
    }

    /// <summary>
    /// Añade varios objetivos de una vez.
    /// </summary>
    public void AddObjectives(IEnumerable<Transform> list)
    {
        if (list == null) return;
        foreach (var t in list)
            if (t != null) objetivosSet.Add(t);
    }

    /// <summary>
    /// Quita un objetivo (por ejemplo, si se destruye fuera de aquí).
    /// </summary>
    public bool RemoveObjective(Transform t)
    {
        if (t == null) return false;
        return objetivosSet.Remove(t);
    }

    private void Update()
    {
        if (jugador == null) return; // nothing to do without a player

        if (objetivosSet == null || objetivosSet.Count == 0)
        {
            // notify once if needed and then early out
            // (avoid spamming logs every frame)
            return;
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
                Debug.Log($"Objetivo más cercano a: {distanciaMin:F2} metros (quedan {objetivosSet.Count})");
            }
        }

        // Verificar si el jugador atrapó ese objetivo
        if (objetivoMasCercano != null && distanciaMin <= distanciaAtrapar)
        {
            Debug.Log($"Objetivo atrapado: {objetivoMasCercano.name}");
            objetivosSet.Remove(objetivoMasCercano);
            try { Destroy(objetivoMasCercano.gameObject); } catch { }
            OnObjectiveCollected?.Invoke(objetivoMasCercano);
            if (objetivosSet.Count == 0)
                OnAllObjectivesCollected?.Invoke();
        }
    }

    /// <summary>
    /// Devuelve los objetivos actuales (solo lectura).
    /// </summary>
    public IReadOnlyCollection<Transform> CurrentObjectives => objetivosSet;
}