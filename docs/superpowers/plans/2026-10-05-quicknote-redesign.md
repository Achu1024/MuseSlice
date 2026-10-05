# QuickNote 视觉重做 + 勾选框 + 删除 实现计划

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 把小条按「掉落下来的便签」重做视觉（近乎无彩，唯一的颜色来自两个 emoji 脸），记录行改成 `- [ ] HH:mm 内容` 的待办格式（读取兼容旧格式），并在搜索页加上空格勾选与 Backspace 删除。

**Architecture:** 纯逻辑全部下沉到 `QuickNote.Core`（`EntryLine` 负责新格式的解析/生成/纯文本变换，`NoteStore` 从纯追加变成追加 + 原子重写两条路径，`CaretPulse` 是一个纯函数）。UI 层只做绘制与接线：`Theme` 换一套 token，`BarChrome` 加分隔线，新增 `CaretOverlay` 自绘光标，`SearchResultsView` 画完成态的脸。

**Tech Stack:** .NET Framework 4.8 + WinForms，零第三方依赖，自检是 `QuickNote.SelfCheck` 里的手写断言（当前 131 条，必须保持全绿）。

**Spec:** `docs/superpowers/specs/2026-10-05-quicknote-redesign.md`（定案，含「接手须知」）。
原始 spec `docs/superpowers/specs/2026-10-05-quicknote-design.md` 第 5 节的记录模型**已被定案 §10.1 修订**——多行内容只有首行带前缀那套对新写入不再适用。

## Global Constraints

- **构建与自检**（dotnet 不在本 shell 的 PATH 上，必须先导出）：
  ```bash
  export PATH="$PATH:/c/Program Files/dotnet"
  dotnet build QuickNote.sln -c Debug
  dotnet run --project src/QuickNote.SelfCheck -c Debug
  ```
- **程序正开着时构建会失败**（`MSB3027` 文件被锁）。先让使用者从托盘退出，**不要强杀**——输入框里可能有没保存的草稿。
- **交给控件属性的颜色必须完全不透明**（alpha < 255 会让 `Control.BackColor` 在构造函数里抛 `ArgumentException`，程序连托盘图标都出不来）。只有纯 GDI+ 绘制的颜色可以带 alpha。
- **纯逻辑类库 `QuickNote.Core` 不引用 `System.Drawing`，也不引用 WinForms。** 颜色、字体、控件一律留在 `QuickNote` 项目。
- **零第三方依赖。** 不新增任何 NuGet 包。
- **`dotnet` 路径 / bash heredoc**：绝不用 bash heredoc 把 Windows 路径写进 python/powershell，`\\` 会掉一层、配置文件直接损坏。写字面量用 Write 工具。
- **合成按键驱动不了 WinForms 输入**，键盘交互只能由真人验证。代码改完必须明确列出「需要你按真键盘确认」的条目。
- 文件编码 **UTF-8 无 BOM**，换行 **LF**。
- 注释与界面文案用中文，与现有代码一致。

## Review Focus

以下五类是定案文档隐含、但任何单个任务的测试都不会自动覆盖的输入。每条都在对应任务里钉了断言。

1. **旧格式记录（`- 20:18 下载Minimax`）是使用者现有的全部数据。** 改格式后它们必须仍被识别为记录、仍能搜索、仍能被勾选与删除，且勾选时升级成新格式而不是消失。钉在 Task 1、Task 2、Task 3。
2. **删除一条记录时不能吃掉标题行或相邻记录。** 旧格式的续行要跟着删，但空行与下一条记录是边界。钉在 Task 1、Task 2。
3. **输入里只有勾选标记、没有正文**（`😶‍🌫️ ` 或 `- [ ] `）不该建文件、不该写空待办。钉在 Task 1、Task 3。
4. **搜索结果生成之后文件被外部改过**（在 Typora 里编了那条记录）。按旧行号勾选/删除必须拒绝写入，而不是改错一条。钉在 Task 2。
5. **查询框有内容时 Backspace 仍是删字符**，确认态只在查询框为空时进入。钉在 Task 9、Task 10。

---

## 文件结构

| 文件 | 动作 | 职责 |
|---|---|---|
| `src/QuickNote.Core/EntryLine.cs` | 新建 | 记录行的解析、生成、勾选改写、删除范围（全部纯函数） |
| `src/QuickNote.Core/DateFormat.cs` | 修改 | `IsEntryStart` 委托给 `EntryLine`；删掉 `EntryPrefix`；加 `ShortHeading` |
| `src/QuickNote.Core/NoteStore.cs` | 修改 | `Append` 改成一行为一条；新增原子重写、`SetDone`、`DeleteEntry` |
| `src/QuickNote.Core/CaretPulse.cs` | 新建 | 光标明暗的纯函数 |
| `src/QuickNote.Core/BarGeometry.cs` | 修改 | 去掉写死的高度公式，只留行数钳制 |
| `src/QuickNote.SelfCheck/BarGeometryChecks.cs` | 修改 | 跟随 `HeightFor` → `ClampLines` |
| `src/QuickNote/Theme.cs` | 重写 | 新 token 与新字体 |
| `src/QuickNote/BarChrome.cs` | 修改 | 上下两条 chrome 各加一条 hairline |
| `src/QuickNote/MarkdownHighlighter.cs` | 修改 | 去掉彩色，改成字重与 ink 层次；认勾选框标记 |
| `src/QuickNote/CaretOverlay.cs` | 新建 | 自绘光标子控件 |
| `src/QuickNote/SearchResultsView.cs` | 修改 | 画完成态的脸、危险态底纹、去掉彩色高亮 |
| `src/QuickNote/CaptureBar.cs` | 修改 | 接线：`- ` 自动补全、空格勾选、Backspace 删除确认态 |
| `src/QuickNote/NativeMethods.cs` | 修改 | 加 `HideCaret` |
| `src/QuickNote.SelfCheck/EntryLineChecks.cs` | 新建 | 新格式与旧格式的全部断言 |
| `src/QuickNote.SelfCheck/CaretPulseChecks.cs` | 新建 | 脉冲曲线断言 |
| `src/QuickNote.SelfCheck/NoteStoreChecks.cs` | 修改 | 跟上一行为一条、原子重写、`.bak` |
| `src/QuickNote.SelfCheck/DateFormatChecks.cs` | 修改 | 跟随 `EntryPrefix` 的迁移 |
| `src/QuickNote.SelfCheck/Program.cs` | 修改 | 挂上两个新检查文件 |

---

### Task 1: `EntryLine` —— 记录行的格式、解析与纯文本变换

**Files:**
- Create: `src/QuickNote.Core/EntryLine.cs`
- Create: `src/QuickNote.SelfCheck/EntryLineChecks.cs`
- Modify: `src/QuickNote.SelfCheck/Program.cs`
- Modify: `src/QuickNote.Core/DateFormat.cs:33-55`

**Interfaces:**
- Consumes: `DateFormat.Stamp(DateTime)`（已存在）
- Produces:
  - `EntryLine.UndoneFace` / `EntryLine.DoneFace`（`const string`，界面上的脸）
  - `EntryLine.UndoneBox` = `"- [ ] "` / `EntryLine.DoneBox` = `"- [x] "`
  - `EntryLine.Prefix(DateTime when, bool done) → string`
  - `EntryLine.TryParse(string line, out EntryInfo info) → bool`
  - `EntryLine.IsEntryStart(string line) → bool`
  - `EntryLine.SetDone(string line, bool done) → string`（不是记录行返回 `null`）
  - `EntryLine.StripMarker(string line, out bool done) → string`
  - `EntryLine.DeleteCount(IList<string> lines, int index) → int`
  - `EntryInfo { bool IsDone; string Stamp; string Content; string Face { get; } }`
  - `DateFormat.ShortHeading(DateTime) → string`（例如 `"10-05 星期一"`）

- [ ] **Step 1: 写失败的断言**

创建 `src/QuickNote.SelfCheck/EntryLineChecks.cs`：

```csharp
using System;
using System.Collections.Generic;
using QuickNote.Core;

namespace QuickNote.SelfCheck
{
    internal static class EntryLineChecks
    {
        private static readonly DateTime When = new DateTime(2026, 10, 5, 14, 3, 0);

        public static void Run()
        {
            Check.Section("EntryLine");
            NewFormatRoundTrip();
            ReadsLegacyFormat();
            RejectsUserWrittenLists();
            StripsMarkers();
            TogglesDone();
            DeleteRange();
        }

        private static void NewFormatRoundTrip()
        {
            Check.Equal(EntryLine.Prefix(When, false), "- [ ] 14:03 ", "新格式前缀：未完成");
            Check.Equal(EntryLine.Prefix(When, true), "- [x] 14:03 ", "新格式前缀：已完成");

            EntryInfo info;
            Check.That(EntryLine.TryParse("- [ ] 14:03 跟老王确认了接口字段", out info), "识别新格式的未完成行");
            Check.Equal(info.IsDone, false, "未完成行的完成态为 false");
            Check.Equal(info.Stamp, "14:03", "取到时间戳");
            Check.Equal(info.Content, "跟老王确认了接口字段", "取到正文");
            Check.Equal(info.Face, EntryLine.UndoneFace, "未完成行给未完成的脸");

            Check.That(EntryLine.TryParse("- [x] 14:03 把 spec 写完", out info), "识别新格式的已完成行");
            Check.Equal(info.IsDone, true, "已完成行的完成态为 true");
            Check.Equal(info.Face, EntryLine.DoneFace, "已完成行给完成的脸");

            Check.That(EntryLine.TryParse("- [X] 14:03 大写 X", out info), "大写 X 也认");
            Check.Equal(info.IsDone, true, "大写 X 视为已完成");
        }

        private static void ReadsLegacyFormat()
        {
            // 使用者现存的文件全是这个格式。认不出来就等于所有旧记录消失。
            EntryInfo info;
            Check.That(EntryLine.TryParse("- 20:18 下载Minimax", out info), "旧格式（无勾选框）仍被识别为记录");
            Check.Equal(info.IsDone, false, "旧格式一律视为未完成");
            Check.Equal(info.Stamp, "20:18", "旧格式也能取到时间戳");
            Check.Equal(info.Content, "下载Minimax", "旧格式也能取到正文");
            Check.Equal(info.Face, EntryLine.UndoneFace, "旧格式的记录显示未完成的脸");

            Check.That(DateFormat.IsEntryStart("- 20:18 下载Minimax"), "DateFormat.IsEntryStart 仍认旧格式");
            Check.That(DateFormat.IsEntryStart("- [ ] 14:03 新格式"), "DateFormat.IsEntryStart 也认新格式");
            Check.That(DateFormat.IsEntryStart("- [x] 14:03 新格式"), "DateFormat.IsEntryStart 认已完成的新格式");
        }

        private static void RejectsUserWrittenLists()
        {
            EntryInfo info;
            Check.That(!EntryLine.TryParse("- [ ] 待办甲", out info), "使用者手打的待办列表项不是记录行（没有时间戳）");
            Check.That(!EntryLine.TryParse("- 一条普通列表", out info), "普通列表项不是记录行");
            Check.That(!EntryLine.TryParse("- 9:05 小时没补零", out info), "小时没补零不算记录行");
            Check.That(!EntryLine.TryParse("- [ ] 9:05 新格式也要两位小时", out info), "新格式同样要求两位小时");
            Check.That(!EntryLine.TryParse("- [ ]14:03 方括号后没有空格", out info), "方括号后缺空格不算记录行");
            Check.That(!EntryLine.TryParse("- 20:18", out info), "只有时间戳没有尾随空格不算");
            Check.That(!EntryLine.TryParse("", out info), "空行不是记录行");
            Check.That(!EntryLine.TryParse(null, out info), "null 不是记录行");
        }

        private static void StripsMarkers()
        {
            bool done;
            Check.Equal(EntryLine.StripMarker(EntryLine.UndoneFace + " 买牛奶", out done), "买牛奶",
                "界面的未完成脸被去掉");
            Check.Equal(done, false, "未完成脸给出未完成态");

            Check.Equal(EntryLine.StripMarker(EntryLine.DoneFace + " 买牛奶", out done), "买牛奶",
                "界面的完成脸被去掉");
            Check.Equal(done, true, "完成脸给出完成态");

            Check.Equal(EntryLine.StripMarker("- [ ] 买牛奶", out done), "买牛奶", "手打的 - [ ] 被去掉");
            Check.Equal(done, false, "手打的 - [ ] 视为未完成");
            Check.Equal(EntryLine.StripMarker("- [x] 买牛奶", out done), "买牛奶", "手打的 - [x] 被去掉");
            Check.Equal(done, true, "手打的 - [x] 视为已完成");

            Check.Equal(EntryLine.StripMarker("买牛奶", out done), "买牛奶", "没有标记的普通行原样返回");
            Check.Equal(done, false, "没有标记视为未完成");
            Check.Equal(EntryLine.StripMarker(EntryLine.UndoneFace, out done), "",
                "只有脸没有正文时返回空串（保存时会被跳过）");
            Check.Equal(EntryLine.StripMarker("- 5 分钟后开会", out done), "- 5 分钟后开会",
                "行首不是勾选标记的短横不被吃掉");
        }

        private static void TogglesDone()
        {
            Check.Equal(EntryLine.SetDone("- [ ] 14:03 买牛奶", true), "- [x] 14:03 买牛奶", "未完成切到已完成");
            Check.Equal(EntryLine.SetDone("- [x] 14:03 买牛奶", false), "- [ ] 14:03 买牛奶", "已完成切回未完成");
            Check.Equal(EntryLine.SetDone("- 20:18 下载Minimax", true), "- [x] 20:18 下载Minimax",
                "勾选旧格式的记录会把它升级成新格式，时间戳与正文不变");
            Check.Equal(EntryLine.SetDone("- [ ] 14:03 ", true), "- [x] 14:03 ", "正文为空的记录也能切换");
            Check.Equal(EntryLine.SetDone("普通人", true), null, "不是记录行时返回 null");
            Check.Equal(EntryLine.SetDone("- [ ] 待办甲", true), null, "使用者手打的列表项不能被当成记录行改写");
        }

        private static void DeleteRange()
        {
            var lines = new List<string>
            {
                "# 2026-10-05 星期一",              // 0
                "",                                   // 1
                "- [ ] 14:30 第一条",                 // 2
                "- [ ] 14:32 第二条",                 // 3
                "",                                   // 4
                "- 20:18 旧的单行",                   // 5
                "",                                   // 6
                "- 21:00 旧的带续行",                 // 7
                "他那边说字段名要改成 snake_case",    // 8  旧格式的续行
                "",                                   // 9
                "- [ ] 21:05 第三条",                 // 10
                ""                                    // 11
            };

            Check.Equal(EntryLine.DeleteCount(lines, 2), 1, "下一条记录紧随其后时不连坐");
            Check.Equal(EntryLine.DeleteCount(lines, 3), 2, "连着自己的分隔空行一起删");
            Check.Equal(EntryLine.DeleteCount(lines, 5), 2, "旧格式的单行记录");
            Check.Equal(EntryLine.DeleteCount(lines, 7), 3, "旧格式的续行跟着一起删，但停在空行");
            Check.Equal(EntryLine.DeleteCount(lines, 10), 2, "删最后一条记录");

            var noTrailingBlank = new List<string> { "- [ ] 14:30 只有一条" };
            Check.Equal(EntryLine.DeleteCount(noTrailingBlank, 0), 1, "文件末尾没有空行时不越界");

            var headingSwallowed = new List<string> { "- [ ] 14:30 最后一条", "# 2026-10-06 星期二" };
            Check.Equal(EntryLine.DeleteCount(headingSwallowed, 0), 1,
                "下一条记录是加粗标题行时同样不被连坐");
        }
    }
}
```

在 `src/QuickNote.SelfCheck/Program.cs` 的 `DateFormatChecks.Run();` 下面插入一行：

```csharp
                EntryLineChecks.Run();
```

- [ ] **Step 2: 跑一遍确认是红的**

```bash
export PATH="$PATH:/c/Program Files/dotnet"
dotnet run --project src/QuickNote.SelfCheck -c Debug
```

期望：编译失败，`CS0103: 当前上下文中不存在名称"EntryLine"` / `CS0246: 未能找到类型或命名空间名"EntryLine"`。这就是「功能还没写」的 RED。

- [ ] **Step 3: 实现 `EntryLine`**

创建 `src/QuickNote.Core/EntryLine.cs`：

```csharp
using System;
using System.Collections.Generic;

namespace QuickNote.Core
{
    /// <summary>一条记录行的内容。</summary>
    public sealed class EntryInfo
    {
        public bool IsDone { get; set; }
        public string Stamp { get; set; }
        public string Content { get; set; }

        /// <summary>界面上代表这个状态的脸。文件里存的是 "- [ ]" / "- [x]"。</summary>
        public string Face { get { return IsDone ? EntryLine.DoneFace : EntryLine.UndoneFace; } }
    }

    /// <summary>
    /// 记录行的解析与生成。
    /// <para>
    /// 写入格式（2026-10-05 定案 §6）：<c>- [ ] HH:mm 内容</c>，已完成的写 <c>- [x]</c>。
    /// 读取必须同时认旧格式 <c>- HH:mm 内容</c>——使用者已有的记录全是旧格式，
    /// 只认新格式会让条数归零、上下文错乱、搜索列不出来。
    /// </para>
    /// </summary>
    public static class EntryLine
    {
        /// <summary>界面上的未完成脸；文件里存 "- [ ]"。</summary>
        public const string UndoneFace = "😶‍🌫️";

        /// <summary>界面上的完成脸；文件里存 "- [x]"。</summary>
        public const string DoneFace = "🥳";

        public const string UndoneBox = "- [ ] ";
        public const string DoneBox = "- [x] ";

        /// <summary>新建一条记录的行首前缀，例如「- [ ] 14:03 」。</summary>
        public static string Prefix(DateTime when, bool done)
        {
            return (done ? DoneBox : UndoneBox) + DateFormat.Stamp(when) + " ";
        }

        /// <summary>
        /// 解析一行记录。使用者手打的 "- [ ] 待办" 与真正的记录行的区别在于时间戳：
        /// 后者有「- HH:mm 」或「- [ ] HH:mm 」，前者没有，因此不会被误认成记录。
        /// </summary>
        public static bool TryParse(string line, out EntryInfo info)
        {
            info = null;
            if (line == null) return false;

            int at;
            var done = false;

            if (line.Length >= 6 && line[0] == '-' && line[1] == ' ' && line[2] == '[' &&
                line[4] == ']' && line[5] == ' ' &&
                (line[3] == ' ' || line[3] == 'x' || line[3] == 'X'))
            {
                done = line[3] != ' ';
                at = 6;
            }
            else if (line.Length >= 2 && line[0] == '-' && line[1] == ' ')
            {
                at = 2;
            }
            else return false;

            if (line.Length < at + 6) return false;
            if (!IsDigit(line[at]) || !IsDigit(line[at + 1])) return false;
            if (line[at + 2] != ':') return false;
            if (!IsDigit(line[at + 3]) || !IsDigit(line[at + 4])) return false;
            if (line[at + 5] != ' ') return false;

            info = new EntryInfo
            {
                IsDone = done,
                Stamp = line.Substring(at, 5),
                Content = line.Substring(at + 6)
            };
            return true;
        }

        public static bool IsEntryStart(string line)
        {
            EntryInfo ignored;
            return TryParse(line, out ignored);
        }

        /// <summary>把一条记录行改写成指定完成态，时间戳与正文原样保留。不是记录行则返回 null。</summary>
        public static string SetDone(string line, bool done)
        {
            EntryInfo info;
            if (!TryParse(line, out info)) return null;
            return (done ? DoneBox : UndoneBox) + info.Stamp + " " + info.Content;
        }

        /// <summary>
        /// 去掉填写页那一行开头的勾选标记（界面上画的脸，或使用者手打的 - [ ] / - [x]），
        /// 返回正文并给出完成态。没有标记时原样返回、视为未完成。
        /// 结果已 Trim——保存时每一行都要变成一条干净的待办正文。
        /// </summary>
        public static string StripMarker(string line, out bool done)
        {
            done = false;
            if (line == null) return "";

            if (StartsWith(line, DoneBox)) { done = true; return line.Substring(DoneBox.Length).Trim(); }
            if (StartsWith(line, UndoneBox)) return line.Substring(UndoneBox.Length).Trim();
            if (StartsWith(line, DoneFace)) { done = true; return line.Substring(DoneFace.Length).Trim(); }
            if (StartsWith(line, UndoneFace)) return line.Substring(UndoneFace.Length).Trim();

            return line.Trim();
        }

        /// <summary>
        /// 删除第 index 行时要一并删掉多少行：这条记录本身 + 它的续行 + 紧随的一个空行。
        /// <para>
        /// 续行只可能来自旧格式——新格式每一行都是独立记录。遇到空行或下一条记录就停，
        /// 否则会把使用者写的 "## 标题" 或下一条记录一起吃掉。
        /// </para>
        /// </summary>
        public static int DeleteCount(IList<string> lines, int index)
        {
            var end = index + 1;
            while (end < lines.Count)
            {
                if (lines[end].Trim().Length == 0) break;
                if (IsEntryStart(lines[end])) break;
                end++;
            }
            if (end < lines.Count && lines[end].Trim().Length == 0) end++;
            return end - index;
        }

        private static bool StartsWith(string line, string prefix)
        {
            return line.Length >= prefix.Length &&
                   string.CompareOrdinal(line, 0, prefix, 0, prefix.Length) == 0;
        }

        private static bool IsDigit(char c) { return c >= '0' && c <= '9'; }
    }
}
```

- [ ] **Step 4: 把 `DateFormat` 迁过来**

`src/QuickNote.Core/DateFormat.cs`：**删掉 `IsEntryStart` 的实现与 `IsDigit`（第 47-60 行），换成委托，并加 `ShortHeading`**。

**`EntryPrefix` 这一轮先留着不删**（第 34-37 行原样保留）：`NoteStore.Append` 此刻还在用它，现在就删会让 Core 编不过、这一步的「变绿」根本达不到。它随 Task 3 一起走——那时 `Append` 已经改用 `EntryLine.Prefix`。

```csharp
        /// <summary>顶部 chrome 用的短标题，例如「10-05 星期一」。不带年份。</summary>
        public static string ShortHeading(DateTime when)
        {
            return when.ToString("MM-dd", CultureInfo.InvariantCulture)
                + " " + when.ToString("dddd", CultureInfo.CurrentCulture);
        }

        /// <summary>判断一行是否是程序生成的记录起始行。格式细节见 <see cref="EntryLine"/>。</summary>
        public static bool IsEntryStart(string line)
        {
            return EntryLine.IsEntryStart(line);
        }
```

- [ ] **Step 5: 跑自检，确认变绿**

```bash
dotnet run --project src/QuickNote.SelfCheck -c Debug
```

期望：`全部通过`，没有新的编译错误。`DateFormatChecks` 一个字都不用改——它现有的 11 条断言全部仍然成立（`EntryPrefix` 这一轮没删，`IsEntryStart` 的旧格式断言照旧通过）。

在 `DateFormatChecks.Run()` 里补三条覆盖新格式与 `ShortHeading` 的断言：

```csharp
            Check.Equal(DateFormat.ShortHeading(t), "10-05 星期一", "顶部 chrome 用的短标题不带年份");
            Check.That(DateFormat.IsEntryStart("- [ ] 14:03 新格式"), "新格式的记录行也能识别");
            Check.That(!DateFormat.IsEntryStart("- [ ] 待办"), "手打的待办列表项仍然不是记录行");
```

（`t` 是 `new DateTime(2026, 10, 5, 14, 3, 0)`，2026-10-05 是星期一。`ShortHeading` 走 `CurrentCulture`，本机是中文区域，所以得到中文星期。）

- [ ] **Step 6: 提交**

```bash
git add src/QuickNote.Core/EntryLine.cs src/QuickNote.Core/DateFormat.cs \
        src/QuickNote.SelfCheck/EntryLineChecks.cs src/QuickNote.SelfCheck/DateFormatChecks.cs \
        src/QuickNote.SelfCheck/Program.cs
git commit -m "feat(core): 记录行改为 - [ ] HH:mm 待办格式，读取兼容旧格式"
```

---

### Task 2: `NoteStore` 原子重写 —— `.bak`、勾选、删除

**Files:**
- Modify: `src/QuickNote.Core/NoteStore.cs`
- Modify: `src/QuickNote.SelfCheck/NoteStoreChecks.cs`

**Interfaces:**
- Consumes: `EntryLine.TryParse`、`EntryLine.SetDone`、`EntryLine.DeleteCount`（Task 1）
- Produces:
  - `NoteStore.SetDone(DateTime day, int lineNumber, string expectedLine, bool done) → bool`
  - `NoteStore.DeleteEntry(DateTime day, int lineNumber, string expectedLine) → bool`
  - `NoteStore.PathFor(DateTime)`（已存在）

- [ ] **Step 1: 写失败的断言**

在 `src/QuickNote.SelfCheck/NoteStoreChecks.cs` 的 `Run()` 里、`CountsEntriesNotListLines();` 之后加一行：

```csharp
            RewritesAtomically();
            SetDoneFlipsOnlyThatLine();
            RefusesStaleLineNumber();
            SecondDeleteKeepsFirstBackup();
```

并在类里加这四个方法：

```csharp
        private static void RewritesAtomically()
        {
            using (var dir = new TempDir())
            {
                var store = new NoteStore(dir.Path);
                store.Append("第一条", Day);
                var path = store.PathFor(Day);
                var before = File.ReadAllText(path, Encoding.UTF8);

                Check.That(store.DeleteEntry(Day, 3, "- [ ] 14:30 第一条"), "删掉第 3 行那条记录");
                Check.Equal(store.CountEntries(Day), 0, "删完之后条数归零");
                Check.That(!File.Exists(path + ".tmp"), "临时文件不残留");
                Check.That(File.Exists(path + ".bak"), "删除前留下 .bak");
                Check.Equal(File.ReadAllText(path + ".bak", Encoding.UTF8), before,
                    ".bak 是动手之前的样子");
                Check.That(!File.Exists(Path.Combine(dir.Path, "2026-10-05.md.bak.md")),
                    ".bak 不会被当成日记文件");
            }
        }

        private static void SetDoneFlipsOnlyThatLine()
        {
            using (var dir = new TempDir())
            {
                var store = new NoteStore(dir.Path);
                store.Append("买牛奶\n买鸡蛋", Day);
                var path = store.PathFor(Day);

                Check.That(store.SetDone(Day, 3, "- [ ] 14:30 买牛奶", true), "把第一条标成已完成");
                Check.Equal(File.ReadAllText(path, Encoding.UTF8),
                    "# 2026-10-05 星期一\n\n- [x] 14:30 买牛奶\n- [ ] 14:30 买鸡蛋\n\n",
                    "只有被勾的那一行变了");

                Check.That(!store.SetDone(Day, 3, "- [x] 14:30 买牛奶", true),
                    "已经是完成态时不再写一次");

                // 旧格式的记录被勾选时要升级成新格式，而不是保持原样
                store.Append("旧的", new DateTime(2026, 10, 6, 9, 0, 0));
                Check.That(store.SetDone(new DateTime(2026, 10, 6), 3, "- 09:00 旧的", true),
                    "勾选旧格式的记录");
                Check.Equal(File.ReadAllText(store.PathFor(new DateTime(2026, 10, 6)), Encoding.UTF8),
                    "# 2026-10-06 星期二\n\n- [x] 09:00 旧的\n\n",
                    "旧格式被升级成新格式，时间戳与正文不变");
            }
        }

        private static void RefusesStaleLineNumber()
        {
            using (var dir = new TempDir())
            {
                var store = new NoteStore(dir.Path);
                store.Append("第一条\n第二条", Day);
                var path = store.PathFor(Day);

                store.DeleteEntry(Day, 3, "- [ ] 14:30 第一条");
                var after = File.ReadAllText(path, Encoding.UTF8);

                // 列表生成之后文件变了，旧行号已经指向另一条记录。
                // 这时宁可不写，也不能按旧行号去删/勾一条使用者没选中的记录。
                Check.That(!store.DeleteEntry(Day, 3, "- [ ] 14:30 第一条"),
                    "行号已失效时拒绝删除");
                Check.Equal(File.ReadAllText(path, Encoding.UTF8), after, "被拒绝时文件一字未动");

                Check.That(!store.SetDone(Day, 99, "- [ ] 14:30 第二条", true), "行号越界时拒绝");
                Check.That(!store.SetDone(Day, 3, "完全不是记录的一行", true), "行内容对不上时拒绝");
                Check.That(!store.DeleteEntry(NextDay, 3, "x"), "那天的文件不存在时返回 false 而不是抛错");
            }
        }

        private static void SecondDeleteKeepsFirstBackup()
        {
            using (var dir = new TempDir())
            {
                var store = new NoteStore(dir.Path);
                store.Append("第一条\n第二条\n第三条", Day);
                var path = store.PathFor(Day);
                var before = File.ReadAllText(path, Encoding.UTF8);

                store.DeleteEntry(Day, 3, "- [ ] 14:30 第一条");
                store.DeleteEntry(Day, 3, "- [ ] 14:30 第二条");

                Check.Equal(File.ReadAllText(path + ".bak", Encoding.UTF8), before,
                    "第二次删除不覆盖 .bak——保留的是今天动手之前的样子");
            }
        }
```

- [ ] **Step 2: 跑一遍确认是红的**

```bash
dotnet run --project src/QuickNote.SelfCheck -c Debug
```

期望：编译失败，`CS1061: "NoteStore"未包含"DeleteEntry"的定义`。

- [ ] **Step 3: 实现重写路径**

在 `src/QuickNote.Core/NoteStore.cs` 里，把 `using System.IO;` 下面补上 `using System.Collections.Generic;`，并在 `CountEntries` 之前插入：

```csharp
        /// <summary>
        /// 把某一天的某一行的勾选状态改成 done。行内容与 <paramref name="expectedLine"/>
        /// 不一致时拒绝写入——那是「列表生成之后文件被外部改过」的信号，
        /// 按旧行号动手会改错一条。
        /// </summary>
        public bool SetDone(DateTime day, int lineNumber, string expectedLine, bool done)
        {
            return Rewrite(day, lineNumber, expectedLine, delegate(List<string> lines, int index)
            {
                var updated = EntryLine.SetDone(lines[index], done);
                if (updated == null || updated == lines[index]) return false;   // 已经是这个状态
                lines[index] = updated;
                return true;
            });
        }

        /// <summary>
        /// 删掉某一天的第 lineNumber 行那条记录。旧格式的续行跟着一起删，
        /// 不留孤儿行（定案 §10.2）。
        /// </summary>
        public bool DeleteEntry(DateTime day, int lineNumber, string expectedLine)
        {
            return Rewrite(day, lineNumber, expectedLine, delegate(List<string> lines, int index)
            {
                lines.RemoveRange(index, EntryLine.DeleteCount(lines, index));
                return true;
            });
        }

        /// <summary>
        /// 删除与勾选共用的重写路径。返回 false 表示「什么都没做」——
        /// 文件不在、行号越界、行内容对不上、或改完没有变化。
        /// </summary>
        private bool Rewrite(DateTime day, int lineNumber, string expectedLine,
                             Func<List<string>, int, bool> mutate)
        {
            var path = PathFor(day);
            if (!File.Exists(path)) return false;

            List<string> lines;
            try
            {
                lines = ReadLines(path);
            }
            catch (Exception ex)
            {
                throw new NoteStoreException("读取 " + path + " 失败：" + ex.Message, ex);
            }

            var index = lineNumber - 1;
            if (index < 0 || index >= lines.Count) return false;
            if (lines[index] != expectedLine) return false;
            if (!mutate(lines, index)) return false;

            BackupOnce(path);
            WriteAtomic(path, lines);
            return true;
        }

        private static List<string> ReadLines(string path)
        {
            var lines = new List<string>();
            using (var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
            using (var r = new StreamReader(fs, Encoding.UTF8))
            {
                string line;
                while ((line = r.ReadLine()) != null) lines.Add(line);
            }
            return lines;
        }

        /// <summary>
        /// 当天首次改写之前留一份原文，之后不再覆盖。
        /// 保留的是「今天动手之前」的样子，那才是使用者真正想找回来的东西；
        /// 每次改写都覆盖的话，连删几条之后就只剩最后一次之前的状态了。
        /// </summary>
        private static void BackupOnce(string path)
        {
            var bak = path + ".bak";
            if (File.Exists(bak)) return;
            try
            {
                File.Copy(path, bak);
            }
            catch (Exception ex)
            {
                throw new NoteStoreException("备份 " + path + " 失败：" + ex.Message, ex);
            }
        }

        /// <summary>
        /// 写临时文件再原子替换。追加写入靠「不碰前面的内容」规避了写坏一半的风险，
        /// 重写就没有这层保护了——中途失败会留下半个文件，所以必须走
        /// File.Replace（NTFS 上要么旧文件、要么新文件）。
        /// <para>
        /// File.Replace 需要目标可写：Typora 之类以独占方式打开着当天文件时会失败。
        /// 那时原文件一字未动，抛出去让界面提示即可。
        /// </para>
        /// </summary>
        private static void WriteAtomic(string path, List<string> lines)
        {
            var tmp = path + ".tmp";
            var sb = new StringBuilder();
            foreach (var line in lines) sb.Append(line).Append('\n');

            try
            {
                File.WriteAllText(tmp, sb.ToString(), new UTF8Encoding(false));
                File.Replace(tmp, path, null);
            }
            catch (Exception ex)
            {
                TryDelete(tmp);
                throw new NoteStoreException("重写 " + path + " 失败：" + ex.Message, ex);
            }
        }

        private static void TryDelete(string path)
        {
            try { if (File.Exists(path)) File.Delete(path); }
            catch { /* 清不掉临时文件不该盖住真正的错误 */ }
        }
```

- [ ] **Step 4: 跑自检，确认变绿**

```bash
dotnet run --project src/QuickNote.SelfCheck -c Debug
```

期望：`全部通过`。

- [ ] **Step 5: 提交**

```bash
git add src/QuickNote.Core/NoteStore.cs src/QuickNote.SelfCheck/NoteStoreChecks.cs
git commit -m "feat(core): NoteStore 加原子重写，支持勾选与删除并留 .bak"
```

---

### Task 3: `NoteStore.Append` —— 每一行各成一条待办

**Files:**
- Modify: `src/QuickNote.Core/NoteStore.cs:29-73`
- Modify: `src/QuickNote.SelfCheck/NoteStoreChecks.cs`

**Interfaces:**
- Consumes: `EntryLine.Prefix`、`EntryLine.StripMarker`（Task 1）
- Produces: `NoteStore.Append(string content, DateTime now) → bool`（签名不变，语义变了）

- [ ] **Step 1: 改断言**

`src/QuickNote.Core/DateFormat.cs`：删掉 `EntryPrefix`（第 34-37 行，Task 1 按 P-1 裁定留给本轮）。`src/QuickNote.SelfCheck/DateFormatChecks.cs` 里对应那条 `Check.Equal(DateFormat.EntryPrefix(t), "- 14:03 ", ...)` 一并删掉——它的继任者是 `EntryLineChecks` 里的 `EntryLine.Prefix` 两条断言。

`src/QuickNote.SelfCheck/NoteStoreChecks.cs`：

把 `MultiLineOnlyFirstLineGetsStamp();` 换成两行：

```csharp
            EachLineBecomesItsOwnEntry();
            MarkerOnlyLinesWriteNothing();
```

把 `MultiLineOnlyFirstLineGetsStamp` 整个方法替换成：

```csharp
        private static void EachLineBecomesItsOwnEntry()
        {
            using (var dir = new TempDir())
            {
                var store = new NoteStore(dir.Path);
                store.Append("第一行\n第二行\n第三行", Day);

                Check.Equal(File.ReadAllText(store.PathFor(Day), Encoding.UTF8),
                    "# 2026-10-05 星期一\n\n- [ ] 14:30 第一行\n- [ ] 14:30 第二行\n- [ ] 14:30 第三行\n\n",
                    "每一次换行各成一条待办，共用保存那一刻的时间戳");
            }
        }

        private static void MarkerOnlyLinesWriteNothing()
        {
            using (var dir = new TempDir())
            {
                var store = new NoteStore(dir.Path);

                // 打了 "- " 会自动补成脸，接着直接回车——不能因此写出一条空待办
                Check.That(!store.Append(EntryLine.UndoneFace + " ", Day), "只有脸没有正文返回 false");
                Check.That(!store.Append("- [ ] ", Day), "只有勾选框没有正文返回 false");
                Check.That(!store.Append("有内容\n" + EntryLine.UndoneFace + " \n也有内容", Day),
                    "夹在中间的空标记行不生成空待办");

                Check.Equal(File.ReadAllText(store.PathFor(Day), Encoding.UTF8),
                    "# 2026-10-05 星期一\n\n- [ ] 14:30 有内容\n- [ ] 14:30 也有内容\n\n",
                    "空标记行被丢弃，其余照常写入");
            }
        }
```

并把现有三个方法的期望值改成新格式：

`NewFileGetsHeading`（第 37-38 行）：

```csharp
                Check.Equal(text, "# 2026-10-05 星期一\n\n- [ ] 14:30 跟老王确认了接口字段\n\n",
                    "新文件含标题行、勾选框、时间戳前缀与结尾空行");
```

`AppendsToExistingFile`（第 51-53 行）：

```csharp
                Check.Equal(text,
                    "# 2026-10-05 星期一\n\n- [ ] 14:30 第一条\n\n- [ ] 14:32 第二条\n\n",
                    "追加不重复写标题行，两条记录各自带勾选框与时间戳");
```

`RollsOverAtMidnight`（第 68-69 行）：

```csharp
                Check.Equal(File.ReadAllText(store.PathFor(NextDay), Encoding.UTF8),
                    "# 2026-10-06 星期二\n\n- [ ] 09:05 第二天的\n\n", "新一天的文件有自己的标题行");
```

`PathWithChineseCharacters`（第 128-130 行）：

```csharp
                Check.Equal(File.ReadAllText(Path.Combine(cn, "2026-10-05.md"), Encoding.UTF8),
                    "# 2026-10-05 星期一\n\n- [ ] 14:30 路径含中文与空格\n\n",
                    "中文路径下内容正确");
```

`CountsEntriesNotListLines` 里第二条记录的断言（第 164-168 行）换成：

```csharp
                // 新模型下每一行都是一条记录；续行只可能来自旧文件。
                store.Append("第二条\n- [ ] 待办甲\n- [ ] 待办乙",
                    new DateTime(2026, 10, 5, 14, 35, 0));
                Check.Equal(store.CountEntries(Day), 4, "每一行各成一条记录");
```

最后一条断言（第 173-174 行）改成 `Check.Equal(store.CountEntries(Day), 5, "个位数小时（09:05）补零后仍能识别");`

并在 `CountsEntriesNotListLines` 末尾追加旧格式的兼容断言：

```csharp
                // 使用者现有的笔记是旧格式，必须照样算数
                File.WriteAllText(Path.Combine(dir.Path, "2026-10-04.md"),
                    "# 2026-10-04 星期日\n\n- 20:18 下载Minimax\n\n- 21:00 另一条\n\n",
                    new UTF8Encoding(false));
                Check.Equal(store.CountEntries(new DateTime(2026, 10, 4)), 2,
                    "旧格式（- HH:mm）的记录一样计数");
```

- [ ] **Step 2: 跑一遍确认是红的**

```bash
dotnet run --project src/QuickNote.SelfCheck -c Debug
```

期望：`NoteStore` 段落出现若干 `FAIL`，例如「每一次换行各成一条待办」实际是 `"- 14:30 第一行\n第二行\n第三行\n"`。

- [ ] **Step 3: 改实现**

`src/QuickNote.Core/NoteStore.cs`，把 `Append` 的方法体换成：

```csharp
        /// <summary>
        /// 追加一笔。<paramref name="content"/> 里的**每一行各成一条待办**（定案 §10.1）：
        /// 使用者的真实用法是「一口气列几件要做的事」，不是写一段带结构的长文。
        /// 每行开头若已有勾选标记（界面上画的脸，或他手打的 - [ ]），沿用它的状态。
        /// 全部行都是空白（连标记都没有正文）时什么都不做，返回 false。
        /// </summary>
        public bool Append(string content, DateTime now)
        {
            if (content == null) return false;

            var normalized = content.Replace("\r\n", "\n").Replace('\r', '\n');
            if (normalized.Trim().Length == 0) return false;

            var lines = normalized.TrimEnd('\n').Split('\n');

            var entries = new StringBuilder();
            foreach (var raw in lines)
            {
                bool done;
                var body = EntryLine.StripMarker(raw, out done);
                if (body.Length == 0) continue;      // 空行、或只有标记没有正文
                entries.Append(EntryLine.Prefix(now, done)).Append(body).Append('\n');
            }
            if (entries.Length == 0) return false;   // 别创建空文件

            var sb = new StringBuilder();
            var path = PathFor(now);
            if (!File.Exists(path))
            {
                sb.Append(DateFormat.Heading(now)).Append('\n').Append('\n');
            }
            sb.Append(entries).Append('\n');

            try
            {
                Directory.CreateDirectory(NotesDir);
                // FileShare.ReadWrite：使用者正用 Typora 打开当天文件时仍能写入。
                using (var fs = new FileStream(path, FileMode.Append, FileAccess.Write, FileShare.ReadWrite))
                using (var w = new StreamWriter(fs, new UTF8Encoding(false)))
                {
                    w.Write(sb.ToString());
                }
            }
            catch (Exception ex)
            {
                throw new NoteStoreException("写入 " + path + " 失败：" + ex.Message, ex);
            }

            return true;
        }
```

- [ ] **Step 4: 跑自检，确认变绿**

```bash
dotnet run --project src/QuickNote.SelfCheck -c Debug
```

期望：`全部通过`。`NoteSearcher` 段落也应保持全绿——读取是兼容的，这一任务不碰它。

- [ ] **Step 5: 提交**

```bash
git add src/QuickNote.Core/NoteStore.cs src/QuickNote.Core/DateFormat.cs \
        src/QuickNote.SelfCheck/NoteStoreChecks.cs src/QuickNote.SelfCheck/DateFormatChecks.cs
git commit -m "feat(core): 填写页每一行各成一条待办"
```

---

### Task 4: `CaretPulse` —— 光标明暗的纯函数

**Files:**
- Create: `src/QuickNote.Core/CaretPulse.cs`
- Create: `src/QuickNote.SelfCheck/CaretPulseChecks.cs`
- Modify: `src/QuickNote.SelfCheck/Program.cs`

**Interfaces:**
- Produces: `CaretPulse.LevelAt(int elapsedMs) → double`（0.35 最暗、1.0 最亮）

- [ ] **Step 1: 写失败的断言**

创建 `src/QuickNote.SelfCheck/CaretPulseChecks.cs`：

```csharp
using System;
using QuickNote.Core;

namespace QuickNote.SelfCheck
{
    internal static class CaretPulseChecks
    {
        public static void Run()
        {
            Check.Section("CaretPulse");

            Check.Equal(Math.Round(CaretPulse.LevelAt(0), 3), 0.35, "起点最暗");
            Check.Equal(Math.Round(CaretPulse.LevelAt(500), 3), 1.0, "半周期最亮");
            Check.Equal(Math.Round(CaretPulse.LevelAt(1000), 3), 0.35, "整周期回到起点");
            Check.Equal(Math.Round(CaretPulse.LevelAt(2000), 3), 0.35, "跨多个周期仍然对齐");
            Check.Equal(Math.Round(CaretPulse.LevelAt(250), 3), 0.675, "上升段线性");

            Check.That(CaretPulse.LevelAt(-1) > 0.34 && CaretPulse.LevelAt(-1) < 0.36,
                "负数毫秒被折回周期内而不是返回怪值");

            var rising = true;
            for (var t = 0; t < 500; t += 10)
            {
                if (CaretPulse.LevelAt(t + 10) < CaretPulse.LevelAt(t)) rising = false;
            }
            Check.That(rising, "前半周期单调变亮，没有抖动");

            var min = 2.0;
            for (var t = 0; t < 1000; t += 10) min = Math.Min(min, CaretPulse.LevelAt(t));
            Check.That(min >= 0.35, "永远不落到全暗——光标整个消失会像是丢了焦点");
        }
    }
}
```

在 `Program.cs` 里 `EntryLineChecks.Run();` 下面插入：

```csharp
                CaretPulseChecks.Run();
```

- [ ] **Step 2: 跑一遍确认是红的**

```bash
dotnet run --project src/QuickNote.SelfCheck -c Debug
```

期望：编译失败，`CS0103: 当前上下文中不存在名称"CaretPulse"`。

- [ ] **Step 3: 实现**

创建 `src/QuickNote.Core/CaretPulse.cs`：

```csharp
namespace QuickNote.Core
{
    /// <summary>
    /// 自绘光标的明暗曲线。1 秒一循环的三角波，不是硬开关的闪烁。
    /// 纯函数，方便离线钉住曲线形状——这东西的对错只能靠眼睛，
    /// 至少把「什么时候最亮」钉死在断言里。
    /// </summary>
    public static class CaretPulse
    {
        public const int PeriodMs = 1000;

        /// <summary>最暗时的不透明度。不落到 0：光标整个消失看起来像丢了焦点。</summary>
        public const double MinLevel = 0.35;

        /// <summary>把经过的毫秒映射成 0.35–1.0 的亮度。</summary>
        public static double LevelAt(int elapsedMs)
        {
            var t = elapsedMs % PeriodMs;
            if (t < 0) t += PeriodMs;

            var half = PeriodMs / 2.0;
            var up = t < half ? t / half : (PeriodMs - t) / half;
            return MinLevel + (1.0 - MinLevel) * up;
        }
    }
}
```

- [ ] **Step 4: 跑自检，确认变绿**

```bash
dotnet run --project src/QuickNote.SelfCheck -c Debug
```

期望：`全部通过`。

- [ ] **Step 5: 提交**

```bash
git add src/QuickNote.Core/CaretPulse.cs src/QuickNote.SelfCheck/CaretPulseChecks.cs \
        src/QuickNote.SelfCheck/Program.cs
git commit -m "feat(core): 光标明暗曲线的纯函数"
```

---

### Task 5: `Theme` —— 换掉整套 token 与字体

**Files:**
- Modify: `src/QuickNote/Theme.cs`（整个 `Palette` 类与两个工厂方法重写）

**Interfaces:**
- Produces: `Palette { Paper, Ink, InkSoft, Muted, Stamp, Hairline, Selection, Highlight, Error, Body, StampFont, Chrome, Heading, Mono, Emoji }`
- 删掉：`BarBack`、`Text`、`Accent`、`Todo`、`Border`、`Small`、`HeadingFont`

**注意：这一任务会让 `BarChrome` / `CaptureBar` / `MarkdownHighlighter` / `SearchResultsView` 编译失败。** 它们是同一个编译单元，必须在 Task 6/7/8 里一起改完才能构建通过。**所以本任务不单独提交**，与 Task 6 合并提交。

- [ ] **Step 1: 重写 `Theme.cs`**

```csharp
using System.Drawing;
using Microsoft.Win32;

namespace QuickNote
{
    /// <summary>
    /// 小条的配色与字体。
    /// <para>
    /// 立场（定案 §1）：这不是生产力看板，是一块掉落下来的便签。全界面近乎无彩，
    /// **唯一的颜色来自那两个 emoji 脸**——所以没有强调色、没有待办橙。
    /// 标题用字重区分，列表符是结构不是装饰，行内代码用 ink-soft 加等宽、不加底色
    /// （RichEdit 画不出圆角底，方底更丑）。
    /// </para>
    /// </summary>
    internal sealed class Palette
    {
        public Color Paper;      // 小条底色
        public Color Ink;        // 正文
        public Color InkSoft;    // 次要正文、续行、行内代码
        public Color Muted;      // 键位提示、状态文字、引用行
        public Color Stamp;      // 时间戳——比 muted 还轻，退到背景里
        public Color Hairline;   // 上下两条 chrome 的分隔线
        public Color Selection;  // 选中行底纹（仅用于绘制，可带 alpha）
        public Color Highlight;  // 搜索命中处底纹（仅用于绘制，可带 alpha）
        public Color Error;      // 保存失败与删除确认。唯一被允许的彩色，是特例

        public Font Body;        // 正文
        public Font StampFont;   // 时间戳。界面里唯一使用第二套字体的地方
        public Font Chrome;      // 上下两条辅助信息行
        public Font Heading;     // 标题（加粗，不是换色）
        public Font Mono;        // 行内代码
        public Font Emoji;       // 两个脸。不指定它的话会被正文 CJK 字体带去黑白字形
    }

    internal static class Theme
    {
        public static bool IsDarkMode()
        {
            try
            {
                using (var key = Registry.CurrentUser.OpenSubKey(
                    @"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize"))
                {
                    if (key == null) return true;
                    var v = key.GetValue("AppsUseLightTheme");
                    if (v is int) return (int)v == 0;
                }
            }
            catch { /* 读不到就按深色处理 */ }
            return true;
        }

        public static bool ShouldUseDark(string themeSetting)
        {
            if (themeSetting == "Dark") return true;
            if (themeSetting == "Light") return false;
            return IsDarkMode();
        }

        public static Palette Current(string themeSetting)
        {
            return ShouldUseDark(themeSetting) ? Dark() : Light();
        }

        // 交给控件属性的颜色必须完全不透明（见接手须知）：WinForms 的 Control.BackColor
        // 收到 alpha < 255 会直接抛 ArgumentException，在构造函数里就把进程打死。
        // 只有纯 GDI+ 绘制的 Selection / Highlight 可以带 alpha。
        private static Palette Light()
        {
            return new Palette
            {
                Paper = Rgb(0xFA, 0xFA, 0xF8),      // 近白微暖，不是奶油色
                Ink = Rgb(0x23, 0x23, 0x26),
                InkSoft = Rgb(0x55, 0x55, 0x5C),
                Muted = Rgb(0x8E, 0x8E, 0x93),
                Stamp = Rgb(0xA9, 0xA9, 0xAE),
                Hairline = Rgb(0xE8, 0xE8, 0xE4),
                Selection = Color.FromArgb(38, 0x23, 0x23, 0x26),
                Highlight = Color.FromArgb(52, 0x23, 0x23, 0x26),
                Error = Rgb(0x9C, 0x3A, 0x34),
                Body = Font(CjkFamilies, 11f, FontStyle.Regular),
                StampFont = Font(NumeralFamilies, 10.5f, FontStyle.Regular),
                Chrome = Font(CjkFamilies, 8.5f, FontStyle.Regular),
                Heading = Font(CjkFamilies, 11f, FontStyle.Bold),
                Mono = Font(MonoFamilies, 10f, FontStyle.Regular),
                Emoji = Font(EmojiFamilies, 11f, FontStyle.Regular)
            };
        }

        private static Palette Dark()
        {
            return new Palette
            {
                Paper = Rgb(0x1A, 0x1A, 0x1C),
                Ink = Rgb(0xE8, 0xE8, 0xE6),
                InkSoft = Rgb(0xB4, 0xB4, 0xB8),
                Muted = Rgb(0x7E, 0x7E, 0x84),
                Stamp = Rgb(0x6E, 0x6E, 0x74),
                Hairline = Rgb(0x2C, 0x2C, 0x30),
                Selection = Color.FromArgb(46, 0xE8, 0xE8, 0xE6),
                Highlight = Color.FromArgb(64, 0xE8, 0xE8, 0xE6),
                Error = Rgb(0xE0, 0x73, 0x6C),
                Body = Font(CjkFamilies, 11f, FontStyle.Regular),
                StampFont = Font(NumeralFamilies, 10.5f, FontStyle.Regular),
                Chrome = Font(CjkFamilies, 8.5f, FontStyle.Regular),
                Heading = Font(CjkFamilies, 11f, FontStyle.Bold),
                Mono = Font(MonoFamilies, 10f, FontStyle.Regular),
                Emoji = Font(EmojiFamilies, 11f, FontStyle.Regular)
            };
        }

        // Noto Sans SC 比雅黑 UI 开阔、字面更匀，不是系统默认脸。
        private static readonly string[] CjkFamilies =
            { "Noto Sans SC", "思源黑体", "Source Han Sans SC", "DengXian", "Microsoft YaHei UI" };

        // Bahnschrift Light 是 DIN 系，数字有技术感，跟中文的人文气质拉开层次。
        private static readonly string[] NumeralFamilies =
            { "Bahnschrift Light", "Bahnschrift", "Consolas" };

        private static readonly string[] MonoFamilies = { "Consolas", "Courier New" };
        private static readonly string[] EmojiFamilies = { "Segoe UI Emoji" };

        private static Color Rgb(int r, int g, int b) { return Color.FromArgb(r, g, b); }

        /// <summary>
        /// 按顺序取第一个装得上的字体。装不上时直接 new Font("不存在", ...) 会静默退回
        /// Microsoft Sans Serif——中文用那个脸很难看，而且没有任何提示。
        /// </summary>
        private static Font Font(string[] families, float size, FontStyle style)
        {
            foreach (var name in families)
            {
                try { return new Font(name, size, style); }
                catch (ArgumentException) { /* 这个家族不存在，试下一个 */ }
            }
            return new Font(FontFamily.GenericSansSerif, size, style);
        }
    }
}
```

- [ ] **Step 2: 不要在这里构建**

`Palette` 的字段名变了，四个 UI 文件此刻必然编译失败。**直接接着做 Task 6，不要提交，不要跑构建。**

---

### Task 6: 分隔线、字号层次、去掉彩色高亮

**Files:**
- Modify: `src/QuickNote/BarChrome.cs`
- Modify: `src/QuickNote/MarkdownHighlighter.cs:79-122`
- Modify: `src/QuickNote/SearchResultsView.cs:86-172`
- Modify: `src/QuickNote/CaptureBar.cs`（字段改名，`TopText`）

**Interfaces:**
- Consumes: Task 5 的 `Palette` 新字段
- Produces: 构建通过

- [ ] **Step 1: `BarChrome` 加 hairline**

```csharp
using System.Drawing;
using System.Windows.Forms;

namespace QuickNote
{
    /// <summary>
    /// 小条的上下两条辅助信息行。刻意做成极简：只有日期、条数与键位提示。
    /// <para>
    /// 各带一条 1px 分隔线。原先完全没有结构线，日期、正文、提示挤在一起，
    /// 那是「乱」的来源之一（定案 §4）。线画在 Label 自己的边缘上，
    /// 不动停靠顺序——动停靠会打乱 Fill 的取空间方式。
    /// </para>
    /// </summary>
    internal sealed class BarChrome
    {
        public readonly Label Top;
        public readonly Label Bottom;

        private Color _hairline;

        public BarChrome(Palette p)
        {
            _hairline = p.Hairline;
            Top = NewLabel(p.Chrome, p.Muted);
            Bottom = NewLabel(p.Chrome, p.Muted);
            Top.Dock = DockStyle.Top;
            Top.Height = 22;
            Bottom.Dock = DockStyle.Bottom;
            Bottom.Height = 22;

            Top.Paint += delegate(object s, PaintEventArgs e)
            {
                using (var pen = new Pen(_hairline))
                    e.Graphics.DrawLine(pen, 0, Top.Height - 1, Top.Width, Top.Height - 1);
            };
            Bottom.Paint += delegate(object s, PaintEventArgs e)
            {
                using (var pen = new Pen(_hairline))
                    e.Graphics.DrawLine(pen, 0, 0, Bottom.Width, 0);
            };
        }

        public void ApplyHairline(Color hairline)
        {
            _hairline = hairline;
            Top.Invalidate();
            Bottom.Invalidate();
        }

        private static Label NewLabel(Font font, Color color)
        {
            return new Label
            {
                Font = font,
                ForeColor = color,
                BackColor = Color.Transparent,
                AutoSize = false,
                TextAlign = ContentAlignment.MiddleLeft,
                Padding = new Padding(2, 0, 2, 0)
            };
        }
    }
}
```

- [ ] **Step 2: `MarkdownHighlighter` 去掉彩色**

把 `ColorLine`（第 79-122 行）换成：

```csharp
        /// <summary>填写页那一行开头的脸（"😶‍🌫️ " / "🥳 "）。</summary>
        private static readonly Regex FaceMarker = new Regex(
            "^(" + EntryLine.UndoneFace + "|" + EntryLine.DoneFace + ")", RegexOptions.Compiled);

        private static void ColorLine(RichTextBox box, string text, int start, int end, Palette p)
        {
            var line = text.Substring(start, end - start);

            // 脸必须显式指定 emoji 字体。正文的 CJK 字体不含这些码位，
            // 交给字体链接去兜的话有可能落到黑白字形上。
            var face = FaceMarker.Match(line);
            if (face.Success) Paint(box, start, face.Length, p.Ink, p.Emoji);

            // 标题不用颜色，用字重（定案 §2 的「颜色的使用纪律」）。
            if (HeadingLine.IsMatch(line))
            {
                Paint(box, start, line.Length, p.Ink, p.Heading);
                return;
            }

            if (QuoteLine.IsMatch(line))
            {
                Paint(box, start, line.Length, p.Muted, p.Body);
                return;
            }

            var todo = TodoMarker.Match(line);
            if (todo.Success)
            {
                // 手打的 "- [ ]"：不换色，只把它压成 ink-soft，让正文仍然是主角
                var markerStart = start + todo.Groups[1].Length;
                Paint(box, start, line.Length, p.Ink, p.Body);
                Paint(box, markerStart, todo.Groups[2].Length, p.InkSoft, p.Body);
            }
            else
            {
                var bullet = BulletMarker.Match(line);
                if (bullet.Success)
                {
                    var markerStart = start + bullet.Groups[1].Length;
                    Paint(box, start, line.Length, p.Ink, p.Body);
                    Paint(box, markerStart, bullet.Groups[2].Length, p.InkSoft, p.Body);
                }
            }

            foreach (Match m in Bold.Matches(line))
            {
                Paint(box, start + m.Index, m.Length, p.Ink, p.Heading);
            }
            foreach (Match m in Code.Matches(line))
            {
                // 行内代码用 ink-soft + 等宽，不加底色——RichEdit 画不出圆角底，方底更丑
                Paint(box, start + m.Index, m.Length, p.InkSoft, p.Mono);
            }
        }
```

同时把 `Apply` 里的两处（第 46-47 行）：

```csharp
                box.SelectAll();
                box.SelectionColor = p.Ink;
                box.SelectionFont = p.Body;
```

并把文件顶部的 `using QuickNote.Core;` 保留（`EntryLine` 用到了）。

- [ ] **Step 3: `SearchResultsView` 换成新 token**

把 `OnPaint` 里选中行的底纹（第 104-105 行）改成：

```csharp
                if (i == _selected)
                {
                    using (var b = new SolidBrush(_danger ? Color.FromArgb(46, _p.Error) : _p.Selection))
                        g.FillRectangle(b, 0, y, Width, RowHeight - 2);
                }
```

把日期行（第 109-110 行）改成时间戳字体：

```csharp
                // 时间戳是唯一使用第二套字体（Bahnschrift）的地方。字号要比 CJK 大一档
                // 才压得住——Bahnschrift 的 x-height 偏小。
                TextRenderer.DrawText(g, hit.Date.ToString("MM-dd"), _p.StampFont,
                    new Point(10, y + 4), _p.Stamp);
                TextRenderer.DrawText(g, hit.Date.ToString("dddd"), _p.Chrome,
                    new Point(62, y + 5), _p.Muted);
```

把「没有找到匹配」（第 93-94 行）的 `_p.Small` 改成 `_p.Chrome`。

把 `DrawMatchLine` 里的 `_p.Text` 全部改成 `_p.Ink`，并把命中处的高亮（第 158-164 行）改成：

```csharp
            var matchedWidth = TextRenderer.MeasureText(g, matched, _p.Body).Width;
            var drawMatched = Math.Min(matchedWidth, remaining);
            using (var b = new SolidBrush(_p.Highlight))
                g.FillRectangle(b, at - 1, box.Y, drawMatched + 2, box.Height);
            TextRenderer.DrawText(g, matched, _p.Body,
                new Rectangle(at, box.Y, drawMatched, box.Height), _p.Ink,
                TextFormatFlags.NoPadding | TextFormatFlags.EndEllipsis);
```

并把构造函数里的 `BackColor = p.BarBack;` 改成 `BackColor = p.Paper;`，`g.Clear(_p.BarBack)` 改成 `g.Clear(_p.Paper)`。

类里加一个字段（第 21 行附近）：

```csharp
        private bool _danger;
```

和公开方法：

```csharp
        /// <summary>删除确认态：选中行改用错误色底纹。</summary>
        public void SetDanger(bool danger)
        {
            if (_danger == danger) return;
            _danger = danger;
            Invalidate();
        }
```

- [ ] **Step 4: 行距按 RichEdit 的实际行高走，内边距跟着 DPI 缩放**

现在的 `LogicalLineHeight = 22` / `LogicalChrome = 90` 是拍出来的：`HeightFor(3, 22, 90) = 156`，而实际内容只需要 20(内边距) + 40(上下两行) + 66(三行文字) = 126——**多出来的 30px 是一块没人要的空当**，就在最后一行下面。字体一换（雅黑 → Noto Sans SC）、DPI 一变，这个写死的值还会让最后一行的下沿被切掉。两件事一起改掉。

先改 `src/QuickNote.Core/BarGeometry.cs`，删掉 `HeightFor`，换成行数钳制：

```csharp
namespace QuickNote.Core
{
    /// <summary>
    /// 小条高度的纯计算。单独抽出来是因为「粘贴一大段文本」这条路径：
    /// 没有上限的话小条会一路长高直到遮住整个屏幕。
    /// <para>
    /// 只钳行数，不再算总高度——一行到底多高由 RichEdit 自己的行距决定，
    /// 写死在 Core 里的话换字体或换 DPI 就会对不上。
    /// </para>
    /// </summary>
    public static class BarGeometry
    {
        public const int MinLines = 3;
        public const int MaxLines = 8;

        /// <summary>
        /// 超过这个字符数就不再逐字着色。设计预期是「几百字节」，而粘贴一大段文本
        /// 会打破这个前提：每次按键重扫全文着色会冻住 UI 线程，而 UI 线程一冻
        /// 就卡住全局键盘钩子，超过约 300ms 会被 Windows 静默摘掉。
        /// </summary>
        public const int MaxHighlightChars = 20000;

        public static bool ShouldHighlight(int textLength)
        {
            return textLength <= MaxHighlightChars;
        }

        public static int ClampLines(int lineCount)
        {
            if (lineCount < MinLines) return MinLines;
            if (lineCount > MaxLines) return MaxLines;
            return lineCount;
        }
    }
}
```

`src/QuickNote.SelfCheck/BarGeometryChecks.cs` 整个换成：

```csharp
using QuickNote.Core;

namespace QuickNote.SelfCheck
{
    internal static class BarGeometryChecks
    {
        public static void Run()
        {
            Check.Section("BarGeometry");

            Check.Equal(BarGeometry.ClampLines(1), 3, "1 行内容时高度取下限 3 行");
            Check.Equal(BarGeometry.ClampLines(3), 3, "3 行时正好等于下限");
            Check.Equal(BarGeometry.ClampLines(5), 5, "5 行时按实际行数");
            Check.Equal(BarGeometry.ClampLines(8), 8, "8 行时正好等于上限");
            Check.Equal(BarGeometry.ClampLines(9), 8, "9 行时被上限钳制");
            Check.Equal(BarGeometry.ClampLines(500), 8, "粘贴 500 行时仍被钳制在上限，不会撑满屏幕");
            Check.Equal(BarGeometry.ClampLines(0), 3, "0 行时取下限");
            Check.Equal(BarGeometry.ClampLines(-7), 3, "负数行数取下限");

            Check.That(BarGeometry.ShouldHighlight(0), "空文档需要着色");
            Check.That(BarGeometry.ShouldHighlight(BarGeometry.MaxHighlightChars),
                "正好等于上限时需要着色");
            Check.That(!BarGeometry.ShouldHighlight(BarGeometry.MaxHighlightChars + 1),
                "超过上限后不再着色——粘贴大段文本后打字才不会被冻住");
        }
    }
}
```

（`ShouldHighlight` 的三条原本在 `ReviewFixChecks` 里，这里一并搬过来；`ReviewFixChecks` 里那三条**保留不动**，重复断言不花钱，而移动它们会掩盖「谁负责这条」的归属。）

- [ ] **Step 5: `CaptureBar` 换掉尺寸算法并跟随改名**

先删掉四个常量，换成：

```csharp
        /// <summary>设计基准尺寸（96 DPI 下的逻辑像素）。</summary>
        private const int LogicalWidth = 560;
        private const int LogicalSearchWidth = 600;
        private const int LogicalSearchHeight = 470;

        /// <summary>内边距。卡片四角的圆弧靠它让开，圆角才不会被内容压出直角。</summary>
        private const int LogicalPadX = 16;
        private const int LogicalPadY = 12;

        /// <summary>上下两条辅助信息行的高度。</summary>
        private const int LogicalChromeLine = 24;
```

把 `Padding = new Padding(14, 10, 14, 10);`（第 70 行）删掉——它现在由 `ApplySize` 负责，因为要跟着 DPI 缩放。

把 `ApplySize` 与 `SetLineCount`（第 155-173 行）换成：

```csharp
        /// <summary>
        /// 一行正文的实际高度，物理像素。
        /// <para>
        /// 取 RichEdit 自己的行高（<see cref="Font.Height"/> 就是它排版单倍行距时用的
        /// 行距）再加一点呼吸，而不是写死一个逻辑常量：字体一换、DPI 一变，
        /// 写死的值会让最后一行的下沿被切掉、或者留出一块晾着的空当。
        /// </para>
        /// </summary>
        private int LineHeightPx
        {
            get
            {
                var font = _input.Font;
                if (font == null) return (int)Math.Round(24 * _scale);
                return font.Height + (int)Math.Round(4 * _scale);
            }
        }

        private int ChromeLinePx { get { return (int)Math.Round(LogicalChromeLine * _scale); } }

        private void ApplySize()
        {
            // 内边距与两条辅助行也要跟着 DPI 缩放。只缩窗体宽高而不缩它们的话，
            // 文字按 DPI 放大了、留白却没放大，高 DPI 下整张卡片会显得局促。
            var padX = (int)Math.Round(LogicalPadX * _scale);
            var padY = (int)Math.Round(LogicalPadY * _scale);
            Padding = new Padding(padX, padY, padX, padY);
            _chrome.Top.Height = ChromeLinePx;
            _chrome.Bottom.Height = ChromeLinePx;

            if (_mode == BarMode.Search)
            {
                Width = (int)Math.Round(LogicalSearchWidth * _scale);
                Height = (int)Math.Round(LogicalSearchHeight * _scale);
                return;
            }

            Width = (int)Math.Round(LogicalWidth * _scale);
            // 高度正好等于「上下内边距 + 上下辅助行 + N 行正文」，一点不多。
            Height = 2 * padY + 2 * ChromeLinePx + _lineCount * LineHeightPx;
        }

        /// <summary>按内容行数调整高度。</summary>
        public void SetLineCount(int lineCount)
        {
            _lineCount = BarGeometry.ClampLines(lineCount);
            ApplySize();
        }
```

`TopText()`（第 332-341 行）换成新文案：

```csharp
        public string TopText()
        {
            var today = DateTime.Now;
            var count = 0;
            try { count = _store.CountEntries(today); }
            catch { /* 读不到就显示 0 条，不打扰使用者 */ }

            return DateFormat.ShortHeading(today) + "　·　今天已记 " + count + " 条";
        }
```

其余按名替换（只改名，不改逻辑）：

| 旧 | 新 |
|---|---|
| `palette.BarBack` / `_palette.BarBack` | `.Paper` |
| `palette.Text` / `_palette.Text` | `.Ink` |
| `palette.Small` | `.Chrome` |
| `palette.Muted` | 不变 |
| `palette.Error` | 不变 |
| `palette.Body` | 不变 |

- [ ] **Step 6: 补齐剩下的编译错误**

`grep -n "\.BarBack\|\.Accent\|\.Todo\|\.Border\|\.Small\b\|\.HeadingFont\|\.Text\b" src/QuickNote/*.cs src/QuickNote.Core/*.cs` 应该没有残留（`.Text` 在 `Control.Text` 上是合法的，注意区分）。

```bash
export PATH="$PATH:/c/Program Files/dotnet"
dotnet build QuickNote.sln -c Debug
```

期望：`0 个错误`。

- [ ] **Step 7: 圆角跟着 DPI 一起放大**

`OnHandleCreated` 里现有的 `DWMWCP_ROUND`（第 216 行）是 Windows 11 按**固定的物理半径**画的，125% 下看会比设计稿显得更方。改成按缩放档位选：

```csharp
            try
            {
                // Windows 11 让系统画圆角，比自绘 region 干净得多。
                // 半径是固定的物理像素，所以低缩放下它相对更大——用 ROUNDSMALL
                // 把高分屏那张"方"一点的脸拉回来，两张卡片看起来才是一个圆角。
                var pref = _scale >= 1.5f ? NativeMethods.DWMWCP_ROUNDSMALL : NativeMethods.DWMWCP_ROUND;
                NativeMethods.DwmSetWindowAttribute(Handle, NativeMethods.DWMWA_WINDOW_CORNER_PREFERENCE,
                    ref pref, sizeof(int));
            }
            catch { /* 非 Win11 上忽略 */ }
```

`NativeMethods.cs` 里补一个常量（`DWMWCP_ROUND` 那行下面）：

```csharp
        internal const int DWMWCP_ROUNDSMALL = 3;
```

**圆角不被内容压成直角，靠的是内边距。** `LogicalPadX = 16` / `LogicalPadY = 12` 让所有子控件（正文框、两条辅助行、结果列表）都离卡片边缘 16/12 逻辑像素——而 Win11 的圆角半径只有 8 物理像素，所以四个角始终是干净的。

- [ ] **Step 8: 跑自检，确认仍是绿的**

```bash
dotnet run --project src/QuickNote.SelfCheck -c Debug
```

期望：`全部通过`。`BarGeometry` 段落断言数不变（`HeightFor` 的 7 条换成 `ClampLines` 的 8 条，另加 3 条 `ShouldHighlight`）。

- [ ] **Step 9: 提交**

```bash
git add src/QuickNote/Theme.cs src/QuickNote/BarChrome.cs src/QuickNote/MarkdownHighlighter.cs \
        src/QuickNote/SearchResultsView.cs src/QuickNote/CaptureBar.cs \
        src/QuickNote/NativeMethods.cs src/QuickNote.Core/BarGeometry.cs \
        src/QuickNote.SelfCheck/BarGeometryChecks.cs
git commit -m "feat(ui): 配色换成近乎无彩的便签调，行距按实际字体走，圆角随 DPI"
```

---

### Task 7: `CaretOverlay` —— 自绘光标

**Files:**
- Create: `src/QuickNote/CaretOverlay.cs`
- Modify: `src/QuickNote/NativeMethods.cs`
- Modify: `src/QuickNote/CaptureBar.cs`

**Interfaces:**
- Consumes: `CaretPulse.LevelAt`（Task 4）、`Palette.Paper/Ink`（Task 5）
- Produces:
  - `CaretOverlay.Attach(RichTextBox box, Control host, float scale)`
  - `CaretOverlay.Follow()`
  - `CaretOverlay.Stop()`
  - `CaretOverlay.SetColors(Palette p)`

- [ ] **Step 1: 加 P/Invoke**

`src/QuickNote/NativeMethods.cs`，在 `WM_KILLFOCUS` 常量下面加：

```csharp
        [DllImport("user32.dll")]
        internal static extern bool HideCaret(IntPtr hWnd);
```

- [ ] **Step 2: 写 `CaretOverlay`**

创建 `src/QuickNote/CaretOverlay.cs`：

```csharp
using System;
using System.Drawing;
using System.Windows.Forms;
using QuickNote.Core;

namespace QuickNote
{
    /// <summary>
    /// 自绘光标。RichEdit 自带的是 1px 硬闪的黑条；这里把它 HideCaret 掉，
    /// 自己画一根 2px 的短竖线，颜色用 ink，1 秒一循环地柔和明暗（定案 §5）。
    /// <para>
    /// 做成独立子控件，而不是往 RichEdit 的 DC 上画：后者每次 RichEdit 重绘
    /// （打字、滚动、改选）都会被抹掉，只能靠高频重画去补，看起来就是闪。
    /// 子控件是独立的 HWND，RichEdit 永远碰不到它。
    /// </para>
    /// <para>
    /// 明暗用改背景色实现，不用透明度：小条背景是纯色，线性插出来的中间色
    /// 与真正的 alpha 混合结果完全一致，而子控件就不必去碰分层窗口。
    /// </para>
    /// <para>
    /// 计时器只在输入框拿到焦点时跑。小条收起时它停掉——待机没有任何计时器。
    /// </para>
    /// </summary>
    internal sealed class CaretOverlay : Control
    {
        private const int StepMs = 40;
        private const int LogicalWidth = 2;

        private readonly Timer _timer;
        private RichTextBox _box;
        private Control _host;
        private float _scale = 1f;
        private Color _paper = Color.White;
        private Color _ink = Color.Black;
        private int _elapsed;

        public CaretOverlay()
        {
            SetStyle(ControlStyles.Selectable, false);   // 绝不抢焦点
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer, true);
            TabStop = false;
            Visible = false;

            _timer = new Timer { Interval = StepMs };
            _timer.Tick += delegate
            {
                _elapsed += StepMs;
                Follow();
            };
        }

        public void Attach(RichTextBox box, Control host, float scale)
        {
            _box = box;
            _host = host;
            _scale = scale;
            Follow();
        }

        public void SetColors(Palette p)
        {
            _paper = p.Paper;
            _ink = p.Ink;
            Follow();
        }

        /// <summary>把光标贴到插入点。输入框没有焦点、或小条已收起时就藏起来并停表。</summary>
        public void Follow()
        {
            if (_box == null || _host == null || !_box.IsHandleCreated || !_box.Visible ||
                !_host.Visible || !_box.Focused)
            {
                Stop();
                return;
            }

            // 系统光标必须反复藏。RichEdit 在按键、滚动、改选之后会自己把它显示回来，
            // 藏一次是不够的——每个节拍补一次，代价是一次 user32 调用。
            NativeMethods.HideCaret(_box.Handle);

            var at = _box.GetPositionFromCharIndex(_box.SelectionStart);
            Location = _host.PointToClient(_box.PointToScreen(at));
            Width = Math.Max(1, (int)Math.Round(LogicalWidth * _scale));
            Height = _box.Font.Height;

            if (!Visible)
            {
                Visible = true;
                _elapsed = 0;   // 光标刚跳过去时正好是全暗的那一瞬会显得它没出现
            }
            Apply();
            if (!_timer.Enabled) _timer.Start();
        }

        public void Stop()
        {
            _timer.Stop();
            if (Visible) Visible = false;
        }

        private void Apply()
        {
            BackColor = Blend(_paper, _ink, CaretPulse.LevelAt(_elapsed));
        }

        private static Color Blend(Color paper, Color ink, double level)
        {
            return Color.FromArgb(
                (int)Math.Round(paper.R + (ink.R - paper.R) * level),
                (int)Math.Round(paper.G + (ink.G - paper.G) * level),
                (int)Math.Round(paper.B + (ink.B - paper.B) * level));
        }
    }
}
```

- [ ] **Step 3: 在 `CaptureBar` 里挂上**

构造函数里，`_content.Controls.Add(_results);`（第 79 行）之后加：

```csharp
            _caret = new CaretOverlay { Parent = _content };
            _caret.BringToFront();   // Add 会把新控件放到 z 序最后，也就是最底下
```

字段声明（第 25 行 `private readonly Panel _content;` 之后）：

```csharp
        private readonly CaretOverlay _caret;
```

`BuildInput` 里给输入框挂焦点事件（在第 106 行的 `return box;` 之前）：

```csharp
            // 光标只在有焦点时存在：失焦立刻停表，待机没有任何计时器在跑。
            box.GotFocus += delegate { _caret.Attach(box, _content, _scale); };
            box.LostFocus += delegate { _caret.Stop(); };
```

`OnHandleCreated` 里 `ApplySize();` 之后（第 211 行后）加：

```csharp
            _caret.Attach(_input, _content, _scale);
```

`FocusInput()`（第 500-513 行）里两处 `Focus()` 之后各加一行：

```csharp
            _caret.Follow();
```

`ApplyTheme`（第 432-448 行）里加一行：

```csharp
            _caret.SetColors(palette);
```

`CloseBar()`（第 473-483 行）里 `HideBar();` 之前加：

```csharp
            _caret.Stop();
```

- [ ] **Step 4: 构建并冒烟**

```bash
dotnet build QuickNote.sln -c Debug
```

期望：`0 个错误`。

启动 `bin\QuickNote\Debug\QuickNote.exe`，确认进程活着（托盘图标出现、双击 Ctrl 弹条）、`%APPDATA%\QuickNote\error.log` 没有新增行，然后从托盘退出。

- [ ] **Step 5: 提交**

```bash
git add src/QuickNote/CaretOverlay.cs src/QuickNote/NativeMethods.cs src/QuickNote/CaptureBar.cs
git commit -m "feat(ui): 自绘光标，2px 竖线 1 秒一循环地柔和明暗"
```

---

### Task 8: 填写页的勾选框 —— 打 `- ` 自动补成脸

**Files:**
- Modify: `src/QuickNote/CaptureBar.cs`

**Interfaces:**
- Consumes: `EntryLine.UndoneFace`（Task 1）、`NoteStore.Append`（Task 3）
- Produces: 无新公开成员

- [ ] **Step 1: 加自动补全**

`BuildInput` 里的 `TextChangedByUser` 处理（第 102-106 行）换成：

```csharp
            box.TextChangedByUser += delegate
            {
                ExpandTodoMarker(box);
                MarkdownHighlighter.Apply(box, _palette);
                FitToContent();
                _caret.Follow();
            };
```

并在 `BuildInput` 方法下面加：

```csharp
        /// <summary>
        /// 打进「- 」就自动补成勾选框并画成脸（定案 §10.4）。屏幕上看到的是脸，
        /// 文件里存的是 "- [ ]"——使用者从未真正手打过 "[ ]"，所以不构成困扰。
        /// <para>
        /// 只在行首正好打出这两个字符时触发。行中间打 "- " 是正常内容，不动它。
        /// </para>
        /// </summary>
        private void ExpandTodoMarker(BarTextBox box)
        {
            if (box.SelectionLength != 0) return;

            var caret = box.SelectionStart;
            if (caret < 2) return;

            var lineStart = box.GetFirstCharIndexOfCurrentLine();
            if (caret - lineStart != 2) return;      // 不是刚在行首打完两个字符

            var text = box.Text;
            if (lineStart + 1 >= text.Length) return;
            if (text[lineStart] != '-' || text[lineStart + 1] != ' ') return;

            // 赋值 SelectedText 会再触发一次 TextChangedByUser。回到这里时行首已经
            // 是脸而不是 "- "，上面那道判断自然拦住，不会递归。
            box.Select(lineStart, 2);
            box.SelectedText = EntryLine.UndoneFace + " ";
            box.SelectionStart = lineStart + EntryLine.UndoneFace.Length + 1;
            box.SelectionLength = 0;
        }
```

第 3 行加 `using QuickNote.Core;`（已经有了，确认即可）。

- [ ] **Step 2: 构建并冒烟**

```bash
dotnet build QuickNote.sln -c Debug
```

期望：`0 个错误`。启动程序确认进程活着、`error.log` 没有新增，从托盘退出。

- [ ] **Step 3: 跑自检**

```bash
dotnet run --project src/QuickNote.SelfCheck -c Debug
```

期望：`全部通过`（本任务只动 UI 接线，逻辑已在 Task 1/3 覆盖）。

- [ ] **Step 4: 提交**

```bash
git add src/QuickNote/CaptureBar.cs
git commit -m "feat(ui): 填写页打 - 自动补成勾选框并画成脸"
```

---

### Task 9: 搜索页画完成态的脸（`SearchResultsView`）

**Files:**
- Modify: `src/QuickNote/SearchResultsView.cs`
- Modify: `src/QuickNote.SelfCheck/NoteSearcherChecks.cs`

**Interfaces:**
- Consumes: `EntryLine.TryParse`（Task 1）、`Palette.Emoji/Stamp/StampFont`（Task 5）
- Produces: `SearchResultsView.SetDanger(bool)`（Task 11 用）

- [ ] **Step 1: 加一条断言：新格式的记录不会把下一条当上下文**

`src/QuickNote.SelfCheck/NoteSearcherChecks.cs` 的 `Run()` 末尾加 `NewFormatEntryHasNoContext();`，并加方法：

```csharp
        private static void NewFormatEntryHasNoContext()
        {
            using (var dir = new TempDir())
            {
                File.WriteAllText(Path.Combine(dir.Path, "2026-10-05.md"),
                    "# 2026-10-05 星期一\n\n- [ ] 14:30 跟老王确认了字段\n- [x] 14:32 把 spec 写完\n\n",
                    new UTF8Encoding(false));

                var result = NoteSearcher.Search(dir.Path, "老王", 200, CancellationToken.None);
                Check.Equal(result.Hits.Count, 1, "新格式的记录能被搜到");
                Check.Equal(result.Hits[0].Context, "",
                    "新格式的下一条记录不会被当成上一条的上下文");
            }
        }
```

- [ ] **Step 2: 跑一遍确认是绿的（这是回归护栏，不是新功能）**

```bash
dotnet run --project src/QuickNote.SelfCheck -c Debug
```

期望：`全部通过`。这条断言在 Task 1 就已经成立（`IsEntryStart` 已认新格式），在这里钉住是为了防止后续改动把它弄坏。如果它是红的，说明 Task 1 的实现有误，回去修。

- [ ] **Step 3: 画脸与时间戳**

`src/QuickNote/SearchResultsView.cs`，把 `OnPaint` 里的循环体（第 108-119 行）换成：

```csharp
                var hit = _result.Hits[i];
                DrawStamp(g, hit, y);
                DrawHitLine(g, hit, y);
            }
        }

        /// <summary>左上是日期与星期：日期用第二套字体（Bahnschrift），星期用 chrome 字号。</summary>
        private void DrawStamp(Graphics g, SearchHit hit, int y)
        {
            TextRenderer.DrawText(g, hit.Date.ToString("MM-dd"), _p.StampFont,
                new Point(10, y + 4), _p.Stamp);
            TextRenderer.DrawText(g, hit.Date.ToString("dddd"), _p.Chrome,
                new Point(62, y + 5), _p.Muted);
        }

        /// <summary>
        /// 匹配行：脸 + 时间戳 + 正文。脸画在行首，文件里的 "- [ ]" 不再显示——
        /// 屏幕上看到的就是文件里那条待办的脸（定案 §10.4）。
        /// </summary>
        private void DrawHitLine(Graphics g, SearchHit hit, int y)
        {
            var box = new Rectangle(10, y + 22, Width - 20, 20);

            EntryInfo info;
            if (!EntryLine.TryParse(hit.Line, out info))
            {
                DrawMatchLine(g, hit.Line, box);
                return;
            }

            var faceWidth = TextRenderer.MeasureText(g, info.Face, _p.Emoji).Width;
            TextRenderer.DrawText(g, info.Face, _p.Emoji, new Point(box.X, box.Y), _p.Ink);

            var at = box.X + faceWidth + 6;
            var stampWidth = TextRenderer.MeasureText(g, info.Stamp, _p.StampFont).Width + 8;
            TextRenderer.DrawText(g, info.Stamp, _p.StampFont,
                new Point(at, box.Y + 1), _p.Stamp);

            var rest = new Rectangle(at + stampWidth, box.Y, box.Right - at - stampWidth, box.Height);
            if (rest.Width <= 0) return;
            DrawMatchLine(g, info.Content, rest);
```

注意：`OnPaint` 里原来的 `for` 循环结尾的花括号需要跟着调整——循环体现在只有三行（取 `hit`、`DrawStamp`、`DrawHitLine`）。改完后 `OnPaint` 应该是：

```csharp
        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.Clear(_p.Paper);

            if (_result.Hits.Count == 0)
            {
                TextRenderer.DrawText(g, "没有找到匹配", _p.Chrome,
                    new Point(12, 12), _p.Muted);
                return;
            }

            var lastVisible = Math.Min(_result.Hits.Count, _firstVisible + VisibleRows);
            for (var i = _firstVisible; i < lastVisible; i++)
            {
                var y = (i - _firstVisible) * RowHeight;
                if (i == _selected)
                {
                    using (var b = new SolidBrush(_danger ? Color.FromArgb(46, _p.Error) : _p.Selection))
                        g.FillRectangle(b, 0, y, Width, RowHeight - 2);
                }

                var hit = _result.Hits[i];
                DrawStamp(g, hit, y);
                DrawHitLine(g, hit, y);
            }
        }
```

上下文行（原来第 114-119 行）挪进 `DrawHitLine` 的末尾之后，或在 `OnPaint` 里补回。**保留它**，加在 `DrawHitLine` 之后：

```csharp
                if (!string.IsNullOrEmpty(hit.Context))
                {
                    TextRenderer.DrawText(g, hit.Context, _p.Chrome,
                        new Rectangle(10, y + 44, Width - 20, 18), _p.Muted,
                        TextFormatFlags.EndEllipsis);
                }
```

并把 `RowHeight` 从 62 改成 66（多出来的 4px 给脸那一行留呼吸）。

- [ ] **Step 4: 构建并跑自检**

```bash
dotnet build QuickNote.sln -c Debug
dotnet run --project src/QuickNote.SelfCheck -c Debug
```

期望：`0 个错误`，`全部通过`。

- [ ] **Step 5: 提交**

```bash
git add src/QuickNote/SearchResultsView.cs src/QuickNote.SelfCheck/NoteSearcherChecks.cs
git commit -m "feat(ui): 搜索结果画完成态的脸与 Bahnschrift 时间戳"
```

---

### Task 10: 搜索页空格勾选完成态

**Files:**
- Modify: `src/QuickNote/CaptureBar.cs`
- Modify: `src/QuickNote.Core/InputRules.cs`
- Modify: `src/QuickNote.SelfCheck/InputRulesChecks.cs`

**Interfaces:**
- Consumes: `NoteStore.SetDone`（Task 2）、`SearchResultsView.SelectedHit`
- Produces: `InputRules.DecideSearchKey(string query, bool confirmPending, Keys key) → SearchKeyAction`

- [ ] **Step 1: 写失败的断言**

`src/QuickNote.SelfCheck/InputRulesChecks.cs` 的 `Run()` 末尾加：

```csharp
            Check.Equal(InputRules.DecideSearchKey("", false, Keys.Space), SearchKeyAction.ToggleDone,
                "查询框为空时空格切换完成态");
            Check.Equal(InputRules.DecideSearchKey("老王", false, Keys.Space), SearchKeyAction.PassThrough,
                "查询框有内容时空格是正文里的空格，不能被抢走");
            Check.Equal(InputRules.DecideSearchKey("", false, Keys.Back), SearchKeyAction.AskDelete,
                "查询框为空时 Backspace 进入删除确认态");
            Check.Equal(InputRules.DecideSearchKey("老", false, Keys.Back), SearchKeyAction.PassThrough,
                "查询框有内容时 Backspace 就是删字符");
            Check.Equal(InputRules.DecideSearchKey("", true, Keys.Enter), SearchKeyAction.ConfirmDelete,
                "确认态下 Enter 的语义被覆盖成确认删除");
            Check.Equal(InputRules.DecideSearchKey("", true, Keys.Escape), SearchKeyAction.CancelDelete,
                "确认态下 Esc 取消");
            Check.Equal(InputRules.DecideSearchKey("", true, Keys.Space), SearchKeyAction.PassThrough,
                "确认态下空格不再切换完成态");
            Check.Equal(InputRules.DecideSearchKey("", false, Keys.Enter), SearchKeyAction.PassThrough,
                "非确认态下 Enter 交给上层打开文件");
```

文件顶部加 `using System.Windows.Forms;`（`Keys` 需要），并在 `InputRulesChecks.cs` 里确认 `QuickNote` 项目的引用——**不行，`QuickNote.SelfCheck` 只引用 `QuickNote.Core`**。所以 `Keys` 不能用。

改用 Core 自己的枚举。在 `InputRules.cs` 里加：

```csharp
    /// <summary>搜索页需要判定的按键。用 Core 自己的枚举，不引用 WinForms。</summary>
    public enum SearchKey
    {
        Space,
        Backspace,
        Enter,
        Escape,
        Other
    }

    public enum SearchKeyAction
    {
        PassThrough,      // 交给控件按常规处理
        ToggleDone,
        AskDelete,
        ConfirmDelete,
        CancelDelete
    }
```

断言改成用 `SearchKey`：

```csharp
            Check.Equal(InputRules.DecideSearchKey("", false, SearchKey.Space), SearchKeyAction.ToggleDone,
                "查询框为空时空格切换完成态");
            Check.Equal(InputRules.DecideSearchKey("老王", false, SearchKey.Space), SearchKeyAction.PassThrough,
                "查询框有内容时空格是正文里的空格，不能被抢走");
            Check.Equal(InputRules.DecideSearchKey("", false, SearchKey.Backspace), SearchKeyAction.AskDelete,
                "查询框为空时 Backspace 进入删除确认态");
            Check.Equal(InputRules.DecideSearchKey("老", false, SearchKey.Backspace), SearchKeyAction.PassThrough,
                "查询框有内容时 Backspace 就是删字符");
            Check.Equal(InputRules.DecideSearchKey("", true, SearchKey.Enter), SearchKeyAction.ConfirmDelete,
                "确认态下 Enter 的语义被覆盖成确认删除");
            Check.Equal(InputRules.DecideSearchKey("", true, SearchKey.Escape), SearchKeyAction.CancelDelete,
                "确认态下 Esc 取消");
            Check.Equal(InputRules.DecideSearchKey("", true, SearchKey.Space), SearchKeyAction.PassThrough,
                "确认态下空格不再切换完成态");
            Check.Equal(InputRules.DecideSearchKey("", false, SearchKey.Enter), SearchKeyAction.PassThrough,
                "非确认态下 Enter 交给上层打开文件");
```

- [ ] **Step 2: 跑一遍确认是红的**

```bash
dotnet run --project src/QuickNote.SelfCheck -c Debug
```

期望：编译失败，`CS0117: "InputRules"未包含"DecideSearchKey"的定义"。

- [ ] **Step 3: 实现判定规则**

`src/QuickNote.Core/InputRules.cs` 末尾加：

```csharp
        /// <summary>
        /// 搜索页的按键判定。和 <see cref="DecideTab"/> 是同一类问题：
        /// 空格与 Backspace 都是最常用的编辑键，只有在查询框为空、它们
        /// 本来也无事可做的时候才能挪用（定案 §10.5）。
        /// <para>
        /// 确认态下 Enter 的语义被覆盖成「确认删除」——这是有意的，
        /// 确认态是个模态瞬间，Esc 能退出来。
        /// </para>
        /// </summary>
        public static SearchKeyAction DecideSearchKey(string query, bool confirmPending, SearchKey key)
        {
            var queryIsEmpty = string.IsNullOrEmpty(query) || query.Length == 0;

            if (confirmPending)
            {
                if (key == SearchKey.Enter) return SearchKeyAction.ConfirmDelete;
                if (key == SearchKey.Escape) return SearchKeyAction.CancelDelete;
                return SearchKeyAction.PassThrough;
            }

            if (key == SearchKey.Space) return queryIsEmpty ? SearchKeyAction.ToggleDone : SearchKeyAction.PassThrough;
            if (key == SearchKey.Backspace) return queryIsEmpty ? SearchKeyAction.AskDelete : SearchKeyAction.PassThrough;
            return SearchKeyAction.PassThrough;
        }
```

- [ ] **Step 4: 跑自检，确认变绿**

```bash
dotnet run --project src/QuickNote.SelfCheck -c Debug
```

期望：`全部通过`。

- [ ] **Step 5: 接线到 `CaptureBar`**

**只加空格这一条分支**，`OnSearchKeyDown` 的整体重写（含确认态）留给 Task 11——确认态的成员那时才存在，提前引用会让这一步编不过（见账本 P-2）。

在 `OnSearchKeyDown`（第 359-369 行）的 `if (e.KeyCode == Keys.Down)` 之前插入：

```csharp
            if (InputRules.DecideSearchKey(_queryBox.Text, false, ToSearchKey(e.KeyCode))
                == SearchKeyAction.ToggleDone)
            {
                e.SuppressKeyPress = true;
                ToggleSelected();
                return;
            }
```

并在 `OnSearchKeyDown` 下面加两个方法：

```csharp
        private static SearchKey ToSearchKey(Keys key)
        {
            if (key == Keys.Space) return SearchKey.Space;
            if (key == Keys.Back) return SearchKey.Backspace;
            if (key == Keys.Enter) return SearchKey.Enter;
            if (key == Keys.Escape) return SearchKey.Escape;
            return SearchKey.Other;
        }

        /// <summary>
        /// 切换选中条目的完成状态。写到盘上的就是这一下——屏幕上脸变了，
        /// 文件里对应的 "- [ ]" 也跟着变成 "- [x]"。
        /// </summary>
        private void ToggleSelected()
        {
            var hit = _results.SelectedHit;
            if (hit == null) return;

            EntryInfo info;
            if (!EntryLine.TryParse(hit.Line, out info)) return;

            try
            {
                if (!_store.SetDone(hit.Date, hit.LineNumber, hit.Line, !info.IsDone))
                {
                    // 文件在列表生成之后被改过。不写，重新列一遍让使用者看到真实内容。
                    _searchStatus.Text = "这条记录已经变了，已重新载入";
                    _search.Query(_lastQuery);
                    return;
                }
            }
            catch (NoteStoreException ex)
            {
                QuickNoteApp.Log(ex.Message);
                _searchStatus.Text = "改写失败：" + ex.Message;
                return;
            }

            _search.Query(_lastQuery);
        }
```

`ToSearchKey` 在 Task 11 会再加两个键（`Back`、`Escape` 已在这里给全，Task 11 直接用）。

搜索页底部的键位提示也跟着补上空格：

```csharp
                _chrome.Bottom.Text = "↑↓ 选择　·　空格 完成　·　Enter 打开　·　Tab 返回";
```

- [ ] **Step 6: 构建、跑自检、冒烟**

```bash
dotnet build QuickNote.sln -c Debug
dotnet run --project src/QuickNote.SelfCheck -c Debug
```

期望：`0 个错误`、`全部通过`。启动程序确认进程活着、`error.log` 无新增，托盘退出。

- [ ] **Step 7: 提交**

```bash
git add src/QuickNote.Core/InputRules.cs src/QuickNote.SelfCheck/InputRulesChecks.cs \
        src/QuickNote/CaptureBar.cs
git commit -m "feat: 搜索页空格切换选中条目的完成状态"
```

---

### Task 11: 搜索页 Backspace 删除，带二次确认

**Files:**
- Modify: `src/QuickNote/CaptureBar.cs`

**Interfaces:**
- Consumes: `NoteStore.DeleteEntry`（Task 2）、`SearchResultsView.SetDanger`（Task 9）、`InputRules.DecideSearchKey`（Task 10）
- Produces: `CaptureBar.EnterDeleteConfirm()` / `CancelDeleteConfirm()` / `DoDelete()`

- [ ] **Step 1: 加状态与三个方法**

字段声明（第 37 行 `_lastQuery` 附近）：

```csharp
        /// <summary>删除确认态。Backspace 进入，Enter 真删，Esc 取消。</summary>
        private bool _confirmDelete;
```

在 `ToggleSelected` 下面加：

```csharp
        private void EnterDeleteConfirm()
        {
            if (_results.SelectedHit == null) return;
            _confirmDelete = true;
            _results.SetDanger(true);

            EntryInfo info;
            EntryLine.TryParse(_results.SelectedHit.Line, out info);
            var what = info == null ? _results.SelectedHit.Line : info.Content;
            if (what.Length > 20) what = what.Substring(0, 20) + "…";

            _chrome.Bottom.Text = "删除「" + what + "」？　Enter 确认　·　Esc 取消";
            _chrome.Bottom.ForeColor = _palette.Error;
        }

        private void CancelDeleteConfirm()
        {
            _confirmDelete = false;
            _results.SetDanger(false);
            _chrome.Bottom.ForeColor = _palette.Muted;
            RefreshChrome();
        }

        /// <summary>
        /// 真删。删除要重写当天文件——spec 第 5 节靠「只追加」换来的
        /// 「崩溃不丢前面内容」在这条路径上没有了，所以 NoteStore 那边
        /// 用临时文件 + 原子替换，并在当天首次改写前留一份 .bak。
        /// </summary>
        private void DoDelete()
        {
            var hit = _results.SelectedHit;
            if (hit == null) { CancelDeleteConfirm(); return; }

            try
            {
                if (!_store.DeleteEntry(hit.Date, hit.LineNumber, hit.Line))
                {
                    _searchStatus.Text = "这条记录已经变了，已重新载入";
                }
            }
            catch (NoteStoreException ex)
            {
                QuickNoteApp.Log(ex.Message);
                _searchStatus.Text = "删除失败：" + ex.Message;
            }

            CancelDeleteConfirm();
            _search.Query(_lastQuery);
        }
```

- [ ] **Step 2: `OnSearchKeyDown` 换成完整的派发**

Task 10 只插了空格那一条分支；现在确认态存在了，把方法换成按 `InputRules.DecideSearchKey` 的结果派发（`ToSearchKey` 已在 Task 10 加好，不动）：

```csharp
        private void OnSearchKeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Down) { _results.MoveSelection(1); e.SuppressKeyPress = true; return; }
            if (e.KeyCode == Keys.Up) { _results.MoveSelection(-1); e.SuppressKeyPress = true; return; }

            var action = InputRules.DecideSearchKey(_queryBox.Text, _confirmDelete, ToSearchKey(e.KeyCode));

            if (action == SearchKeyAction.ToggleDone)
            {
                e.SuppressKeyPress = true;
                ToggleSelected();
                return;
            }
            if (action == SearchKeyAction.AskDelete)
            {
                e.SuppressKeyPress = true;
                EnterDeleteConfirm();
                return;
            }
            if (action == SearchKeyAction.ConfirmDelete)
            {
                e.SuppressKeyPress = true;
                DoDelete();
                return;
            }
            if (action == SearchKeyAction.CancelDelete)
            {
                e.SuppressKeyPress = true;
                CancelDeleteConfirm();
                return;
            }

            if (e.KeyCode == Keys.Enter) { _results.ActivateSelected(); e.SuppressKeyPress = true; return; }
            if (e.KeyCode == Keys.Tab || e.KeyCode == Keys.Escape)
            {
                e.SuppressKeyPress = true;
                SwitchTo(BarMode.Record);
            }
        }
```

注意 `Escape` 在非确认态走的是「切回填写」，在确认态走的是「取消」——顺序有讲究：先判 `action`，再判键本身。反过来写的话 Esc 会直接切模式、确认态永远退不出来。

- [ ] **Step 3: 三条退出路径都要收掉确认态**

`RefreshChrome()`（第 343-355 行）里，搜索分支换成：

```csharp
            if (_mode == BarMode.Search)
            {
                _chrome.Top.Text = "搜索条目";
                if (_confirmDelete) return;      // 确认文案由 EnterDeleteConfirm 负责，别覆盖掉
                _chrome.Bottom.Text = "↑↓ 选择　·　空格 完成　·　Backspace 删除　·　Enter 打开　·　Tab 返回";
            }
```

`SwitchTo`（第 386-416 行）开头加：

```csharp
            CancelDeleteConfirm();   // 切模式的瞬间确认态必须失效，否则 Enter 会删掉看不见的东西
```

`CloseBar()`（第 473-483 行）开头加同样的 `CancelDeleteConfirm();`。

`SetStore`（第 419-429 行）里 `RefreshChrome();` 之前加 `CancelDeleteConfirm();`。

- [ ] **Step 4: 构建、跑自检、冒烟**

```bash
dotnet build QuickNote.sln -c Debug
dotnet run --project src/QuickNote.SelfCheck -c Debug
```

期望：`0 个错误`、`全部通过`。

- [ ] **Step 5: 用真文件验一遍删除的落盘效果**

程序从托盘退出后，手工往 `notes\2026-10-05.md` 追加两条旧格式记录，然后启动程序、进搜索页、删掉其中一条，退出程序，检查：

- 文件里少的是那一条，其余内容一字未动
- `notes\2026-10-05.md.bak` 存在，内容是动手之前的样子
- `notes\` 下没有 `.tmp` 残留
- 自检里的删除断言已经覆盖了同样的路径，这一步只是确认真实文件上的表现

- [ ] **Step 6: 提交**

```bash
git add src/QuickNote/CaptureBar.cs
git commit -m "feat: 搜索页 Backspace 删除，二次确认 + .bak + 原子重写"
```

---

### Task 12: 收尾 —— 全量自检、构建、交接说明

**Files:**
- Modify: `.superpowers/sdd/2026-10-05-quicknote/progress.md`
- Modify: `docs/superpowers/specs/2026-10-05-quicknote-redesign.md`（把「仍未验证的」更新成现状）

- [ ] **Step 1: 全量构建两个配置**

让使用者先从托盘退出程序，然后：

```bash
export PATH="$PATH:/c/Program Files/dotnet"
dotnet build QuickNote.sln -c Debug
dotnet build QuickNote.sln -c Release
```

期望：都是 `0 个错误 0 个警告`。

- [ ] **Step 2: 全量自检**

```bash
dotnet run --project src/QuickNote.SelfCheck -c Debug
```

期望：`全部通过`，且条数比开工时的 **131 条**多。把实际条数记进下面的账本。

- [ ] **Step 3: 内存与冷启动没有退化**

启动 Debug 版，等 60 秒，用任务管理器读私有工作集；原来的基线是 21.7MB（预算 35MB）。自绘光标的计时器只在有焦点时跑，待机不该有变化。

从托盘退出。若超过 35MB，排查顺序见 spec §13。

- [ ] **Step 4: 写账本**

在 `.superpowers/sdd/2026-10-05-quicknote/progress.md` 末尾追加一节，逐条记录：

- 每个任务的提交区间与自检结果
- **架构偏移**：`Append` 从「多行只有首行带前缀」改成「一行为一条」（定案 §10.1 授权）
- **有意偏离定案的地方**，目前已知两处，都要写清楚：
  1. `.bak` 是**当天首次**改写前留一份，之后不覆盖——按定案 §10.3 原文实现
  2. 顶部 chrome 是**单条左对齐**文案（`10-05 星期一　·　今天已记 N 条`），不是定案 §4 里那种左右分列的排版。理由：一个 Label 做不到两端对齐，为这点观感加一个 Panel 与两个 Label 不划算
- **明确没做的**：定案 §11「仍未验证的」四条一条都没自动验证过（中文输入法组合态、双击 Ctrl 后直接打字、投影圆角着色、光标是否跳），另外新增两条：脸在 RichEdit 里是否真的渲染成彩色、自绘光标是否闪
- **需要使用者按真键盘确认的清单**（见 Step 5）

- [ ] **Step 5: 把交接说明交给使用者**

汇报时明确列出**只能由真人验证**的条目，一条都不要含糊过去：

1. 双击左 Ctrl 弹出小条，**直接打字不碰鼠标**就能输入
2. **中文输入法组合态按 Enter 只上屏候选词，不保存、不关条**（最关键的一条，失效则中文写作完全不可用）
3. 空框按 Tab 进搜索
4. 填写页打 `- ` 是否当场变成 😶‍🌫️，且 emoji 是**彩色**的
5. 填写页写三行按 Enter，搜索页应看到三条各自带脸
6. 搜索页按**空格**切换选中条目的脸（😶‍🌫️ ↔ 🥳），文件里对应变成 `- [ ]` / `- [x]`
7. 搜索页查询框为空时按 **Backspace** 进入确认态，Enter 真删、Esc 取消；有查询词时 Backspace 必须是正常删字符
8. **自绘光标**：是否跳、是否闪、位置对不对、明暗过渡是否柔和
9. 上下两条 hairline 是否出现，字体是否变成了 Noto Sans SC / Bahnschrift
10. **行距**：最后一行下面不该有多余空当，行与行之间不该挤；打满 8 行时最后一行不被切掉。按 Enter 保存后重开，高度应当一样
11. **圆角**：四个角是圆的，且没被正文框或分隔线压出直角；125% 下圆角大小看着舒服（太方就改用 `DWMWCP_ROUND`）
12. 投影、不进任务栏、失焦隐藏

- [ ] **Step 6: 提交**

```bash
git add .superpowers/sdd/2026-10-05-quicknote/progress.md \
        docs/superpowers/specs/2026-10-05-quicknote-redesign.md
git commit -m "docs: 视觉重做与勾选/删除的执行账本与未验证清单"
```

---

## 自查记录

**Spec 覆盖**（逐节对定案文档）：

| 定案章节 | 落在哪个任务 |
|---|---|
| §2 Token（浅色/深色两套） | Task 5 |
| §2 颜色的使用纪律（标题用字重、列表符不换色、行内代码用 ink-soft） | Task 6 |
| §3 字体（Noto Sans SC + Bahnschrift Light，三档字号） | Task 5、Task 9 |
| §4 布局（左对齐、上下 hairline） | Task 6 |
| §5 光标（HideCaret + 2px 柔和明暗） | Task 4、Task 7 |
| §6 文件格式（`- [ ] HH:mm`） | Task 1、Task 3 |
| §6 读取兼容旧格式 | Task 1、Task 2 |
| §10.1 勾选只在搜索页、每行一条 | Task 3、Task 10 |
| §10.2 删除连续行 | Task 1、Task 2 |
| §10.3 不撤销、二次确认、`.bak`、原子替换 | Task 2、Task 11 |
| §10.4 两个界面都显示脸 | Task 6、Task 8、Task 9 |
| §10.5 键位表 | Task 8、Task 10、Task 11 |

**已知的取舍**（都写进了 Task 12 的账本）：

- 顶部 chrome 用单条左对齐而非左右分列（定案 §4 画的是左右两端对齐）。
- `.bak` 按定案原文只在当天首次改写前留。
- **小条变矮了**：`HeightFor(3, 22, 90) = 156` 里有 30px 是没人要的空当，新算法按「内边距 + 上下辅助行 + 实际行高 × 行数」算，三行时约 133。这是有意的——定案 §1 要的是「掉落下来的便签」，不是一块留白很多的对话框。
- 圆角的半径由系统给（`DWMWCP_ROUND` / `ROUNDSMALL`），不自绘。定案 §6 只要求「圆角」，没规定半径。

**类型一致性**：`EntryLine.Prefix(DateTime, bool)` / `TryParse(string, out EntryInfo)` / `SetDone(string, bool)` / `StripMarker(string, out bool)` / `DeleteCount(IList<string>, int)` 在 Task 1 定义，Task 2/3/8/9/10/11 使用的签名与之一致。`NoteStore.SetDone(DateTime, int, string, bool)` / `DeleteEntry(DateTime, int, string)` 在 Task 2 定义，Task 10/11 一致。`SearchResultsView.SetDanger(bool)` 在 Task 9 定义，Task 11 使用。`InputRules.DecideSearchKey(string, bool, SearchKey)` 与 `SearchKey` / `SearchKeyAction` 在 Task 10 定义并使用。
