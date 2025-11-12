using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

/// <summary>
/// Script attached to the "Mision Info Point" object.
/// - Listens for sculptures finishing their dissolve and, when all are gone, enables an emissive highlight.
/// - When the player enters its trigger it shows a mission canvas. Accepting the mission will call MissionManager.AcceptMission()
///   (which will deactivate the door) and will spawn a configurable number of sculptures at random spawn points.
///
/// Usage:
/// - Assign the point's Renderer to `infoRenderer`.
/// - Assign `missionCanvas` (UI panel with accept button). Hook the button to `AcceptMission()`.
/// - Configure `sculpturePrefabs` and `spawnPoints` for respawning.
///
public class MisionBotero : MonoBehaviour
{
    [Header("Visual / Emission")]
    [SerializeField] private Renderer infoRenderer;
    [Tooltip("If empty, all Renderers on this GameObject and its children will be used")]
    [SerializeField] private bool includeChildRenderers = true;
    [SerializeField] private Color emissionColor = Color.yellow;
    [SerializeField, Tooltip("Intensity when 'off' (usually 0)")] private float emissionIntensityOff = 0f;
    [SerializeField, Tooltip("Intensity when mission becomes available")] private float emissionIntensityOn = 2f;

    [Header("Mission UI")]
    [SerializeField] private GameObject missionCanvas; // panel that shows mission text + accept button
    [SerializeField] private string playerTag = "Player";
    [Tooltip("Optional: assign the museum door here if you prefer to wire it from the Mision Info Point. This will call MissionManager.SetDoor at Start.")]
    [SerializeField] private GameObject doorReference;

    [Header("Respawn / Sculptures")]
    [SerializeField, Tooltip("Prefabs to spawn when the player accepts the mission")] private GameObject[] miniaturasPrefabs;
    [SerializeField, Tooltip("Possible spawn locations for respawned sculptures")] private Transform[] spawnPoints;
    [SerializeField, Tooltip("Optional: prefab for a house miniature to instantiate when the mission is accepted")] private GameObject miniaturaCasaPrefab;
    [SerializeField, Tooltip("Optional: spawn location for the house miniature. If null and doorReference is assigned, doorReference.transform will be used.")] private Transform casaSpawnPoint;
    [SerializeField, Tooltip("How many sculptures to spawn when mission accepted")]
    private int miniaturasToSpawn = 4;

    // runtime
    private List<Renderer> infoRenderers = new List<Renderer>();
    private List<Material> infoMaterials = new List<Material>();
    private int totalInitialSculptures = 0;
    private int sculpturesRemaining = 0;
    private bool missionAvailable = false;
    private bool missionAccepted = false;

    private void Start()
    {
        // collect renderers and instance materials so emission changes affect children too
        if (infoRenderer != null)
        {
            if (includeChildRenderers)
            {
                infoRenderers.AddRange(infoRenderer.GetComponentsInChildren<Renderer>(true));
            }
            else
            {
                infoRenderers.Add(infoRenderer);
            }

            infoMaterials.Clear();
            foreach (var r in infoRenderers)
            {
                if (r == null) continue;
                var mat = r.material; // creates instance
                if (mat != null)
                {
                    infoMaterials.Add(mat);
                }
            }

            SetEmissionIntensity(emissionIntensityOff);
        }

        if (missionCanvas != null)
            missionCanvas.SetActive(false);

        // If this info point has a door reference assigned, set it on the MissionManager so the manager controls it.
        if (doorReference != null && MissionManager.Instance != null)
        {
            MissionManager.Instance.SetDoor(doorReference);
        }

        // Diagnostic logs: report MissionManager state and door wiring
        Debug.Log($"MisionBotero.Start(): MissionManager.Instance={(MissionManager.Instance!=null)}, doorReference={(doorReference!=null ? doorReference.name : "NULL")}");

        // Auto-wire UI buttons (convenience): if the missionCanvas contains Buttons named Accept/Aceptar or Close/Cancelar,
        // attach AcceptMission and CloseMissionUI so the UI works even if the user forgot to hook the OnClick in the Inspector.
        if (missionCanvas != null)
        {
            var buttons = missionCanvas.GetComponentsInChildren<Button>(true);
            foreach (var b in buttons)
            {
                if (b == null) continue;
                string n = b.gameObject.name.ToLowerInvariant();
                // try to detect accept button
                if (n.Contains("accept") || n.Contains("acept"))
                {
                    b.onClick.RemoveListener(AcceptMission);
                    b.onClick.AddListener(AcceptMission);
                    Debug.Log($"MisionBotero: Auto-wired AcceptMission to button '{b.gameObject.name}'");
                }
                // try to detect close/cancel button
                if (n.Contains("close") || n.Contains("cancel") || n.Contains("cerrar") || n.Contains("cancelar"))
                {
                    b.onClick.RemoveListener(CloseMissionUI);
                    b.onClick.AddListener(CloseMissionUI);
                    Debug.Log($"MisionBotero: Auto-wired CloseMissionUI to button '{b.gameObject.name}'");
                }
            }
            // If the canvas uses an ImagePopupController, wire its OnClosed event to CloseMissionUI
            var ipc = missionCanvas.GetComponentInChildren<ImagePopupController>(true);
            if (ipc != null)
            {
                ipc.OnClosed.RemoveAllListeners();
                ipc.OnClosed.AddListener(CloseMissionUI);
                Debug.Log($"MisionBotero: Auto-wired ImagePopupController.OnClosed to CloseMissionUI (component '{ipc.gameObject.name}')");
            }
        }

        // Warn if MissionManager exists but no door is assigned there; user should assign one either in MissionManager or here
        if (MissionManager.Instance != null && MissionManager.Instance.IsDoorOpened() == false && MissionManager.Instance != null && MissionManager.Instance.IsMissionAccepted() == false && MissionManager.Instance != null)
        {
            // no-op; kept for clarity. If you want a warning when no door assigned, uncomment below.
            // Debug.LogWarning("MisionBotero: MissionManager exists but door may not be assigned. Consider assigning a door in the MissionManager Inspector or assigning doorReference on this component.");
        }

        // Subscribe to sculpture dissolve events and count current sculptures in scene
        // We need to include inactive objects as well, otherwise if sculptures are present but disabled
        // the mission will be incorrectly considered available (totalInitialSculptures == 0).
        // Find all SculptureDisolve instances in loaded scenes (include inactive)
        // We avoid the obsolete FindObjectsOfType(bool) API and instead walk root GameObjects
        // of each loaded scene and collect components (including inactive ones).
        var sculptureList = new List<SculptureDisolve>();
        for (int si = 0; si < SceneManager.sceneCount; si++)
        {
            var scene = SceneManager.GetSceneAt(si);
            if (!scene.isLoaded) continue;
            var roots = scene.GetRootGameObjects();
            for (int ri = 0; ri < roots.Length; ri++)
            {
                var root = roots[ri];
                if (root == null) continue;
                sculptureList.AddRange(root.GetComponentsInChildren<SculptureDisolve>(true));
            }
        }
        var sculptures = sculptureList.ToArray();
        totalInitialSculptures = sculptures.Length;
        // Count only those that have NOT already dissolved (in case some dissolved before this Start ran)
        sculpturesRemaining = sculptures.Count(s => s != null && !s.IsDissolved);
        Debug.Log($"MisionBotero: found {totalInitialSculptures} SculptureDisolve instances; remaining (not yet dissolved) = {sculpturesRemaining}");
        // List discovered sculptures for debugging (name + dissolved state)
        for (int i = 0; i < sculptures.Length; i++)
        {
            var s = sculptures[i];
            if (s == null) continue;
            Debug.Log($"MisionBotero: sculpture[{i}] = '{s.gameObject.name}', IsDissolved={s.IsDissolved}");
        }

        // Subscribe after we sampled the current state
        SculptureDisolve.OnSculptureDissolved += HandleSculptureDissolved;
        SculptureDisolve.OnSculptureCreated += HandleNewSculpture;

        // If there are no sculptures at start, we consider the mission info point available immediately
        if (totalInitialSculptures == 0)
            EnableMissionPoint();
    }

    private void OnDestroy()
    {
        SculptureDisolve.OnSculptureDissolved -= HandleSculptureDissolved;
        SculptureDisolve.OnSculptureCreated -= HandleNewSculpture;
    }

    private void HandleSculptureDissolved(SculptureDisolve s)
    {
        // decrement remaining and when reaches zero, enable the mission point
        sculpturesRemaining = Mathf.Max(0, sculpturesRemaining - 1);
        Debug.Log($"MisionBotero: Sculpture dissolved -> remaining {sculpturesRemaining}");
        if (sculpturesRemaining == 0 && !missionAvailable && !missionAccepted)
        {
            EnableMissionPoint();
        }
    }

    private void HandleNewSculpture(SculptureDisolve s) {
        if (s == null) return;
        totalInitialSculptures++;
        if (!s.IsDissolved) sculpturesRemaining++;
    }

    private void EnableMissionPoint()
    {
        missionAvailable = true;
        if (infoMaterials != null && infoMaterials.Count > 0)
        {
            SetEmissionIntensity(emissionIntensityOn);
            foreach (var m in infoMaterials)
                m.EnableKeyword("_EMISSION");
        }
        Debug.Log("MisionBotero: Misión disponible — activado punto de información (emisión ON).");
    }

    private void SetEmissionIntensity(float intensity)
    {
        if (infoMaterials == null || infoMaterials.Count == 0) return;
        Color final = emissionColor * Mathf.LinearToGammaSpace(intensity);
        foreach (var infoMat in infoMaterials)
        {
            if (infoMat.HasProperty("_EmissionColor"))
                infoMat.SetColor("_EmissionColor", final);
            else if (infoMat.HasProperty("_Emission"))
                infoMat.SetColor("_Emission", final);
        }

        // Try to update realtime GI without forcing a dependency
        try
        {
            var dynType = AppDomain.CurrentDomain.GetAssemblies()
                .Select(a => a.GetType("UnityEngine.Experimental.GlobalIllumination.DynamicGI"))
                .FirstOrDefault(t => t != null);
            if (dynType != null)
            {
                var method = dynType.GetMethod("SetEmissive", System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.Public);
                if (method != null && infoRenderers != null && infoRenderers.Count > 0)
                {
                    // update GI for each renderer instance we modified
                    foreach (var r in infoRenderers)
                    {
                        if (r != null)
                            method.Invoke(null, new object[] { r, final });
                    }
                }
            }
        }
        catch { }
    }

    /// <summary>
    /// Close the mission UI without accepting. Hook this to a 'Cancel' or 'Close' button.
    /// </summary>
    public void CloseMissionUI()
    {
        if (missionCanvas != null)
            missionCanvas.SetActive(false);
        
        // If the player already accepted the mission, make sure MissionManager processes it
        // (this will deactivate the door if appropriate).
        if (missionAccepted)
        {
            if (MissionManager.Instance != null)
            {
                // Ensure mission state is processed; AcceptMission will deactivate the door (open exit)
                MissionManager.Instance.AcceptMission();
            }
            else
            {
                Debug.LogWarning("MisionBotero: MissionManager no encontrado al cerrar el UI tras aceptar la misión.");
            }
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!other.CompareTag(playerTag)) return;
        if (!missionAvailable) return;
        if (missionAccepted) return;

        if (missionCanvas != null)
            missionCanvas.SetActive(true);
    }

    private void OnTriggerExit(Collider other)
    {
        if (!other.CompareTag(playerTag)) return;
        if (missionCanvas != null)
            missionCanvas.SetActive(false);
    }

    /// <summary>
    /// Call from UI button when the player accepts the mission.
    /// </summary>
    public void AcceptMission()
    {
        Debug.Log($"MisionBotero.AcceptMission() called. missionAvailable={missionAvailable}, missionAccepted={missionAccepted}");
        if (!missionAvailable || missionAccepted)
        {
            Debug.LogWarning("MisionBotero: No se puede aceptar la misión en el estado actual.");
            return;
        }
        missionAccepted = true;
        missionAvailable = false;

        if (missionCanvas != null)
            missionCanvas.SetActive(false);

        // turn emission off or reduce it to indicate mission in-progress
        SetEmissionIntensity(emissionIntensityOff);

        // Tell MissionManager (it will deactivate the door per its AcceptMission implementation)
        if (MissionManager.Instance != null)
        {
            MissionManager.Instance.AcceptMission();
        }
        else
        {
            Debug.LogWarning("MisionBotero: MissionManager no encontrado al aceptar la misión.");
        }

        // Spawn sculptures randomly
        int spawnCount = Mathf.Clamp(miniaturasToSpawn, 0, (spawnPoints != null) ? spawnPoints.Length : 0);
        Debug.Log($"MisionBotero: intentando spawnear {spawnCount} esculturas. miniaturasPrefabs={(miniaturasPrefabs!=null?miniaturasPrefabs.Length:0)}, spawnPoints={(spawnPoints!=null?spawnPoints.Length:0)}");
        if (spawnCount > 0 && miniaturasPrefabs != null && miniaturasPrefabs.Length > 0 && spawnPoints != null && spawnPoints.Length > 0)
        {
            SpawnSculpturesRandom(spawnCount);
            sculpturesRemaining = spawnCount; // start tracking for the new set
        }
        else
        {
            Debug.LogWarning("MisionBotero: No hay prefabs o spawnPoints configurados para generar esculturas.");
        }

        // Optional: instantiate a house miniature if configured
        if (miniaturaCasaPrefab != null)
        {
            Transform spawnForHouse = casaSpawnPoint != null ? casaSpawnPoint : (doorReference != null ? doorReference.transform : null);
            Vector3 pos = spawnForHouse != null ? spawnForHouse.position : transform.position;
            Quaternion rot = spawnForHouse != null ? spawnForHouse.rotation : Quaternion.identity;

            var house = Instantiate(miniaturaCasaPrefab, pos, rot);
            house.SetActive(true);
            house.transform.SetParent(null);
            Debug.Log($"MisionBotero: spawned house miniatura '{miniaturaCasaPrefab.name}' at {pos}.");
            // If the house has a SculptureDisolve component, ensure MisionBotero tracks it by incrementing counter
            var sd = house.GetComponentInChildren<SculptureDisolve>(true);
            if (sd != null)
            {
                // If we spawned new sculptures for the mission, they should be tracked by counting and event subscription;
                // update counters accordingly so the mission endpoint waits for the house to dissolve if relevant.
                totalInitialSculptures++;
                if (!sd.IsDissolved) sculpturesRemaining++;
                Debug.Log("MisionBotero: house contains SculptureDisolve — counters updated.");
            }
        }
    }

    private void SpawnSculpturesRandom(int count)
    {
        if (spawnPoints == null || spawnPoints.Length == 0)
        {
            Debug.LogWarning("MisionBotero.SpawnSculpturesRandom: no hay spawnPoints configurados.");
            return;
        }
        // Build a shuffled list of spawn indices so we don't pick the same spawn twice
        var spawnIndices = new List<int>(spawnPoints.Length);
        for (int i = 0; i < spawnPoints.Length; i++) spawnIndices.Add(i);

        System.Random rnd = new System.Random();
        // Fisher-Yates shuffle
        for (int i = spawnIndices.Count - 1; i > 0; i--)
        {
            int j = rnd.Next(i + 1);
            int tmp = spawnIndices[i]; spawnIndices[i] = spawnIndices[j]; spawnIndices[j] = tmp;
        }

        // Prepare prefab indices for selection without replacement when possible
        List<int> prefabIndices = new List<int>();
        if (miniaturasPrefabs != null && miniaturasPrefabs.Length > 0)
        {
            for (int i = 0; i < miniaturasPrefabs.Length; i++) prefabIndices.Add(i);
            // shuffle prefabs too so selection without replacement is random
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
            if (spawn == null)
            {
                Debug.LogWarning($"MisionBotero: spawn point {spawnIndex} es null, saltando.");
                continue; // try next spawn index
            }

            // choose a prefab: prefer no-repeat while we have unique prefabs available
            GameObject prefab = null;
            if (prefabIndices != null && prefabIndices.Count > 0)
            {
                int pick = prefabIndices[0];
                prefabIndices.RemoveAt(0);
                prefab = miniaturasPrefabs[pick];
            }
            else if (miniaturasPrefabs != null && miniaturasPrefabs.Length > 0)
            {
                prefab = miniaturasPrefabs[rnd.Next(miniaturasPrefabs.Length)];
            }

            if (prefab == null)
            {
                Debug.LogWarning("MisionBotero: prefab seleccionado es null, saltando.");
                continue;
            }

            var go = Instantiate(prefab, spawn.position, spawn.rotation);
            go.SetActive(true);
            go.transform.SetParent(null);

            Debug.Log($"MisionBotero: spawned '{prefab.name}' at {spawn.position} (spawnIndex={spawnIndex}).");
            spawned++;
        }

        if (spawned < count)
        {
            Debug.LogWarning($"MisionBotero: Sólo se pudieron spawnear {spawned} de {count} esculturas solicitadas (comprueba prefabs y spawnPoints no nulos).\nconfig: spawnPoints={spawnPoints.Length}, prefabs={(miniaturasPrefabs!=null?miniaturasPrefabs.Length:0)}");
        }
    }
}
