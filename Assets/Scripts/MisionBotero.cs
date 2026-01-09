using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

public class MisionBotero : MonoBehaviour
{
    [Header("Components")]
    [SerializeField] private EmissionController emissionController;
    [SerializeField] private MissionUIController uiController;
    [SerializeField] private SculptureSpawner spawner;

    [Header("Mission")]
    [SerializeField] private string playerTag = "Player";
    [SerializeField] private KeyCode acceptKey = KeyCode.E;
    [SerializeField] private GameObject doorReference;
    [SerializeField] private int miniaturasToSpawn = 4;

    private bool isPlayerInside = false;
    private int totalInitialSculptures = 0;
    private int sculpturesRemaining = 0;
    private bool missionAvailable = false;
    private bool missionAccepted = false;

    private ProximidadObjetivos[] proximityListeners;
    private ProyectorSlot[] projectorSlots;

    // mission runtime tracking
    private int missionSpawnedCount = 0;
    private int hologramsPlaced = 0;
    // Progress event: (collected, total)
    public event Action<int,int> OnMissionProgressChanged;

    // Evento global que notifica cuando la misión se completa (estatuas colocadas)
    public static event Action OnMissionCompleted;

    private void Start()
    {
        emissionController?.Initialize();
        uiController?.Initialize(AcceptMission, CloseMissionUI);

        if (doorReference != null && MissionManager.Instance != null)
            MissionManager.Instance.SetDoor(doorReference);

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
        sculpturesRemaining = sculptures.Count(s => s != null && !s.IsDissolved);

        SculptureDisolve.OnSculptureDissolved += HandleSculptureDissolved;
        SculptureDisolve.OnSculptureCreated += HandleNewSculpture;

        var proxList = new List<ProximidadObjetivos>();
        for (int si2 = 0; si2 < SceneManager.sceneCount; si2++)
        {
            var scene = SceneManager.GetSceneAt(si2);
            if (!scene.isLoaded) continue;
            var roots = scene.GetRootGameObjects();
            for (int ri2 = 0; ri2 < roots.Length; ri2++)
            {
                var root = roots[ri2];
                if (root == null) continue;
                proxList.AddRange(root.GetComponentsInChildren<ProximidadObjetivos>(true));
            }
        }
        proximityListeners = proxList.ToArray();

        // find all projector slots in loaded scenes and subscribe to their events
        var projList = new List<ProyectorSlot>();
        for (int si3 = 0; si3 < SceneManager.sceneCount; si3++)
        {
            var scene = SceneManager.GetSceneAt(si3);
            if (!scene.isLoaded) continue;
            var roots = scene.GetRootGameObjects();
            for (int ri3 = 0; ri3 < roots.Length; ri3++)
            {
                var root = roots[ri3];
                if (root == null) continue;
                projList.AddRange(root.GetComponentsInChildren<ProyectorSlot>(true));
            }
        }
        projectorSlots = projList.ToArray();

        // subscribe to proximity listeners to track pickups (optional, only if they expose events)
        foreach (var pl in proximityListeners)
        {
            if (pl == null) continue;
            pl.OnObjectiveCollected += HandleObjectiveCollected;
            pl.OnAllObjectivesCollected += HandleAllObjectivesCollected;
        }

        // subscribe to hologram activation events
        foreach (var ps in projectorSlots)
        {
            if (ps == null) continue;
            if (ps.OnHologramActivated != null)
                ps.OnHologramActivated.AddListener(HandleHologramActivated);
        }

        if (totalInitialSculptures == 0)
            EnableMissionPoint();
    }

    private void OnDestroy()
    {
        SculptureDisolve.OnSculptureDissolved -= HandleSculptureDissolved;
        SculptureDisolve.OnSculptureCreated -= HandleNewSculpture;
        // unsubscribe proximity listeners
        if (proximityListeners != null)
        {
            foreach (var pl in proximityListeners)
            {
                if (pl == null) continue;
                pl.OnObjectiveCollected -= HandleObjectiveCollected;
                pl.OnAllObjectivesCollected -= HandleAllObjectivesCollected;
            }
        }

        // unsubscribe projector listeners
        if (projectorSlots != null)
        {
            foreach (var ps in projectorSlots)
            {
                if (ps == null) continue;
                if (ps.OnHologramActivated != null)
                    ps.OnHologramActivated.RemoveListener(HandleHologramActivated);
            }
        }
    }

    private void HandleSculptureDissolved(SculptureDisolve s)
    {
        sculpturesRemaining = Mathf.Max(0, sculpturesRemaining - 1);
        if (sculpturesRemaining == 0 && !missionAvailable && !missionAccepted)
            EnableMissionPoint();
    }

    private void HandleNewSculpture(SculptureDisolve s)
    {
        if (s == null) return;
        totalInitialSculptures++;
        if (!s.IsDissolved) sculpturesRemaining++;
    }

    private void HandleObjectiveCollected(Transform t)
    {
        sculpturesRemaining = Mathf.Max(0, sculpturesRemaining - 1);
        // Debug log removed for build cleanliness
        // Notify listeners about progress: collected = total - remaining
        int collected = missionSpawnedCount - sculpturesRemaining;
        OnMissionProgressChanged?.Invoke(collected, missionSpawnedCount);
    }

    private void HandleAllObjectivesCollected()
    {
        sculpturesRemaining = 0;
        // Debug log removed for build cleanliness
        int collected = missionSpawnedCount;
        OnMissionProgressChanged?.Invoke(collected, missionSpawnedCount);
    }

    private void HandleHologramActivated(int prefabIndex)
    {
        hologramsPlaced++;
        // Debug log removed for build cleanliness
        if (missionSpawnedCount > 0 && hologramsPlaced >= missionSpawnedCount)
        {
            CompleteMission();
        }
    }

    private void CompleteMission()
    {
        // Debug log removed for build cleanliness
        // abrir la puerta o notificar al MissionManager según convenga
        if (MissionManager.Instance != null)
        {
            // por defecto, pedimos que la puerta se cierre (CloseDoor = abrir salida)
            MissionManager.Instance.CloseDoor();
        }
        // limpiar estado de misión
        missionAccepted = false;
        missionAvailable = false;
        uiController?.Hide();
        emissionController?.Disable();

        // Notificar a listeners (teleporters, etc.) que reactiven
        try { OnMissionCompleted?.Invoke(); } catch { }
    }

    private void EnableMissionPoint()
    {
        missionAvailable = true;
        emissionController?.Enable();
    }

    public void CloseMissionUI()
    {
        uiController?.Hide();
        if (missionAccepted)
        {
            if (MissionManager.Instance != null)
                MissionManager.Instance.AcceptMission();
            else
            {
                // Debug warning removed for build cleanliness
            }
        }
    }

    private void Update()
    {
        if (isPlayerInside && missionAvailable && !missionAccepted)
        {
            if (Input.GetKeyDown(acceptKey))
                AcceptMission();
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!other.CompareTag(playerTag)) return;
        isPlayerInside = true;
        if (!missionAvailable) return;
        if (missionAccepted) return;
        uiController?.Show();
    }

    private void OnTriggerExit(Collider other)
    {
        if (!other.CompareTag(playerTag)) return;
        isPlayerInside = false;
        uiController?.Hide();
    }

    public void AcceptMission()
    {
        if (!missionAvailable || missionAccepted)
        {
            // Debug warning removed for build cleanliness
            return;
        }
        missionAccepted = true;
        missionAvailable = false;

        uiController?.Hide();
        emissionController?.Disable();

        if (MissionManager.Instance != null)
            MissionManager.Instance.AcceptMission();
        else
        {
            // Debug warning removed for build cleanliness
        }

        int spawnCount = Mathf.Clamp(miniaturasToSpawn, 0, 9999);
        var instances = new List<GameObject>();
        if (spawner != null)
            instances = spawner.SpawnInstances(spawnCount, proximityListeners);

        missionSpawnedCount = instances != null ? instances.Count : 0;
        hologramsPlaced = 0;

        sculpturesRemaining = missionSpawnedCount;

        // Notify initial progress (0 collected)
        OnMissionProgressChanged?.Invoke(0, missionSpawnedCount);

        if (spawner == null || missionSpawnedCount == 0)
        {
            // Debug warning removed for build cleanliness
        }

    }
}
