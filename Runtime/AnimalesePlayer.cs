using System;
using UnityEngine;

namespace Majulizi.Animalese
{
    /// <summary>
    /// Core Animalese audio playback component driven by an internal Update() state machine.
    /// </summary>
    [RequireComponent(typeof(AudioSource))]
    public class AnimalesePlayer : MonoBehaviour
    {
        [Header("Audio Components")]
        [SerializeField]
        private AudioSource _audioSource;

        [Header("Voice Configuration")]
        [Tooltip("The VoiceProfile asset used by this player.")]
        [SerializeField]
        private VoiceProfileSO _profile;

        // Runtime state machine fields
        private VoiceTokenList _tokens;
        private int _currentIndex = 0;
        private float _timer = 0f;
        private float _speedMultiplier = 1.0f;
        private bool _isPlaying = false;
        private bool _isPaused = false;
        private int _phonemeStepCounter = 0;

        // Filter component caches
        private AudioLowPassFilter _lowPassFilter;
        private AudioHighPassFilter _highPassFilter;

        /// <summary>
        /// Dispatched whenever a token is consumed and played. Useful for synchronized UI typewriter effects.
        /// (Parameters: VoiceToken token, int tokenIndex)
        /// </summary>
        public event Action<VoiceToken, int> OnTokenPlayed;

        /// <summary>
        /// Dispatched when playback of the entire token sequence completes.
        /// </summary>
        public event Action OnPlayCompleted;

        /// <summary>
        /// The active VoiceProfile configuration for this player (Single Source of Truth).
        /// </summary>
        public VoiceProfileSO Profile
        {
            get => _profile;
            set
            {
                _profile = value;
                ApplyProfileFilters(_profile);
            }
        }

        public bool IsPlaying => _isPlaying;
        public bool IsPaused => _isPaused;
        public float SpeedMultiplier => _speedMultiplier;

        private void Awake()
        {
            if (_audioSource == null)
            {
                _audioSource = GetComponent<AudioSource>();
            }

            if (_audioSource != null)
            {
                _audioSource.playOnAwake = false;
                _audioSource.loop = false;
            }

            _lowPassFilter = GetComponent<AudioLowPassFilter>();
            _highPassFilter = GetComponent<AudioHighPassFilter>();
        }

        private void Update()
        {
            if (!_isPlaying || _isPaused || _tokens == null)
            {
                return;
            }

            // Advance timer (supports dynamic speed changes)
            _timer -= Time.deltaTime * Mathf.Max(0.01f, _speedMultiplier);

            while (_timer <= 0f && _isPlaying)
            {
                if (_currentIndex >= _tokens.Count)
                {
                    CompletePlayback();
                    return;
                }

                VoiceToken token = _tokens[_currentIndex];
                ProcessToken(token);
                _currentIndex++;
            }
        }

        /// <summary>
        /// Parses text and plays it using the currently assigned Profile.
        /// </summary>
        public void Play(string text)
        {
            Play(text, _profile);
        }

        /// <summary>
        /// Overrides the current Profile, parses the text, and plays it.
        /// </summary>
        public void Play(string text, VoiceProfileSO profile)
        {
            if (profile != null)
            {
                _profile = profile;
            }

            var tokens = AnimaleseParser.Parse(text);
            Play(tokens, _profile);
        }

        /// <summary>
        /// Plays an existing token list using the currently assigned Profile.
        /// </summary>
        public void Play(VoiceTokenList tokens)
        {
            Play(tokens, _profile);
        }

        /// <summary>
        /// Overrides the current Profile and plays an existing token list.
        /// </summary>
        public void Play(VoiceTokenList tokens, VoiceProfileSO profile)
        {
            Stop();

            if (profile != null)
            {
                _profile = profile;
            }

            if (tokens == null || tokens.Count == 0)
            {
                OnPlayCompleted?.Invoke();
                return;
            }

            _tokens = tokens;
            _currentIndex = 0;
            _timer = 0f;
            _phonemeStepCounter = 0;
            _isPlaying = true;
            _isPaused = false;

            ApplyProfileFilters(_profile);
        }

        /// <summary>
        /// Immediately stops playback and clears state.
        /// </summary>
        public void Stop()
        {
            _isPlaying = false;
            _isPaused = false;
            _currentIndex = 0;
            _timer = 0f;

            if (_audioSource != null && _audioSource.isPlaying)
            {
                _audioSource.Stop();
            }
        }

        /// <summary>
        /// Pauses playback.
        /// </summary>
        public void Pause()
        {
            _isPaused = true;
        }

        /// <summary>
        /// Resumes paused playback.
        /// </summary>
        public void Resume()
        {
            _isPaused = false;
        }

        /// <summary>
        /// Sets the playback speed multiplier dynamically (e.g. 2x fast-forward).
        /// </summary>
        public void SetSpeedMultiplier(float multiplier)
        {
            _speedMultiplier = Mathf.Max(0.1f, multiplier);
        }

        private void ProcessToken(VoiceToken token)
        {
            // 1. Calculate physical time required for this token
            float baseInterval = _profile != null ? _profile.baseInterval : 0.06f;
            float speechRate = (_profile != null && _profile.speechRate > 0.01f) ? _profile.speechRate : 1.0f;
            float duration = (baseInterval * token.RelativeDuration) / speechRate;
            _timer += duration;

            // 2. If it is a phoneme token, perform audio playback
            if (token.Type == VoiceTokenType.Phoneme)
            {
                _phonemeStepCounter++;
                int step = _profile != null ? Mathf.Max(1, _profile.phonemeStep) : 1;

                if (_phonemeStepCounter % step == 0)
                {
                    AudioClip clip = FindClipForToken(token);
                    if (clip != null && _audioSource != null)
                    {
                        float basePitch = _profile != null ? _profile.basePitch : 1.0f;
                        float riseFactor = _profile != null ? _profile.questionPitchRise : 0.35f;
                        float jitterRange = _profile != null ? _profile.pitchJitter : 0.08f;
                        float jitter = UnityEngine.Random.Range(-jitterRange, jitterRange);

                        float finalPitch = basePitch * (1.0f + token.PitchOffset * riseFactor) + jitter;
                        _audioSource.pitch = Mathf.Clamp(finalPitch, 0.1f, 3.0f);

                        float baseVolume = _profile != null ? _profile.volume : 1.0f;
                        float finalVolume = Mathf.Clamp01(baseVolume * token.VolumeScale);
                        _audioSource.PlayOneShot(clip, finalVolume);
                    }
                }
            }

            // 3. Dispatch token played event
            OnTokenPlayed?.Invoke(token, _currentIndex);
        }

        private AudioClip FindClipForToken(VoiceToken token)
        {
            if (_profile == null || _profile.phonemeMap == null)
            {
                return null;
            }

            // 1. If explicit phoneme ID exists (e.g. English letter or digraph), look it up directly
            if (!string.IsNullOrEmpty(token.PhonemeId))
            {
                if (_profile.phonemeMap.TryGetClip(token.PhonemeId, out var clip))
                {
                    return clip;
                }
            }

            // 2. Fallback using Unicode codepoint hash for non-English or missing phonemes
            int codePoint = char.ConvertToUtf32(token.SourceChar.ToString(), 0);
            if (_profile.phonemeMap.GetFallbackClip(codePoint, out _, out var fallbackClip))
            {
                return fallbackClip;
            }

            return null;
        }

        private void ApplyProfileFilters(VoiceProfileSO profile)
        {
            if (profile == null)
            {
                if (_lowPassFilter != null) _lowPassFilter.enabled = false;
                if (_highPassFilter != null) _highPassFilter.enabled = false;
                return;
            }

            // Low-pass filter handling
            if (profile.enableLowPass)
            {
                if (_lowPassFilter == null)
                {
                    _lowPassFilter = gameObject.AddComponent<AudioLowPassFilter>();
                }
                _lowPassFilter.enabled = true;
                _lowPassFilter.cutoffFrequency = profile.lowPassCutoff;
                _lowPassFilter.lowpassResonanceQ = profile.lowPassResonance;
            }
            else if (_lowPassFilter != null)
            {
                _lowPassFilter.enabled = false;
            }

            // High-pass filter handling
            if (profile.enableHighPass)
            {
                if (_highPassFilter == null)
                {
                    _highPassFilter = gameObject.AddComponent<AudioHighPassFilter>();
                }
                _highPassFilter.enabled = true;
                _highPassFilter.cutoffFrequency = profile.highPassCutoff;
                _highPassFilter.highpassResonanceQ = profile.highPassResonance;
            }
            else if (_highPassFilter != null)
            {
                _highPassFilter.enabled = false;
            }
        }

        private void CompletePlayback()
        {
            _isPlaying = false;
            _currentIndex = 0;
            _timer = 0f;
            OnPlayCompleted?.Invoke();
        }
    }
}
