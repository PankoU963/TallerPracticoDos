using Unity.Collections;
using UnityEngine;

namespace Shaders
{
    public class Lintern : MonoBehaviour
    {
    [Header("References")]
    [SerializeField] private Camera _camera;
    [SerializeField] private Texture2D _darkMaskBase;
    [SerializeField] private Renderer _targetRenderer; // where to assign generated mask
    [SerializeField] private string _shaderTextureProperty = "_DarkTex";
    [SerializeField, Tooltip("Flip the UV Y coordinate when mapping hit textureCoord to the mask (use if shader expects flipped UVs)")] private bool _flipUVY = false;
    [SerializeField, Tooltip("Enable debug logs for UV, pixel coordinates when painting") ] private bool _debugUV = false;

    [Header("Reveal Source")]
    [Tooltip("On desktop reveal follows mouse position; on mobile reveal follows camera center. You can force camera-center reveal.")]
    [SerializeField] private bool _forceCameraCenterReveal = false;

    [Header("Mask / Performance")]
    [Tooltip("Scale (0.1..1) applied to the base mask to reduce resolution for performance.")]
    [SerializeField, Range(0.1f, 1f)] private float _maskScale = 0.5f;
    [SerializeField, Tooltip("Apply texture changes every N frames (grouping Apply calls).")] private int _applyEveryNFrames = 2;

    [Header("Brush (cheap circular)")]
    [SerializeField, Tooltip("Radius in pixels (on the downscaled mask)")] private int _brushRadius = 16;
    [SerializeField, Range(0f, 1f)] private float _brushStrength = 0.75f;

    // Visual-only flashlight (kept for visuals, NOT used for revealing)
    [Header("Visual Light (not used to reveal)")]
    [SerializeField] private Light _flashlight;

    [Header("Range settings")]
    [Tooltip("When enabled, only reveal when the raycast hit is within the flashlight's range (or the fallback max distance).")]
    [SerializeField] private bool _useFlashlightRange = true;
    [Tooltip("Multiplier applied to the flashlight.range to allow slightly larger/smaller reveal area.")]
    [SerializeField] private float _rangeMultiplier = 1.0f;
    [Tooltip("Fallback max reveal distance when no _flashlight is assigned (used only if _useFlashlightRange is true). Set to 0 to disable fallback and let it reveal at any distance.")]
    [SerializeField] private float _fallbackMaxRevealDistance = 0f;

    // runtime
    private Texture2D _mask;
    private Color32[] _pixels;
    private int _maskWidth;
    private int _maskHeight;
    private bool _maskDirty;
    private int _framesSinceApply;

    private void OnValidate()
    {
        if (_darkMaskBase == null) Debug.LogWarning($"{name}: _darkMaskBase not assigned.");
        if (_targetRenderer == null) Debug.LogWarning($"{name}: _targetRenderer not assigned - generated mask won't be visible.");
        if (_camera == null) _camera = Camera.main;
        _applyEveryNFrames = Mathf.Max(1, _applyEveryNFrames);
        _brushRadius = Mathf.Max(1, _brushRadius);
    }

    private void Start()
    {
        if (_camera == null) _camera = Camera.main;
        CreateMask();
    }

    private void Update()
    {
        // Decide reveal source: mouse (desktop) or camera-center (mobile / forced)
        bool useCameraCenter = _forceCameraCenterReveal || Application.isMobilePlatform;

        if (!useCameraCenter)
        {
            // reveal by mouse "where it passes" (no click required)
            Vector2 mousePos = Input.mousePosition;
            ProcessReveal(mousePos);
        }
        else
        {
            // reveal by camera center (useful for mobile where camera move defines view)
            Vector2 center = new Vector2(Screen.width * 0.5f, Screen.height * 0.5f);
            ProcessReveal(center);
        }

        // grouped Apply
        if (_maskDirty)
        {
            _framesSinceApply++;
            if (_framesSinceApply >= _applyEveryNFrames)
            {
                _mask.SetPixels32(_pixels);
                _mask.Apply(false);
                _maskDirty = false;
                _framesSinceApply = 0;
            }
        }
    }

    // Raycast from camera through screenPosition and paint if hit object with UVs
    private void ProcessReveal(Vector2 screenPosition)
    {
        if (_camera == null || _mask == null) return;

        // Reject invalid input that can produce Infinity/NaN screen coords
        if (float.IsNaN(screenPosition.x) || float.IsNaN(screenPosition.y) ||
            float.IsInfinity(screenPosition.x) || float.IsInfinity(screenPosition.y))
        {
            // Use camera center as a safe fallback
            screenPosition = new Vector2(_camera.pixelWidth * 0.5f, _camera.pixelHeight * 0.5f);
        }

        // Clamp to camera pixel rect to avoid ScreenPointToRay errors when outside the viewport
        Rect pixelRect = _camera.pixelRect;
        screenPosition.x = Mathf.Clamp(screenPosition.x, pixelRect.xMin, pixelRect.xMax - 1f);
        screenPosition.y = Mathf.Clamp(screenPosition.y, pixelRect.yMin, pixelRect.yMax - 1f);

        Ray ray = _camera.ScreenPointToRay(screenPosition);
        if (!Physics.Raycast(ray, out RaycastHit hit)) return;

        // Optional: limit reveal to flashlight range
        if (_useFlashlightRange)
        {
            float maxDist = 0f;
            if (_flashlight != null)
            {
                // Light.range is the effective range for many light types (spot/point)
                maxDist = _flashlight.range * Mathf.Max(0.0001f, _rangeMultiplier);
            }
            else
            {
                maxDist = _fallbackMaxRevealDistance;
            }

            if (maxDist > 0f)
            {
                if (hit.distance > maxDist) return; // out of reveal range
            }
        }

        Vector2 uv = hit.textureCoord;

        // If we previously assigned the texture to a specific material property,
        // account for the material's texture scale & offset so UV maps correctly.
        if (!string.IsNullOrEmpty(_assignedTextureProperty))
        {
            Material mat = _targetRenderer != null ? _targetRenderer.material : null;
            if (mat != null && mat.HasProperty(_assignedTextureProperty))
            {
                Vector2 scale = mat.GetTextureScale(_assignedTextureProperty);
                Vector2 offset = mat.GetTextureOffset(_assignedTextureProperty);
                uv = Vector2.Scale(uv, scale) + offset;
            }
            else if (mat != null && mat.HasProperty("_MainTex"))
            {
                Vector2 scale = mat.GetTextureScale("_MainTex");
                Vector2 offset = mat.GetTextureOffset("_MainTex");
                uv = Vector2.Scale(uv, scale) + offset;
            }
        }

        // Optional Y flip - some shaders / UV conventions require this
        if (_flipUVY)
        {
            uv.y = 1f - uv.y;
        }

        // Wrap or clamp UVs into 0..1 in case tiling/offset moved them
        uv.x = uv.x - Mathf.Floor(uv.x);
        uv.y = uv.y - Mathf.Floor(uv.y);

        int px = Mathf.RoundToInt(uv.x * (_maskWidth - 1));
        int py = Mathf.RoundToInt(uv.y * (_maskHeight - 1));

        if (_debugUV)
        {
            Debug.Log($"Lintern: hit.uv={hit.textureCoord} adjustedUV={uv} px={px} py={py} prop={_assignedTextureProperty}");
        }
        PaintAt(px, py);
    }

    // Simple circular brush applied to the downscaled mask buffer (Color32[])
    private void PaintAt(int cx, int cy)
    {
        if (_pixels == null) return;

        int r = Mathf.Max(1, _brushRadius);
        int r2 = r * r;

        int xmin = Mathf.Max(0, cx - r);
        int xmax = Mathf.Min(_maskWidth - 1, cx + r);
        int ymin = Mathf.Max(0, cy - r);
        int ymax = Mathf.Min(_maskHeight - 1, cy + r);

        for (int y = ymin; y <= ymax; y++)
        {
            int dy = y - cy;
            int dy2 = dy * dy;
            int row = y * _maskWidth;
            for (int x = xmin; x <= xmax; x++)
            {
                int dx = x - cx;
                int dist2 = dx * dx + dy2;
                if (dist2 > r2) continue;

                float dist = Mathf.Sqrt(dist2);
                float t = 1f - (dist / r); // 1 center -> 0 edge
                float delta = Mathf.Clamp01(t * _brushStrength);

                int idx = row + x;
                // mask uses green channel as "darkness" (preserved behavior). Work on 0..255
                float currentG = _pixels[idx].g / 255f;
                float newG = Mathf.Clamp01(currentG * (1f - delta));
                _pixels[idx].g = (byte)Mathf.RoundToInt(newG * 255f);
                _pixels[idx].r = 0;
                _pixels[idx].b = 0;
                _pixels[idx].a = 255;
            }
        }

        _maskDirty = true;
    }

    private void CreateMask()
    {
        if (_darkMaskBase == null)
        {
            Debug.LogError("Lintern: _darkMaskBase is not assigned.");
            return;
        }

        // compute downscaled size
        _maskWidth = Mathf.Max(32, Mathf.RoundToInt(_darkMaskBase.width * _maskScale));
        _maskHeight = Mathf.Max(32, Mathf.RoundToInt(_darkMaskBase.height * _maskScale));

        _mask = new Texture2D(_maskWidth, _maskHeight, TextureFormat.RGBA32, false);
        // Fill mask by sampling base texture (bilinear) so visual look is preserved but at lower res
        Color[] tmp = new Color[_maskWidth * _maskHeight];
        for (int y = 0; y < _maskHeight; y++)
        {
            float v = (y + 0.5f) / _maskHeight;
            for (int x = 0; x < _maskWidth; x++)
            {
                float u = (x + 0.5f) / _maskWidth;
                tmp[y * _maskWidth + x] = _darkMaskBase.GetPixelBilinear(u, v);
            }
        }
        _mask.SetPixels(tmp);
        _mask.Apply(false);

        _pixels = _mask.GetPixels32();

        AssignTextureToTarget(_mask);
    }

    private void AssignTextureToTarget(Texture2D tex)
    {
        if (_targetRenderer == null)
        {
            Debug.LogWarning("Lintern: no _targetRenderer assigned to receive the generated mask.");
            return;
        }

        Material mat = _targetRenderer.material; // instance
        if (mat == null)
        {
            Debug.LogWarning("Lintern: target renderer has no material.");
            return;
        }
        // Try the configured property first (if provided), otherwise try common texture properties
        string[] tryProps = new string[] { _shaderTextureProperty, "_MainTex", "_BaseMap" };
        bool assigned = false;

        foreach (var prop in tryProps)
        {
            if (string.IsNullOrEmpty(prop)) continue;
            if (mat.HasProperty(prop))
            {
                mat.SetTexture(prop, tex);
                assigned = true;
                _assignedTextureProperty = prop;
                break;
            }
        }

        if (!assigned)
        {
            // Last resort: assign to mainTexture field which works for many materials
            mat.mainTexture = tex;

            // indicate we used mainTexture fallback
            _assignedTextureProperty = "_MainTex";

            // Warn only when user explicitly configured a property that doesn't exist
            if (!string.IsNullOrEmpty(_shaderTextureProperty))
            {
                Debug.LogWarning($"Lintern: target material shader does not have property '{_shaderTextureProperty}'. Assigned to material.mainTexture as fallback.");
            }
        }
    }

    // property actually used to assign the generated mask (helps map material tiling/offset)
    private string _assignedTextureProperty = null;
    }
}
