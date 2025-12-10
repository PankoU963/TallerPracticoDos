using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class EmissionController : MonoBehaviour
{
    [SerializeField] private Renderer sourceRenderer;
    [SerializeField] private bool includeChildRenderers = true;
    [SerializeField] private Color emissionColor = Color.yellow;
    [SerializeField] private float intensityOff = 0f;
    [SerializeField] private float intensityOn = 2f;

    private List<Renderer> renderers = new List<Renderer>();
    private List<Material> materials = new List<Material>();

    private void Awake() => Initialize();

    public void Initialize()
    {
        renderers.Clear();
        materials.Clear();
        if (sourceRenderer == null) return;
        if (includeChildRenderers)
            renderers.AddRange(sourceRenderer.GetComponentsInChildren<Renderer>(true));
        else
            renderers.Add(sourceRenderer);

        foreach (var r in renderers)
        {
            if (r == null) continue;
            var mat = r.material;
            if (mat != null) materials.Add(mat);
        }

        SetIntensity(intensityOff);
    }

    public void SetIntensity(float intensity)
    {
        if (materials == null || materials.Count == 0) return;
        Color final = emissionColor * Mathf.LinearToGammaSpace(intensity);
        foreach (var m in materials)
        {
            if (m.HasProperty("_EmissionColor"))
                m.SetColor("_EmissionColor", final);
            else if (m.HasProperty("_Emission"))
                m.SetColor("_Emission", final);
        }

        try
        {
            var dynType = AppDomain.CurrentDomain.GetAssemblies()
                .Select(a => a.GetType("UnityEngine.Experimental.GlobalIllumination.DynamicGI"))
                .FirstOrDefault(t => t != null);
            if (dynType != null)
            {
                var method = dynType.GetMethod("SetEmissive", System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.Public);
                if (method != null && renderers != null && renderers.Count > 0)
                {
                    foreach (var r in renderers)
                    {
                        if (r != null)
                            method.Invoke(null, new object[] { r, final });
                    }
                }
            }
        }
        catch { }
    }

    public void Enable()
    {
        SetIntensity(intensityOn);
        if (materials == null) return;
        foreach (var m in materials) m.EnableKeyword("_EMISSION");
    }

    public void Disable()
    {
        SetIntensity(intensityOff);
    }
}
