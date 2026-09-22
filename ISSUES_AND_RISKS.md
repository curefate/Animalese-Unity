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
| [x] | **[PERF-02](#perf-02-animaleseparser-解析期间大量临时字符串分配)** | 性能与 GC | **中 (Medium)** | `AnimaleseParser.cs` | 英文双音素拼接与单字母 `ToString()` 产生堆分配 **(已解决：整型打包字典 + 字母常量池，彻底 0 GC)** |
| [x] | **[PERF-03](#perf-03-voicetokenlist-内部双重分配冗余)** | 性能与 GC | **低 (Low)** | `VoiceToken.cs` | 冗余的包装类分配 **(已解决：直接废弃 VoiceTokenList，全面拥抱原生泛型容器)** |
| [x] | **[PERF-04](#perf-04-运行时动态-addcomponent-音频滤波器的开销与状态管理)** | 性能与架构 | **低 (Low)** | `AnimalesePlayer.cs` | 动态 AddComponent 导致卡顿 **(已解决：Awake 集中初始化并默认禁用，运行时纯参数赋值)** |
| [x] | **[FEAT-01](#feat-01-缺失-textmeshpro-富文本标签rich-text-tags过滤穿透支持)** | 功能扩展 | **高 (High)** | `AnimaleseSampleTypewriter.cs`, `README.md` | 通过 TMP 的 `GetParsedText()` 提取平文本给 Parser，完美兼容富文本并与打字机绝对同步 **(已完美解决)** |
| [x] | **[FEAT-02](#feat-02-标点重音回溯判定在复合短句下的边界处理)** | 韵律体验 | **低 (Low)** | `AnimaleseParser.cs` | 标点重音回溯判定 **(已关闭：保持现状，当前节奏与表现力符合预期)** |
| [x] | **[FEAT-03](#feat-03-缺乏对象池或零分配解析重载支持)** | 架构设计 | **低 (Low)** | `AnimaleseParser.cs`, `AnimalesePlayer.cs` | 缺乏复用现有列表的重载 **(已解决：新增 Parse(text, results) 零分配重载，Player 接口对齐 IReadOnlyList)** |
| [x] | **[FEAT-04](#feat-04-双音素硬编码于代码中缺乏外部扩展性)** | 架构扩展 | **低 (Low)** | `AnimaleseParser.cs` | 双音素外部扩展 **(已关闭：保持现状，坚持轻量零配置方案，避免过度设计)** |

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

### - [x] [PERF-02] AnimaleseParser 解析期间大量临时字符串分配

- **涉及文件**：
  - `Runtime/AnimaleseParser.cs`
- **问题描述**：
  原实现在双音素贪心匹配时通过 `c.ToString() + text[i+1].ToString()` 产生多次临时字符串分配；在单字母匹配时通过 `c.ToString()` 频繁产生堆对象垃圾。
- **解决结果**：
  - **单字母 0 GC 查表**：预置 26 个常驻小写英文字母静态常量池 `LowerLetters[26]`，通过字符 ASCII 码偏移取模直接获取常驻字符串指针，消除 `ToString()` 分配。
  - **双音素通用整型打包查表**：使用 `(c1 << 16) | c2` 将两个 16 位字符无分配快速打包为 32 位整型 Key，配合 `Dictionary<int, string>` 字典检索。彻底消除字符串拼接与 GC Alloc，同时为未来多语言复合音素扩展提供了极其灵活且无耦合的架构支撑。

---

### - [x] [PERF-03] VoiceTokenList 内部双重分配冗余

- **涉及文件**：
  - `Runtime/VoiceToken.cs`
- **问题描述**：
  原 `VoiceTokenList` 自定义类不仅产生包装堆分配，还在字段声明与构造函数中重复 `new List<VoiceToken>()` 分配冗余对象。
- **解决结果**：
  - 彻底移除 `VoiceTokenList` 类，全面使用 C# 原生 `List<VoiceToken>` 与 `IReadOnlyList<VoiceToken>`。消除多余包装与冗余 List 分配，降低学习成本，天然兼容标准泛型池化工具。

---

### - [x] [PERF-04] 运行时动态 AddComponent 音频滤波器的开销与状态管理

- **涉及文件**：
  - `Runtime/AnimalesePlayer.cs`
- **问题描述**：
  在游戏运行中如果切换不同的角色 Profile，原实现会在 `ApplyProfileFilters` 中动态调用 `gameObject.AddComponent`，引起单帧 CPU 尖刺并触发 Unity 音频 DSP 图的运行时重建。
- **解决结果**：
  - 将滤波器组件的获取与创建统一收拢到 `Awake` 初始化阶段，并默认禁用（`enabled = false`）。
  - `ApplyProfileFilters` 纯粹化为参数应用逻辑：仅控制 `filter.enabled` 开关与数值赋值，彻底消除了运行期间动态挂载组件的性能开销与潜在组件残留。

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

### - [x] [FEAT-02] 标点重音回溯判定在复合短句下的边界处理

- **涉及文件**：
  - `Runtime/AnimaleseParser.cs` (行 238~284)
- **问题描述**：
  在 `ApplyPunctuationEmphasis` 和 `ApplyDecrescendo` 中，使用 `token.RelativeDuration >= 3.0f` 作为句首/前一句子边界进行回溯中断。逗号顿号相对时长为 `2.0f`，在极短句接问号（如 `"Wait, what?"`）时可能越过逗号回溯。
- **讨论与结论**：
  - **保持现状（Keep as is）**：经评估，当前动森语的升调与韵律表现力听感良好，即使极短复合句跨越逗号轻微上扬也符合动森语活泼俏皮的拟音风格，无需引入更繁琐的语法分词逻辑，维持现有规则。该条目关闭。

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

### - [x] [FEAT-04] 双音素硬编码于代码中，缺乏外部扩展性

- **涉及文件**：
  - `Runtime/AnimaleseParser.cs` (行 13)
- **问题描述**：
  双音素目前硬编码为英文 5 组常见组合（`ch`, `sh`, `th`, `wh`, `ph`）。
- **讨论与结论**：
  - **保持现状（Keep as is）**：坚持轻量零配置设计，避免将 ScriptableObject 引入原本纯静态独立的 `AnimaleseParser` 中，避免过度设计。该条目关闭。

---

## 总结与审查结论

审查发现的全部 **10 个条目**已全部讨论并推进完毕：
- **稳定性缺陷（已修复）**：`[BUG-01]`（代理对崩溃）、`[BUG-02]`（无效脚手架清理）、`[BUG-03]`（掉帧音频限流与透支保护）
- **性能与极致零 GC（已重构）**：`[PERF-01]`（字符哈希零分配）、`[PERF-02]`（单/双音素查表零分配）、`[PERF-03]`（废弃包装类）、`[PERF-04]`（滤波器组件加载期就绪）
- **对话系统集成与体验（已落地/明确决策）**：`[FEAT-01]`（TMP 富文本提取与字符级绝对对齐）、`[FEAT-02]`（标点语调保持现状）、`[FEAT-03]`（提供 Parse 零分配重载与只读接口抽象）、`[FEAT-04]`（双音素保持轻量内置）

当前项目在**稳定性**、**性能（全流程端到端 0 GC）**以及**TextMeshPro 文本打字机音画同步**方面均达到了极高的工业级标准。
