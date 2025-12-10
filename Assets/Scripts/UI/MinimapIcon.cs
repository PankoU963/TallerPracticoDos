using UnityEngine;

/// <summary>
/// Controla la escala/visibilidad de un icono de minimapa asociado a un objeto objetivo.
/// El icono puede ser cualquier GameObject (por ejemplo un sprite o un mesh) que se escalará
/// en función de la distancia al jugador.
/// </summary>
public class MinimapIcon : MonoBehaviour
{
    [Tooltip("Transform que representa el icono (si está vacío se usa este GameObject)")]
    public Transform iconTransform;

    [Tooltip("Escala mínima cuando el jugador está lejos")]
    public float minScale = 0.1f;

    [Tooltip("Escala máxima cuando el jugador está muy cerca")]
    public float maxScale = 1f;

    [Tooltip("Distancia a la que la escala llegará a maxScale")]
    public float maxDistance = 30f;

    [Tooltip("Si true, el icono siempre estará activo (pero muy pequeño si está lejos). Si false, se ocultará si está fuera de maxDistance")]
    public bool alwaysVisible = true;
    [Header("UI Icon (optional)")]
    [Tooltip("Prefab del icono UI que se colocará en el minimap container. Si no se asigna, no habrá icono UI en el minimapa.")]
    public GameObject uiIconPrefab;
    [Tooltip("Padding en píxeles desde el borde del minimapa cuando se clampa el icono al borde")]
    public float edgePadding = 8f;

    private RectTransform uiIconRect;
    [Header("Manual references (optional)")]
    [Tooltip("Cámara del minimapa (si no se asigna, intentará encontrar una cámara llamada 'MinimapCamera')")]
    public Camera minimapCamera;
    [Tooltip("RectTransform contenedor del minimapa (si no se asigna, intentará encontrar un objecto llamado 'MinimapContainer')")]
    public RectTransform minimapContainer;

    private void Reset()
    {
        if (iconTransform == null) iconTransform = transform;
    }

    private void OnDestroy()
    {
        if (uiIconRect != null)
        {
            try { Destroy(uiIconRect.gameObject); } catch { }
            uiIconRect = null;
        }
    }

    private void Awake()
    {
        if (iconTransform == null) iconTransform = transform;
    }

    /// <summary>
    /// Actualiza la escala del icono según la distancia al jugador.
    /// </summary>
    public void UpdateScaleByDistance(float distance)
    {
        if (iconTransform == null) return;

        float t = Mathf.Clamp01(1f - (distance / maxDistance));
        float s = Mathf.Lerp(minScale, maxScale, t);
        iconTransform.localScale = Vector3.one * s;

        if (alwaysVisible)
        {
            if (!iconTransform.gameObject.activeSelf) iconTransform.gameObject.SetActive(true);
        }
        else
        {
            bool shouldShow = distance <= maxDistance;
            if (iconTransform.gameObject.activeSelf != shouldShow)
                iconTransform.gameObject.SetActive(shouldShow);
        }

        // Update or create UI icon in minimap (if prefab available). The minimap camera/container
        // can be assigned manually on this component; otherwise we try to find objects by name.
        if (uiIconPrefab != null)
        {
            if (minimapCamera == null)
            {
                var camGO = GameObject.Find("MinimapCamera");
                if (camGO != null) minimapCamera = camGO.GetComponent<Camera>();
            }
            if (minimapContainer == null)
            {
                var contGO = GameObject.Find("MinimapContainer");
                if (contGO != null) minimapContainer = contGO.GetComponent<RectTransform>();
            }

            if (minimapCamera != null && minimapContainer != null)
            {
                if (uiIconRect == null)
                {
                    // Instantiate without parent to avoid errors when parent is a persistent prefab asset.
                    var go = Instantiate(uiIconPrefab);
                    uiIconRect = go.GetComponent<RectTransform>();
                    if (uiIconRect == null)
                        uiIconRect = go.AddComponent<RectTransform>();

                    // If the minimap container exists in the active scene, parent the icon to it.
                    if (minimapContainer != null && minimapContainer.gameObject.scene.IsValid())
                    {
                        uiIconRect.SetParent(minimapContainer, false);
                    }
                }

                UpdateUIPositionAndVisibility(minimapCamera, minimapContainer);
                if (uiIconRect != null) uiIconRect.localScale = Vector3.one * s;
            }
        }
    }

    private void UpdateUIPositionAndVisibility(Camera minimapCam, RectTransform container)
    {
        if (uiIconRect == null || minimapCam == null || container == null) return;

        Vector3 worldPos = transform.position;
        Vector3 vp = minimapCam.WorldToViewportPoint(worldPos);

        bool inFront = vp.z > 0f;
        Vector2 viewport = new Vector2(vp.x, vp.y);

        Vector2 size = container.rect.size;
        Vector2 center = size * 0.5f;

        // Compute local position relative to center
        Vector2 localPos = (viewport - Vector2.one * 0.5f) * size;

        // Determine if inside viewport [0,1]
        bool onScreen = inFront && viewport.x >= 0f && viewport.x <= 1f && viewport.y >= 0f && viewport.y <= 1f;

        Vector2 finalPos = localPos;
        if (!onScreen)
        {
            // Clamp to edge: keep direction from center, clamp magnitude to rect half-size minus padding
            float halfW = size.x * 0.5f - edgePadding;
            float halfH = size.y * 0.5f - edgePadding;
            // Clamp by ellipse approximation
            float rx = halfW;
            float ry = halfH;
            if (localPos == Vector2.zero)
                finalPos = new Vector2(0f, ry);
            else
            {
                float x = localPos.x;
                float y = localPos.y;
                // scale factor to fit into ellipse x^2/rx^2 + y^2/ry^2 = 1
                float k = Mathf.Sqrt((x * x) / (rx * rx) + (y * y) / (ry * ry));
                if (k > 0f)
                    finalPos = new Vector2(x / k, y / k);
                else
                    finalPos = new Vector2(Mathf.Sign(x) * rx, Mathf.Sign(y) * ry);
            }
        }

        uiIconRect.anchoredPosition = finalPos;

        // Visibility based on alwaysVisible and distance/onscreen
        if (alwaysVisible)
        {
            if (!uiIconRect.gameObject.activeSelf) uiIconRect.gameObject.SetActive(true);
        }
        else
        {
            uiIconRect.gameObject.SetActive(onScreen);
        }
    }
}
