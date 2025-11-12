using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;

public class ActivacionCuadro : MonoBehaviour
{
    [Header("Frame / Emission")]
    [Tooltip("Renderer of the frame whose emission will be enabled when the wall is cleaned")]
    [SerializeField] private Renderer frameRenderer;
    [SerializeField] private Color emissionColor = Color.yellow;
    [SerializeField, Tooltip("Multiplier for emission color intensity")] private float emissionIntensity = 2f;

    [Header("UI: prompt and image")]
    [Tooltip("GameObject que contiene el TextMeshPro (o Canvas) que se muestra cuando el cuadro es interactuable")]
    [SerializeField] private GameObject promptObject;
    [Tooltip("Canvas/GameObject que contiene la imagen a mostrar cuando el jugador interactúa")]
    [SerializeField] private GameObject imageCanvas;

    [Header("Totems / Spawn")]
    [Tooltip("Prefabs to spawn as totems. If multiple, they will be chosen in order.")]
    [SerializeField] private GameObject[] totemPrefabs;
    [Tooltip("Target positions (where the totems should end up). Start position will be below by riseHeight.")]
    [SerializeField] private Transform[] totemSpawnTargets;
    [SerializeField, Tooltip("How far below target the totem starts (meters)")] private float riseHeight = 2f;
    [SerializeField, Tooltip("Time for a totem to rise into position (seconds)")] private float riseTime = 1f;

    [Header("Camera Shake")]
    [Tooltip("Optional: CameraShakeController in the scene. If null the script will call CameraShakeController.Instance.")]
    [SerializeField] private CameraShakeController cameraShakeController;
    [SerializeField, Tooltip("Amplitude gain used for the shake")] private float cameraShakeIntensity = 3f;
    [SerializeField, Tooltip("How long the camera will shake (seconds)")] private float cameraShakeDuration = 5f;

    // state
    private bool isCleaned = false;
    private bool activated = false;
    // pre-instantiated totems so other systems can detect them in Awake/Start
    private List<GameObject> prespawnedTotems;
    
    [Header("Raycast Activation (optional)")]
    [Tooltip("Optional origin transform for the raycast (camera or flashlight). If null, Camera.main will be used at Start.")]
    [SerializeField] private Transform rayOrigin;
    [SerializeField, Tooltip("Distance for the raycast to detect the cuadro")] private float rayRange = 6f;
    [SerializeField, Tooltip("Layer mask for the raycast; set to only hit wall/cuadro layers to avoid false positives")] private LayerMask rayMask = ~0;
    [SerializeField, Tooltip("Optional: material float property name to read on the hit renderer to detect 'clean' state. Leave empty to trigger purely by aim/dwell.")] private string materialCleanProperty = "";
    [SerializeField, Tooltip("If using material property, threshold above which it's considered clean")] private float cleanThreshold = 0.5f;
    [SerializeField, Tooltip("Seconds the player must aim at the cuadro for the raycast to activate (prevents accidental triggers)")] private float dwellTime = 0.5f;
    private float lookTimer = 0f;

    // small contract:
    // - OnWallCleaned() -> enables emission on frame AND shows promptObject
    // - Interact() or OnMouseDown() -> if cleaned and not activated, spawn totems, show imageCanvas and activar puerta via MissionManager.OpenDoor()

    /// <summary>
    /// Call this from your Lintern/cleaning script when the wall/area is cleaned.
    /// This will enable emission on the frame's material so the player sees it's interactable.
    /// </summary>
    public void OnWallCleaned()
    {
        if (isCleaned) return;
        isCleaned = true;

        if (frameRenderer == null)
        {
            Debug.LogWarning("ActivacionCuadro: frameRenderer not assigned.");
        }
        else
        {
            // Use instance material so we don't modify shared asset unintentionally in editor.
            Material mat = frameRenderer.material;
            mat.EnableKeyword("_EMISSION");
            Color final = emissionColor * Mathf.LinearToGammaSpace(emissionIntensity);
            if (mat.HasProperty("_EmissionColor"))
                mat.SetColor("_EmissionColor", final);
            else if (mat.HasProperty("_Emission"))
                mat.SetColor("_Emission", final);

            // Try to update realtime GI if available (uses reflection so it doesn't force a compile dependency)
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
            catch (Exception) { /* non-fatal if API not present */ }
        }

        // Show the prompt (TMP) so player knows it's interactuable
        if (promptObject != null)
        {
            promptObject.SetActive(true);
        }
    }

    // note: Camera shake logic moved to CameraShakeController to keep responsibilities separated

    /// <summary>
    /// Player interaction entry. You can call this from your interaction system.
    /// </summary>
    public void Interact()
    {
        if (!isCleaned)
        {
            Debug.Log("ActivacionCuadro: not cleaned yet — cannot interact.");
            return;
        }

        if (activated) return;
        activated = true;
        StartCoroutine(ActivateSequence());
    }

    // convenience: allow clicking with mouse (requires Collider on same object)
    private void OnMouseDown()
    {
        Interact();
    }

    private void Start()
    {
        if (rayOrigin == null && Camera.main != null)
            rayOrigin = Camera.main.transform;

        // ensure UIs are hidden initially
        if (promptObject != null) promptObject.SetActive(false);
        if (imageCanvas != null) imageCanvas.SetActive(false);
    }

    private void Awake()
    {
        // Pre-instantiate totems (inactive) so MisionBotero or other systems that scan in Start()
        // can find the instances and count them. We keep them inactive and then activate/animate
        // them in ActivateSequence().
        if (totemSpawnTargets != null && totemSpawnTargets.Length > 0 && totemPrefabs != null && totemPrefabs.Length > 0)
        {
            prespawnedTotems = new List<GameObject>(totemSpawnTargets.Length);
            for (int i = 0; i < totemSpawnTargets.Length; i++)
            {
                Transform target = totemSpawnTargets[i];
                if (target == null)
                {
                    prespawnedTotems.Add(null);
                    continue;
                }

                GameObject prefab = totemPrefabs[i % totemPrefabs.Length];
                if (prefab == null)
                {
                    prespawnedTotems.Add(null);
                    continue;
                }

                Vector3 endPos = target.position;
                Vector3 startPos = endPos - Vector3.up * riseHeight;

                GameObject spawned = Instantiate(prefab, startPos, target.rotation);
                // Keep inactive so Start() on other scripts runs before they become active
                spawned.SetActive(false);
                spawned.transform.SetParent(null);
                prespawnedTotems.Add(spawned);

                Debug.Log($"ActivacionCuadro.Awake: pre-instantiated '{prefab.name}' for target {i} (inactive).");
            }
        }
        else
        {
            prespawnedTotems = new List<GameObject>();
        }
    }

    private void Update()
    {
        // If already cleaned we don't need ray activation anymore
        if (isCleaned) return;

        // raycast-based activation: player aims at this object for dwellTime, optionally check material property
        if (rayOrigin == null) return;

        Ray ray = new Ray(rayOrigin.position, rayOrigin.forward);
        if (Physics.Raycast(ray, out RaycastHit hit, rayRange, rayMask))
        {
            // consider hit if it's this object or a child of this object
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
                        // material property not present, don't trigger until explicit cleaning
                        considerClean = false;
                    }
                }

                if (considerClean)
                {
                    lookTimer += Time.deltaTime;
                    if (lookTimer >= dwellTime)
                    {
                        Debug.Log("ActivacionCuadro: Raycast dwell satisfied — marking as cleaned.");
                        OnWallCleaned();
                        // optional: automatically interact a short time after cleaning
                        // Interact();
                    }
                }
                else
                {
                    lookTimer = 0f;
                }
                return; // if we hit target, skip resetting timer below
            }
        }

        // not looking at target right now
        lookTimer = 0f;
    }

    private IEnumerator ActivateSequence()
    {
        // hide prompt
        if (promptObject != null) promptObject.SetActive(false);

        // Show image UI
        if (imageCanvas != null)
            imageCanvas.SetActive(true);

        // Spawn totems (existing behavior)
        if (totemSpawnTargets == null || totemSpawnTargets.Length == 0)
            Debug.LogWarning("ActivacionCuadro: No totemSpawnTargets assigned — no totems will be spawned.");
        if (totemPrefabs == null || totemPrefabs.Length == 0)
            Debug.LogWarning("ActivacionCuadro: No totemPrefabs assigned — no totems will be spawned.");

        if (totemSpawnTargets != null && totemSpawnTargets.Length > 0 && totemPrefabs != null && totemPrefabs.Length > 0)
        {
            for (int i = 0; i < totemSpawnTargets.Length; i++)
            {
                Transform target = totemSpawnTargets[i];
                if (target == null) continue;

                Vector3 endPos = target.position;
                Vector3 startPos = endPos - Vector3.up * riseHeight;

                GameObject spawned = null;
                // Try to reuse pre-instantiated totem if present
                if (prespawnedTotems != null && i < prespawnedTotems.Count && prespawnedTotems[i] != null)
                {
                    spawned = prespawnedTotems[i];
                    spawned.transform.position = startPos;
                    spawned.transform.rotation = target.rotation;
                    spawned.SetActive(true);
                }
                else
                {
                    // fallback: instantiate if no prespawned object
                    GameObject prefab = totemPrefabs[i % totemPrefabs.Length];
                    if (prefab == null) continue;
                    spawned = Instantiate(prefab, startPos, target.rotation);
                    spawned.SetActive(true);
                    spawned.transform.SetParent(null);
                }

                if (spawned != null)
                    StartCoroutine(RiseToPosition(spawned.transform, startPos, endPos, riseTime));

                yield return new WaitForSeconds(0.15f);
            }
        }

        // Camera shake
        CameraShakeController shaker = cameraShakeController != null ? cameraShakeController : CameraShakeController.Instance;
        if (shaker != null)
        {
            shaker.Shake(cameraShakeDuration, cameraShakeIntensity);
        }

        // Activate (show) the door via MissionManager so the player gets enclosed when inspecting the cuadro.
        if (MissionManager.Instance != null)
        {
            MissionManager.Instance.OpenDoor();
        }
        else
        {
            Debug.LogWarning("ActivacionCuadro: MissionManager no encontrado en la escena.");
        }

        yield break;
    }

    private IEnumerator RiseToPosition(Transform t, Vector3 from, Vector3 to, float time)
    {
        float elapsed = 0f;
        while (elapsed < time)
        {
            elapsed += Time.deltaTime;
            float f = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(elapsed / time));
            t.position = Vector3.Lerp(from, to, f);
            yield return null;
        }
        t.position = to;
    }

    
}
