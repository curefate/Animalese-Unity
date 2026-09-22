using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Majulizi.Animalese
{
    public enum VoiceTokenType
    {
        Phoneme,
        Pause
    }

    [Serializable]
    public struct VoiceToken
    {
        public VoiceTokenType Type;
        public string PhonemeId;
        public float PitchOffset;
        public float RelativeDuration;
        public float VolumeScale;
        public int CharIndex;
        public char SourceChar;

        public VoiceToken(VoiceTokenType type, string phonemeId, float pitchOffset, float relativeDuration, float volumeScale, int charIndex, char sourceChar)
        {
            Type = type;
            PhonemeId = phonemeId;
            PitchOffset = pitchOffset;
            RelativeDuration = relativeDuration;
            VolumeScale = volumeScale;
            CharIndex = charIndex;
            SourceChar = sourceChar;
        }

        public static VoiceToken CreatePhoneme(string phonemeId, int charIndex, char sourceChar, float pitchOffset = 0f, float relativeDuration = 1.0f, float volumeScale = 1.0f)
        {
            return new VoiceToken(VoiceTokenType.Phoneme, phonemeId, pitchOffset, relativeDuration, volumeScale, charIndex, sourceChar);
        }

        public static VoiceToken CreatePause(int charIndex, char sourceChar, float relativeDuration)
        {
            return new VoiceToken(VoiceTokenType.Pause, string.Empty, 0f, relativeDuration, 1.0f, charIndex, sourceChar);
        }

        public override string ToString()
        {
            return Type == VoiceTokenType.Phoneme
                ? $"[Phoneme: '{PhonemeId}', Char: '{SourceChar}', PitchOffset: {PitchOffset:F2}, Vol: {VolumeScale:F2}, RelDur: {RelativeDuration:F2}]"
                : $"[Pause: '{SourceChar}', RelDur: {RelativeDuration:F2}]";
        }
    }
}
