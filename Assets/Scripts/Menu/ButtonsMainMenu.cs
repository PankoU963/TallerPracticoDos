using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Handlers for the main menu buttons: Play, Credits, Exit.
/// - Assign `cinematicSceneName` to the cinematic scene (optional). If empty, Play loads `gameSceneName` directly.
/// - Credits: assign a UI panel GameObject to `creditsPanel` (recommended: inside the main menu scene so it toggles visibility).
/// - Exit will stop play mode in the editor and call Application.Quit() in builds.
/// </summary>
public class ButtonsMainMenu : MonoBehaviour
{
    [Header("Scene Names")]
    [Tooltip("Optional cinematic scene to play first. If empty, Play will load Game Scene directly.")]
    [SerializeField] private string cinematicSceneName = "";
    [Tooltip("Scene to load after the cinematic OR if no cinematic is set.")]
    [SerializeField] private string gameSceneName = "GameScene";

    [Header("UI")]
    [Tooltip("Optional credits panel (UI GameObject). We'll toggle its active state when the Credits button is used.")]
    [SerializeField] private GameObject creditsPanel;

    [Header("Optional audio")]
    [Tooltip("Optional AudioSource to play UI click sounds")]
    [SerializeField] private AudioSource uiAudio;
    [SerializeField] private AudioClip clickClip;

    // Public methods to wire to UI Buttons OnClick in the Inspector
    public void OnPlayButton()
    {
        if (uiAudio != null && clickClip != null) uiAudio.PlayOneShot(clickClip);

        if (!string.IsNullOrEmpty(cinematicSceneName))
        {
            SceneManager.LoadScene(cinematicSceneName);
        }
        else if (!string.IsNullOrEmpty(gameSceneName))
        {
            SceneManager.LoadScene(gameSceneName);
        }
        else
        {
            Debug.LogWarning("ButtonsMainMenu: No cinematicSceneName or gameSceneName configured.");
        }
    }

    /// <summary>
    /// Toggle the Credits panel. Use `true` to show, `false` to hide.
    /// If you prefer a single button that toggles, wire it to `ToggleCredits()` instead.
    /// </summary>
    public void ShowCredits(bool show)
    {
        if (uiAudio != null && clickClip != null) uiAudio.PlayOneShot(clickClip);
        if (creditsPanel != null) creditsPanel.SetActive(show);
        else Debug.LogWarning("ButtonsMainMenu: creditsPanel not assigned.");
    }

    /// <summary>
    /// Convenience toggle for a single Credits button.
    /// </summary>
    public void ToggleCredits()
    {
        if (creditsPanel == null)
        {
            Debug.LogWarning("ButtonsMainMenu: creditsPanel not assigned.");
            return;
        }
        bool next = !creditsPanel.activeSelf;
        ShowCredits(next);
    }

    /// <summary>
    /// Exit the game. In Editor this stops play mode; in builds it quits the application.
    /// </summary>
    public void OnExitButton()
    {
        if (uiAudio != null && clickClip != null) uiAudio.PlayOneShot(clickClip);

        Debug.Log("ButtonsMainMenu: Exit requested.");
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }
}
