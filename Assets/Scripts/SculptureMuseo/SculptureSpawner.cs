using System;
using System.Collections.Generic;
using UnityEngine;

public class SculptureSpawner : MonoBehaviour
{
    [SerializeField] private GameObject[] prefabs;
    [SerializeField] private Transform[] spawnPoints;
    [Header("Minimap icon setup (optional)")]
    [Tooltip("Prefab UI used for minimap icons (assign an UI Image prefab)")]
    [SerializeField] private GameObject uiIconPrefab;
    [Tooltip("Minimap camera reference (optional). If not set, MinimapIcon will try to find it at runtime.")]
    [SerializeField] private Camera minimapCamera;
    [Tooltip("RectTransform container for minimap UI icons (optional). If not set, MinimapIcon will try to find it at runtime.")]
    [SerializeField] private RectTransform minimapContainer;
    [SerializeField] [Tooltip("Busca el Script ProximidadObjetivos del jugador")] private ProximidadObjetivos proximityTarget;

    public int Spawn(int count, ProximidadObjetivos[] proximityListeners)
    {
        if (spawnPoints == null || spawnPoints.Length == 0) return 0;
        var spawnIndices = new List<int>(spawnPoints.Length);
        for (int i = 0; i < spawnPoints.Length; i++) spawnIndices.Add(i);

        System.Random rnd = new System.Random();
        for (int i = spawnIndices.Count - 1; i > 0; i--)
        {
            int j = rnd.Next(i + 1);
            int tmp = spawnIndices[i]; spawnIndices[i] = spawnIndices[j]; spawnIndices[j] = tmp;
        }

        List<int> prefabIndices = new List<int>();
        if (prefabs != null && prefabs.Length > 0)
        {
            for (int i = 0; i < prefabs.Length; i++) prefabIndices.Add(i);
            for (int i = prefabIndices.Count - 1; i > 0; i--)
            {
                int j = rnd.Next(i + 1);
                int tmp = prefabIndices[i]; prefabIndices[i] = prefabIndices[j]; prefabIndices[j] = tmp;
            }
        }

        int spawned = 0;
        int cursor = 0;
        while (spawned < count && cursor < spawnIndices.Count)
        {
            int spawnIndex = spawnIndices[cursor++];
            Transform spawn = spawnPoints[spawnIndex];
            if (spawn == null) continue;

            int pickedIndex = -1;
            GameObject prefab = null;
            if (prefabIndices != null && prefabIndices.Count > 0)
            {
                pickedIndex = prefabIndices[0];
                prefabIndices.RemoveAt(0);
                prefab = prefabs[pickedIndex];
            }
            else if (prefabs != null && prefabs.Length > 0)
            {
                pickedIndex = rnd.Next(prefabs.Length);
                prefab = prefabs[pickedIndex];
            }

            if (prefab == null) continue;

            var go = Instantiate(prefab, spawn.position, spawn.rotation);
            go.SetActive(true);
            go.transform.SetParent(null);

            // Ensure spawned object has active colliders so projectors can detect it.
            var childColliders = go.GetComponentsInChildren<Collider>(true);
            if (childColliders != null && childColliders.Length > 0)
            {
                foreach (var c in childColliders)
                {
                    if (c != null)
                        c.enabled = true;
                }
            }
            else
            {
                // If no collider found, add a BoxCollider on the root as a fallback
                var bc = go.AddComponent<BoxCollider>();
                bc.isTrigger = false;
            }

            // Ensure the spawned object is marked as placeable with the prefab index.
            // If the prefab already contains a PlaceableSculpture with a serialized index, respect it.
            var placeable = go.GetComponentInChildren<PlaceableSculpture>(true);
            if (placeable == null)
            {
                placeable = go.AddComponent<PlaceableSculpture>();
                if (pickedIndex >= 0)
                    placeable.Initialize(pickedIndex);
            }
            else
            {
                // If the placeable already has an index (<0 means uninitialized), only initialize when missing.
                if (placeable.PrefabIndex < 0 && pickedIndex >= 0)
                    placeable.Initialize(pickedIndex);
            }

            if (proximityTarget != null)
            {
                try { proximityTarget.AddObjective(go.transform); } catch { }
            }
            else if (proximityListeners != null && proximityListeners.Length > 0)
            {
                foreach (var pl in proximityListeners)
                {
                    if (pl == null) continue;
                    try { pl.AddObjective(go.transform); } catch { }
                }
            }

            // Configure minimap icon on the spawned object (assign prefab/camera/container)
            try
            {
                var mmIcon = go.GetComponentInChildren<MinimapIcon>(true);
                if (mmIcon != null)
                {
                    if (uiIconPrefab != null)
                        mmIcon.uiIconPrefab = uiIconPrefab;
                    if (minimapCamera != null)
                        mmIcon.minimapCamera = minimapCamera;
                    if (minimapContainer != null)
                        mmIcon.minimapContainer = minimapContainer;
                }
            }
            catch { }

            spawned++;
        }

        return spawned;
    }

    /// <summary>
    /// Variante que devuelve las instancias creadas. No altera el comportamiento de la versión
    /// que devuelve solo el contador, pero es útil para que el caller pueda configurar las instancias.
    /// </summary>
    public List<GameObject> SpawnInstances(int count, ProximidadObjetivos[] proximityListeners)
    {
        var instances = new List<GameObject>();
        if (spawnPoints == null || spawnPoints.Length == 0) return instances;
        var spawnIndices = new List<int>(spawnPoints.Length);
        for (int i = 0; i < spawnPoints.Length; i++) spawnIndices.Add(i);

        System.Random rnd = new System.Random();
        for (int i = spawnIndices.Count - 1; i > 0; i--)
        {
            int j = rnd.Next(i + 1);
            int tmp = spawnIndices[i]; spawnIndices[i] = spawnIndices[j]; spawnIndices[j] = tmp;
        }

        List<int> prefabIndices = new List<int>();
        if (prefabs != null && prefabs.Length > 0)
        {
            for (int i = 0; i < prefabs.Length; i++) prefabIndices.Add(i);
            for (int i = prefabIndices.Count - 1; i > 0; i--)
            {
                int j = rnd.Next(i + 1);
                int tmp = prefabIndices[i]; prefabIndices[i] = prefabIndices[j]; prefabIndices[j] = tmp;
            }
        }

        int spawned = 0;
        int cursor = 0;
        while (spawned < count && cursor < spawnIndices.Count)
        {
            int spawnIndex = spawnIndices[cursor++];
            Transform spawn = spawnPoints[spawnIndex];
            if (spawn == null) continue;

            int pickedIndex = -1;
            GameObject prefab = null;
            if (prefabIndices != null && prefabIndices.Count > 0)
            {
                pickedIndex = prefabIndices[0];
                prefabIndices.RemoveAt(0);
                prefab = prefabs[pickedIndex];
            }
            else if (prefabs != null && prefabs.Length > 0)
            {
                pickedIndex = rnd.Next(prefabs.Length);
                prefab = prefabs[pickedIndex];
            }

            if (prefab == null) continue;

            var go = Instantiate(prefab, spawn.position, spawn.rotation);
            go.SetActive(true);
            go.transform.SetParent(null);

            // Ensure spawned object has active colliders so projectors can detect it.
            var childColliders = go.GetComponentsInChildren<Collider>(true);
            if (childColliders != null && childColliders.Length > 0)
            {
                foreach (var c in childColliders)
                {
                    if (c != null)
                        c.enabled = true;
                }
            }
            else
            {
                // If no collider found, add a BoxCollider on the root as a fallback
                var bc = go.AddComponent<BoxCollider>();
                bc.isTrigger = false;
            }

            // Ensure the spawned object is marked as placeable with the prefab index.
            // If the prefab already contains a PlaceableSculpture with a serialized index, respect it.
            var placeable = go.GetComponentInChildren<PlaceableSculpture>(true);
            if (placeable == null)
            {
                placeable = go.AddComponent<PlaceableSculpture>();
                if (pickedIndex >= 0)
                    placeable.Initialize(pickedIndex);
            }
            else
            {
                // If the placeable already has an index (<0 means uninitialized), only initialize when missing.
                if (placeable.PrefabIndex < 0 && pickedIndex >= 0)
                    placeable.Initialize(pickedIndex);
            }

            if (proximityTarget != null)
            {
                try { proximityTarget.AddObjective(go.transform); } catch { }
            }
            else if (proximityListeners != null && proximityListeners.Length > 0)
            {
                foreach (var pl in proximityListeners)
                {
                    if (pl == null) continue;
                    try { pl.AddObjective(go.transform); } catch { }
                }
            }

            // Configure minimap icon on the spawned object (assign prefab/camera/container)
            try
            {
                var mmIcon = go.GetComponentInChildren<MinimapIcon>(true);
                if (mmIcon != null)
                {
                    if (uiIconPrefab != null)
                        mmIcon.uiIconPrefab = uiIconPrefab;
                    if (minimapCamera != null)
                        mmIcon.minimapCamera = minimapCamera;
                    if (minimapContainer != null)
                        mmIcon.minimapContainer = minimapContainer;
                }
            }
            catch { }

            instances.Add(go);

            spawned++;
        }

        return instances;
    }

}
