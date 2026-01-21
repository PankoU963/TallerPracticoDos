using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class DuplicateMissionUIController : MonoBehaviour
{
    [Tooltip("Panel o Canvas que contiene el mensaje de misión completada (desactivado por defecto)")]
    public GameObject messagePanel;

    [Tooltip("Texto UI (UnityEngine.UI.Text) opcional para el mensaje")]
    public Text messageText;

    [Tooltip("Texto UI (TextMeshPro) opcional para el mensaje")]
    public TMP_Text messageTMPText;

    private Coroutine hideCoroutine;

    public void ShowMessage(string message, float duration)
    {
        if (messagePanel != null) messagePanel.SetActive(true);
        if (messageText != null) messageText.text = message;
        if (messageTMPText != null) messageTMPText.text = message;

        if (hideCoroutine != null) StopCoroutine(hideCoroutine);
        if (duration > 0f) hideCoroutine = StartCoroutine(HideAfterSeconds(duration));
    }

    private IEnumerator HideAfterSeconds(float seconds)
    {
        yield return new WaitForSeconds(seconds);
        if (messagePanel != null) messagePanel.SetActive(false);
        if (messageText != null) messageText.text = string.Empty;
        if (messageTMPText != null) messageTMPText.text = string.Empty;
        hideCoroutine = null;
    }
}
