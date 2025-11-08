using UnityEngine;
using System.Collections;

public class SculptureDisolve : MonoBehaviour
{
    [Header("Target")]
    [Tooltip("El objeto hijo que debe disolverse. Si está vacío se toma el primer hijo.")]
    [SerializeField] private GameObject targetChild;

    [Header("Dissolve")]
    [Tooltip("Nombre de la propiedad float del shader que controla el dissolve")]
    [SerializeField] private string dissolveProperty = "_Dissolve";
    [Tooltip("Velocidad de animación (unidades por segundo)")]
    [SerializeField] private float dissolveSpeed = 1f;
    [SerializeField] private bool reverseOnExit = false;
    [Tooltip("Si está activo, al completar el dissolve el objeto targetChild se desactiva.")]
    [SerializeField] private bool deactivateWhenDissolved = true;

    [Header("Trigger / Player")]
    [Tooltip("Tag que identifica al jugador")]
    [SerializeField] private string playerTag = "Player";

    private Renderer[] targetRenderers;
    private Coroutine runningCoroutine;
    private Coroutine hideCoroutine; // <--- nueva coroutine para esconder después de delay
    private int propID;
    private float currentValue = 0f;

    private void Awake()
    {
        if (targetChild == null && transform.childCount > 0)
            targetChild = transform.GetChild(0).gameObject;

        if (targetChild == null)
        {
            Debug.LogWarning($"{name}: No targetChild asignado y no hay hijos.");
            return;
        }

        // Aseguramos que el target esté activo al inicio (visible)
        if (!targetChild.activeSelf) targetChild.SetActive(true);

        targetRenderers = targetChild.GetComponentsInChildren<Renderer>();
        if (targetRenderers == null || targetRenderers.Length == 0)
            Debug.LogWarning($"{name}: No se encontraron Renderers en el targetChild.");

        propID = Shader.PropertyToID(dissolveProperty);

        // Inicializa la propiedad en 0 (visible).
        SetDissolveValue(0f);
        currentValue = 0f;
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!other.CompareTag(playerTag)) return;

        // iniciar dissolve (mantengo la llamada existente)
        StartDissolveTo(1f);

        // cancelar cualquier hide pendiente y arrancar nuevo hide después de 2s
        if (hideCoroutine != null) StopCoroutine(hideCoroutine);
        hideCoroutine = StartCoroutine(HideAfterDelay(2f));
    }

    private void OnTriggerExit(Collider other)
    {
        if (!other.CompareTag(playerTag)) return;

        // Si queremos revertir el dissolve al salir
        if (reverseOnExit)
            StartDissolveTo(0f);

        // cancelar la ocultación si el jugador sale antes de los 2s
        if (hideCoroutine != null)
        {
            StopCoroutine(hideCoroutine);
            hideCoroutine = null;
        }
    }

    private void StartDissolveTo(float target)
    {
        // Si vamos a animar hacia visible, aseguramos que el objeto esté activo
        if (targetChild != null && target < 1f && !targetChild.activeSelf)
            targetChild.SetActive(true);

        if (runningCoroutine != null) StopCoroutine(runningCoroutine);
        runningCoroutine = StartCoroutine(AnimateDissolve(target));
    }

    private IEnumerator AnimateDissolve(float target)
    {
        while (!Mathf.Approximately(currentValue, target))
        {
            currentValue = Mathf.MoveTowards(currentValue, target, dissolveSpeed * Time.deltaTime);
            SetDissolveValue(currentValue);
            yield return null;
        }
        runningCoroutine = null;

        // Mantengo la lógica previa por compatibilidad (hay inconsistencias de valores en shader).
        if (Mathf.Approximately(target, -1f) && deactivateWhenDissolved)
        {
            if (targetChild != null)
                targetChild.SetActive(false);
        }
    }

    // nueva coroutine: espera segundos y luego desactiva el hijo
    private IEnumerator HideAfterDelay(float seconds)
    {
        yield return new WaitForSeconds(seconds);

        if (deactivateWhenDissolved && targetChild != null)
        {
            targetChild.SetActive(false);
            Debug.Log($"{name}: targetChild desactivado.");
        }
        else
        {
            Debug.Log($"{name}: HideAfterDelay terminó (no se desactivó por deactivateWhenDissolved={deactivateWhenDissolved}).");
        }

        // NOTIFICAR al MissionManager siempre que termine el hide (para cubrir casos en que
        // la desactivación ocurra aquí o que otro sistema de desactivación esté presente).
        if (MissionManager.Instance != null)
        {
            Debug.Log($"{name}: Notificando a MissionManager.");
            MissionManager.Instance.NotifySculptureDeactivated();
        }
        else
        {
            Debug.LogWarning($"{name}: MissionManager.Instance es null. Asegura que el MissionManager esté en la escena.");
        }

        hideCoroutine = null;
    }

    private void SetDissolveValue(float value)
    {
        if (targetRenderers == null) return;

        foreach (var rend in targetRenderers)
        {
            var mpb = new MaterialPropertyBlock();
            rend.GetPropertyBlock(mpb);
            mpb.SetFloat(propID, value);
            rend.SetPropertyBlock(mpb);
        }
    }
}
