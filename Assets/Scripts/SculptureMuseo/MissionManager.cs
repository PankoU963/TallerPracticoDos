using UnityEngine;

public class MissionManager : MonoBehaviour
{
    public static MissionManager Instance { get; private set; }

    [Tooltip("La puerta que se activará cuando la condición se cumpla")]
    [SerializeField] private GameObject door;

    [Tooltip("Solo abrir la puerta si el jugador aceptó la misión")]
    [SerializeField] private bool missionAccepted = false;

    // indica que ya procesamos la apertura por la primera escultura desactivada o por cuadro
    private bool doorOpened = false;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            gameObject.SetActive(false);
            return;
        }
        Instance = this;
        // Inicialmente la puerta está cerrada/desactivada
        if (door != null) door.SetActive(false);
    }

    // Llamar desde UI / sistema de misión cuando el jugador acepta la misión
    public void AcceptMission()
    {
        missionAccepted = true;

        // When the player accepts the mission we want the museum door to be closed/disabled.
        if (door != null)
        {
            Debug.Log($"MissionManager.AcceptMission(): door='{door.name}', activeBefore={door.activeSelf}, doorOpenedFlag={doorOpened}");
            door.SetActive(false);
            // mark as closed
            doorOpened = false;
            Debug.Log($"MissionManager.AcceptMission(): door.SetActive(false) called. activeNow={door.activeSelf}");

            // Verify next frame that nothing re-enabled the door (helps catch other scripts reactivating it)
            // Use a short coroutine to check in the next frame.
            if (door != null)
                StartCoroutine(CheckDoorStateNextFrame());
        }
        else
        {
            Debug.LogWarning("MissionManager: misión aceptada, pero 'door' no está asignada en el Inspector.");
        }
    }

    private System.Collections.IEnumerator CheckDoorStateNextFrame()
    {
        yield return null; // wait one frame
        if (door == null) yield break;
        if (door.activeSelf)
        {
            Debug.LogWarning($"MissionManager: después de AcceptMission el GameObject '{door.name}' sigue activo. Algo lo reactivó.");
        }
        else
        {
            Debug.Log($"MissionManager: verificación: '{door.name}' está inactivo después de AcceptMission (OK).");
        }
    }

    // Antes: esta función abría la puerta cuando una escultura se desactivaba.
    // Ahora NO abre la puerta. Se deja para notificar otros sistemas si lo deseas.
    public void NotifySculptureDeactivated()
    {
        Debug.Log($"MissionManager: NotifySculptureDeactivated() llamada. (no abre puerta automáticamente ahora).");
        // opcional: puedes registrar que una escultura fue destruida, pero no abrimos la puerta aquí.
    }

    // Nueva API: abrir la puerta por interacción explícita (p.ej. por ActivacionCuadro.Interact)
    public void OpenDoor()
    {
        if (doorOpened)
        {
            Debug.Log("MissionManager: OpenDoor() llamado pero la puerta ya está abierta.");
            return;
        }

        if (door != null)
        {
            // Activate the door GameObject to make the door appear (close the museum)
            door.SetActive(true);
            doorOpened = true;
            Debug.Log("MissionManager: puerta activada (apareció) vía OpenDoor().");
        }
        else
        {
            Debug.LogWarning("MissionManager: 'door' no asignada en el Inspector en OpenDoor().");
        }
    }

    public void CloseDoor()
    {
        if (!doorOpened)
        {
            Debug.Log("MissionManager: CloseDoor() llamado pero la puerta ya está cerrada (ya abierta la salida).");
            return;
        }

        if (door != null)
        {
            // Deactivate the door GameObject to open the museum exit
            door.SetActive(false);
            doorOpened = false;
            Debug.Log("MissionManager: puerta desactivada (salida abierta) vía CloseDoor().");
        }
        else
        {
            Debug.LogWarning("MissionManager: 'door' no asignada en el Inspector en CloseDoor().");
        }
    }

    public bool IsMissionAccepted() => missionAccepted;
    public bool IsDoorOpened() => doorOpened;

    /// <summary>
    /// Allow other scripts to assign the door GameObject at runtime via code or inspector helper.
    /// If assigned, the door will be deactivated initially (open) to match existing Awake behaviour.
    /// </summary>
    public void SetDoor(GameObject doorObj)
    {
        door = doorObj;
        if (door != null)
        {
            door.SetActive(false);
            doorOpened = false;
            Debug.Log("MissionManager: Door assigned via SetDoor(); door deactivated by default.");
        }
        else
        {
            Debug.LogWarning("MissionManager.SetDoor called with null.");
        }
    }
}