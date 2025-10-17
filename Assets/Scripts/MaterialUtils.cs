using UnityEngine;

// Utilities to help copy common material properties between materials and to set
// properties per-renderer using MaterialPropertyBlock.
// Usage:
// - Attach MaterialCopier to a GameObject, set Source and Target materials and
//   use the component's context menu (in inspector) "Copy Properties" to copy.
// - Attach MPBSetter to a GameObject which has a Renderer, set Source and the
//   property names that match the shader on the target material, and use
//   "Apply To Renderer".

public static class MaterialUtils
{
    // Common property name alternatives used by Built-in and URP/HDRP
    private static readonly string[] BaseMapProps = {"_BaseMap", "_MainTex"};
    private static readonly string[] NormalMapProps = {"_NormalMap", "_BumpMap"};
    private static readonly string[] MetallicProps = {"_Metallic", "_MetallicGlossMap"};
    private static readonly string[] SmoothnessProps = {"_Smoothness", "_Glossiness"};
    private static readonly string[] EmissionMapProps = {"_EmissionMap", "_EmissiveColorMap"};
    private static readonly string[] EmissionColorProps = {"_EmissionColor", "_EmissiveColor"};

    // Copy common properties from source to target if the target has the property.
    public static void CopyCommonProperties(Material source, Material target)
    {
        if (source == null || target == null) return;

        CopyTexturePropertyIfExists(source, target, BaseMapProps);
        CopyTexturePropertyIfExists(source, target, NormalMapProps);
        CopyFloatPropertyIfExists(source, target, MetallicProps);
        CopyFloatPropertyIfExists(source, target, SmoothnessProps);
        CopyTexturePropertyIfExists(source, target, EmissionMapProps);
        CopyColorPropertyIfExists(source, target, EmissionColorProps);

        // Copy tiling/offset for base map if both have the prop
        foreach (var p in BaseMapProps)
        {
            if (source.HasProperty(p) && target.HasProperty(p))
            {
                Vector2 scale = source.GetTextureScale(p);
                Vector2 offset = source.GetTextureOffset(p);
                target.SetTextureScale(p, scale);
                target.SetTextureOffset(p, offset);
                break;
            }
        }

        // Preserve keywords like emission
        if (HasAnyProperty(source, EmissionMapProps) || HasAnyProperty(source, EmissionColorProps))
        {
            if (target.IsKeywordEnabled("_EMISSION") == false)
                target.EnableKeyword("_EMISSION");
        }
    }

    private static bool HasAnyProperty(Material mat, string[] props)
    {
        foreach (var p in props) if (mat.HasProperty(p)) return true;
        return false;
    }

    private static void CopyTexturePropertyIfExists(Material source, Material target, string[] props)
    {
        foreach (var p in props)
        {
            if (source.HasProperty(p) && target.HasProperty(p))
            {
                var tex = source.GetTexture(p);
                target.SetTexture(p, tex);
                // Copy scale/offset per-property
                target.SetTextureScale(p, source.GetTextureScale(p));
                target.SetTextureOffset(p, source.GetTextureOffset(p));
                break;
            }
        }
    }

    private static void CopyFloatPropertyIfExists(Material source, Material target, string[] props)
    {
        foreach (var p in props)
        {
            if (source.HasProperty(p) && target.HasProperty(p))
            {
                float v = source.GetFloat(p);
                target.SetFloat(p, v);
                break;
            }
        }
    }

    private static void CopyColorPropertyIfExists(Material source, Material target, string[] props)
    {
        foreach (var p in props)
        {
            if (source.HasProperty(p) && target.HasProperty(p))
            {
                Color c = source.GetColor(p);
                target.SetColor(p, c);
                break;
            }
        }
    }

    // Apply properties from a source material to a specific renderer using MaterialPropertyBlock.
    // This doesn't create new materials and is good for per-instance overrides.
    public static void ApplyToRendererWithPropertyBlock(Material source, Renderer renderer, string[] propertyNamesToCopy = null)
    {
        if (source == null || renderer == null) return;

        var mpb = new MaterialPropertyBlock();
        renderer.GetPropertyBlock(mpb);

        // if list is null, try common ones
        if (propertyNamesToCopy == null)
        {
            propertyNamesToCopy = new string[] {"_BaseMap", "_MainTex", "_NormalMap", "_BumpMap", "_EmissionMap", "_EmissiveColorMap"};
        }

        foreach (var p in propertyNamesToCopy)
        {
            if (!source.HasProperty(p)) continue;
            var t = source.GetTexture(p);
            if (t != null)
            {
                mpb.SetTexture(p, t);
                mpb.SetVector(p + "_ST", new Vector4(source.GetTextureScale(p).x, source.GetTextureScale(p).y, source.GetTextureOffset(p).x, source.GetTextureOffset(p).y));
                continue;
            }

            if (source.HasProperty(p) && source.GetTexture(p) == null && source.HasProperty(p))
            {
                // try float/color
                try
                {
                    float f = source.GetFloat(p);
                    mpb.SetFloat(p, f);
                    continue;
                }
                catch { }

                try
                {
                    Color c = source.GetColor(p);
                    mpb.SetColor(p, c);
                    continue;
                }
                catch { }
            }
        }

        renderer.SetPropertyBlock(mpb);
    }
}

// MonoBehaviour helper to copy properties from a source material to a target material.
public class MaterialCopier : MonoBehaviour
{
    public Material SourceMaterial;
    public Material TargetMaterial;

    [ContextMenu("Copy Properties")]
    private void Copy()
    {
        if (SourceMaterial == null || TargetMaterial == null)
        {
            Debug.LogWarning("Source or Target material not set.");
            return;
        }

        MaterialUtils.CopyCommonProperties(SourceMaterial, TargetMaterial);
        Debug.Log($"Copied properties from {SourceMaterial.name} to {TargetMaterial.name}");
    }
}

// MonoBehaviour helper to apply a material's textures/values to a Renderer with MaterialPropertyBlock.
public class MPBSetter : MonoBehaviour
{
    public Material SourceMaterial;
    public Renderer TargetRenderer;

    [ContextMenu("Apply To Renderer")]
    private void Apply()
    {
        if (SourceMaterial == null || TargetRenderer == null)
        {
            Debug.LogWarning("SourceMaterial or TargetRenderer not set.");
            return;
        }

        MaterialUtils.ApplyToRendererWithPropertyBlock(SourceMaterial, TargetRenderer);
        Debug.Log($"Applied properties from {SourceMaterial.name} to renderer on {gameObject.name}");
    }
}
