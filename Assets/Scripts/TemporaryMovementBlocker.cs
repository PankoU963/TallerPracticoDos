using UnityEngine;

public class TemporaryMovementBlocker : MonoBehaviour
{
    public Vector3 LockPosition;

    private void OnEnable()
    {
        LockPosition = transform.position;
    }

    private void LateUpdate()
    {
        transform.position = LockPosition;
    }
}
