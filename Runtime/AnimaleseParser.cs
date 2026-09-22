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
        // 26 个小写英文字母常驻常量池，避免运行时反复调用 c.ToString() 产生 GC 堆分配
        private static readonly string[] LowerLetters = new string[26]
        {
            "a", "b", "c", "d", "e", "f", "g", "h", "i", "j", "k", "l", "m",
            "n", "o", "p", "q", "r", "s", "t", "u", "v", "w", "x", "y", "z"
        };

        // 将两个 16 位字符打包为一个 32 位整型 Key，实现零堆分配检索
        private static int Pack(char c1, char c2) => (c1 << 16) | c2;

        // 常见英文双音素快速查找表（基于打包整型 Key，100% 零 GC，且支持灵活扩展任意双字符组合）
        private static readonly Dictionary<int, string> DigraphLookup = new Dictionary<int, string>
        {
            [Pack('c', 'h')] = "ch",
            [Pack('s', 'h')] = "sh",
            [Pack('t', 'h')] = "th",
            [Pack('w', 'h')] = "wh",
            [Pack('p', 'h')] = "ph"
        };

        /// <summary>
        /// Parses the input string and allocates a new List of VoiceTokens.
        /// </summary>
        public static List<VoiceToken> Parse(string text)
        {
            var tokens = new List<VoiceToken>();
            Parse(text, tokens);
            return tokens;
        }

        /// <summary>
        /// Parses the input string into the provided destination list (clears existing contents).
        /// Enables zero-allocation parsing in high-frequency dialogue systems.
        /// </summary>
        public static void Parse(string text, List<VoiceToken> results)
        {
            if (results == null)
            {
                throw new ArgumentNullException(nameof(results));
            }

            results.Clear();

            if (string.IsNullOrEmpty(text))
            {
                return;
            }

            int length = text.Length;
            int i = 0;
            var tokens = results;

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

                // 6. 英文复合音素贪心匹配 (ch, sh, th, wh, ph) - 0 GC 快速检索
                if (i + 1 < length && IsAsciiLetter(c) && IsAsciiLetter(text[i + 1]))
                {
                    char c1 = char.ToLowerInvariant(c);
                    char c2 = char.ToLowerInvariant(text[i + 1]);

                    if (DigraphLookup.TryGetValue(Pack(c1, c2), out string matchedDigraph))
                    {
                        tokens.Add(VoiceToken.CreatePhoneme(matchedDigraph, i, c, pitchOffset: 0f, relativeDuration: 1.0f));
                        i += 2;
                        continue;
                    }
                }

                // 7. 单字母英文匹配 ('a' ~ 'z') - 0 GC 常量池查表
                if (IsAsciiLetter(c))
                {
                    int letterIndex = (c >= 'a' && c <= 'z') ? (c - 'a') : (c - 'A');
                    string phonemeId = LowerLetters[letterIndex];
                    tokens.Add(VoiceToken.CreatePhoneme(phonemeId, i, c, pitchOffset: 0f, relativeDuration: 1.0f));
                    i++;
                    continue;
                }

                // 8. UTF-16 代理对字符（如 Emoji、生僻字等）：不发音，作为短暂停顿展示字符推进打字机
                if (char.IsSurrogatePair(text, i))
                {
                    tokens.Add(VoiceToken.CreatePause(i, c, 0.5f));
                    i += 2;
                    continue;
                }

                // 9. 普通非英文字符（中文、日文假名等基本多语言平面字符）：生成留空的 PhonemeId，保留原字符，由播放器在播放时根据 Profile 统一哈希降级
                tokens.Add(VoiceToken.CreatePhoneme(string.Empty, i, c, pitchOffset: 0f, relativeDuration: 1.0f));
                i++;
            }
        }

        private static bool IsAsciiLetter(char c)
        {
            return (c >= 'a' && c <= 'z') || (c >= 'A' && c <= 'Z');
        }

        /// <summary>
        /// Extends the playback duration of the most recent active phoneme token.
        /// </summary>
        private static void ExtendLastPhonemeDuration(List<VoiceToken> tokens, float extendDuration)
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
        private static void ApplyPitchOffsetToLastPhoneme(List<VoiceToken> tokens, float pitchOffset)
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
        private static void ApplyPunctuationEmphasis(List<VoiceToken> tokens, float pitchRiseLast, float pitchRiseSecond, float volumeScaleLast, float volumeScaleSecond)
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
        private static void ApplyDecrescendo(List<VoiceToken> tokens)
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
