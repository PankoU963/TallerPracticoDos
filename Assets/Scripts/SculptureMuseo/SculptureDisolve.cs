using UnityEngine;
using System.Collections;
using System;

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
    private bool hasDissolved = false;

    // Event raised when this sculpture has finished its dissolve/hide sequence.
    // Subscribers can track how many sculptures remain.
    public static event Action<SculptureDisolve> OnSculptureDissolved;

    public static event Action<SculptureDisolve> OnSculptureCreated;

    // Public read-only accessor so other systems can check if this sculpture already finished dissolving.
    public bool IsDissolved => hasDissolved;

    private void Awake()
    {
        if (targetChild == null && transform.childCount > 0)
            targetChild = transform.GetChild(0).gameObject;

        if (targetChild == null)
        {
            Debug.LogWarning($"{name}: No targetChild asignado y no hay hijos.");
            return;
        }

        if (!targetChild.activeSelf) targetChild.SetActive(true);

        targetRenderers = targetChild.GetComponentsInChildren<Renderer>();
        if (targetRenderers == null || targetRenderers.Length == 0)
            Debug.LogWarning($"{name}: No se encontraron Renderers en el targetChild.");

        propID = Shader.PropertyToID(dissolveProperty);

        SetDissolveValue(0f);
        currentValue = 0f;

        OnSculptureCreated?.Invoke(this);
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!other.CompareTag(playerTag)) return;

        StartDissolveTo(1f);

        if (hideCoroutine != null) StopCoroutine(hideCoroutine);
        hideCoroutine = StartCoroutine(HideAfterDelay(2f));
    }

    private void OnTriggerExit(Collider other)
    {
        if (!other.CompareTag(playerTag)) return;

        if (reverseOnExit)
            StartDissolveTo(0f);

        if (hideCoroutine != null)
        {
            StopCoroutine(hideCoroutine);
            hideCoroutine = null;
        }
    }

    private void StartDissolveTo(float target)
    {
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

        if (Mathf.Approximately(target, -1f) && deactivateWhenDissolved)
        {
            if (targetChild != null)
                targetChild.SetActive(false);
        }
    }

    private IEnumerator HideAfterDelay(float seconds)
    {
        yield return new WaitForSeconds(seconds);

        if (deactivateWhenDissolved && targetChild != null)
        {
            targetChild.SetActive(false);
            Debug.Log($"{name}: targetChild desactivado.");
            if (!hasDissolved)
            {
                hasDissolved = true;
                try
                {
                    OnSculptureDissolved?.Invoke(this);
                }
                catch (Exception ex)
                {
                    Debug.LogWarning($"{name}: error al invocar OnSculptureDissolved: {ex}");
                }
            }
        }
        else
        {
            Debug.Log($"{name}: HideAfterDelay terminado (no se desactivó por deactivateWhenDissolved={deactivateWhenDissolved}).");
        }

        // Nota: ya no notificamos a MissionManager para evitar que la puerta
        // se abra automáticamente cuando una escultura se disuelva.
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
