using System;
using System.Collections;
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
    // - OnWallCleaned() -> enables emission on frame
    // - Interact() or OnMouseDown() -> if cleaned and not activated, spawn totems and shake camera

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
            return;
        }

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
            // find type and method via reflection
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
        // Spawn totems
        if (totemSpawnTargets == null || totemSpawnTargets.Length == 0)
        {
            Debug.LogWarning("ActivacionCuadro: No totemSpawnTargets assigned — no totems will be spawned.");
        }
        if (totemPrefabs == null || totemPrefabs.Length == 0)
        {
            Debug.LogWarning("ActivacionCuadro: No totemPrefabs assigned — no totems will be spawned.");
        }

        if (totemSpawnTargets != null && totemSpawnTargets.Length > 0 && totemPrefabs != null && totemPrefabs.Length > 0)
        {
            Debug.Log($"ActivacionCuadro: Spawning {totemSpawnTargets.Length} totems (prefabs: {totemPrefabs.Length}).");
            for (int i = 0; i < totemSpawnTargets.Length; i++)
            {
                Transform target = totemSpawnTargets[i];
                if (target == null)
                {
                    Debug.LogWarning($"ActivacionCuadro: totemSpawnTargets[{i}] is null — skipping.");
                    continue;
                }

                GameObject prefab = totemPrefabs[i % totemPrefabs.Length];
                if (prefab == null)
                {
                    Debug.LogWarning($"ActivacionCuadro: totemPrefabs[{i % totemPrefabs.Length}] is null — skipping.");
                    continue;
                }

                Vector3 endPos = target.position;
                Vector3 startPos = endPos - Vector3.up * riseHeight;

                GameObject spawned = Instantiate(prefab, startPos, target.rotation);
                if (spawned == null)
                {
                    Debug.LogWarning($"ActivacionCuadro: Failed to instantiate prefab for target index {i}.");
                    continue;
                }
                spawned.SetActive(true);
                Debug.Log($"ActivacionCuadro: Instantiated totem '{spawned.name}' at {startPos} -> will rise to {endPos}.");
                // optional: parent under a container for cleanliness
                spawned.transform.SetParent(null);
                StartCoroutine(RiseToPosition(spawned.transform, startPos, endPos, riseTime));
                // small stagger so they don't all appear exact same frame
                yield return new WaitForSeconds(0.15f);
            }
        }

        // Camera shake: delegate to CameraShakeController (keeps responsibilities separated)
        CameraShakeController shaker = cameraShakeController != null ? cameraShakeController : CameraShakeController.Instance;
        if (shaker != null)
        {
            shaker.Shake(cameraShakeDuration, cameraShakeIntensity);
        }
        else
        {
            Debug.LogWarning("ActivacionCuadro: No CameraShakeController found in scene; camera will not shake.");
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
