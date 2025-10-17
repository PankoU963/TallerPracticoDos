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

    private void Start()
    {
        if (_camera == null)
        {
            _camera = Camera.main;
            if (_camera == null)
                Debug.LogWarning("Clean: _camera not assigned and Camera.main is null. Raycasts will not work.");
        }

        CreateTexture();
    }

    private void Update()
    {
        if (Input.GetMouseButton(0))
        {
            if (_camera == null)
            {
                // nothing to do without a camera
                return;
            }

            if (_templateDarkMask == null)
            {
                // texture not created (CreateTexture failed), skip
                return;
            }

            if (Physics.Raycast(_camera.ScreenPointToRay(Input.mousePosition), out RaycastHit hit))
            {
                Vector2 textureCoord = hit.textureCoord;

                int pixelX = (int)(textureCoord.x * _templateDarkMask.width);
                int pixelY = (int)(textureCoord.y * _templateDarkMask.height);

                // Safety: ensure we don't read/write outside texture bounds
                if (_brush == null)
                {
                    Debug.LogWarning("Clean: _brush not assigned. Assign a brush texture in the inspector.");
                }

                for (int i = 0; i < (_brush != null ? _brush.width : 0); i++)
                {
                    for (int j = 0; j < (_brush != null ? _brush.height : 0); j++)
                    {
                        int tx = pixelX + i;
                        int ty = pixelY + j;
                        if (tx < 0 || ty < 0 || tx >= _templateDarkMask.width || ty >= _templateDarkMask.height)
                            continue;

                        if (_brush == null)
                            continue;

                        if (!_brush.isReadable)
                        {
                            Debug.LogWarning("Brush texture is not readable. Enable Read/Write in import settings for the brush.");
                            continue;
                        }

                        Color pixelDark = _brush.GetPixel(i, j);
                        Color pixelDarkMask = _templateDarkMask.GetPixel(tx, ty);

                        _templateDarkMask.SetPixel(tx, ty,
                            new Color(0, pixelDarkMask.g * pixelDark.g, 0));
                    }
                }
                _templateDarkMask.Apply();
            }
        }
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
