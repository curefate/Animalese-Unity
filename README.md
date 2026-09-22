# Animalese for Unity

A lightweight, zero-dependency, procedural "Animalese" speech synthesizer for Unity dialogue systems. Converts raw text into rhythmic character gibberish voice lines in real time, with expressive intonations and millisecond-accurate UI typewriter synchronization.

---

## Features

- **Decoupled Architecture**: 
  - `AnimaleseParser`: A purely static, zero-dependency parser that converts text into normalized, dimensionless voice tokens.
  - `AnimalesePlayer`: An `Update()` state machine driving audio playback with zero allocation during dialogue.
- **Expressive Prosody & Punctuation**:
  - `?`: Question pitch rising towards the end of sentences.
  - `!`: Volume burst and pitch jump for exclamations.
  - `?!` / `!?`: Combined shock intonation.
  - `-` / `—`: Naturally extends the duration of the preceding phoneme.
  - `~`: Playful/casual tail stretching with a subtle upward inflection.
  - `...`: Gradual volume decrescendo and pitch drop before pauses.
- **Character Voice Profiles (`VoiceProfileSO`)**:
  - Customize pitch, pitch jitter (randomness), speech rate, and pause lengths per character.
  - Optional built-in **Low-Pass Filter** (deep, muffled, or large creatures) and **High-Pass Filter** (crisp, small creatures, or radio/intercom effects).
- **Multilingual Support**:
  - Native greedy digraph matching for English (`ch`, `sh`, `th`, `wh`, `ph`).
  - Automatic, deterministic Unicode hash-modulo fallback for non-English text (e.g. Chinese, Japanese).
- **Perfect Typewriter Sync**:
  - Emits `OnTokenPlayed` events to advance TextMeshPro `maxVisibleCharacters` in exact lockstep with sound clips, completely eliminating audio-visual desync and layout jitter.

---

## Quick Start

### 1. Installation

Install via the Unity Package Manager (UPM):
1. In the Unity Editor, open **Window** > **Package Manager**.
2. Click the `+` button at the top-left and choose **Add package from git URL...**.
3. Enter the repository URL and click **Add**:
   ```
   https://github.com/curefate/Animalese-Unity.git
   ```
4. *(Optional)* Once installed, select **Animalese** in the Package Manager list, expand the **Samples** section, and click **Import** next to **Example** to try out pre-configured voice profiles, audio assets, and an interactive demo scene (requires TextMeshPro).

### 2. Basic Playback via Code

Attach an `AnimalesePlayer` component to a GameObject with an `AudioSource`, assign a `VoiceProfileSO`, and call:

```csharp
using UnityEngine;
using Majulizi.Animalese;

public class DialogueExample : MonoBehaviour
{
    [SerializeField] private AnimalesePlayer _player;

    public void Speak()
    {
        // Plays text using the currently assigned player.Profile
        _player.Play("Hello, world! Do you like this island?");
    }

    public void Stop()
    {
        _player.Stop();
    }
}
```

### 3. Synchronizing with TextMeshPro Typewriter

```csharp
using TMPro;
using UnityEngine;
using Majulizi.Animalese;

public class TypewriterExample : MonoBehaviour
{
    [SerializeField] private TMP_Text _dialogueText;
    [SerializeField] private AnimalesePlayer _player;

    private List<VoiceToken> _tokens = new List<VoiceToken>();
    private string _fullText;

    public void PlayDialogue(string text)
    {
        _fullText = text;
        _dialogueText.text = text;
        _dialogueText.maxVisibleCharacters = 0;

        // Force TMP to parse tags and extract plain text (supports rich text tags seamlessly)
        _dialogueText.ForceMeshUpdate();
        string plainText = _dialogueText.GetParsedText();

        // Zero-allocation parsing by reusing destination list
        AnimaleseParser.Parse(plainText, _tokens);

        _player.OnTokenPlayed += OnTokenPlayed;
        _player.OnPlayCompleted += OnPlayCompleted;

        _player.Play(_tokens);
    }

    private void OnTokenPlayed(VoiceToken token, int index)
    {
        int nextChar = (index + 1 < _tokens.Count) ? _tokens[index + 1].CharIndex : _dialogueText.textInfo.characterCount;
        _dialogueText.maxVisibleCharacters = nextChar;
    }

    private void OnPlayCompleted()
    {
        _dialogueText.maxVisibleCharacters = _dialogueText.textInfo.characterCount;
        _player.OnTokenPlayed -= OnTokenPlayed;
        _player.OnPlayCompleted -= OnPlayCompleted;
    }
}
```

---

## Samples

Once imported via the Unity Package Manager, you can import the **Example** sample from the Package Manager window:
- Includes ready-to-use audio samples (`eileen2`).
- A pre-configured `PhonemeMap_Eileen2.asset` and `VoiceProfile_Default.asset`.
- An interactive demo scene with an input field, button, and synced TMP typewriter display (`AnimaleseSampleTypewriter.cs`).

> **Note:** The Example sample requires **TextMeshPro**.

---

## Credits

- The sample phoneme audio assets included in the Example package are sourced from [ztc0611/Ren-py-Animalese](https://github.com/ztc0611/Ren-py-Animalese), which were originally based on work by Henry and made available for free use and modification.

---

## License

MIT License. See [LICENSE](LICENSE.md) for details.