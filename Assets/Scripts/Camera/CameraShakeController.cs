using System;
using System.Collections;
using System.Linq;
using System.Reflection;
using Unity.Cinemachine;
using UnityEngine;

/// <summary>
/// Central camera shake controller. Use a single instance in the scene and call Shake(duration, intensity).
/// It will try to use a Cinemachine BasicMultiChannelPerlin if available on the assigned Virtual Camera,
/// otherwise falls back to a simple transform-based shake on Camera.main (or assigned fallbackCamera).
/// </summary>
public class CameraShakeController : MonoBehaviour
{
    public static CameraShakeController Instance { get; private set; }

    [Header("References")]
    [Tooltip("Optional: assign a Cinemachine Virtual Camera GameObject (the VCam, not the Brain). If null the controller will try to find one in the scene.")]
    public GameObject cinemachineVirtualCameraObject;
    [Tooltip("Optional fallback camera to apply transform shake to. If null, Camera.main will be used.")]
    public Camera fallbackCamera;
    public CinemachineCamera cinemachineFP;

    [Header("Defaults")]
    public float defaultIntensity = 3f;
    public float defaultDuration = 5f;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }

    private Type FindTypeByName(string shortTypeName)
    {
        foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
        {
            Type[] types = null;
            try { types = asm.GetTypes(); } catch { continue; }
            foreach (var t in types)
            {
                if (t == null) continue;
                if (t.Name == shortTypeName) return t;
                if (!string.IsNullOrEmpty(t.FullName) && t.FullName.EndsWith("." + shortTypeName)) return t;
            }
        }
        return null;
    }

    public void Shake(float duration, float intensity)
    {
        if (!gameObject.activeInHierarchy) return;
        StartCoroutine(DoShake(duration, intensity));
    }

    public void Shake() => Shake(defaultDuration, defaultIntensity);

    private IEnumerator DoShake(float duration, float intensity)
    {
        // Try Cinemachine first
        CinemachineCamera vcamObj = cinemachineFP;
        // vcamObj = cinemachineVirtualCameraObject;
        if (vcamObj == null)
        {
            var vcamType = FindTypeByName("CinemachineVirtualCamera");
            if (vcamType != null)
            {
                // find an instance in scene
                var roots = UnityEngine.SceneManagement.SceneManager.GetActiveScene().GetRootGameObjects();
                foreach (var root in roots)
                {
                    var mbs = root.GetComponentsInChildren<MonoBehaviour>(true);
                    foreach (var mb in mbs)
                    {
                        if (mb == null) continue;
                        var t = mb.GetType();
                        if (t == vcamType || t.IsSubclassOf(vcamType))
                        {
                            vcamObj = mb.gameObject.GetComponent<CinemachineCamera>();
                            break;
                        }
                    }
                    if (vcamObj != null) break;
                }
            }
        }

        // Perlin type
        var perlinType = FindTypeByName("CinemachineBasicMultiChannelPerlin");
        if (vcamObj != null && perlinType != null)
        {
            Component perlinComp = vcamObj.GetComponent(perlinType) as Component;
            if (perlinComp == null) perlinComp = vcamObj.GetComponentInChildren(perlinType) as Component;
            if (perlinComp != null)
            {
                FieldInfo ampField = perlinType.GetField("m_AmplitudeGain", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                PropertyInfo ampProp = null;
                float original = 0f;
                if (ampField != null)
                {
                    original = (float)ampField.GetValue(perlinComp);
                    ampField.SetValue(perlinComp, intensity);
                }
                else
                {
                    ampProp = perlinType.GetProperty("m_AmplitudeGain", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                    if (ampProp != null)
                    {
                        original = (float)ampProp.GetValue(perlinComp);
                        ampProp.SetValue(perlinComp, intensity);
                    }
                    else
                    {
                        // cannot modify perlin amplitude; fallback
                        perlinComp = null;
                    }
                }

                if (perlinComp != null)
                {
                    // hold
                    float elapsed = 0f;
                    while (elapsed < duration)
                    {
                        elapsed += Time.deltaTime;
                        yield return null;
                    }

                    // smooth restore
                    float smoothTime = 0.5f;
                    float tSmooth = 0f;
                    float start = intensity;
                    while (tSmooth < smoothTime)
                    {
                        tSmooth += Time.deltaTime;
                        float p = Mathf.Clamp01(tSmooth / smoothTime);
                        float current = Mathf.Lerp(start, original, p);
                        if (ampField != null) ampField.SetValue(perlinComp, current);
                        else if (ampProp != null) ampProp.SetValue(perlinComp, current);
                        yield return null;
                    }

                    if (ampField != null) ampField.SetValue(perlinComp, original);
                    else if (ampProp != null) ampProp.SetValue(perlinComp, original);
                    yield break;
                }
            }
        }

        // Fallback: transform-based shake on fallbackCamera or Camera.main
        Camera cam = fallbackCamera != null ? fallbackCamera : Camera.main;
        if (cam == null)
        {
            Debug.LogWarning("CameraShakeController: No camera available for fallback shake.");
            yield break;
        }

        Transform ct = cam.transform;
        Vector3 originalPos = ct.localPosition;
        Quaternion originalRot = ct.localRotation;
        float e = 0f;
        System.Random rnd = new System.Random();
        while (e < duration)
        {
            e += Time.deltaTime;
            float damper = 1f - Mathf.Clamp01(e / duration);
            float x = ((float)rnd.NextDouble() * 2f - 1f) * intensity * 0.02f * damper;
            float y = ((float)rnd.NextDouble() * 2f - 1f) * intensity * 0.02f * damper;
            float z = ((float)rnd.NextDouble() * 2f - 1f) * intensity * 0.02f * damper;
            ct.localPosition = originalPos + new Vector3(x, y, z);
            ct.localRotation = originalRot * Quaternion.Euler(x * 2f, y * 2f, z * 2f);
            yield return null;
        }

        ct.localPosition = originalPos;
        ct.localRotation = originalRot;
    }

#if UNITY_EDITOR
    [ContextMenu("Test Shake (Cinemachine or fallback)")]
    private void EditorTestShake()
    {
        Shake(defaultDuration, defaultIntensity);
    }
#endif
}
