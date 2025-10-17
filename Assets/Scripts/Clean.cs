using Unity.Collections;
using UnityEngine;
using UnityEngine.UIElements;

public class Clean : MonoBehaviour
{
    [SerializeField] private Camera _camera;
    [SerializeField] private Texture2D _darkMaskBase;
    [SerializeField] private Texture2D _brush;

    [SerializeField] private Material _material;
    // Optional: target renderer whose material will receive the generated texture at runtime.
    // If set, we'll use renderer.material (instance) which will immediately affect the visible object.
    [SerializeField] private Renderer _targetRenderer;

    // Shader texture property name. Common names: _MainTex, _BaseMap, _DarkTex (custom shader).
    [SerializeField] private string _shaderTextureProperty = "_DarkTex";

    // Brush settings
    [Header("Brush Settings")]
    [SerializeField] private bool _useCircularBrush = true; // if true, ignores _brush texture and uses procedural circle
    [SerializeField] private float _brushRadius = 16f; // in pixels when using circular brush
    [SerializeField, Range(0f, 1f)] private float _brushStrength = 0.75f; // 0..1 how strong the brush affects the mask
    [SerializeField] private bool _useBrushTextureAlpha = true; // when using _brush texture, sample its alpha for intensity

    private Texture2D _templateDarkMask;

    void OnValidate()
    {
        // advertencias en editor para evitar olvidos comunes
        if (_darkMaskBase == null)
            Debug.LogWarning($"{name}: _darkMaskBase no asignada en inspector.");
        if (_brush == null)
            Debug.LogWarning($"{name}: _brush no asignada en inspector (ok si usas GPU path).");
        if (_material == null && _targetRenderer == null)
            Debug.LogWarning($"{name}: neither _material nor _targetRenderer assigned. The generated texture won't be applied to any material at runtime.");

        if (string.IsNullOrEmpty(_shaderTextureProperty))
            Debug.LogWarning($"{name}: _shaderTextureProperty is empty. Set the shader property name where the texture should be assigned (e.g. _DarkTex, _BaseMap, _MainTex).");
    }

    private void Start()
    {
        if (_camera == null)
        {
            _camera = Camera.main;
            if (_camera == null)
                Debug.LogWarning("Clean: _camera not assigned and Camera.main is null. Raycasts will not work.");
        }

        CreateTexture();

        // informa claramente si falta algo crítico
        if (_templateDarkMask == null)
            Debug.LogError("Clean: _templateDarkMask no fue creado. Revisa _darkMaskBase.");
        // if a material is configured, check that the shader likely contains the property
        if (_material != null && !string.IsNullOrEmpty(_shaderTextureProperty) && !_material.HasProperty(_shaderTextureProperty))
            Debug.LogWarning($"Clean: the provided _material's shader does not have the property '{_shaderTextureProperty}'. The texture won't show unless the shader samples that property.");
        if (_brush != null && !_brush.isReadable)
            Debug.LogWarning("Clean: _brush no es readable. Habilita Read/Write si usas CPU painting.");
    }

    private void Update()
    {
        // Soporta input de mouse y touch (multi-touch)
        // Mouse: mantenido (GetMouseButton(0))
        if (Input.GetMouseButton(0))
        {
            ProcessPointer(Input.mousePosition);
            return;
        }

        // Touch: procesar todos los touches activos (Began, Moved, Stationary)
        if (Input.touchCount > 0)
        {
            for (int t = 0; t < Input.touchCount; t++)
            {
                Touch touch = Input.GetTouch(t);
                if (touch.phase == TouchPhase.Began || touch.phase == TouchPhase.Moved || touch.phase == TouchPhase.Stationary)
                {
                    ProcessPointer(touch.position);
                }
            }
        }
    }

    // Extrae la lógica de raycast/pintado para reutilizarla con mouse y touch
    private void ProcessPointer(Vector2 screenPosition)
    {
        if (_camera == null)
        {
            Debug.LogWarning("Clean: _camera es null; no se puede raycastear.");
            return;
        }

        if (_templateDarkMask == null)
        {
            Debug.LogWarning("Clean: _templateDarkMask es null; CreateTexture falló.");
            return;
        }

        Ray ray = _camera.ScreenPointToRay(screenPosition);
        if (!Physics.Raycast(ray, out RaycastHit hit))
            return;

        Vector2 textureCoord = hit.textureCoord;

        int pixelX = (int)(textureCoord.x * _templateDarkMask.width);
        int pixelY = (int)(textureCoord.y * _templateDarkMask.height);

        // Procedural circular brush
        if (_useCircularBrush || _brush == null)
        {
            int radius = Mathf.Max(1, Mathf.RoundToInt(_brushRadius));
            int r2 = radius * radius;

            for (int oy = -radius; oy <= radius; oy++)
            {
                for (int ox = -radius; ox <= radius; ox++)
                {
                    int tx = pixelX + ox;
                    int ty = pixelY + oy;
                    if (tx < 0 || ty < 0 || tx >= _templateDarkMask.width || ty >= _templateDarkMask.height)
                        continue;

                    int dist2 = ox * ox + oy * oy;
                    if (dist2 > r2) continue;

                    float dist = Mathf.Sqrt(dist2);
                    float t = 1f - (dist / radius); // 1 at center, 0 at edge
                    float delta = Mathf.Clamp01(t * _brushStrength);

                    Color pixelDarkMask = _templateDarkMask.GetPixel(tx, ty);
                    float newG = pixelDarkMask.g * (1f - delta);
                    _templateDarkMask.SetPixel(tx, ty, new Color(0f, newG, 0f));
                }
            }

            _templateDarkMask.Apply();
            return;
        }

        // Texture-based brush: center the brush texture at the hit point
        int bw = _brush != null ? _brush.width : 0;
        int bh = _brush != null ? _brush.height : 0;
        bool brushReadable = _brush != null && _brush.isReadable;
        if (_brush == null)
        {
            Debug.LogWarning("Clean: _brush no asignado. Assign a brush texture in the inspector if you rely on CPU painting.");
            return;
        }
        if (!brushReadable)
        {
            Debug.LogWarning("Brush texture is not readable. Enable Read/Write in import settings for the brush.");
            return;
        }

        int halfBw = bw / 2;
        int halfBh = bh / 2;

        for (int i = 0; i < bw; i++)
        {
            for (int j = 0; j < bh; j++)
            {
                int tx = pixelX - halfBw + i;
                int ty = pixelY - halfBh + j;
                if (tx < 0 || ty < 0 || tx >= _templateDarkMask.width || ty >= _templateDarkMask.height)
                    continue;

                Color brushPixel = _brush.GetPixel(i, j);
                float intensity = _useBrushTextureAlpha ? brushPixel.a : (brushPixel.grayscale);
                float delta = Mathf.Clamp01(intensity * _brushStrength);

                Color pixelDarkMask = _templateDarkMask.GetPixel(tx, ty);
                float newG = pixelDarkMask.g * (1f - delta);
                _templateDarkMask.SetPixel(tx, ty, new Color(0f, newG, 0f));
            }
        }

        _templateDarkMask.Apply();
    }

    private void CreateTexture()
    {
        if (_darkMaskBase == null)
        {
            Debug.LogError("_darkMaskBase is not assigned in the inspector.");
            return;
        }

        if (!_darkMaskBase.isReadable)
        {
            Debug.LogError("_darkMaskBase texture is not readable. Enable Read/Write in the texture import settings (select the texture in Project -> check 'Read/Write Enabled') and re-import.");
            return;
        }

        _templateDarkMask = new Texture2D(_darkMaskBase.width, _darkMaskBase.height, TextureFormat.RGBA32, false);
        _templateDarkMask.SetPixels(_darkMaskBase.GetPixels());
        _templateDarkMask.Apply();

        // Assign the generated texture to the configured target (renderer or material).
        AssignTextureToTarget(_templateDarkMask);
    }

    // Assigns the texture to either the target renderer's material (preferred, affects visible object)
    // or to the provided material. Logs helpful diagnostics when the target or shader property is missing.
    private void AssignTextureToTarget(Texture2D tex)
    {
        string prop = string.IsNullOrEmpty(_shaderTextureProperty) ? "_DarkTex" : _shaderTextureProperty;

        if (_targetRenderer != null)
        {
            Material mat = _targetRenderer.material; // creates instance if needed and affects visible renderer
            if (mat == null)
            {
                Debug.LogWarning($"Clean: target renderer '{_targetRenderer.name}' has no material to assign the texture to.");
                return;
            }

            if (!mat.HasProperty(prop))
                Debug.LogWarning($"Clean: renderer material's shader does not have property '{prop}'. The texture may not be used by the shader.");

            mat.SetTexture(prop, tex);
            Debug.Log($"Clean: assigned generated texture to renderer '{_targetRenderer.name}' material property '{prop}'.");
            return;
        }

        if (_material != null)
        {
            if (!_material.HasProperty(prop))
                Debug.LogWarning($"Clean: provided _material's shader does not have property '{prop}'. The texture may not be used by the shader.");

            _material.SetTexture(prop, tex);
            Debug.Log($"Clean: assigned generated texture to provided material '{_material.name}' property '{prop}'.");
            return;
        }

        Debug.LogWarning("Clean: no target to assign the generated texture to. Assign either _targetRenderer or _material in the inspector.");
    }
}
