using UnityEngine;
using UnityEngine.SceneManagement;

public class CinematicTrigger : MonoBehaviour
{
    [Tooltip("Nombre de la escena con la cinemática (añadir a Build Settings)")]
    public string cinematicSceneName;

    [Tooltip("Si true carga la escena de forma aditiva; si false reemplaza la actual")]
    public bool loadAdditively = false;

    [Tooltip("Tag del jugador para activar la carga")]
    public string playerTag = "Player";

    Collider col;

    void Awake()
    {
        col = GetComponent<Collider>();
        if (col == null || !col.isTrigger)
        {
            Debug.LogWarning("CinematicTrigger requiere un Collider configurado como isTrigger.");
            enabled = false;
            return;
        }
        col.enabled = false; // desactivado hasta completar misión
    }

    void OnEnable()
    {
        MisionBotero.OnMissionCompleted += OnMissionCompleted;
    }

    void OnDisable()
    {
        MisionBotero.OnMissionCompleted -= OnMissionCompleted;
    }

    void OnMissionCompleted()
    {
        // activar el trigger para que el jugador pueda entrar y lanzar la cinemática
        if (col != null) col.enabled = true;
    }

    void OnTriggerEnter(Collider other)
    {
        if (!other.CompareTag(playerTag)) return;
        if (string.IsNullOrEmpty(cinematicSceneName))
        {
            Debug.LogWarning("CinematicTrigger: cinematicSceneName no asignada.");
            return;
        }

        if (loadAdditively)
            SceneManager.LoadSceneAsync(cinematicSceneName, LoadSceneMode.Additive);
        else
            SceneManager.LoadScene(cinematicSceneName);
    }
}