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
                Debug.LogWarning("MisionBotero: MissionManager no encontrado al cerrar el UI tras aceptar la misión.");
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
            Debug.LogWarning("MisionBotero: No se puede aceptar la misión en el estado actual.");
            return;
        }
        missionAccepted = true;
        missionAvailable = false;

        uiController?.Hide();
        emissionController?.Disable();

        if (MissionManager.Instance != null)
            MissionManager.Instance.AcceptMission();
        else
            Debug.LogWarning("MisionBotero: MissionManager no encontrado al aceptar la misión.");

        int spawnCount = Mathf.Clamp(miniaturasToSpawn, 0, 9999);
        int spawned = 0;
        if (spawner != null)
            spawned = spawner.Spawn(spawnCount, proximityListeners);

        sculpturesRemaining = spawned;

        if (spawner == null || spawned == 0)
            Debug.LogWarning("MisionBotero: No se han generado esculturas (revisa prefabs y spawnPoints).");

        //var house = spawner != null ? spawner.SpawnHouse(doorReference != null ? doorReference.transform : null) : null;
        // if (house != null)
        // {
        //     var sd = house.GetComponentInChildren<SculptureDisolve>(true);
        //     if (sd != null)
        //     {
        //         totalInitialSculptures++;
        //         if (!sd.IsDissolved) sculpturesRemaining++;
        //     }
        // }
    }
}
