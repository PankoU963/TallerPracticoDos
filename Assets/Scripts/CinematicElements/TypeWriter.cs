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

        _skipDelay = new WaitForSeconds(1 / (charactersPerSecond * skipSpeedUp));
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
        if (!_readyForNewText)  return;

        _readyForNewText = false;


        if (_typingWriteCoroutine != null)
        {
            StopCoroutine(_typingWriteCoroutine);
        }

        // _textBox.text = text;
        _textBox.maxVisibleCharacters = 0;
        _currentVisibleCharacterIndex = 0;

        _typingWriteCoroutine = StartCoroutine(TypewriterCoroutine());
    }

    private IEnumerator TypewriterCoroutine()
    {
        TMP_TextInfo textInfo = _textBox.textInfo;

        while (_currentVisibleCharacterIndex < textInfo.characterCount + 1)
        {
            var lastCharacterIndex = textInfo.characterCount - 1;

            if (_currentVisibleCharacterIndex == lastCharacterIndex)
            {
                _textBox.maxVisibleCharacters++;
                yield return _textboxFullEventDelay;
                CompleteTextRevealed?.Invoke();
                _readyForNewText = true;
                yield break;
            }

            char character = textInfo.characterInfo[_currentVisibleCharacterIndex].character;
            _textBox.maxVisibleCharacters++; 

            if (!CurrentSkipping && (character == '.' || character == ',' || character == ';' || character == ':' || 
            character == '!' || character == '?' || character == '-' ))
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
    }

    // void Skip()
    // {
    //     if (CurrentSkipping)
    //         return;

    //     CurrentSkipping = true;
    //     if (!quickSkip)
    //     {
    //         StartCoroutine(SkipSpeedUpReset());
    //         return;
    //     }

    //     StopCoroutine(_typingWriteCoroutine);
    //     _textBox.maxVisibleCharacters = _textBox.textInfo.characterCount;
    //     _readyForNewText = true;
    //     CompleteTextRevealed?.Invoke();

    // }
    private void Skip(bool quickSkipNeeded = false)
        {
            if (CurrentSkipping)
                return;
            
            CurrentSkipping = true;

            if (!quickSkip || !quickSkipNeeded)
            {
                StartCoroutine(SkipSpeedUpReset());
                return;
            }

            StopCoroutine(_typingWriteCoroutine);
            _textBox.maxVisibleCharacters = _textBox.textInfo.characterCount;
            _readyForNewText = true;
            CompleteTextRevealed?.Invoke();
        }

    private IEnumerator SkipSpeedUpReset()
    {
        yield return new WaitUntil(() =>_textBox.maxVisibleCharacters == _textBox.textInfo.characterCount - 1);
        CurrentSkipping = false;
    }

}
