using System.Collections.Generic;
using UnityEngine;

public class ProximidadObjetivos : MonoBehaviour
{
    public Transform jugador;                // Asignar en el Inspector
    public List<Transform> objetivos;        // 5 objetivos asignados en el Inspector
    public float distanciaAtrapar = 1f;      // Distancia mínima para considerar atrapado

    /// <summary>
    /// Añade un objetivo dinámicamente (por ejemplo, un objeto instanciado en runtime).
    /// Evita duplicados.
    /// </summary>
    public void AddObjective(Transform t)
    {
        if (t == null) return;
        if (objetivos == null) objetivos = new List<Transform>();
        if (!objetivos.Contains(t)) objetivos.Add(t);
    }

    /// <summary>
    /// Añade varios objetivos de una vez.
    /// </summary>
    public void AddObjectives(IEnumerable<Transform> list)
    {
        if (list == null) return;
        if (objetivos == null) objetivos = new List<Transform>();
        foreach (var t in list)
            if (t != null && !objetivos.Contains(t)) objetivos.Add(t);
    }

    void Update()
    {
        if (objetivos.Count == 0)
        {
            Debug.Log("¡Todos los objetivos han sido atrapados!");
            return;
        }

        Transform objetivoMasCercano = null;
        float distanciaMin = Mathf.Infinity;

        // Buscar el objetivo más cercano
        foreach (Transform objetivo in objetivos)
        {
            float distancia = Vector3.Distance(jugador.position, objetivo.position);

            if (distancia < distanciaMin)
            {
                distanciaMin = distancia;
                objetivoMasCercano = objetivo;
            }
        }

        // Imprimir la distancia al objetivo más cercano
        Debug.Log("Objetivo más cercano a: " + distanciaMin.ToString("F2") + " metros");

        // Verificar si el jugador atrapó ese objetivo
        if (distanciaMin <= distanciaAtrapar)
        {
            Debug.Log("Objetivo atrapado: " + objetivoMasCercano.name);
            objetivos.Remove(objetivoMasCercano);
            Destroy(objetivoMasCercano.gameObject);
        }
    }
}