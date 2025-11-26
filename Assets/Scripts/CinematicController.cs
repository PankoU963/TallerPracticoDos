using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.SceneManagement;

/// <summary>
/// Attach to an object that owns the PlayableDirector (Timeline) for the cinematic.
/// When the PlayableDirector stops (cinematic ends) this controller will load the configured next scene.
/// This is a simple, reliable way to move from a dedicated cinematic scene into the main gameplay scene.
/// </summary>
public class CinematicController : MonoBehaviour
{
    [Tooltip("PlayableDirector that plays the cinematic. If left empty, the script will try to get one from the same GameObject.")]
    [SerializeField] private PlayableDirector director;
    [Tooltip("Name of the scene to load when the cinematic finishes.")]
    [SerializeField] private string nextSceneName = "GameScene";
    [Header("Skip")]
    [Tooltip("Allow the player to skip the cinematic by input.")]
    [SerializeField] private bool allowSkip = true;
    [Tooltip("Key used to skip the cinematic (when allowSkip is true).")]
    [SerializeField] private KeyCode skipKey = KeyCode.Escape;
    [Tooltip("Also allow skipping by mouse click (left button)")]
    [SerializeField] private bool allowMouseClick = true;

    private void Awake()
    {
        if (director == null)
            director = GetComponent<PlayableDirector>();
    }

    private void OnEnable()
    {
        if (director != null)
            director.stopped += OnPlayableStopped;
    }

    private void OnDisable()
    {
        if (director != null)
            director.stopped -= OnPlayableStopped;
    }

    private void OnPlayableStopped(PlayableDirector pd)
    {
        if (!string.IsNullOrEmpty(nextSceneName))
        {
            SceneManager.LoadScene(nextSceneName);
        }
        else
        {
            Debug.LogWarning("CinematicController: nextSceneName is empty — nothing to load after cinematic.");
        }
    }

    private void Update()
    {
        if (!allowSkip || director == null) return;

        // Only allow skipping while the director is playing
        if (director.state != PlayState.Playing) return;

        // Check key
        if (Input.GetKeyDown(skipKey))
        {
            SkipCinematic();
            return;
        }

        // Check mouse
        if (allowMouseClick && Input.GetMouseButtonDown(0))
        {
            SkipCinematic();
            return;
        }
    }

    /// <summary>
    /// Public method to skip the cinematic programmatically (or from a UI button).
    /// </summary>
    public void SkipCinematic()
    {
        if (director != null && director.state == PlayState.Playing)
        {
            // Stop director to make sure stopped event doesn't fire twice; we'll manually load next scene.
            director.stopped -= OnPlayableStopped;
            director.Stop();
        }

        if (!string.IsNullOrEmpty(nextSceneName))
        {
            SceneManager.LoadScene(nextSceneName);
        }
        else
        {
            Debug.LogWarning("CinematicController.SkipCinematic: nextSceneName is empty — cannot load next scene.");
        }
    }
}
