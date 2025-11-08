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

    [Header("Trigger / Player")]
    [Tooltip("Tag que identifica al jugador")]
    [SerializeField] private string playerTag = "Player";

    private Renderer[] targetRenderers;
    private Coroutine runningCoroutine;
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

        targetRenderers = targetChild.GetComponentsInChildren<Renderer>();
        if (targetRenderers == null || targetRenderers.Length == 0)
            Debug.LogWarning($"{name}: No se encontraron Renderers en el targetChild.");

        propID = Shader.PropertyToID(dissolveProperty);

        // Inicializa la propiedad en 0 (visible). Ajusta si tu shader espera otro rango.
        SetDissolveValue(0f);
        currentValue = 0f;
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!other.CompareTag(playerTag)) return;
        StartDissolveTo(1f); // disolver al entrar
    }

    private void OnTriggerExit(Collider other)
    {
        if (!other.CompareTag(playerTag)) return;
        if (reverseOnExit)
            StartDissolveTo(0f); // revertir al salir si está activado
    }

    private void StartDissolveTo(float target)
    {
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
