using System.Collections;
using UnityEngine;

public class CinematicTrainArrival : MonoBehaviour
{
    [Header("References")]
    public CameraController cameraController;   // asignar CameraController de la escena
    public Transform trainTransform;            // transform del tren (para que la cámara lo siga)
    public Transform playerRoot;                // root del jugador (para moverlo)
    public Transform playerExitAnchor;          // punto donde debe aparecer el jugador fuera del tren

    [Header("Seat")]
    [Tooltip("Si asignas este anchor, el jugador se parenta a este punto dentro del tren durante la cinemática. Si es null se usa trainTransform.")]
    public Transform trainSeatAnchor;

    [Header("Player control")]
    [Tooltip("Componentes que deben deshabilitarse durante la cinemática (por ejemplo tu script de movimiento)")]
    public Behaviour[] playerControlsToDisable;

    [Header("Visuals / timing")]
    public bool hideRenderersDuringCinematic = true;
    public float movePlayerDuration = 1.0f;     // tiempo para desplazar al jugador fuera del tren
    public float afterMoveDelay = 0.15f;        // pequeño delay antes de restaurar controles

    [Header("Physics during cinematic")]
    [Tooltip("Si está activado, se desactivará la gravedad / física en Rigidbodies (3D/2D) dentro del playerRoot durante la cinemática.")]
    public bool disableRigidbodiesDuringCinematic = true;

    Transform originalCameraTarget;
    bool isRunning = false;
    // guardamos el parent original del jugador para restaurarlo
    Transform originalPlayerParent;
    // caches para restaurar estados físicos
    Rigidbody[] cachedRigidbodies;
    bool[] prevUseGravity3D;
    bool[] prevIsKinematic3D;

    Rigidbody2D[] cachedRigidbodies2D;
    float[] prevGravityScale2D;
    RigidbodyType2D[] prevBodyType2D;

    // CharacterController cache
    CharacterController[] cachedCharacterControllers;
    bool[] prevCharControllerEnabled;

    public void StartCinematic()
    {
        if (isRunning) return;
        StartCoroutine(RunStartCinematic());
    }

    IEnumerator RunStartCinematic()
    {
        isRunning = true;
        // guardar target original para restaurar después
        if (cameraController != null) originalCameraTarget = cameraController.target;

        // deshabilitar controles
        SetControlsEnabled(false);

        // ocultar renderers si se desea
        SetPlayerRenderersEnabled(!hideRenderersDuringCinematic);

        // apuntar cámara al tren para que siga la llegada
        if (cameraController != null && trainTransform != null)
        {
            cameraController.target = trainTransform;
            // opcional: forzar modo TopDown si quieres (descomentar)
            // cameraController.SetMode(CameraController.CameraMode.TopDown);
        }

        // Parentar el jugador al seat anchor (si existe) o al transform del tren para que se mueva con la animación
        if (playerRoot != null && (trainSeatAnchor != null || trainTransform != null))
        {
            originalPlayerParent = playerRoot.parent;
            Transform parentTo = trainSeatAnchor != null ? trainSeatAnchor : trainTransform;
            // parentar y ubicar exactamente en el anchor (mantener offset opcional)
            playerRoot.SetParent(parentTo, true);
            // si quieres mantener la posición mundial actual en vez de colocar en anchor, usa SetParent(parentTo, true)
        }

        // preparar y desactivar gravedad/física y CharacterControllers si corresponde
        if (disableRigidbodiesDuringCinematic)
        {
            CachePhysicsComponents();
            SetPhysicsDuringCinematic(true);
        }
        else
        {
            // aunque no toquemos rigidbodies, aún cacheamos CharacterControllers para deshabilitarlos
            CachePhysicsComponents();
            SetCharacterControllersEnabled(false);
        }

        yield break;
    }

    // Llamar este método desde un Animation Event al terminar la animación del tren
    public void OnTrainArrived()
    {
        if (!isRunning) return;
        StartCoroutine(RunEndCinematic());
    }

    IEnumerator RunEndCinematic()
    {
        // quitar parent para que el movimiento al anchor se haga en el espacio mundial
        if (playerRoot != null)
        {
            playerRoot.SetParent(originalPlayerParent, true);
        }

        // mover jugador desde su posición actual (dentro del tren) al anchor de salida
        if (playerRoot != null && playerExitAnchor != null)
        {
            Vector3 startPos = playerRoot.position;
            Quaternion startRot = playerRoot.rotation;
            Vector3 endPos = playerExitAnchor.position;
            Quaternion endRot = playerExitAnchor.rotation;
            float t = 0f;
            while (t < 1f)
            {
                t += Time.deltaTime / Mathf.Max(0.0001f, movePlayerDuration);
                float s = Mathf.SmoothStep(0f, 1f, t);
                playerRoot.position = Vector3.Lerp(startPos, endPos, s);
                playerRoot.rotation = Quaternion.Slerp(startRot, endRot, s);
                yield return null;
            }
        }

        // hacer visible al jugador
        SetPlayerRenderersEnabled(true);

        // restaurar física/character controllers antes de devolver control para evitar que caiga al mismo tiempo que recibe input
        if (disableRigidbodiesDuringCinematic)
        {
            SetPhysicsDuringCinematic(false);
        }
        else
        {
            SetCharacterControllersEnabled(true);
        }

        // esperar un instante para evitar saltos visuales
        yield return new WaitForSeconds(afterMoveDelay);

        // restaurar controles
        SetControlsEnabled(true);

        // restaurar cámara a seguir al jugador
        if (cameraController != null)
        {
            cameraController.target = playerRoot;
            // opcional: devolver modo por defecto o FirstPerson si quieres
            // cameraController.SetMode(CameraController.CameraMode.FirstPerson);
        }

        isRunning = false;
    }

    void SetControlsEnabled(bool enabled)
    {
        if (playerControlsToDisable != null)
        {
            foreach (var b in playerControlsToDisable)
            {
                if (b != null) b.enabled = enabled;
            }
        }
    }

    void SetPlayerRenderersEnabled(bool enabled)
    {
        if (playerRoot == null) return;
        var renderers = playerRoot.GetComponentsInChildren<Renderer>(true);
        foreach (var r in renderers) r.enabled = enabled;
    }

    // --- Física: cacheo y toggling ---------------------------------------
    void CachePhysicsComponents()
    {
        if (playerRoot == null) return;

        // 3D rigidbodies
        cachedRigidbodies = playerRoot.GetComponentsInChildren<Rigidbody>(true);
        if (cachedRigidbodies != null && cachedRigidbodies.Length > 0)
        {
            prevUseGravity3D = new bool[cachedRigidbodies.Length];
            prevIsKinematic3D = new bool[cachedRigidbodies.Length];
            for (int i = 0; i < cachedRigidbodies.Length; i++)
            {
                var rb = cachedRigidbodies[i];
                if (rb != null)
                {
                    prevUseGravity3D[i] = rb.useGravity;
                    prevIsKinematic3D[i] = rb.isKinematic;
                }
            }
        }

        // 2D rigidbodies
        cachedRigidbodies2D = playerRoot.GetComponentsInChildren<Rigidbody2D>(true);
        if (cachedRigidbodies2D != null && cachedRigidbodies2D.Length > 0)
        {
            prevGravityScale2D = new float[cachedRigidbodies2D.Length];
            prevBodyType2D = new RigidbodyType2D[cachedRigidbodies2D.Length];
            for (int i = 0; i < cachedRigidbodies2D.Length; i++)
            {
                var rb2 = cachedRigidbodies2D[i];
                if (rb2 != null)
                {
                    prevGravityScale2D[i] = rb2.gravityScale;
                    prevBodyType2D[i] = rb2.bodyType;
                }
            }
        }

        // CharacterControllers
        cachedCharacterControllers = playerRoot.GetComponentsInChildren<CharacterController>(true);
        if (cachedCharacterControllers != null && cachedCharacterControllers.Length > 0)
        {
            prevCharControllerEnabled = new bool[cachedCharacterControllers.Length];
            for (int i = 0; i < cachedCharacterControllers.Length; i++)
            {
                var cc = cachedCharacterControllers[i];
                if (cc != null) prevCharControllerEnabled[i] = cc.enabled;
            }
        }
    }

    void SetPhysicsDuringCinematic(bool cinematic)
    {
        // 3D rigidbodies: desactivar gravedad y volver kinematic para evitar fuerzas; restaurar al finalizar
        if (cachedRigidbodies != null)
        {
            for (int i = 0; i < cachedRigidbodies.Length; i++)
            {
                var rb = cachedRigidbodies[i];
                if (rb == null) continue;

                if (cinematic)
                {
                    // parar movimiento y desactivar gravedad
                    try
                    {
                        rb.linearVelocity = Vector3.zero;
                        rb.angularVelocity = Vector3.zero;
                        rb.useGravity = false;
                        rb.isKinematic = true;
                    }
                    catch { }
                }
                else
                {
                    // restaurar estados previos
                    try
                    {
                        if (i < prevIsKinematic3D.Length) rb.isKinematic = prevIsKinematic3D[i];
                        if (i < prevUseGravity3D.Length) rb.useGravity = prevUseGravity3D[i];
                    }
                    catch { }
                }
            }
        }

        // 2D rigidbodies: poner gravityScale a 0 y pausar movimiento; restaurar al finalizar
        if (cachedRigidbodies2D != null)
        {
            for (int i = 0; i < cachedRigidbodies2D.Length; i++)
            {
                var rb2 = cachedRigidbodies2D[i];
                if (rb2 == null) continue;

                if (cinematic)
                {
                    try
                    {
                        rb2.linearVelocity = Vector2.zero;
                        rb2.angularVelocity = 0f;
                        rb2.gravityScale = 0f;
                        // opcional: poner Kinematic para prevenir interacciones físicas inesperadas
                        rb2.bodyType = RigidbodyType2D.Kinematic;
                    }
                    catch { }
                }
                else
                {
                    try
                    {
                        if (i < prevGravityScale2D.Length) rb2.gravityScale = prevGravityScale2D[i];
                        if (i < prevBodyType2D.Length) rb2.bodyType = prevBodyType2D[i];
                    }
                    catch { }
                }
            }
        }

        // CharacterControllers: deshabilitar para que no interrumpan la animación/parentado
        SetCharacterControllersEnabled(!cinematic);
    }

    void SetCharacterControllersEnabled(bool enabled)
    {
        if (cachedCharacterControllers == null) return;
        for (int i = 0; i < cachedCharacterControllers.Length; i++)
        {
            var cc = cachedCharacterControllers[i];
            if (cc == null) continue;
            try
            {
                // restaurar a su valor previo si estamos habilitando
                if (enabled)
                {
                    if (i < prevCharControllerEnabled.Length) cc.enabled = prevCharControllerEnabled[i];
                    else cc.enabled = true;
                }
                else
                {
                    cc.enabled = false;
                }
            }
            catch { }
        }
    }
}