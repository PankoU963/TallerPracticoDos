using Unity.Collections;
using UnityEngine;

public class Lintern : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Camera _camera;
    [SerializeField] private Texture2D _darkMaskBase;
    [SerializeField] private Renderer _targetRenderer; // where to assign generated mask
    [SerializeField] private string _shaderTextureProperty = "_DarkTex";

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

        Ray ray = _camera.ScreenPointToRay(screenPosition);
        if (!Physics.Raycast(ray, out RaycastHit hit)) return;

        Vector2 uv = hit.textureCoord;
        int px = Mathf.RoundToInt(uv.x * (_maskWidth - 1));
        int py = Mathf.RoundToInt(uv.y * (_maskHeight - 1));
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

        if (!string.IsNullOrEmpty(_shaderTextureProperty) && mat.HasProperty(_shaderTextureProperty) == false)
            Debug.LogWarning($"Lintern: target material shader does not have property '{_shaderTextureProperty}'.");

        mat.SetTexture(_shaderTextureProperty, tex);
    }
}
