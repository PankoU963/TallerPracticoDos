using System;
using System.Collections;
using UnityEngine;
using TMPro;
using Object = UnityEngine.Object;

[RequireComponent(typeof(TMP_Text))]

public class TypeWriter : MonoBehaviour
{
    private TMP_Text _textBox;


    //Basic TypeWriter funcionality
    private int _currentVisibleCharacterIndex;
    private Coroutine _typingWriteCoroutine;
    private bool _readyForNewText = true;

    private WaitForSeconds _simpleDelay;
    private WaitForSeconds _interpuntuationDelay;

    [Header("TypeWriter Settings")]
    [SerializeField] private float charactersPerSecond = 20f;
    [SerializeField] private float interpuntuationDelay = 0.3f;

    // Skipping functionality
    public bool CurrentSkipping { get; private set; }
    private WaitForSeconds _skipDelay;

    [Header("Skip options")]
    [SerializeField] private bool quickSkip;
    [SerializeField] [Min(1)] private int skipSpeedUp = 5;

    private ParagraphSequencer paragraphSequencer;

    // Events Funcionality
    private WaitForSeconds _textboxFullEventDelay;
    [SerializeField] [Range(0.1f, 0.5f)] private float sendDoneDelay = 0.25f;
    public static event Action CompleteTextRevealed;
    public static event Action<char> CharacterRevealed;

    private void Awake()
    {
        _textBox = GetComponent<TMP_Text>();
        _simpleDelay = new WaitForSeconds(1f / charactersPerSecond);
        _interpuntuationDelay = new WaitForSeconds(interpuntuationDelay);
        _skipDelay = new WaitForSeconds(1f / (charactersPerSecond * skipSpeedUp));
        _textboxFullEventDelay = new WaitForSeconds(sendDoneDelay);

        // paragraphSequencer may be assigned in the inspector; only call StartSequence if assigned.
        if (paragraphSequencer != null)
            paragraphSequencer.StartSequence();
    }

    private void OnEnable()
    {
        TMPro_EventManager.TEXT_CHANGED_EVENT.Add(PrepareForNewText);
    }

    private void OnDisable()
    {
        TMPro_EventManager.TEXT_CHANGED_EVENT.Remove(PrepareForNewText);
    }

    private void Update()
    {
        if (Input.GetMouseButtonDown(1))
        {
            if (_textBox.maxVisibleCharacters != _textBox.textInfo.characterCount - 1)
                Skip();
        }
    }

    public void PrepareForNewText(Object obj)
    {
        if (!_readyForNewText) return;

        // Ensure we have a reference to the TMP_Text. The event may pass the TMP_Text instance
        // (or be called before Awake depending on execution order), so try to recover from the
        // provided object or GetComponent fallback.
        if (_textBox == null)
        {
            // Try the event object first
            if (obj is TMP_Text tmpFromEvent)
            {
                _textBox = tmpFromEvent;
            }
            else if (obj is GameObject go)
            {
                _textBox = go.GetComponent<TMP_Text>();
            }
            else
            {
                // Try to find one on this GameObject
                _textBox = GetComponent<TMP_Text>();
            }
        }

        if (_textBox == null)
        {
            Debug.LogWarning("TypeWriter: TMP_Text is missing. Cannot start typewriter.");
            return;
        }

        _readyForNewText = false;

        if (_typingWriteCoroutine != null)
        {
            StopCoroutine(_typingWriteCoroutine);
            _typingWriteCoroutine = null;
        }

        _textBox.maxVisibleCharacters = 0;
        _currentVisibleCharacterIndex = 0;

        _typingWriteCoroutine = StartCoroutine(TypewriterCoroutine());
    }

    private IEnumerator TypewriterCoroutine()
    {
        TMP_TextInfo textInfo = _textBox.textInfo;
        int totalChars = textInfo.characterCount;

        while (_currentVisibleCharacterIndex < totalChars)
        {
            // defensive: refresh textInfo each loop in case TMP updated it
            textInfo = _textBox.textInfo;
            if (_currentVisibleCharacterIndex >= textInfo.characterCount)
                break;

            char character = textInfo.characterInfo[_currentVisibleCharacterIndex].character;
            _textBox.maxVisibleCharacters++;

            if (!CurrentSkipping && (character == '.' || character == ',' || character == ';' || character == ':' ||
                                     character == '!' || character == '?' || character == '-'))
            {
                yield return _interpuntuationDelay;
            }
            else
            {
                yield return CurrentSkipping ? _skipDelay : _simpleDelay;
            }

            CharacterRevealed?.Invoke(character);
            _currentVisibleCharacterIndex++;
        }

        // finalization: wait a small configured delay then signal completion
        if (_textboxFullEventDelay != null)
            yield return _textboxFullEventDelay;
        CompleteTextRevealed?.Invoke();
        _readyForNewText = true;
        yield break;
    }

    void Skip()
    {
        if (CurrentSkipping)
            return;

        CurrentSkipping = true;
        if (!quickSkip)
        {
            StartCoroutine(SkipSpeedUpReset());
            return;
        }

        StopCoroutine(_typingWriteCoroutine);
        _textBox.maxVisibleCharacters = _textBox.textInfo.characterCount;
        _readyForNewText = true;
        CompleteTextRevealed?.Invoke();

    }
    // private void Skip(bool quickSkipNeeded = false)
    //     {
    //         if (CurrentSkipping)
    //             return;
            
    //         CurrentSkipping = true;

    //         if (!quickSkip || !quickSkipNeeded)
    //         {
    //             StartCoroutine(SkipSpeedUpReset());
    //             return;
    //         }

    //         StopCoroutine(_typingWriteCoroutine);
    //         _textBox.maxVisibleCharacters = _textBox.textInfo.characterCount;
    //         _readyForNewText = true;
    //         CompleteTextRevealed?.Invoke();
    //     }

    private IEnumerator SkipSpeedUpReset()
    {
        yield return new WaitUntil(() =>_textBox.maxVisibleCharacters == _textBox.textInfo.characterCount - 1);
        CurrentSkipping = false;
    }

}
