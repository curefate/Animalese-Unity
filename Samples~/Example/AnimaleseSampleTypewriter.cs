using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

namespace Majulizi.Animalese.Samples
{
    /// <summary>
    /// Example typewriter component for Animalese:
    /// Reads text from a TMP_InputField, displays it character-by-character on a TMP_Text,
    /// and maintains strict timing synchronization with an AnimalesePlayer instance.
    /// </summary>
    public class AnimaleseSampleTypewriter : MonoBehaviour
    {
        [Header("UI Components")]
        [Tooltip("Input source: TMP_InputField containing the text to speak.")]
        [SerializeField]
        private TMP_InputField _inputField;

        [Tooltip("Output target: TextMeshPro component used for typewriter display.")]
        [SerializeField]
        private TMP_Text _outputText;

        [Header("Animalese Player")]
        [Tooltip("The AnimalesePlayer instance responsible for audio playback.")]
        [SerializeField]
        private AnimalesePlayer _animalesePlayer;

        // Runtime state fields
        private string _cachedFullText;
        private readonly List<VoiceToken> _currentTokens = new List<VoiceToken>();
        private bool _isListening = false;

        private void OnDisable()
        {
            UnbindPlayerEvents();
        }

        private void OnDestroy()
        {
            UnbindPlayerEvents();
        }

        /// <summary>
        /// Entry point for UI Button OnClick() events.
        /// Retrieves text from the assigned InputField and starts synchronized typewriter playback.
        /// </summary>
        public void OnClickPlayButton()
        {
            if (_inputField == null)
            {
                Debug.LogWarning("[AnimaleseSampleTypewriter] InputField not assigned in Inspector!", this);
                return;
            }

            PlayFromText(_inputField.text);
        }

        /// <summary>
        /// Starts synchronized typing and audio playback for the given text string.
        /// </summary>
        public void PlayFromText(string text)
        {
            if (_outputText == null || _animalesePlayer == null)
            {
                Debug.LogWarning("[AnimaleseSampleTypewriter] Output TMP_Text or AnimalesePlayer reference missing!", this);
                return;
            }

            // 1. Stop any ongoing playback
            StopPlayback();

            if (string.IsNullOrEmpty(text))
            {
                _outputText.text = string.Empty;
                return;
            }

            _cachedFullText = text;

            // 2. Set full text upfront but hide all characters initially to avoid layout recalculation jitter
            _outputText.text = _cachedFullText;
            _outputText.maxVisibleCharacters = 0;

            // 3. Force TMP to parse tags immediately and extract plain text for speech parsing
            _outputText.ForceMeshUpdate();
            string plainText = _outputText.GetParsedText();

            // 4. Parse plain text into reusable VoiceToken list (zero allocation)
            AnimaleseParser.Parse(plainText, _currentTokens);

            if (_currentTokens.Count == 0)
            {
                _outputText.maxVisibleCharacters = _outputText.textInfo.characterCount;
                return;
            }

            // 5. Subscribe to Player events for synchronous typing
            BindPlayerEvents();

            // 6. Start audio playback
            _animalesePlayer.Play(_currentTokens);
        }

        /// <summary>
        /// Instantly skips the typewriter effect, showing the full text and stopping audio.
        /// </summary>
        public void Skip()
        {
            StopPlayback();
            if (_outputText != null)
            {
                _outputText.maxVisibleCharacters = _outputText.textInfo.characterCount;
            }
        }

        /// <summary>
        /// Stops current playback and cleans up event subscriptions.
        /// </summary>
        public void StopPlayback()
        {
            UnbindPlayerEvents();

            if (_animalesePlayer != null && _animalesePlayer.IsPlaying)
            {
                _animalesePlayer.Stop();
            }
        }

        private void BindPlayerEvents()
        {
            if (_animalesePlayer != null && !_isListening)
            {
                _animalesePlayer.OnTokenPlayed += HandleTokenPlayed;
                _animalesePlayer.OnPlayCompleted += HandlePlayCompleted;
                _isListening = true;
            }
        }

        private void UnbindPlayerEvents()
        {
            if (_animalesePlayer != null && _isListening)
            {
                _animalesePlayer.OnTokenPlayed -= HandleTokenPlayed;
                _animalesePlayer.OnPlayCompleted -= HandlePlayCompleted;
                _isListening = false;
            }
        }

        /// <summary>
        /// Callback invoked whenever a token is consumed, advancing visible characters to match the token range.
        /// </summary>
        private void HandleTokenPlayed(VoiceToken token, int tokenIndex)
        {
            if (_outputText == null)
            {
                return;
            }

            int visibleCount;

            // Advance to the start of the next token, properly covering multi-character tokens like digraphs or ellipses
            if (_currentTokens != null && tokenIndex + 1 < _currentTokens.Count)
            {
                visibleCount = _currentTokens[tokenIndex + 1].CharIndex;
            }
            else
            {
                // Final token: reveal all characters
                visibleCount = _outputText.textInfo.characterCount;
            }

            _outputText.maxVisibleCharacters = Mathf.Clamp(visibleCount, 0, _outputText.textInfo.characterCount);
        }

        /// <summary>
        /// Ensures all characters are visible when playback completes.
        /// </summary>
        private void HandlePlayCompleted()
        {
            UnbindPlayerEvents();

            if (_outputText != null)
            {
                _outputText.maxVisibleCharacters = _outputText.textInfo.characterCount;
            }
        }
    }
}
