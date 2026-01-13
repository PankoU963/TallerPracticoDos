using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Events;
using TMPro;
using UnityEngine.EventSystems; // agregado

public class ImagePopupController : MonoBehaviour
{
    [Header("References")]
    [Tooltip("Root del panel que contiene la imagen (se activa/desactiva)")]
    public GameObject imageRoot;

    [Tooltip("Botón visible para cerrar (opcional)")]
    public Button closeButton;

    [Tooltip("Botón invisible que ocupa toda la pantalla para detectar clicks anywhere (opcional)")]
    public Button fullscreenCloseButton;

    [Tooltip("TextMeshProUGUI que mostrará el mensaje de cierre (opcional)")]
    public TextMeshProUGUI closePromptText;

    [Header("Settings")]
    [Tooltip("Texto que se muestra para indicar al jugador que haga click")]
    public string prompt = "Click para cerrar";
    [Tooltip("Si true también cierra al presionar Escape")]
    public bool closeWithEscape = true;
    [Tooltip("Si true también cierra al presionar la tecla E")]
    public bool closeWithE = true;

    [Header("Events")]
    public UnityEvent OnClosed;

    // NUEVO: tiempo en el que se abrió la ventana; usamos un pequeño delay para ignorar el mismo click que la abrió.
    [SerializeField, Tooltip("Segundos a ignorar clicks justo después de abrir")] private float ignoreClickDelay = 0.1f;
    private float openedAt = -10f;

    void Start()
    {
        if (imageRoot != null) imageRoot.SetActive(false);

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

        if (closePromptText != null)
            closePromptText.text = prompt;
    }

    void Update()
    {
        if (imageRoot != null && imageRoot.activeSelf)
        {
            if (closeWithEscape && Input.GetKeyDown(KeyCode.Escape))
                Close();

            if (closeWithE && Input.GetKeyDown(KeyCode.E))
                Close();

            // cerrar por click global SOLO si no existe fullscreenCloseButton
            if (Input.GetMouseButtonDown(0))
            {
                // ignorar el click si acabamos de abrir (evita que el click que abrió la imagen la cierre inmediatamente)
                if (Time.time - openedAt < ignoreClickDelay) return;

                // ignorar clicks sobre otros elementos UI
                if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject()) return;

                if (fullscreenCloseButton == null)
                {
                    Close();
                }
                // si fullscreenCloseButton está asignado, se espera que ese botón maneje el cierre
            }
        }
    }

    public void Open()
    {
        if (imageRoot == null) return;
        imageRoot.SetActive(true);
        openedAt = Time.time; // registrar cuándo se abrió
        if (closePromptText != null) closePromptText.text = prompt;
    }

    public void Close()
    {
        if (imageRoot == null) return;
        imageRoot.SetActive(false);
        OnClosed?.Invoke();
    }
}