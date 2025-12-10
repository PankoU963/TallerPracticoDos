using UnityEngine;

/// <summary>
/// Provee referencias globales del minimapa (cámara que renderiza a RenderTexture
/// y el contenedor UI donde se ubican los iconos). Colocar una única instancia
/// en la escena (por ejemplo en el Canvas que contiene la imagen del minimapa).
/// </summary>
public class MinimapController : MonoBehaviour
{
    public static MinimapController Instance { get; private set; }

    [Tooltip("Cámara que renderiza el minimapa (top-down)")]
    public Camera minimapCamera;

    [Tooltip("RectTransform del contenedor UI donde se colocarán los iconos del minimapa")]
    public RectTransform minimapContainer;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            // Debug warning removed for build cleanliness
            enabled = false;
            return;
        }
        Instance = this;
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }
}
