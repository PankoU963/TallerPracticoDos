using UnityEngine;

/// <summary>
/// Simple script for a minimap camera: follows the player's position but keeps
/// a fixed top-down rotation (north-up) so the minimap does not rotate with the player.
/// Attach to the minimap Camera (the one rendering to the RenderTexture).
/// </summary>
[ExecuteAlways]
public class MinimapFollow : MonoBehaviour
{
    [Tooltip("Transform del jugador a seguir")]
    public Transform target;

    [Tooltip("Altura sobre el jugador (en unidades de mundo)")]
    public float height = 50f;

    [Tooltip("Si true, la cámara sólo seguirá la posición X/Z del jugador (recomendado)")]
    public bool followXZOnly = true;

    [Tooltip("Suavizado de posición (0 = instantáneo)")]
    public float smoothSpeed = 10f;

    [Tooltip("Mantener el minimapa orientado al norte (Y = 0). Si false, la cámara rotará para coincidir con el yaw del jugador)")]
    public bool northUp = true;

    [Tooltip("Ángulo X fijo para la cámara (90 = mirando directamente hacia abajo)")]
    public float topDownAngle = 90f;

    void LateUpdate()
    {
        if (target == null) return;

        // Desired position: same X/Z as target (or full position) + height on Y
        Vector3 desired = followXZOnly ? new Vector3(target.position.x, target.position.y + height, target.position.z) : target.position + Vector3.up * height;

        if (smoothSpeed > 0f)
            transform.position = Vector3.Lerp(transform.position, desired, Time.deltaTime * smoothSpeed);
        else
            transform.position = desired;

        // Fix rotation: if northUp keep Y=0, otherwise align Y to target yaw
        if (northUp)
        {
            transform.rotation = Quaternion.Euler(topDownAngle, 0f, 90f);
        }
        else
        {
            float yaw = target.eulerAngles.y;
            transform.rotation = Quaternion.Euler(topDownAngle, yaw, 0f);
        }
    }
}
