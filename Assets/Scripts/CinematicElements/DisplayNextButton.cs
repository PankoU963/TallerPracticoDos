using UnityEngine;

public class DisplayNextButton : MonoBehaviour
{
    [SerializeField] private GameObject nextButton;
    
    private void OnEnable()
    {
        TypeWriter.CompleteTextRevealed += ShowNextButton;
    }
    private void OnDisable()
    {
        TypeWriter.CompleteTextRevealed -= ShowNextButton;
    }

    private void ShowNextButton()
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
