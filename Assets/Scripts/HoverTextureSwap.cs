using UnityEngine;

// Swaps between _MainTex (or given mainTextureProperty) and _DarkTex (or given darkTextureProperty)
// when the mouse pointer is over the object's collider. Works with Renderer.material (instance).
// Attach to a GameObject with a Collider (IsTrigger can be false) and a Renderer.
public class HoverTextureSwap : MonoBehaviour
{
    [Tooltip("Name of the shader property used as the 'dark' texture (default _DarkTex)")]
    public string darkTextureProperty = "_DarkTex";

    [Tooltip("Name of the shader property used as the main texture (default _MainTex)")]
    public string mainTextureProperty = "_MainTex";

    [Tooltip("Optional texture to show while hovering. If empty, the shader's dark texture will be used.")]
    public Texture hoverTexture;

    private Renderer _renderer;
    private Texture _originalMainTexture;
    private Texture _originalDarkTexture;
    private Material _instancedMaterial;

    void Awake()
    {
        _renderer = GetComponent<Renderer>();
        if (_renderer == null)
        {
            Debug.LogWarning("HoverTextureSwap: no Renderer found on the GameObject. The script requires a Renderer.", this);
            enabled = false;
            return;
        }

        // Use renderer.material to get an instance so we can modify textures per-object safely
        _instancedMaterial = _renderer.material;

        if (!_instancedMaterial.HasProperty(mainTextureProperty) && !_instancedMaterial.HasProperty(darkTextureProperty))
        {
            Debug.LogWarning($"HoverTextureSwap: material does not have properties '{mainTextureProperty}' or '{darkTextureProperty}'.", this);
        }

        if (_instancedMaterial.HasProperty(mainTextureProperty))
            _originalMainTexture = _instancedMaterial.GetTexture(mainTextureProperty);
        if (_instancedMaterial.HasProperty(darkTextureProperty))
            _originalDarkTexture = _instancedMaterial.GetTexture(darkTextureProperty);

        // If hoverTexture not set, use the shader's _DarkTex property as the hover texture fallback
        if (hoverTexture == null && _originalDarkTexture != null)
        {
            hoverTexture = _originalDarkTexture;
        }
    }

    void OnMouseEnter()
    {
        if (_instancedMaterial == null) return;

        if (_instancedMaterial.HasProperty(mainTextureProperty) && hoverTexture != null)
        {
            _instancedMaterial.SetTexture(mainTextureProperty, hoverTexture);
        }
        else if (_instancedMaterial.HasProperty(darkTextureProperty) && _instancedMaterial.HasProperty(mainTextureProperty))
        {
            // swap by copying dark into main
            var darkTex = _instancedMaterial.GetTexture(darkTextureProperty);
            if (darkTex != null)
                _instancedMaterial.SetTexture(mainTextureProperty, darkTex);
        }
    }

    void OnMouseExit()
    {
        if (_instancedMaterial == null) return;

        if (_instancedMaterial.HasProperty(mainTextureProperty))
        {
            _instancedMaterial.SetTexture(mainTextureProperty, _originalMainTexture);
        }
    }

    private void OnDestroy()
    {
        // Clean up the instantiated material to avoid leaking materials in editor/runtime
        if (_instancedMaterial != null)
        {
            // In editor, DestroyImmediate; in play mode, Destroy
            #if UNITY_EDITOR
            if (!Application.isPlaying)
                DestroyImmediate(_instancedMaterial);
            else
                Destroy(_instancedMaterial);
            #else
            Destroy(_instancedMaterial);
            #endif
        }
    }
}
