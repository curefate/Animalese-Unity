# Animalese-Unity 源代码审查：已知问题与风险清单 (Issues & Risks)

本文件整理了对当前代码库审查发现的所有已知缺陷、潜在崩溃风险、性能损耗点及功能扩展盲区。每个条目均分配了独立的标识符（ID）与复选框 `[ ]` / `[x]`，以便逐条展开讨论并跟踪推进状态。

---

## 目录与条目索引

| 状态 | 条目 ID | 类型 / 领域 | 严重程度 | 涉及文件 | 简述 |
| :---: | :--- | :--- | :---: | :--- | :--- |
| [x] | **[BUG-01](#bug-01-utf-16-代理对emoji--高位字符引发-argumentexception-崩溃)** | 稳定性缺陷 | **高 (Critical)** | `AnimaleseParser.cs`, `AnimalesePlayer.cs` | 包含 Emoji 或扩展 Unicode 字符时，代理对导致运行时抛出 `ArgumentException` 崩溃 **(已修复：静音短停顿处理 + 避免 ConvertToUtf32)** |
| [x] | **[BUG-02](#bug-02-phonememapsoautopopulatefromsamples-在-upm-模式下路径失效)** | 工具链缺陷 | **中 (Medium)** | `PhonemeMapSO.cs` | `Samples~` 目录被 Unity 忽略导致 ContextMenu 提取音频失效 **(已解决：直接删除无用脚手架方法，净化 Runtime 核心)** |
| [x] | **[BUG-03](#bug-03-低帧率追帧时的音频瞬间堆叠并发与-pitch-互相踩踏)** | 音频逻辑 | **中 (Medium)** | `AnimalesePlayer.cs` | 掉帧时 while 循环连续消费 token 导致爆音 **(已修复：单帧音频限流至多发声 1 次 + 时间透支下限)** |
| [x] | **[PERF-01](#perf-01-findclipfortoken-每次播放非英文字符均产生-string-堆分配)** | 性能与 GC | **中 (Medium)** | `AnimalesePlayer.cs` | 非英文字符播放时每次都 `ToString()` 分配新字符串用于 `char.ConvertToUtf32` **(已修复：直接转为 int 消除 GC)** |
| [ ] | **[PERF-02](#perf-02-animaleseparser-解析期间大量临时字符串分配)** | 性能与 GC | **中 (Medium)** | `AnimaleseParser.cs` | 英文贪心双音素拼接与单字母 `ToString()` 产生不必要的堆分配 |
| [x] | **[PERF-03](#perf-03-voicetokenlist-内部双重分配冗余)** | 性能与 GC | **低 (Low)** | `VoiceToken.cs` | 冗余的包装类分配 **(已解决：直接废弃 VoiceTokenList，全面拥抱原生泛型容器)** |
| [ ] | **[PERF-04](#perf-04-运行时动态-addcomponent-音频滤波器的开销与状态管理)** | 性能与架构 | **低 (Low)** | `AnimalesePlayer.cs` | 动态 `AddComponent<AudioFilter>` 产生单帧卡顿与潜在的组件残留隐患 |
| [x] | **[FEAT-01](#feat-01-缺失-textmeshpro-富文本标签rich-text-tags过滤穿透支持)** | 功能扩展 | **高 (High)** | `AnimaleseSampleTypewriter.cs`, `README.md` | 通过 TMP 的 `GetParsedText()` 提取平文本给 Parser，完美兼容富文本并与打字机绝对同步 **(已完美解决)** |
| [ ] | **[FEAT-02](#feat-02-标点重音回溯判定在复合短句下的边界处理)** | 韵律体验 | **低 (Low)** | `AnimaleseParser.cs` | 逗号等短暂停顿在特定语法结构下可能会被重音回溯越界穿透 |
| [x] | **[FEAT-03](#feat-03-缺乏对象池或零分配解析重载支持)** | 架构设计 | **低 (Low)** | `AnimaleseParser.cs`, `AnimalesePlayer.cs` | 缺乏复用现有列表的重载 **(已解决：新增 Parse(text, results) 零分配重载，Player 接口对齐 IReadOnlyList)** |
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

### - [x] [BUG-02] PhonemeMapSO.AutoPopulateFromSamples 在 UPM 模式下路径失效

- **涉及文件**：
  - `Runtime/PhonemeMapSO.cs`
- **问题描述**：
  原代码中包含硬编码针对特定 Sample 路径的右键 ContextMenu 方法，在 UPM 安装模式下因 `Samples~` 忽略规则而彻底失效，且对第三方包使用者没有任何通用价值，具有误导性。
- **解决结果**：
  - 确认该功能仅为开发初期的一次性临时脚手架，预制好的 `PhonemeMap_Eileen2.asset` 数据已独立序列化保存，直接将无用的 ContextMenu 方法从 `Runtime/PhonemeMapSO.cs` 中移除，净化 Runtime 核心。

---

### - [x] [BUG-03] 低帧率/追帧时的音频瞬间堆叠并发与 Pitch 互相踩踏

- **涉及文件**：
  - `Runtime/AnimalesePlayer.cs` (行 87~101, 203~240)
- **问题描述**：
  在帧率波动或发生卡顿掉帧时，`Time.deltaTime` 较大，`while` 循环在同一帧内连续消费多个音素 Token，导致同一瞬间对 `_audioSource` 触发多次 `PlayOneShot` 并连续覆盖 `pitch`，产生爆音、失真或杂音重叠。
- **解决结果**：
  - **单帧音频发声限流（Rate Limiting）**：在 `Update` 追帧循环中加入单帧发声标志位，当帧内已有音素发声后，后续因追赶而消费的 Token 仅推进时长和触发 `OnTokenPlayed` 打字机事件，跳过物理发声，杜绝音频爆破与音高踩踏。同时使高倍速快进播放听感更加自然密集。
  - **防卡顿螺旋透支保护**：增加 `_timer = Mathf.Max(_timer, -0.2f);`，防止切场景或极重度卡顿时死循环追赶。

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

### - [x] [PERF-03] VoiceTokenList 内部双重分配冗余

- **涉及文件**：
  - `Runtime/VoiceToken.cs`
- **问题描述**：
  原 `VoiceTokenList` 自定义类不仅产生包装堆分配，还在字段声明与构造函数中重复 `new List<VoiceToken>()` 分配冗余对象。
- **解决结果**：
  - 彻底移除 `VoiceTokenList` 类，全面使用 C# 原生 `List<VoiceToken>` 与 `IReadOnlyList<VoiceToken>`。消除多余包装与冗余 List 分配，降低学习成本，天然兼容标准泛型池化工具。

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

### - [x] [FEAT-03] 缺乏对象池或零分配解析重载支持

- **涉及文件**：
  - `Runtime/AnimaleseParser.cs`
  - `Runtime/AnimalesePlayer.cs`
  - `Samples~/Example/AnimaleseSampleTypewriter.cs`
- **问题描述**：
  原解析方法每次调用都分配新的容器对象，长篇对话频繁翻页时容易造成内存碎片，且无法配合对象池（如 `ListPool<T>`）复用。
- **解决结果**：
  - 在 `AnimaleseParser` 中增加了 `public static void Parse(string text, List<VoiceToken> results)` 零分配重载，支持外部预分配或对象池复用。
  - `AnimalesePlayer.Play` 统一提升为面向抽象接口 `IReadOnlyList<VoiceToken>` 编程，灵活接纳任意列表或数组。
  - 在 `AnimaleseSampleTypewriter` 中引入全局预分配列表 `_currentTokens`，实现了打字机解析播放全流程 0 GC 堆分配。

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

1. **已解决条目**：`[BUG-01]`, `[BUG-02]`, `[BUG-03]`, `[PERF-01]`, `[PERF-03]`, `[FEAT-01]`, `[FEAT-03]`。
2. **待讨论条目**：
   - 彻底零 GC 改造（最后一步）：**`[PERF-02]`**（解析阶段单/双字符比对 0 GC）。
   - 标点体验与语调调优：**`[FEAT-02]`**（标点重音边界判定）。
   - 架构工程与扩展：**`[PERF-04]`**（滤波器组件管理）、**`[FEAT-04]`**（双音素多语言扩展）。
