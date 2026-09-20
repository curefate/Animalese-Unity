using UnityEngine;

namespace Majulizi.Animalese
{
    [CreateAssetMenu(fileName = "VoiceProfile", menuName = "Animalese/VoiceProfile")]
    public class VoiceProfileSO : ScriptableObject
    {
        [Header("Phoneme Asset Mapping")]
        [Tooltip("Phoneme map asset used by this character.")]
        public PhonemeMapSO phonemeMap;

        [Header("Pitch & Volume")]
        [Range(0f, 2f)]
        public float volume = 1.0f;

        [Range(0.5f, 2.5f)]
        [Tooltip("Base pitch of the character voice.")]
        public float basePitch = 1.0f;

        [Range(0f, 0.5f)]
        [Tooltip("Random pitch jitter range (0 = flat/robotic, higher values sound more energetic).")]
        public float pitchJitter = 0.08f;

        [Tooltip("Pitch rise amount applied at the end of questions.")]
        public float questionPitchRise = 0.35f;

        [Header("Timing & Speed")]
        [Range(0.2f, 3.0f)]
        [Tooltip("Speech rate multiplier (higher values mean faster speech).")]
        public float speechRate = 1.0f;

        [Tooltip("Base interval in seconds between consecutive phonemes.")]
        public float baseInterval = 0.06f;

        [Header("Articulation")]
        [Range(1, 4)]
        [Tooltip("Phoneme playback step: 1 = play every phoneme, 2 = play every 2nd phoneme.")]
        public int phonemeStep = 1;

        [Header("Low-Pass Filter (Deep / Muffled / Large Creature)")]
        public bool enableLowPass = false;

        [Range(10f, 22000f)]
        public float lowPassCutoff = 5000f;

        [Range(1f, 10f)]
        public float lowPassResonance = 1.0f;

        [Header("High-Pass Filter (Thin / Crisp / Small Creature / Radio)")]
        public bool enableHighPass = false;

        [Range(10f, 22000f)]
        public float highPassCutoff = 1000f;

        [Range(1f, 10f)]
        public float highPassResonance = 1.0f;
    }
}
