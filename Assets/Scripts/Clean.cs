using Unity.Collections;
using UnityEngine;
using UnityEngine.UIElements;

public class Clean : MonoBehaviour
{
    [SerializeField] private Camera _camera;
    [SerializeField] private Texture2D _darkMaskBase;
    [SerializeField] private Texture2D _brush;

    [SerializeField] private Material _material;

    private Texture2D _templateDarkMask;

    void OnValidate()
    {
        // advertencias en editor para evitar olvidos comunes
        if (_darkMaskBase == null)
            Debug.LogWarning($"{name}: _darkMaskBase no asignada en inspector.");
        if (_brush == null)
            Debug.LogWarning($"{name}: _brush no asignada en inspector (ok si usas GPU path).");
        if (_material == null)
            Debug.LogWarning($"{name}: _material no asignado en inspector (el shader no recibirá la textura generada).");
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

        if (_brush == null)
        {
            Debug.LogWarning("Clean: _brush no asignado. Assign a brush texture in the inspector if you rely on CPU painting.");
        }

        int bw = _brush != null ? _brush.width : 0;
        int bh = _brush != null ? _brush.height : 0;

        bool brushReadable = _brush != null && _brush.isReadable;
        if (_brush != null && !brushReadable)
            Debug.LogWarning("Brush texture is not readable. Enable Read/Write in import settings for the brush.");

        for (int i = 0; i < bw; i++)
        {
            for (int j = 0; j < bh; j++)
            {
                int tx = pixelX + i;
                int ty = pixelY + j;
                if (tx < 0 || ty < 0 || tx >= _templateDarkMask.width || ty >= _templateDarkMask.height)
                    continue;

                if (!brushReadable)
                    continue;

                Color pixelDark = _brush.GetPixel(i, j);
                Color pixelDarkMask = _templateDarkMask.GetPixel(tx, ty);

                _templateDarkMask.SetPixel(tx, ty,
                    new Color(0, pixelDarkMask.g * pixelDark.g, 0));
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

        // NOTE: shader property name should match the expected property (e.g. _DarkTex in your shader)
        if (_material != null)
            _material.SetTexture("_DarkTex", _templateDarkMask);
    }
}
