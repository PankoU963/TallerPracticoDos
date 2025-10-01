using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.AI;

public class TouchManager : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private GameObject player;

    [Header("Raycast / NavMesh")]
    [Tooltip("Capas que el raycast considerará al tocar (por defecto: todas)")]
    [SerializeField] private LayerMask raycastLayerMask = ~0;
    [Tooltip("Distancia máxima para samplear el NavMesh desde el punto tocado")]
    [SerializeField] private float maxSampleDistance = 1f;

    private NavMeshAgent agent;
    private PlayerInput playerInput;
    private InputAction touchPositionAction;
    private InputAction touchPressAction;

    private void Awake()
    {
        playerInput = GetComponent<PlayerInput>();
        if (playerInput == null)
        {
            Debug.LogWarning("TouchManager: no se encontró PlayerInput en este GameObject. Asegúrate de añadir uno o asignar acciones manualmente.");
        }

        if (player != null)
        {
            agent = player.GetComponent<NavMeshAgent>();
            if (agent == null)
            {
                // Intentar añadir uno automáticamente para evitar NREs. Preferible configurar en el prefab.
                agent = player.AddComponent<NavMeshAgent>();
                Debug.LogWarning("TouchManager: NavMeshAgent no encontrado en 'player'. Se añadió uno en tiempo de ejecución. Ajusta parámetros en el inspector si es necesario.");
            }
        }
        else
        {
            Debug.LogError("TouchManager: referencia 'player' no asignada.");
        }

        if (playerInput != null)
        {
            touchPositionAction = playerInput.actions.FindAction("TouchPosition");
            touchPressAction = playerInput.actions.FindAction("TouchPress");
            if (touchPositionAction == null) Debug.LogWarning("TouchManager: acción 'TouchPosition' no encontrada en PlayerInput actions.");
            if (touchPressAction == null) Debug.LogWarning("TouchManager: acción 'TouchPress' no encontrada en PlayerInput actions.");
        }
    }

    private void OnEnable()
    {
        if (touchPressAction != null)
            touchPressAction.performed += TouchPressed;
    }

    private void OnDisable()
    {
        if (touchPressAction != null)
            touchPressAction.performed -= TouchPressed;
    }

    private void TouchPressed(InputAction.CallbackContext context)
    {
        if (player == null || agent == null)
        {
            Debug.LogWarning("TouchManager: player o NavMeshAgent no configurado.");
            return;
        }

        Vector2 screenPos;
        if (touchPositionAction != null)
            screenPos = touchPositionAction.ReadValue<Vector2>();
        else if (context.control != null && context.control.device is Pointer)
            screenPos = Pointer.current.position.ReadValue();
        else
        {
            Debug.LogWarning("TouchManager: no se pudo obtener la posición de toque.");
            return;
        }

        Camera cam = Camera.main;
        if (cam == null)
        {
            Debug.LogError("TouchManager: Camera.main es null.");
            return;
        }

        Ray ray = cam.ScreenPointToRay(screenPos);
        RaycastHit hit;

        Vector3 targetPoint = Vector3.zero;
        bool havePoint = false;

        // Primero intentamos raycast contra el mundo (suelo u objetos colisionables)
        if (Physics.Raycast(ray, out hit, Mathf.Infinity, raycastLayerMask))
        {
            targetPoint = hit.point;
            havePoint = true;
        }
        else
        {
            // Si el raycast no golpea nada, proyectamos contra el plano de la Y del jugador (útil para cámaras en perspectiva)
            Plane plane = new Plane(Vector3.up, player.transform.position);
            float enter;
            if (plane.Raycast(ray, out enter))
            {
                targetPoint = ray.GetPoint(enter);
                havePoint = true;
            }
        }

        if (!havePoint)
        {
            Debug.LogWarning("TouchManager: no se pudo determinar un punto de destino desde la pantalla.");
            return;
        }

        // Ajustar destino al NavMesh cercanos
        NavMeshHit navHit;
        if (NavMesh.SamplePosition(targetPoint, out navHit, maxSampleDistance, NavMesh.AllAreas))
        {
            agent.SetDestination(navHit.position);
        }
        else
        {
            // Si no hay NavMesh cercano, intentar aun así moverse al punto directo (puede fallar si agente no puede llegar)
            agent.SetDestination(targetPoint);
            Debug.LogWarning("TouchManager: No se encontró una posición en el NavMesh cerca del punto tocado. Se usa el punto directo.");
        }
    }
}
