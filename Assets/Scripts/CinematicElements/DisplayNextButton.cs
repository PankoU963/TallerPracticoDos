using UnityEngine;

public class DisplayNextButton : MonoBehaviour
{
    [SerializeField] private GameObject nextButton;
    
    public void OnEnable()
    {
        TypeWriter.CompleteTextRevealed += ShowNextButton;
    }
    public void OnDisable()
    {
        TypeWriter.CompleteTextRevealed -= ShowNextButton;
    }

    public void ShowNextButton()
    {
        if (nextButton != null)
            nextButton.SetActive(true);
    }

    public void HideNextButton()
    {
        if (nextButton != null)
            nextButton.SetActive(false);
    }
}
