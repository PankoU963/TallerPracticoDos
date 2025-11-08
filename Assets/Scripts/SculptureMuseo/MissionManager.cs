using UnityEngine;

public class MissionManager : MonoBehaviour
{
    public static MissionManager Instance { get; private set; }

    [Tooltip("La puerta que se activará cuando la condición se cumpla")]
    [SerializeField] private GameObject door;

    [Tooltip("Solo abrir la puerta si el jugador aceptó la misión")]
    [SerializeField] private bool missionAccepted = false;

    // indica que ya procesamos la apertura por la primera escultura desactivada
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

        // Cuando el jugador acepta la misión la puerta debe desactivarse si estaba abierta.
        if (doorOpened && door != null)
        {
            door.SetActive(false);
            Debug.Log("MissionManager: misión aceptada -> puerta desactivada.");
            // dejamos doorOpened en true para evitar que otras desactivaciones vuelvan a abrirla
        }
        else
        {
            Debug.Log($"MissionManager: misión aceptada. doorOpened={doorOpened}");
        }
    }

    // Notificar que una escultura se desactivó.
    // La primera notificación abrirá la puerta independientemente de missionAccepted.
    public void NotifySculptureDeactivated()
    {
        Debug.Log($"MissionManager: NotifySculptureDeactivated() llamada. missionAccepted={missionAccepted}, doorOpened={doorOpened}");

        if (doorOpened)
        {
            Debug.Log("MissionManager: ya procesada la apertura anteriormente, ignorando.");
            return;
        }

        if (door != null)
        {
            door.SetActive(true);
            Debug.Log("MissionManager: puerta activada por desactivación de escultura.");
        }
        else
        {
            Debug.LogWarning("MissionManager: 'door' no asignada en el Inspector.");
        }

        // Marcamos que ya procesamos la apertura para que otras esculturas no vuelvan a afectar la puerta
        doorOpened = true;
    }

    public bool IsMissionAccepted() => missionAccepted;
    public bool IsDoorOpened() => doorOpened;
}