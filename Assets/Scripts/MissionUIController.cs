using System;
using UnityEngine;
using UnityEngine.UI;

public class MissionUIController : MonoBehaviour
{
    [SerializeField] private GameObject missionCanvas;

    private Action onAccept;
    private Action onClose;

    private void Awake()
    {
        if (missionCanvas != null) missionCanvas.SetActive(false);
    }

    public void Initialize(Action acceptCallback, Action closeCallback)
    {
        onAccept = acceptCallback;
        onClose = closeCallback;
        if (missionCanvas == null) return;

        var buttons = missionCanvas.GetComponentsInChildren<Button>(true);
        foreach (var b in buttons)
        {
            if (b == null) continue;
            string n = b.gameObject.name.ToLowerInvariant();
            if (n.Contains("accept") || n.Contains("acept"))
            {
                b.onClick.RemoveAllListeners();
                b.onClick.AddListener(() => onAccept?.Invoke());
            }
            if (n.Contains("close") || n.Contains("cancel") || n.Contains("cerrar") || n.Contains("cancelar"))
            {
                b.onClick.RemoveAllListeners();
                b.onClick.AddListener(() => onClose?.Invoke());
            }
        }

        var ipc = missionCanvas.GetComponentInChildren<ImagePopupController>(true);
        if (ipc != null)
        {
            ipc.OnClosed.RemoveAllListeners();
            ipc.OnClosed.AddListener(() => onClose?.Invoke());
        }
    }

    public void Show()
    {
        if (missionCanvas != null) missionCanvas.SetActive(true);
    }

    public void Hide()
    {
        if (missionCanvas != null) missionCanvas.SetActive(false);
    }
}
