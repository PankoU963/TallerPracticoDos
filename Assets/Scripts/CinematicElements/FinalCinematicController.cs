using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.SceneManagement;

public class FinalCinematicController : MonoBehaviour
{
    [SerializeField] private PlayableDirector director;
    [SerializeField] private string mainMenuSceneName = "MainMenu";
    [Header("Skip")]
    [SerializeField] private bool allowSkip = true;
    [SerializeField] private KeyCode skipKey = KeyCode.Escape;
    [SerializeField] private bool allowMouseClick = true;

    private void Awake()
    {
        if (director == null) director = GetComponent<PlayableDirector>();
    }

    private void OnEnable()
    {
        if (director != null) director.stopped += OnPlayableStopped;
    }

    private void OnDisable()
    {
        if (director != null) director.stopped -= OnPlayableStopped;
    }

    private void OnPlayableStopped(PlayableDirector pd)
    {
        LoadMainMenu();
    }

    private void Update()
    {
        if (!allowSkip || director == null) return;
        if (director.state != PlayState.Playing) return;

        if (Input.GetKeyDown(skipKey) || (allowMouseClick && Input.GetMouseButtonDown(0)))
        {
            SkipCinematic();
        }
    }

    public void SkipCinematic()
    {
        if (director != null && director.state == PlayState.Playing)
        {
            director.stopped -= OnPlayableStopped;
            director.Stop();
        }
        LoadMainMenu();
    }

    private void LoadMainMenu()
    {
        // Asegurarse de que el cursor y el tiempo estén en estado normal antes de cambiar de escena
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
        Time.timeScale = 1f;

        if (!string.IsNullOrEmpty(mainMenuSceneName))
            SceneManager.LoadScene(mainMenuSceneName);
        else
            Debug.LogWarning("FinalCinematicController: mainMenuSceneName is empty.");
    }
}