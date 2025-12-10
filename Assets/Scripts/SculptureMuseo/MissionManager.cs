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
            // Debug log removed for build cleanliness
            door.SetActive(false);
            // mark as closed
            doorOpened = false;
            // Debug log removed for build cleanliness

            // Verify next frame that nothing re-enabled the door (helps catch other scripts reactivating it)
            // Use a short coroutine to check in the next frame.
            if (door != null)
                StartCoroutine(CheckDoorStateNextFrame());
        }
        else
        {
            // Debug warning removed for build cleanliness
        }
    }

    private System.Collections.IEnumerator CheckDoorStateNextFrame()
    {
        yield return null; // wait one frame
        if (door == null) yield break;
        if (door.activeSelf)
        {
            // Debug warning removed for build cleanliness
        }
        else
        {
            // Debug log removed for build cleanliness
        }
    }

    // Antes: esta función abría la puerta cuando una escultura se desactivaba.
    // Ahora NO abre la puerta. Se deja para notificar otros sistemas si lo deseas.
    public void NotifySculptureDeactivated()
    {
        // Debug log removed for build cleanliness
        // opcional: puedes registrar que una escultura fue destruida, pero no abrimos la puerta aquí.
    }

    // Nueva API: abrir la puerta por interacción explícita (p.ej. por ActivacionCuadro.Interact)
    public void OpenDoor()
    {
        if (doorOpened)
        {
            // Debug log removed for build cleanliness
            return;
        }

        if (door != null)
        {
            // Activate the door GameObject to make the door appear (close the museum)
            door.SetActive(true);
            doorOpened = true;
            // Debug log removed for build cleanliness
        }
        else
        {
            // Debug warning removed for build cleanliness
        }
    }

    public void CloseDoor()
    {
        if (!doorOpened)
        {
            // Debug log removed for build cleanliness
            return;
        }

        if (door != null)
        {
            // Deactivate the door GameObject to open the museum exit
            door.SetActive(false);
            doorOpened = false;
            // Debug log removed for build cleanliness
        }
        else
        {
            // Debug warning removed for build cleanliness
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
            // Debug log removed for build cleanliness
        }
        else
        {
            // Debug warning removed for build cleanliness
        }
    }
}