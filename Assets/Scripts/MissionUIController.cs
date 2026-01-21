using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class MissionUIController : MonoBehaviour
{
    [SerializeField] private GameObject missionCanvas;
    [SerializeField] private TextMeshProUGUI collectedText;
    [Header("Completion Message (optional)")]
    [Tooltip("Panel o GameObject que contiene el mensaje de misión completada (se activa al mostrar)")]
    public GameObject completionMessagePanel;

    [Tooltip("Texto UI (UnityEngine.UI.Text) opcional para el mensaje")]
    public Text uiMessageText;

    [Tooltip("Texto UI (TextMeshPro) opcional para el mensaje")]
    public TMP_Text messageTMPText;

    private Coroutine hideCoroutine;

    private Action onAccept;
    private Action onClose;

    private void Awake()
    {
        if (missionCanvas != null) missionCanvas.SetActive(false);
        if (completionMessagePanel != null) completionMessagePanel.SetActive(false);
        if (uiMessageText != null) uiMessageText.text = string.Empty;
        if (messageTMPText != null) messageTMPText.text = string.Empty;
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

    public void UpdateCollected(int collected, int total)
    {
        if (collectedText == null) return;

        // mostrar mensaje especial cuando se recolecten las 4 miniaturas
        if (total == 4 && collected >= total)
            collectedText.text = "Esculturas recolectadas. Lleva a la luz lo olvidado";
        else
            collectedText.text = $"{collected}/{total}";
    }

    public void ShowMessage(string message, float duration)
    {
        if (completionMessagePanel != null) completionMessagePanel.SetActive(true);
        if (uiMessageText != null) uiMessageText.text = message;
        if (messageTMPText != null) messageTMPText.text = message;

        if (hideCoroutine != null) StopCoroutine(hideCoroutine);
        if (duration > 0f) hideCoroutine = StartCoroutine(HideAfterSeconds(duration));
    }

    private IEnumerator HideAfterSeconds(float seconds)
    {
        yield return new WaitForSeconds(seconds);
        if (completionMessagePanel != null) completionMessagePanel.SetActive(false);
        if (uiMessageText != null) uiMessageText.text = string.Empty;
        if (messageTMPText != null) messageTMPText.text = string.Empty;
        hideCoroutine = null;
    }
}
