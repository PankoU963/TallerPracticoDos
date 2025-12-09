using System;
using System.Collections.Generic;
using UnityEngine;

public class SculptureSpawner : MonoBehaviour
{
    [SerializeField] private GameObject[] prefabs;
    [SerializeField] private Transform[] spawnPoints;
    [SerializeField] [Tooltip("Busca el Script ProximidadObjetivos del jugador")] private ProximidadObjetivos proximityTarget;
    [SerializeField] private GameObject housePrefab;
    [SerializeField] private Transform casaSpawnPoint;

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

            spawned++;
        }

        return spawned;
    }

    public GameObject SpawnHouse(Transform doorReference)
    {
        if (housePrefab == null) return null;
        Transform spawnForHouse = casaSpawnPoint != null ? casaSpawnPoint : (doorReference != null ? doorReference : null);
        Vector3 pos = spawnForHouse != null ? spawnForHouse.position : transform.position;
        Quaternion rot = spawnForHouse != null ? spawnForHouse.rotation : Quaternion.identity;
        var house = Instantiate(housePrefab, pos, rot);
        house.SetActive(true);
        house.transform.SetParent(null);
        return house;
    }
}
