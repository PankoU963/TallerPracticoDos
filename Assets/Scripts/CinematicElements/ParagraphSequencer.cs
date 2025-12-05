using System;
using System.Collections;
using UnityEngine;
using TMPro;

/// <summary>
/// Shows multiple paragraphs sequentially using the existing `TypeWriter` on the same TMP_Text.
/// Usage:
/// - Assign the `targetText` (TMP_Text) which should also have a `TypeWriter` component.
/// - Fill `paragraphs` in the Inspector or call `StartSequence(string[])` at runtime.
/// - When each paragraph finishes revealing, the sequencer can either wait for a key press, a UI button
///   (call `Advance()`), or auto-advance after `autoAdvanceDelay` seconds.
/// </summary>
public class ParagraphSequencer : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private TMP_Text targetText;
    [SerializeField, Tooltip("Optional: if not assigned, will try to get a TypeWriter from the same GameObject")] private TypeWriter typeWriter;
    [Header("Auto Start")]
    [SerializeField, Tooltip("If true the sequencer will start automatically when the GameObject is enabled (useful for Play mode testing)")] private bool startOnEnable = false;
    [SerializeField, Tooltip("Delay in seconds before auto-starting the sequence when Start On Enable is true")] private float startDelay = 0f;

    [Header("Paragraphs")]
    [TextArea(3,6)]
    [SerializeField] private string[] paragraphs;

    [Header("Advance Options")]
    [SerializeField, Tooltip("If true, waits for player input (key or button) to move to the next paragraph")] private bool waitForPlayerInput = true;
    [SerializeField, Tooltip("Key to advance when waitForPlayerInput is true")] private KeyCode advanceKey = KeyCode.Space;
    [SerializeField, Tooltip("Optional UI button to show when paragraph is complete. Call its OnClick to call Advance().")]
    private GameObject nextButton;
    [SerializeField, Tooltip("If > 0 and waitForPlayerInput is false, the sequencer will auto-advance after this delay (seconds).")]
    private float autoAdvanceDelay = 0f;
    [SerializeField, Tooltip("If true, automatically advance to the next paragraph immediately after the TypeWriter completes.")]
    private bool advanceOnComplete = true;

    // runtime
    private int currentIndex = -1;
    private bool waitingForAdvance = false;
    private bool sequenceRunning = false;

    private void Awake()
    {
        if (targetText == null)
            targetText = GetComponent<TMP_Text>();
        if (typeWriter == null && targetText != null)
            typeWriter = targetText.GetComponent<TypeWriter>();

        if (nextButton != null)
            nextButton.SetActive(false);
    }

    private void OnEnable()
    {
        if (startOnEnable)
        {
            if (startDelay <= 0f)
                StartSequence();
            else
                StartCoroutine(AutoStartAfterDelay(startDelay));
        }
    }

    private IEnumerator AutoStartAfterDelay(float delay)
    {
        yield return new WaitForSeconds(delay);
        StartSequence();
    }

    private void OnDisable()
    {
        // hide button when disabled
        if (nextButton != null)
            nextButton.SetActive(false);
    }

    /// <summary>
    /// Start the paragraph sequence using the paragraphs assigned in the Inspector.
    /// </summary>
    public void StartSequence()
    {
        if (sequenceRunning) return;
        StartCoroutine(RunSequence(paragraphs));
    }

    /// <summary>
    /// Start the paragraph sequence with the supplied paragraphs.
    /// </summary>
    public void StartSequence(string[] paragraphsToUse)
    {
        if (sequenceRunning) return;
        StartCoroutine(RunSequence(paragraphsToUse));
    }

    /// <summary>
    /// Call this from a UI button (OnClick) to advance to the next paragraph when the sequencer is waiting.
    /// </summary>
    public void Advance()
    {
        if (!waitingForAdvance) return;
        waitingForAdvance = false;
    }

    private IEnumerator RunSequence(string[] list)
    {
        if (list == null || list.Length == 0)
            yield break;

        sequenceRunning = true;
        currentIndex = 0;

        for (; currentIndex < list.Length; currentIndex++)
        {
            string paragraph = list[currentIndex] ?? string.Empty;

            // We'll subscribe to the TypeWriter completion event BEFORE changing the text
            // to avoid races where the TypeWriter finishes too quickly and the sequencer misses the event.
            bool paragraphDone = false;
            Action onComplete = () => paragraphDone = true;
            TypewriterEffect.CompleteTextRevealed += onComplete;

            // Set text: TypeWriter listens to TMPro text changed event, so setting text will trigger the typing.
            if (targetText != null)
                targetText.text = paragraph;
            else
            {
                Debug.LogWarning("ParagraphSequencer: targetText not assigned.");
            }

            // If we have a TypeWriter instance, explicitly prepare it in case its TEXT_CHANGED event
            // subscription timing caused it to miss the change (keeps behavior robust at Start/OnEnable ordering).
            if (typeWriter != null)
            {
                typeWriter.PrepareForNewText(targetText);
            }
            else
            {
                Debug.LogWarning("ParagraphSequencer: no TypeWriter assigned on target text. Sequencer will not wait for TypeWriter completion.");
                // If no TypeWriter is present, mark paragraph done immediately so sequencer can continue.
                paragraphDone = true;
            }

            // Wait until TypeWriter signals completion
            yield return new WaitUntil(() => paragraphDone);

            TypewriterEffect.CompleteTextRevealed -= onComplete;

            // Show/hide next button depending on whether we auto-advance
            if (nextButton != null)
                nextButton.SetActive(!advanceOnComplete);

            // Auto-advance immediately on complete if requested
            if (advanceOnComplete)
            {
                if (autoAdvanceDelay > 0f)
                    yield return new WaitForSeconds(autoAdvanceDelay);
            }
            else
            {
                // Wait for advance (key/button) or auto advance
                if (waitForPlayerInput)
                {
                    waitingForAdvance = true;
                    // If user presses key while waiting, advance
                    while (waitingForAdvance)
                    {
                        if (Input.GetKeyDown(advanceKey))
                        {
                            waitingForAdvance = false;
                            break;
                        }
                        yield return null;
                    }
                }
                else if (autoAdvanceDelay > 0f)
                {
                    yield return new WaitForSeconds(autoAdvanceDelay);
                }

                // hide next button and continue
                if (nextButton != null)
                    nextButton.SetActive(false);
            }

            // Allow a small frame so UI updates before starting next paragraph
            yield return null;
        }

        sequenceRunning = false;
        currentIndex = -1;
    }

    // (No local PrepareForNewText implementation needed here.)
}
