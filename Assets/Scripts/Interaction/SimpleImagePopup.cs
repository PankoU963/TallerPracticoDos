using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;

public class SimpleImagePopup : MonoBehaviour
{
    [Header("References")]
    public GameObject popupRoot;
    public Image imageDisplay;
    public Button closeButton;
    public Button fullscreenCloseButton;
    public TextMeshProUGUI promptText;

    [Header("Settings")]
    public string prompt = "Click para cerrar";
    public bool closeWithEscape = true;
    [SerializeField] private float ignoreClickDelay = 0.1f;

    private float openedAt = -10f;

    void Start()
    {
        if (popupRoot != null) popupRoot.SetActive(false);

        if (closeButton != null)
        {
            closeButton.onClick.RemoveAllListeners();
            closeButton.onClick.AddListener(Close);
        }
        if (fullscreenCloseButton != null)
        {
            fullscreenCloseButton.onClick.RemoveAllListeners();
            fullscreenCloseButton.onClick.AddListener(Close);
        }

        if (promptText != null) promptText.text = prompt;
    }

    void Update()
    {
        if (popupRoot == null || !popupRoot.activeSelf) return;

        if (closeWithEscape && Input.GetKeyDown(KeyCode.Escape))
        {
            Close();
            return;
        }

        if (Input.GetMouseButtonDown(0))
        {
            if (Time.time - openedAt < ignoreClickDelay) return;
            if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject()) return;
            if (fullscreenCloseButton == null) Close();
        }
    }

    // Abre el popup; opcionalmente asigna sprite antes de abrir
    public void Open(Sprite sprite = null)
    {
        if (popupRoot == null) return;
        if (imageDisplay != null && sprite != null) imageDisplay.sprite = sprite;
        if (promptText != null) promptText.text = prompt;
        popupRoot.SetActive(true);
        openedAt = Time.time;
    }

    public void Close()
    {
        if (popupRoot == null) return;
        popupRoot.SetActive(false);
    }
}