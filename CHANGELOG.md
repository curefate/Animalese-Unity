# Changelog

All notable changes to this package will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.0.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

---

## [0.2.0] - 2026-09-23

### Fixed
- **UTF-16 Surrogate Pair Crash**: Fixed an unhandled `ArgumentException` when parsing text containing Emoji or supplementary Unicode plane characters (e.g. 🐱, 🎉). Surrogate pairs are now recognized as silent pauses (`VoiceToken.CreatePause`), preserving proper typewriter character stepping without throwing or playing audio.
- **Rate-Limited Audio Playback**: Resolved audio cacophony, distortion, and pitch override issues during framerate drops or fast-forward playback. Audio output is now strictly rate-limited to at most once per frame, while typewriter event dispatch and token timings catch up accurately.
- **Framerate Drop Time Debt Clamping**: Clamped maximum time debt to `-0.2s` in `AnimalesePlayer.Update` to prevent infinite catch-up spiral loops during severe hitches or scene loading.
- **TextMeshPro Rich Text Sync**: Seamlessly integrated `TMP_Text.GetParsedText()` and `textInfo.characterCount` into `AnimaleseSampleTypewriter` to bypass formatting tags (`<color>`, `<b>`, `<i>`, `<size>`) during phoneme generation and maintain exact 1:1 character index synchronization.

### Performance & Zero-Allocation (0 GC)
- **Eliminated Non-ASCII String Allocations**: Replaced `token.SourceChar.ToString()` with direct integer character code conversion in `AnimalesePlayer.FindClipForToken`, eliminating heap allocations during Chinese/Japanese character playback.
- **Constant Pool for Letters**: Added static 26-letter array (`LowerLetters`) in `AnimaleseParser` to look up ASCII letters by code offset, eliminating repetitive `c.ToString()` allocations.
- **Bit-Packed Digraph Lookup**: Replaced string-based concatenation and linear search for English digraphs (`ch`, `sh`, `th`, `wh`, `ph`) with a bit-packed 32-bit integer key dictionary (`(c1 << 16) | c2`), achieving zero GC allocations and O(1) matching.
- **Parser Buffer Reuse**: Added `AnimaleseParser.Parse(string text, List<VoiceToken> results)` overload to support reusable destination buffers and object pooling (e.g. `UnityEngine.Pool.ListPool<T>`).
- **Pre-Cached Audio Filters**: Cached and disabled `AudioLowPassFilter` and `AudioHighPassFilter` components in `AnimalesePlayer.Awake`, eliminating runtime `AddComponent` calls and DSP graph rebuilds during voice profile switches.

### Changed
- **Standard Generic Collections**: Replaced the custom wrapper `VoiceTokenList` with standard `List<VoiceToken>` and `IReadOnlyList<VoiceToken>`.
- **Decoupled Player API**: Updated `AnimalesePlayer.Play` to accept `IReadOnlyList<VoiceToken>`, enabling seamless interop with lists, arrays, and pooled collections.

### Removed
- **Redundant Wrapper Class**: Removed `VoiceTokenList` from `VoiceToken.cs` to reduce memory overhead and redundant `List` instantiations.
- **Obsolete Editor Scaffold**: Removed the broken and hardcoded `AutoPopulateFromSamples` context menu from `PhonemeMapSO.cs`.

---

## [0.1.0] - 2026-09-22

### Added
- Initial release of procedural Animalese speech synthesizer for Unity dialogue systems.
- Purely static, decoupled `AnimaleseParser` supporting prosody, pitch rise for questions, exclamation bursts, and elongation.
- `AnimalesePlayer` state machine for audio playback.
- `VoiceProfileSO` and `PhonemeMapSO` ScriptableObject configurations.
- TextMeshPro typewriter synchronization example.

