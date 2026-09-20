using System;
using System.Collections.Generic;
using UnityEngine;

namespace Majulizi.Animalese
{
    /// <summary>
    /// Parses raw text into a normalized VoiceTokenList (purely static, deterministic, and zero-dependency).
    /// </summary>
    public static class AnimaleseParser
    {
        // Common English digraphs matched in greedy order
        private static readonly string[] Digraphs = { "ch", "sh", "th", "wh", "ph" };

        /// <summary>
        /// Parses the input string into a list of VoiceTokens.
        /// </summary>
        /// <param name="text">The raw input text (supports English, Chinese, Japanese, and various punctuation marks).</param>
        public static VoiceTokenList Parse(string text)
        {
            var tokens = new VoiceTokenList();
            if (string.IsNullOrEmpty(text))
            {
                return tokens;
            }

            int length = text.Length;
            int i = 0;

            while (i < length)
            {
                char c = text[i];

                // 1. 连续省略号处理 (如 ... 或 ……)
                if (c == '…' || (c == '.' && i + 2 < length && text[i + 1] == '.' && text[i + 2] == '.'))
                {
                    // 渐弱修饰：句末前置音素音量减弱与音高微降
                    ApplyDecrescendo(tokens);

                    tokens.Add(VoiceToken.CreatePause(i, c, 5.0f));
                    if (c == '.')
                    {
                        while (i < length && text[i] == '.') i++;
                    }
                    else
                    {
                        while (i < length && text[i] == '…') i++;
                    }
                    continue;
                }

                // 2. 问号与感叹号（支持单一及 ?! / !? 复合震惊标点）
                if (c == '?' || c == '？' || c == '!' || c == '！')
                {
                    bool isQuestion = (c == '?' || c == '？');
                    bool isExclamation = (c == '!' || c == '！');
                    bool isCombined = false;

                    // 检查是否紧跟相反标点 (如 ?! 或 !?)
                    if (i + 1 < length)
                    {
                        char nextChar = text[i + 1];
                        if ((isQuestion && (nextChar == '!' || nextChar == '！')) ||
                            (isExclamation && (nextChar == '?' || nextChar == '？')))
                        {
                            isCombined = true;
                        }
                    }

                    if (isCombined)
                    {
                        // 复合标点：问号大幅升调 + 感叹号音量爆发
                        ApplyPunctuationEmphasis(tokens, pitchRiseLast: 0.45f, pitchRiseSecond: 0.25f, volumeScaleLast: 1.4f, volumeScaleSecond: 1.2f);
                        tokens.Add(VoiceToken.CreatePause(i, c, 4.0f));
                        i += 2; // 跳过组合字符
                        // 跳过多余连续标点
                        while (i < length && (text[i] == '?' || text[i] == '？' || text[i] == '!' || text[i] == '！')) i++;
                        continue;
                    }

                    if (isQuestion)
                    {
                        // 单一问号：句末上扬
                        ApplyPunctuationEmphasis(tokens, pitchRiseLast: 0.35f, pitchRiseSecond: 0.15f, volumeScaleLast: 1.0f, volumeScaleSecond: 1.0f);
                        tokens.Add(VoiceToken.CreatePause(i, c, 3.0f));
                        while (i < length && (text[i] == '?' || text[i] == '？')) i++;
                        continue;
                    }

                    // 单一感叹号：拔高音量与音调
                    ApplyPunctuationEmphasis(tokens, pitchRiseLast: 0.25f, pitchRiseSecond: 0.10f, volumeScaleLast: 1.35f, volumeScaleSecond: 1.15f);
                    tokens.Add(VoiceToken.CreatePause(i, c, 4.0f));
                    while (i < length && (text[i] == '!' || text[i] == '！')) i++;
                    continue;
                }

                // 3. 破折号/连字符/日文长音符号 (-、—、ー)：延长上个音素相对时长
                if (c == '-' || c == '—' || c == 'ー')
                {
                    ExtendLastPhonemeDuration(tokens, extendDuration: 1.0f);
                    // 保留微弱间歇，同时记录字符供 UI 打字机显示
                    tokens.Add(VoiceToken.CreatePause(i, c, 0.2f));
                    i++;
                    continue;
                }

                // 4. 波浪号 (~、～)：撒娇/悠闲尾音（延长上个音素 + 音高微扬）
                if (c == '~' || c == '～')
                {
                    ExtendLastPhonemeDuration(tokens, extendDuration: 0.8f);
                    ApplyPitchOffsetToLastPhoneme(tokens, pitchOffset: 0.15f);
                    tokens.Add(VoiceToken.CreatePause(i, c, 0.2f));
                    i++;
                    continue;
                }

                // 5. 常规标点符号与停顿
                if (c == ',' || c == '，' || c == '、')
                {
                    tokens.Add(VoiceToken.CreatePause(i, c, 2.0f));
                    i++;
                    continue;
                }

                if (c == '.' || c == '。' || c == ';' || c == '；' || c == ':' || c == '：')
                {
                    tokens.Add(VoiceToken.CreatePause(i, c, 4.0f));
                    i++;
                    continue;
                }

                if (char.IsWhiteSpace(c))
                {
                    tokens.Add(VoiceToken.CreatePause(i, c, 0.5f));
                    i++;
                    continue;
                }

                // 略过纯修饰标点（括号、引号等，不发音也不明显停顿）
                if (c == '"' || c == '\'' || c == '“' || c == '”' || c == '‘' || c == '’' ||
                    c == '(' || c == ')' || c == '（' || c == '）' || c == '【' || c == '】' ||
                    c == '[' || c == ']' || c == '{' || c == '}' || c == '<' || c == '>')
                {
                    i++;
                    continue;
                }

                // 6. 英文复合音素贪心匹配 (ch, sh, th, wh, ph)
                if (i + 1 < length && IsAsciiLetter(c) && IsAsciiLetter(text[i + 1]))
                {
                    string twoChar = (char.ToLowerInvariant(c).ToString() + char.ToLowerInvariant(text[i + 1]));
                    bool matchedDigraph = false;
                    foreach (var digraph in Digraphs)
                    {
                        if (twoChar == digraph)
                        {
                            tokens.Add(VoiceToken.CreatePhoneme(digraph, i, c, pitchOffset: 0f, relativeDuration: 1.0f));
                            i += 2;
                            matchedDigraph = true;
                            break;
                        }
                    }

                    if (matchedDigraph)
                    {
                        continue;
                    }
                }

                // 7. 单字母英文匹配 ('a' ~ 'z')
                if (IsAsciiLetter(c))
                {
                    string phonemeId = char.ToLowerInvariant(c).ToString();
                    tokens.Add(VoiceToken.CreatePhoneme(phonemeId, i, c, pitchOffset: 0f, relativeDuration: 1.0f));
                    i++;
                    continue;
                }

                // 8. 非英文字符（中文、日文等）：生成留空的 PhonemeId，保留原字符，由播放器在播放时根据 Profile 统一哈希降级
                tokens.Add(VoiceToken.CreatePhoneme(string.Empty, i, c, pitchOffset: 0f, relativeDuration: 1.0f));

                // 若是 UTF-16 代理对，步进 2
                if (char.IsSurrogatePair(text, i))
                {
                    i += 2;
                }
                else
                {
                    i++;
                }
            }

            return tokens;
        }

        private static bool IsAsciiLetter(char c)
        {
            return (c >= 'a' && c <= 'z') || (c >= 'A' && c <= 'Z');
        }

        /// <summary>
        /// Extends the playback duration of the most recent active phoneme token.
        /// </summary>
        private static void ExtendLastPhonemeDuration(VoiceTokenList tokens, float extendDuration)
        {
            for (int k = tokens.Count - 1; k >= 0; k--)
            {
                if (tokens[k].Type == VoiceTokenType.Phoneme)
                {
                    var token = tokens[k];
                    token.RelativeDuration += extendDuration;
                    tokens[k] = token;
                    break;
                }
            }
        }

        /// <summary>
        /// Applies a pitch offset to the most recent active phoneme token.
        /// </summary>
        private static void ApplyPitchOffsetToLastPhoneme(VoiceTokenList tokens, float pitchOffset)
        {
            for (int k = tokens.Count - 1; k >= 0; k--)
            {
                if (tokens[k].Type == VoiceTokenType.Phoneme)
                {
                    var token = tokens[k];
                    token.PitchOffset += pitchOffset;
                    tokens[k] = token;
                    break;
                }
            }
        }

        /// <summary>
        /// Retroactively emphasizes ending phonemes for questions, exclamations, or combined punctuation.
        /// </summary>
        private static void ApplyPunctuationEmphasis(VoiceTokenList tokens, float pitchRiseLast, float pitchRiseSecond, float volumeScaleLast, float volumeScaleSecond)
        {
            int phonemesFound = 0;
            int lastIndex = -1;
            int secondLastIndex = -1;

            for (int k = tokens.Count - 1; k >= 0; k--)
            {
                var token = tokens[k];
                if (token.Type == VoiceTokenType.Pause && token.RelativeDuration >= 3.0f)
                {
                    break;
                }

                if (token.Type == VoiceTokenType.Phoneme)
                {
                    if (phonemesFound == 0)
                    {
                        lastIndex = k;
                        phonemesFound++;
                    }
                    else if (phonemesFound == 1)
                    {
                        secondLastIndex = k;
                        phonemesFound++;
                        break;
                    }
                }
            }

            if (secondLastIndex != -1)
            {
                var token = tokens[secondLastIndex];
                token.PitchOffset += pitchRiseSecond;
                token.VolumeScale = Mathf.Max(token.VolumeScale, volumeScaleSecond);
                tokens[secondLastIndex] = token;
            }

            if (lastIndex != -1)
            {
                var token = tokens[lastIndex];
                token.PitchOffset += pitchRiseLast;
                token.VolumeScale = Mathf.Max(token.VolumeScale, volumeScaleLast);
                tokens[lastIndex] = token;
            }
        }

        /// <summary>
        /// Applies decrescendo (volume fade and slight pitch drop) to preceding phonemes before an ellipsis.
        /// </summary>
        private static void ApplyDecrescendo(VoiceTokenList tokens)
        {
            int phonemesFound = 0;
            for (int k = tokens.Count - 1; k >= 0; k--)
            {
                var token = tokens[k];
                if (token.Type == VoiceTokenType.Pause && token.RelativeDuration >= 3.0f)
                {
                    break;
                }

                if (token.Type == VoiceTokenType.Phoneme)
                {
                    if (phonemesFound == 0)
                    {
                        token.VolumeScale = 0.6f;
                        token.PitchOffset -= 0.1f;
                        tokens[k] = token;
                        phonemesFound++;
                    }
                    else if (phonemesFound == 1)
                    {
                        token.VolumeScale = 0.8f;
                        tokens[k] = token;
                        break;
                    }
                }
            }
        }
    }
}
