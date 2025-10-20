using UnityEngine;

public class ModeZone : MonoBehaviour
{
    public PlayerController.ControlMode zoneMode = PlayerController.ControlMode.FirstPerson;

    // ModeZone is now a thin delegate: the PlayerController owns cooldown/pause/exit state.
    private void OnTriggerEnter(Collider other)
    {
        var pc = other.GetComponent<PlayerController>();
        if (pc != null)
        {
            pc.TrySetModeFromZone(zoneMode);
        }
    }

    private void OnTriggerExit(Collider other)
    {
        var pc = other.GetComponent<PlayerController>();
        if (pc != null)
        {
            // pass the zone's center/position so the player can orient outward when exiting
            pc.NotifyZoneExit(transform.position);
        }
    }
}
