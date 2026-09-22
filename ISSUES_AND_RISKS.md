# Animalese-Unity 源代码审查：已知问题与风险清单 (Issues & Risks)

本文件整理了对当前代码库审查发现的所有已知缺陷、潜在崩溃风险、性能损耗点及功能扩展盲区。每个条目均分配了独立的标识符（ID）与复选框 `[ ]` / `[x]`，以便逐条展开讨论并跟踪推进状态。

---

## 目录与条目索引

| 状态 | 条目 ID | 类型 / 领域 | 严重程度 | 涉及文件 | 简述 |
| :---: | :--- | :--- | :---: | :--- | :--- |
| [x] | **[BUG-01](#bug-01-utf-16-代理对emoji--高位字符引发-argumentexception-崩溃)** | 稳定性缺陷 | **高 (Critical)** | `AnimaleseParser.cs`, `AnimalesePlayer.cs` | 包含 Emoji 或扩展 Unicode 字符时，代理对导致运行时抛出 `ArgumentException` 崩溃 **(已修复：静音短停顿处理 + 避免 ConvertToUtf32)** |
| [ ] | **[BUG-02](#bug-02-phonememapsoautopopulatefromsamples-在-upm-模式下路径失效)** | 工具链缺陷 | **中 (Medium)** | `PhonemeMapSO.cs` | `Samples~` 目录被 Unity 忽略，导致 Editor 下 ContextMenu 自动提取音频彻底失效 |
| [ ] | **[BUG-03](#bug-03-低帧率追帧时的音频瞬间堆叠并发与-pitch-互相踩踏)** | 音频逻辑 | **中 (Medium)** | `AnimalesePlayer.cs` | 掉帧时 `while` 循环连续消费 token，导致同一帧触发多次 `PlayOneShot` 并覆盖 pitch，产生爆音 |
| [x] | **[PERF-01](#perf-01-findclipfortoken-每次播放非英文字符均产生-string-堆分配)** | 性能与 GC | **中 (Medium)** | `AnimalesePlayer.cs` | 非英文字符播放时每次都 `ToString()` 分配新字符串用于 `char.ConvertToUtf32` **(已修复：直接转为 int 消除 GC)** |
| [ ] | **[PERF-02](#perf-02-animaleseparser-解析期间大量临时字符串分配)** | 性能与 GC | **中 (Medium)** | `AnimaleseParser.cs` | 英文贪心双音素拼接与单字母 `ToString()` 产生不必要的堆分配 |
| [ ] | **[PERF-03](#perf-03-voicetokenlist-内部双重分配冗余)** | 性能与 GC | **低 (Low)** | `VoiceToken.cs` | 声明字段处与无参构造函数处重复 `new List<VoiceToken>()`，产生多余的垃圾对象 |
| [ ] | **[PERF-04](#perf-04-运行时动态-addcomponent-音频滤波器的开销与状态管理)** | 性能与架构 | **低 (Low)** | `AnimalesePlayer.cs` | 动态 `AddComponent<AudioFilter>` 产生单帧卡顿与潜在的组件残留隐患 |
| [x] | **[FEAT-01](#feat-01-缺失-textmeshpro-富文本标签rich-text-tags过滤穿透支持)** | 功能扩展 | **高 (High)** | `AnimaleseSampleTypewriter.cs`, `README.md` | 通过 TMP 的 `GetParsedText()` 提取平文本给 Parser，完美兼容富文本并与打字机绝对同步 **(已完美解决)** |
| [ ] | **[FEAT-02](#feat-02-标点重音回溯判定在复合短句下的边界处理)** | 韵律体验 | **低 (Low)** | `AnimaleseParser.cs` | 逗号等短暂停顿在特定语法结构下可能会被重音回溯越界穿透 |
| [ ] | **[FEAT-03](#feat-03-缺乏对象池或零分配解析重载支持)** | 架构设计 | **低 (Low)** | `AnimaleseParser.cs`, `VoiceToken.cs` | 每次调用 `Play(string)` 都创建新的 `VoiceTokenList`，缺乏复用现有列表的重载 |
| [ ] | **[FEAT-04](#feat-04-双音素硬编码于代码中缺乏外部扩展性)** | 架构扩展 | **低 (Low)** | `AnimaleseParser.cs` | `Digraphs` 仅硬编码了英文 5 组组合，无法配置扩展日文罗马字或拼音音节 |

---

## 详细条目列表

### - [x] [BUG-01] UTF-16 代理对（Emoji / 高位字符）引发 ArgumentException 崩溃

- **涉及文件**：
  - `Runtime/AnimaleseParser.cs`
  - `Runtime/AnimalesePlayer.cs`
- **问题描述**：
  在 C# 中，标准 `char` 仅能表达 16 位 UTF-16 字符单元（BMP）。当文本中含有 Emoji（例如 🐱、🎉）或生僻汉字等辅助平面字符时，是由两个 `char`（高代理与低代理）组成的代理对。
  1. `AnimaleseParser.cs` 遍历时仅将高代理字符 `c` 传给了 `SourceChar`。
  2. `AnimalesePlayer.cs` 在执行哈希回退计算 CodePoint 时对孤立的高代理字符调用 `char.ConvertToUtf32`，抛出未捕获异常 `System.ArgumentException: The string contains an invalid surrogate pair.`，导致播放状态机崩溃。
- **解决结果**：
  - 在 `AnimaleseParser.cs` 中增加对 `char.IsSurrogatePair(text, i)` 的专门判断，遇到代理对时生成不发音的短暂停顿 Token（`CreatePause(i, c, 0.5f)`）并步进 2 个字符，既不发声又准确驱动打字机展示字符。
  - 在 `AnimalesePlayer.cs` 中将 `char.ConvertToUtf32(token.SourceChar.ToString(), 0)` 替换为 `(int)token.SourceChar`，彻底规避异常抛出，同时消除了字符串堆分配。

---

### - [ ] [BUG-02] PhonemeMapSO.AutoPopulateFromSamples 在 UPM 模式下路径失效

- **涉及文件**：
  - `Runtime/PhonemeMapSO.cs` (行 122)
- **问题描述**：
  ```csharp
  const string samplePath = "Packages/com.majulizi.animalese/Samples/Example/eileen2";
  var guids = UnityEditor.AssetDatabase.FindAssets("t:AudioClip", new[] { samplePath });
  ```
  在 Unity UPM 规范中，包含示例的目录被命名为 `Samples~`（末尾带波浪号）。Unity 引擎在构建 AssetDatabase 时会**强制忽略**所有以 `~` 结尾的文件夹。因此，通过 Package Manager 引用此包时，该路径下根本不会生成任何 Asset GUID，该 ContextMenu 函数在导入前或安装后均无法正常工作。
- **讨论要点**：
  - 该菜单项是否应改用 `System.IO` 物理路径遍历并利用 `AssetDatabase.ImportAsset`？
  - 或者改为搜索用户工程已导入的示例路径（例如 `Assets/Samples/Animalese/...`）？
  - 或者将默认示例预制体直接作为 Package 的 Runtime 基础资源固化，不再依赖此菜单？

---

### - [ ] [BUG-03] 低帧率/追帧时的音频瞬间堆叠并发与 Pitch 互相踩踏

- **涉及文件**：
  - `Runtime/AnimalesePlayer.cs` (行 87~101)
- **问题描述**：
  在 `AnimalesePlayer.Update` 中：
  ```csharp
  _timer -= Time.deltaTime * Mathf.Max(0.01f, _speedMultiplier);
  while (_timer <= 0f && _isPlaying)
  {
      VoiceToken token = _tokens[_currentIndex];
      ProcessToken(token);
      _currentIndex++;
  }
  ```
  当游戏出现帧率波动（如掉帧至 15~20 FPS，或切换场景发生卡顿）时，`Time.deltaTime` 会远大于单个音素的 `duration`（通常仅为 0.04~0.06s）。此时 `while` 循环会在**同一帧内连续执行多次 `ProcessToken`**。
  在 `ProcessToken` 内部：
  1. 多次触发 `_audioSource.pitch = ...`：后一个音素的 pitch 会立刻覆盖前一个刚触发的 pitch。
  2. 多次调用 `_audioSource.PlayOneShot(clip, finalVolume)`：多个音素音频在同一瞬间并发叠加。
  3. **后果**：玩家会听到严重的“爆音”、“金属重叠杂音”或发音破裂。
- **讨论要点**：
  - 掉帧时，打字机文本应立即推进，但音频播放是否应当限制发声频率？
  - 方案 A：单帧最多仅允许发声 1 次（丢弃多余发声，仅触发打字机事件）。
  - 方案 B：增加计时器最大透支上限（Time Debt Clamping），防止一次性追帧过多。

---

### - [x] [PERF-01] FindClipForToken 每次播放非英文字符均产生 string 堆分配

- **涉及文件**：
  - `Runtime/AnimalesePlayer.cs` (行 256)
- **问题描述**：
  原实现使用 `int codePoint = char.ConvertToUtf32(token.SourceChar.ToString(), 0);`。对于中文、日文等非英文字符，播放器每播放一个字符都会在托管堆分配一个新字符串对象。
- **解决结果**：
  - 已随 `[BUG-01]` 修复一并重构为 `int codePoint = (int)token.SourceChar;`，完全消除运行期间的字符串 GC Alloc。

---

### - [ ] [PERF-02] AnimaleseParser 解析期间大量临时字符串分配

- **涉及文件**：
  - `Runtime/AnimaleseParser.cs` (行 151, 173)
- **问题描述**：
  1. **双音素贪心匹配**：
     ```csharp
     string twoChar = (char.ToLowerInvariant(c).ToString() + char.ToLowerInvariant(text[i + 1]));
     ```
     每次遇到两个字母，都会调用两次 `ToString()` 并进行一次 `+` 拼接，产生 3 个 GC 字符串。
  2. **单字母匹配**：
     ```csharp
     string phonemeId = char.ToLowerInvariant(c).ToString();
     ```
     每个单字母又产生一次 `ToString()`。
- **讨论要点**：
  - 针对双音素：直接比对字符 `(c1, c2)`，或者使用固定查找表。
  - 针对单字母：建立只读的 `static readonly string[] AsciiLower = { "a", "b", ..., "z" };`，按 `c - 'a'` 直接通过静态只读引用索引，解析阶段可实现 0 GC 字符串分配。

---

### - [ ] [PERF-03] VoiceTokenList 内部双重分配冗余

- **涉及文件**：
  - `Runtime/VoiceToken.cs` (行 58, 62)
- **问题描述**：
  ```csharp
  [SerializeField]
  private List<VoiceToken> _tokens = new List<VoiceToken>(); // 1. 字段声明时分配了一次

  public VoiceTokenList()
  {
      _tokens = new List<VoiceToken>(); // 2. 无参构造函数中再次重新分配覆盖
  }
  ```
  每次执行 `new VoiceTokenList()`，都会在堆上分配两个 `List<VoiceToken>` 对象，第一个瞬间沦为垃圾内存。
- **讨论要点**：
  - 删除构造函数内的多余 `new` 即可。

---

### - [ ] [PERF-04] 运行时动态 AddComponent 音频滤波器的开销与状态管理

- **涉及文件**：
  - `Runtime/AnimalesePlayer.cs` (行 279, 295)
- **问题描述**：
  ```csharp
  if (profile.enableLowPass)
  {
      if (_lowPassFilter == null)
      {
          _lowPassFilter = gameObject.AddComponent<AudioLowPassFilter>();
      }
      ...
  }
  ```
  在游戏运行中如果切换不同的角色 Profile，动态调用 `AddComponent` 会引起单帧 CPU 尖刺并触发 Unity 音频 DSP 图的重建。
- **讨论要点**：
  - 是否在 `Awake()` 中直接缓存现有组件？
  - 是否建议在组件初始化或 Inspector 预制体中预先添加并默认禁用（`enabled = false`），运行时仅做开关控制？

---

### - [x] [FEAT-01] 缺失 TextMeshPro 富文本标签（Rich Text Tags）过滤/穿透支持

- **涉及文件**：
  - `Samples~/Example/AnimaleseSampleTypewriter.cs`
  - `README.md`
- **问题描述**：
  在富文本输入场景下（如 `<color=red>`），若直接将原始文本传给解析器，标签会被当作音素发音并干扰打字机同步。
- **解决结果**：
  - 在 `AnimaleseSampleTypewriter.cs` 中，利用 TextMeshPro 原生的解析能力：设置文本后调用 `_outputText.ForceMeshUpdate()` 并通过 `_outputText.GetParsedText()` 提取已剔除所有标签的纯平文本传给 `AnimaleseParser.Parse`。
  - 打字机字符推进改为以 `_outputText.textInfo.characterCount` 作为上限，使 Token 的 `CharIndex` 与 TMP 内部已解析的字符索引达到绝对严密的 1:1 物理对齐。
  - `AnimaleseParser` 核心保持纯平文本输入契约，无需内置任何复杂的标签正则表达式，零依赖且天然兼容 TMP 所有的富文本标签。

---

### - [ ] [FEAT-02] 标点重音回溯判定在复合短句下的边界处理

- **涉及文件**：
  - `Runtime/AnimaleseParser.cs` (行 238~284)
- **问题描述**：
  在 `ApplyPunctuationEmphasis` 和 `ApplyDecrescendo` 中，使用 `token.RelativeDuration >= 3.0f` 作为句首/前一句子边界进行回溯中断：
  - 逗号、顿号的相对时长为 `2.0f`。
  - 对于短句加问号（如 `"Wait, what?"`），由于逗号小于 `3.0f`，问号的末尾升调可能会越过逗号回溯到 `"Wait"` 上的音素。
- **讨论要点**：
  - 回溯重音机制是否应当在遇到任何有效停顿（包括逗号等小停顿）时就停下，还是保持当前的跨越判定？

---

### - [ ] [FEAT-03] 缺乏对象池或零分配解析重载支持

- **涉及文件**：
  - `Runtime/AnimaleseParser.cs`
  - `Runtime/VoiceToken.cs`
- **问题描述**：
  当前外部调用 `AnimaleseParser.Parse(text)` 每次都会 `new VoiceTokenList()`。在长篇对话、频繁翻页的剧情游戏中，频繁分配会导致内存碎片。
- **讨论要点**：
  - 是否增加支持传入已存在列表的重载方法：
    `public static void Parse(string text, VoiceTokenList outputList)`
    在内部调用 `outputList.Clear()` 后复用，实现真正的端到端零分配？

---

### - [ ] [FEAT-04] 双音素硬编码于代码中，缺乏外部扩展性

- **涉及文件**：
  - `Runtime/AnimaleseParser.cs` (行 13)
- **问题描述**：
  ```csharp
  private static readonly string[] Digraphs = { "ch", "sh", "th", "wh", "ph" };
  ```
  双音素完全硬编码在 C# 文件内部。如果开发者想为日语罗马字（如 `ts`, `ky`, `sh`）或汉语拼音（如 `zh`, `ch`, `sh`）定制复合发音片段，无法在 ScriptableObject（如 `PhonemeMapSO`）中自定义。
- **讨论要点**：
  - 是否保持当前的轻量英文预设，还是将复合音素规则移至 `PhonemeMapSO` 供配置？

---

## 讨论建议与步骤

1. **已解决条目**：`[BUG-01]`, `[PERF-01]`, `[FEAT-01]`。
2. **待讨论条目推荐顺序**：
   - 音频体验与状态机健壮性：**`[BUG-03]`**（掉帧爆音防范）与 **`[FEAT-02]`**（标点重音判定）。
   - 彻底零 GC 改造：**`[PERF-02]`**, **`[PERF-03]`**, **`[FEAT-03]`**。
   - 工程与扩展性：**`[BUG-02]`**, **`[PERF-04]`**, **`[FEAT-04]`**。
