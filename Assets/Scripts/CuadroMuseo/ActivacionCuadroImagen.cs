using System;
using System.Linq;
using System.Reflection;
using UnityEngine;

public class ActivacionCuadroImagen : MonoBehaviour
{
    [Header("Frame / Emission")]
    [SerializeField] private Renderer frameRenderer;
    [SerializeField] private Color emissionColor = Color.yellow;
    [SerializeField, Tooltip("Multiplier for emission color intensity")] private float emissionIntensity = 2f;

    [Header("UI: prompt and image")]
    [SerializeField, Tooltip("GameObject que contiene el TextMeshPro (o Canvas) que se muestra cuando el cuadro es interactuable")]
    private GameObject promptObject;
    [SerializeField, Tooltip("Canvas/GameObject que contiene la imagen a mostrar cuando el jugador interactúa")]
    private GameObject imageCanvas;

    // state
    private bool isCleaned = false;
    private bool activated = false;

    [Header("Raycast Activation (optional)")]
    [SerializeField, Tooltip("Optional origin transform for the raycast (camera or flashlight). If null, Camera.main will be used at Start.")]
    private Transform rayOrigin;
    [SerializeField, Tooltip("Distance for the raycast to detect the cuadro")] private float rayRange = 6f;
    [SerializeField, Tooltip("Layer mask for the raycast; set to only hit wall/cuadro layers to avoid false positives")] private LayerMask rayMask = ~0;
    [SerializeField, Tooltip("Optional: material float property name to read on the hit renderer to detect 'clean' state. Leave empty to trigger purely by aim/dwell.")] private string materialCleanProperty = "";
    [SerializeField, Tooltip("If using material property, threshold above which it's considered clean")] private float cleanThreshold = 0.5f;
    [SerializeField, Tooltip("Seconds the player must aim at the cuadro for the raycast to activate (prevents accidental triggers)")] private float dwellTime = 0.5f;
    private float lookTimer = 0f;

    public void OnWallCleaned()
    {
        if (isCleaned) return;
        isCleaned = true;

        if (frameRenderer == null)
        {
            Debug.LogWarning("ActivacionCuadroImagen: frameRenderer not assigned.");
        }
        else
        {
            Material mat = frameRenderer.material;
            mat.EnableKeyword("_EMISSION");
            Color final = emissionColor * Mathf.LinearToGammaSpace(emissionIntensity);
            if (mat.HasProperty("_EmissionColor"))
                mat.SetColor("_EmissionColor", final);
            else if (mat.HasProperty("_Emission"))
                mat.SetColor("_Emission", final);

            try
            {
                var dynType = AppDomain.CurrentDomain.GetAssemblies()
                    .Select(a => a.GetType("UnityEngine.Experimental.GlobalIllumination.DynamicGI"))
                    .FirstOrDefault(t => t != null);
                if (dynType != null)
                {
                    var method = dynType.GetMethod("SetEmissive", BindingFlags.Static | BindingFlags.Public);
                    if (method != null)
                        method.Invoke(null, new object[] { frameRenderer, final });
                }
            }
            catch (Exception) { }
        }

        if (promptObject != null) promptObject.SetActive(true);
    }

    public void Interact()
    {
        if (!isCleaned)
        {
            Debug.Log("ActivacionCuadroImagen: not cleaned yet — cannot interact.");
            return;
        }

        if (activated) return;
        activated = true;
        OpenImage();
    }

    private void OnMouseDown()
    {
        Interact();
    }

    private void Start()
    {
        if (rayOrigin == null && Camera.main != null) rayOrigin = Camera.main.transform;
        if (promptObject != null) promptObject.SetActive(false);
        if (imageCanvas != null) imageCanvas.SetActive(false);
    }

    private void Update()
    {
        if (isCleaned) return;
        if (rayOrigin == null) return;

        Ray ray = new Ray(rayOrigin.position, rayOrigin.forward);
        if (Physics.Raycast(ray, out RaycastHit hit, rayRange, rayMask))
        {
            var hitTransform = hit.collider.transform;
            bool isTarget = hitTransform == transform || hitTransform.IsChildOf(transform);
            if (isTarget)
            {
                bool considerClean = true;
                if (!string.IsNullOrEmpty(materialCleanProperty))
                {
                    var hitRenderer = hitTransform.GetComponent<Renderer>();
                    if (hitRenderer != null && hitRenderer.material != null && hitRenderer.material.HasProperty(materialCleanProperty))
                    {
                        float val = hitRenderer.material.GetFloat(materialCleanProperty);
                        considerClean = val >= cleanThreshold;
                    }
                    else
                    {
                        considerClean = false;
                    }
                }

                if (considerClean)
                {
                    lookTimer += Time.deltaTime;
                    if (lookTimer >= dwellTime)
                    {
                        Debug.Log("ActivacionCuadroImagen: Raycast dwell satisfied — marking as cleaned.");
                        OnWallCleaned();
                    }
                }
                else
                {
                    lookTimer = 0f;
                }
                return;
            }
        }

        lookTimer = 0f;
    }

    public void OpenImage()
    {
        if (imageCanvas != null) imageCanvas.SetActive(true);
    }

    public void CloseImage()
    {
        if (imageCanvas != null) imageCanvas.SetActive(false);
        activated = false;
    }
}