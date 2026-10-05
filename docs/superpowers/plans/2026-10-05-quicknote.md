# QuickNote 实现计划

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 在 Windows 11 上做一个开机自启、双击左 Ctrl 唤起悬浮小条、按天追加 markdown 的常驻轻量备忘录，待机内存 ≤ 35MB。

**Architecture:** .NET Framework 4.8 + WinForms。纯逻辑（写入格式、双击状态机、搜索、配置、按键规则）全部放在不引用 WinForms 的 `QuickNote.Core` 类库中，由一个控制台自检程序用普通断言覆盖；WinForms 程序只做界面与系统集成（全局键盘钩子、托盘、悬浮窗口）。这种切分让最微妙的逻辑可以离线测试，而全局键盘钩子的存在使得「UI 线程不卡」变成了硬约束——任何耗时操作都不能跑在 UI 线程上。

**Tech Stack:** C# / .NET Framework 4.8 / WinForms / Win32 P-Invoke（`SetWindowsHookEx`、`RegisterHotKey`、`DwmSetWindowAttribute`）/ MSBuild（经 .NET SDK 8）

**Spec:** `docs/superpowers/specs/2026-10-05-quicknote-design.md`

## Global Constraints

- 目标框架 **net48**，所有项目统一。`LangVersion` 用 `latest`（SDK 自带 Roslyn，不受系统 `csc.exe` 的 C# 5 限制）。
- **运行时零第三方依赖。** 除 .NET Framework 自带程序集外，唯一允许的包是 `Microsoft.NETFramework.ReferenceAssemblies`（微软官方、构建期、`PrivateAssets="all"`），它只为在没有 Visual Studio 的机器上提供 net48 参考程序集，不进产物。
- **不使用任何测试框架。** 自检是一个控制台程序，用普通断言，失败时退出码非 1。
- 笔记文件编码 **UTF-8 无 BOM**，换行 **LF**，写入方式 **追加**，打开模式 **共享读写**。
- 文件名格式 `yyyy-MM-dd.md`；时间戳格式 `HH:mm`（24 小时制、补零）；标题行 `# yyyy-MM-dd dddd`。
- 日期数字部分一律用 `CultureInfo.InvariantCulture` 格式化，避免受系统区域设置影响。
- 笔记目录默认 `%USERPROFILE%\Documents\QuickNote`，可在设置中修改。**本项目的笔记实际存放于 `D:\Project\daliy_log\notes`**，首次运行后在设置里改过去。
- 配置 `%APPDATA%\QuickNote\config.ini`；错误日志 `%APPDATA%\QuickNote\error.log`。
- 全局键盘钩子回调里**只允许**记录时间戳与投递消息，禁止 IO、分配、异常抛出、`Stopwatch` 实例化（用 `Stopwatch.GetTimestamp()` 静态方法）。
- 双击判定默认阈值 **300ms**；缩进宽度 **2 个空格**；搜索结果上限 **200 条**；搜索防抖 **200ms**。
- 只认**左 Ctrl**（`VK_LCONTROL`）。备用组合键默认 `Ctrl+Alt+N`；必须避开 `Ctrl+Space` 与 `Ctrl+Shift`（输入法切换）。

## Review Focus

以下是规格暗示、但没有哪条任务的测试会自然覆盖到的输入与失败模式，最可能在实际使用中咬人。每条都在其所属任务里配了测试。

1. **中文输入法组合状态下按 `Enter`** — 拼音候选框开着时 `Enter` 的语义是「上屏候选词」。若被当作保存处理，使用者用中文写作时每选一次词就会保存并关闭小条一次。期望：组合状态下 `Enter` 被忽略。（Task 4）
2. **粘贴一大段文本到小条** — 期望：小条高度被钳制在上限，内部滚动，而不是无限长高遮住整个屏幕。（Task 8）
3. **全空白内容（空格、制表符、换行）** — 期望：视为空输入，不写文件、不创建文件。（Task 2）
4. **笔记目录路径含中文或不可写** — 本机用户名就是中文（`阿础`），路径含中文是常态；期望正常读写。目录不可写时期望抛出明确的异常让界面提示，而不是静默失败。（Task 2）
5. **空搜索词或纯空白搜索词** — 期望：不扫描、不返回结果，而不是匹配所有行。（Task 6）

---

## 文件结构

```
D:\Project\daliy_log\
├─ Directory.Build.props                     # 统一 net48 / LangVersion / 输出目录 / 参考程序集包
├─ QuickNote.sln
├─ notes\                                    # 笔记（gitignore）
└─ src\
   ├─ QuickNote.Core\                        # 纯逻辑，不引用 WinForms
   │   ├─ QuickNote.Core.csproj
   │   ├─ DateFormat.cs                      # 文件名 / 时间戳 / 标题行格式化
   │   ├─ NoteStore.cs                       # 按天追加写入
   │   ├─ NoteStoreException.cs
   │   ├─ AppConfig.cs                       # config.ini 读写
   │   ├─ InputRules.cs                      # Tab 与 Enter 的判定规则
   │   ├─ BarMode.cs                         # Record | Search 枚举
   │   ├─ BarGeometry.cs                     # 小条高度钳制
   │   ├─ DoubleTapDetector.cs               # 双击 Ctrl 状态机
   │   ├─ KeyAction.cs                       # LeftCtrlDown | LeftCtrlUp | OtherKey
   │   ├─ NoteSearcher.cs                    # 流式全文搜索
   │   └─ SearchResult.cs                    # SearchHit / SearchResult
   ├─ QuickNote\                             # WinForms 界面与系统集成
   │   ├─ QuickNote.csproj
   │   ├─ app.manifest                       # PerMonitorV2 DPI 感知
   │   ├─ Program.cs                         # 入口、单实例、全局异常
   │   ├─ NativeMethods.cs                   # 全部 P/Invoke 声明
   │   ├─ Theme.cs                           # 主题探测与配色表
   │   ├─ KeyboardHook.cs                    # WH_KEYBOARD_LL
   │   ├─ FallbackHotkey.cs                  # RegisterHotKey 与降级
   │   ├─ HotkeyManager.cs                   # 串起钩子与备用键，向上报「唤起了」
   │   ├─ CaptureBar.cs                      # 小条窗口
   │   ├─ BarTextBox.cs                      # RichTextBox 子类：IME 组合跟踪 + 无闪烁重绘
   │   ├─ MarkdownHighlighter.cs             # 语法高亮
   │   ├─ SearchController.cs                # 防抖、后台扫描、结果回 UI 线程
   │   ├─ TrayIcon.cs                        # 托盘与菜单
   │   ├─ AutoStart.cs                       # 注册表自启与路径自查
   │   └─ SettingsDialog.cs                  # 设置对话框
   └─ QuickNote.SelfCheck\                   # 控制台自检
       ├─ QuickNote.SelfCheck.csproj
       ├─ Program.cs
       ├─ Check.cs                           # 断言与报告
       ├─ TempDir.cs                         # 临时目录辅助
       ├─ DateFormatChecks.cs
       ├─ NoteStoreChecks.cs
       ├─ AppConfigChecks.cs
       ├─ InputRulesChecks.cs
       ├─ BarGeometryChecks.cs
       ├─ DoubleTapDetectorChecks.cs
       └─ NoteSearcherChecks.cs
```

构建命令统一为 `dotnet build QuickNote.sln -c Debug`；自检统一为 `dotnet run --project src/QuickNote.SelfCheck -c Debug`。

---

### Task 1: 构建工具链、项目骨架与自检框架

**Files:**
- Create: `Directory.Build.props`
- Create: `QuickNote.sln`
- Create: `src/QuickNote.Core/QuickNote.Core.csproj`
- Create: `src/QuickNote/QuickNote.csproj`, `src/QuickNote/app.manifest`
- Create: `src/QuickNote.SelfCheck/QuickNote.SelfCheck.csproj`
- Create: `src/QuickNote.SelfCheck/Check.cs`, `src/QuickNote.SelfCheck/Program.cs`
- Create: `src/QuickNote.Core/DateFormat.cs`
- Create: `src/QuickNote.SelfCheck/DateFormatChecks.cs`

**Interfaces:**
- Consumes: 无
- Produces: `QuickNote.Core.DateFormat.FileName(DateTime) → string`；`DateFormat.Stamp(DateTime) → string`；`DateFormat.Heading(DateTime) → string`；`DateFormat.Heading(DateTime, CultureInfo) → string`；`QuickNote.SelfCheck.Check.Section(string)` / `Check.That(bool, string)` / `Check.Equal<T>(T, T, string)` / `Check.Report() → int`

- [ ] **Step 1: 安装 .NET SDK 8**

本机当前没有任何构建工具链（无 `dotnet`、无 `MSBuild.exe`、无 Visual Studio）。.NET Framework 4.8 运行时已安装（4.8.09032），所以只缺构建侧。

```powershell
winget install --id Microsoft.DotNet.SDK.8 --accept-package-agreements --accept-source-agreements
```

装完**新开一个终端**（PATH 需要刷新），确认：

```powershell
dotnet --version
```

预期：输出 `8.0.x`。若 winget 安装失败，改从 <https://dotnet.microsoft.com/download/dotnet/8.0> 下载 SDK 8 的 x64 安装包。

- [ ] **Step 2: 创建各项目的公共构建属性**

`Directory.Build.props`：

```xml
<Project>
  <PropertyGroup>
    <TargetFramework>net48</TargetFramework>
    <LangVersion>latest</LangVersion>
    <AppendTargetFrameworkToOutputPath>false</AppendTargetFrameworkToOutputPath>
    <BaseOutputPath>$(MSBuildThisFileDirectory)bin\$(MSBuildProjectName)\</BaseOutputPath>
    <GenerateAssemblyInfo>true</GenerateAssemblyInfo>
    <AutoGenerateBindingRedirects>true</AutoGenerateBindingRedirects>
  </PropertyGroup>
  <ItemGroup>
    <PackageReference Include="Microsoft.NETFramework.ReferenceAssemblies" Version="1.0.3" PrivateAssets="all" />
  </ItemGroup>
</Project>
```

`Microsoft.NETFramework.ReferenceAssemblies` 是**构建期**包：它提供 net48 的参考程序集，让没有 Visual Studio 的机器也能编译。`PrivateAssets="all"` 保证它不进产物、不传递。这是本项目唯一允许的包。

- [ ] **Step 3: 创建三个项目文件**

`src/QuickNote.Core/QuickNote.Core.csproj`：

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <RootNamespace>QuickNote.Core</RootNamespace>
  </PropertyGroup>
</Project>
```

`src/QuickNote.SelfCheck/QuickNote.SelfCheck.csproj`：

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <OutputType>Exe</OutputType>
    <RootNamespace>QuickNote.SelfCheck</RootNamespace>
  </PropertyGroup>
  <ItemGroup>
    <ProjectReference Include="..\QuickNote.Core\QuickNote.Core.csproj" />
  </ItemGroup>
</Project>
```

`src/QuickNote/QuickNote.csproj`：

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <OutputType>WinExe</OutputType>
    <UseWindowsForms>true</UseWindowsForms>
    <RootNamespace>QuickNote</RootNamespace>
    <ApplicationManifest>app.manifest</ApplicationManifest>
  </PropertyGroup>
  <ItemGroup>
    <ProjectReference Include="..\QuickNote.Core\QuickNote.Core.csproj" />
  </ItemGroup>
</Project>
```

`src/QuickNote/app.manifest`（多显示器 DPI 缩放正确所必需）：

```xml
<?xml version="1.0" encoding="utf-8"?>
<assembly manifestVersion="1.0" xmlns="urn:schemas-microsoft-com:asm.v1">
  <assemblyIdentity version="1.0.0.0" name="QuickNote.app"/>
  <application xmlns="urn:schemas-microsoft-com:asm.v3">
    <windowsSettings>
      <dpiAware xmlns="http://schemas.microsoft.com/SMI/2005/WindowsSettings">true/pm</dpiAware>
      <dpiAwareness xmlns="http://schemas.microsoft.com/SMI/2016/WindowsSettings">PerMonitorV2</dpiAwareness>
    </windowsSettings>
  </application>
</assembly>
```

- [ ] **Step 4: 写自检断言工具**

`src/QuickNote.SelfCheck/Check.cs`：

```csharp
using System;
using System.Collections.Generic;

namespace QuickNote.SelfCheck
{
    internal static class Check
    {
        private static readonly List<string> Failures = new List<string>();
        private static string _section = "(未分组)";

        public static void Section(string name)
        {
            _section = name;
            Console.WriteLine();
            Console.WriteLine("== " + name + " ==");
        }

        public static void That(bool condition, string what)
        {
            if (condition)
            {
                Console.WriteLine("  PASS  " + what);
                return;
            }
            Console.WriteLine("  FAIL  " + what);
            Failures.Add(_section + " / " + what);
        }

        public static void Equal<T>(T actual, T expected, string what)
        {
            if (EqualityComparer<T>.Default.Equals(actual, expected))
            {
                Console.WriteLine("  PASS  " + what);
                return;
            }
            Console.WriteLine("  FAIL  " + what);
            Console.WriteLine("        期望: " + Show(expected));
            Console.WriteLine("        实际: " + Show(actual));
            Failures.Add(_section + " / " + what);
        }

        private static string Show(object v)
        {
            if (v == null) return "<null>";
            return "\"" + v.ToString()
                .Replace("\r", "\\r").Replace("\n", "\\n").Replace("\t", "\\t") + "\"";
        }

        public static int Report()
        {
            Console.WriteLine();
            if (Failures.Count == 0)
            {
                Console.WriteLine("全部通过");
                return 0;
            }
            Console.WriteLine(Failures.Count + " 项失败：");
            foreach (var f in Failures) Console.WriteLine("  - " + f);
            return 1;
        }
    }
}
```

- [ ] **Step 5: 写会失败的自检入口**

`src/QuickNote.SelfCheck/Program.cs`：

```csharp
using System;
using System.Text;

namespace QuickNote.SelfCheck
{
    internal static class Program
    {
        private static int Main()
        {
            Console.OutputEncoding = Encoding.UTF8;
            try
            {
                DateFormatChecks.Run();
                // 后续任务在此处依次追加
            }
            catch (Exception ex)
            {
                Console.WriteLine("自检自身崩溃：" + ex);
                return 2;
            }
            return Check.Report();
        }
    }
}
```

`src/QuickNote.SelfCheck/DateFormatChecks.cs`：

```csharp
using System;
using System.Globalization;
using QuickNote.Core;

namespace QuickNote.SelfCheck
{
    internal static class DateFormatChecks
    {
        public static void Run()
        {
            Check.Section("DateFormat");

            var t = new DateTime(2026, 10, 5, 14, 3, 0);

            Check.Equal(DateFormat.FileName(t), "2026-10-05", "文件名用 ISO 日期");
            Check.Equal(DateFormat.Stamp(t), "14:03", "时间戳 24 小时制、分钟补零");

            var zh = CultureInfo.GetCultureInfo("zh-CN");
            Check.Equal(DateFormat.Heading(t, zh), "# 2026-10-05 星期一", "标题行含中文星期");

            Check.Equal(DateFormat.Stamp(new DateTime(2026, 1, 1, 0, 0, 0)), "00:00",
                "零点显示为 00:00 而不是 24:00");
            Check.Equal(DateFormat.FileName(new DateTime(2026, 1, 9, 0, 0, 0)), "2026-01-09",
                "月与日补零");
        }
    }
}
```

- [ ] **Step 6: 创建解决方案并确认编译失败**

```bash
cd /d/Project/daliy_log
dotnet new sln -n QuickNote
dotnet sln add src/QuickNote.Core/QuickNote.Core.csproj src/QuickNote/QuickNote.csproj src/QuickNote.SelfCheck/QuickNote.SelfCheck.csproj
dotnet build QuickNote.sln -c Debug
```

预期：**编译失败**，报 `DateFormat` 不存在（CS0103）。

若报的是「未找到 .NETFramework,Version=v4.8 的参考程序集」，说明 `Directory.Build.props` 里的包没生效，检查文件名是否拼错、位置是否在仓库根目录。

- [ ] **Step 7: 实现 DateFormat**

`src/QuickNote.Core/DateFormat.cs`：

```csharp
using System;
using System.Globalization;

namespace QuickNote.Core
{
    /// <summary>笔记文件与时间戳的格式化。数字部分一律用不变文化，避免受系统区域设置影响。</summary>
    public static class DateFormat
    {
        public const string FileNamePattern = "yyyy-MM-dd";
        public const string StampPattern = "HH:mm";

        public static string FileName(DateTime when)
        {
            return when.ToString(FileNamePattern, CultureInfo.InvariantCulture);
        }

        public static string Stamp(DateTime when)
        {
            return when.ToString(StampPattern, CultureInfo.InvariantCulture);
        }

        public static string Heading(DateTime when)
        {
            return Heading(when, CultureInfo.CurrentCulture);
        }

        /// <summary>文件首行，例如「# 2026-10-05 星期一」。星期名随传入文化。</summary>
        public static string Heading(DateTime when, CultureInfo culture)
        {
            return "# " + FileName(when) + " " + when.ToString("dddd", culture);
        }
    }
}
```

- [ ] **Step 8: 运行自检，确认通过**

```bash
dotnet run --project src/QuickNote.SelfCheck -c Debug
```

预期：`DateFormat` 全部 PASS，最后一行 `全部通过`，退出码 0。

- [ ] **Step 9: 提交**

```bash
git add Directory.Build.props QuickNote.sln src
git commit -m "feat: 项目骨架、自检框架与 DateFormat"
```

---

### Task 2: NoteStore — 按天追加写入

**Files:**
- Create: `src/QuickNote.Core/NoteStoreException.cs`
- Create: `src/QuickNote.Core/NoteStore.cs`
- Create: `src/QuickNote.SelfCheck/TempDir.cs`
- Create: `src/QuickNote.SelfCheck/NoteStoreChecks.cs`
- Modify: `src/QuickNote.SelfCheck/Program.cs`

**Interfaces:**
- Consumes: `DateFormat.FileName` / `Stamp` / `Heading`（Task 1）
- Produces: `NoteStore(string notesDir)`；`NoteStore.NotesDir → string`；`NoteStore.PathFor(DateTime) → string`；`NoteStore.Append(string content, DateTime now) → bool`；`NoteStore.CountEntries(DateTime) → int`；`NoteStoreException`

- [ ] **Step 1: 写会失败的自检**

`src/QuickNote.SelfCheck/TempDir.cs`：

```csharp
using System;
using System.IO;

namespace QuickNote.SelfCheck
{
    /// <summary>一次性的临时目录，Dispose 时递归删除。目录名故意含中文，因为本机用户名就是中文。</summary>
    internal sealed class TempDir : IDisposable
    {
        public string Path { get; private set; }

        public TempDir()
        {
            Path = System.IO.Path.Combine(
                System.IO.Path.GetTempPath(),
                "qnote 自检-" + Guid.NewGuid().ToString("N").Substring(0, 8));
            Directory.CreateDirectory(Path);
        }

        public void Dispose()
        {
            try { if (Directory.Exists(Path)) Directory.Delete(Path, true); }
            catch { /* 自检清理失败不影响结论 */ }
        }
    }
}
```

`src/QuickNote.SelfCheck/NoteStoreChecks.cs`：

```csharp
using System;
using System.IO;
using System.Text;
using QuickNote.Core;

namespace QuickNote.SelfCheck
{
    internal static class NoteStoreChecks
    {
        private static readonly DateTime Day = new DateTime(2026, 10, 5, 14, 30, 0);
        private static readonly DateTime NextDay = new DateTime(2026, 10, 6, 9, 5, 0);

        public static void Run()
        {
            Check.Section("NoteStore");

            NewFileGetsHeading();
            AppendsToExistingFile();
            RollsOverAtMidnight();
            MultiLineOnlyFirstLineGetsStamp();
            BlankContentWritesNothing();
            Utf8WithoutBom();
            PathWithChineseCharacters();
            LockedFileThrows();
        }

        private static void NewFileGetsHeading()
        {
            using (var dir = new TempDir())
            {
                var store = new NoteStore(dir.Path);
                var wrote = store.Append("跟老王确认了接口字段", Day);
                var text = File.ReadAllText(store.PathFor(Day), Encoding.UTF8);

                Check.That(wrote, "首次写入返回 true");
                Check.Equal(text, "# 2026-10-05 星期一\n\n- 14:30 跟老王确认了接口字段\n\n",
                    "新文件含标题行、时间戳前缀与结尾空行");
            }
        }

        private static void AppendsToExistingFile()
        {
            using (var dir = new TempDir())
            {
                var store = new NoteStore(dir.Path);
                store.Append("第一条", Day);
                store.Append("第二条", new DateTime(2026, 10, 5, 14, 32, 0));
                var text = File.ReadAllText(store.PathFor(Day), Encoding.UTF8);

                Check.Equal(text,
                    "# 2026-10-05 星期一\n\n- 14:30 第一条\n\n- 14:32 第二条\n\n",
                    "追加不重复写标题行，两条记录各自带时间戳");
            }
        }

        private static void RollsOverAtMidnight()
        {
            using (var dir = new TempDir())
            {
                var store = new NoteStore(dir.Path);
                store.Append("今天的", Day);
                store.Append("第二天的", NextDay);

                Check.That(File.Exists(store.PathFor(Day)), "前一天文件仍存在");
                Check.Equal(store.PathFor(NextDay),
                    Path.Combine(dir.Path, "2026-10-06.md"), "跨天写入新文件");
                Check.Equal(File.ReadAllText(store.PathFor(NextDay), Encoding.UTF8),
                    "# 2026-10-06 星期二\n\n- 09:05 第二天的\n\n", "新一天的文件有自己的标题行");
            }
        }

        private static void MultiLineOnlyFirstLineGetsStamp()
        {
            using (var dir = new TempDir())
            {
                var store = new NoteStore(dir.Path);
                store.Append("第一行\n## 下午计划\n- [ ] 把 spec 写完", Day);
                var text = File.ReadAllText(store.PathFor(Day), Encoding.UTF8);

                Check.Equal(text,
                    "# 2026-10-05 星期一\n\n- 14:30 第一行\n## 下午计划\n- [ ] 把 spec 写完\n\n",
                    "多行内容仅首行带时间戳，后续行原样不缩进");
            }
        }

        private static void BlankContentWritesNothing()
        {
            using (var dir = new TempDir())
            {
                var store = new NoteStore(dir.Path);

                Check.That(!store.Append("", Day), "空字符串返回 false");
                Check.That(!store.Append("   ", Day), "纯空格返回 false");
                Check.That(!store.Append("\n\n", Day), "纯换行返回 false");
                Check.That(!store.Append(" \t \r\n  ", Day), "空格制表符回车混合返回 false");
                Check.That(!store.Append(null, Day), "null 返回 false");

                Check.That(!Directory.Exists(dir.Path) || Directory.GetFiles(dir.Path).Length == 0,
                    "空白输入不创建任何文件");
            }
        }

        private static void Utf8WithoutBom()
        {
            using (var dir = new TempDir())
            {
                var store = new NoteStore(dir.Path);
                store.Append("中文内容", Day);
                var bytes = File.ReadAllBytes(store.PathFor(Day));

                Check.That(bytes.Length >= 3 &&
                           !(bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF),
                    "文件不以 UTF-8 BOM 开头");
            }
        }

        private static void PathWithChineseCharacters()
        {
            using (var dir = new TempDir())
            {
                var cn = Path.Combine(dir.Path, "我的 笔记");
                var store = new NoteStore(cn);
                store.Append("路径含中文与空格", Day);

                Check.That(File.Exists(Path.Combine(cn, "2026-10-05.md")),
                    "目录名含中文与空格时正常创建并写入");
                Check.Equal(File.ReadAllText(Path.Combine(cn, "2026-10-05.md"), Encoding.UTF8),
                    "# 2026-10-05 星期一\n\n- 14:30 路径含中文与空格\n\n",
                    "中文路径下内容正确");
            }
        }

        private static void LockedFileThrows()
        {
            using (var dir = new TempDir())
            {
                var store = new NoteStore(dir.Path);
                store.Append("先写一条", Day);

                using (var _ = new FileStream(store.PathFor(Day), FileMode.Open,
                                              FileAccess.Read, FileShare.None))
                {
                    var threw = false;
                    try { store.Append("被锁住时写入", Day); }
                    catch (NoteStoreException) { threw = true; }

                    Check.That(threw, "文件被独占锁定时抛 NoteStoreException 而不是静默失败");
                }
            }
        }
    }
}
```

在 `Program.cs` 的 `try` 块中、`DateFormatChecks.Run();` 之后加入：

```csharp
                NoteStoreChecks.Run();
```

- [ ] **Step 2: 运行自检，确认失败**

```bash
dotnet build QuickNote.sln -c Debug
```

预期：编译失败，`NoteStore` / `NoteStoreException` 不存在（CS0246）。

- [ ] **Step 3: 实现 NoteStoreException**

`src/QuickNote.Core/NoteStoreException.cs`：

```csharp
using System;

namespace QuickNote.Core
{
    /// <summary>
    /// 写入笔记失败。抛出这个异常而不是静默失败，是为了让界面能明确提示
    /// 「内容还在，但没写进去」——丢数据比任何崩溃都严重。
    /// </summary>
    public sealed class NoteStoreException : Exception
    {
        public NoteStoreException(string message, Exception inner) : base(message, inner) { }
    }
}
```

- [ ] **Step 4: 实现 NoteStore**

`src/QuickNote.Core/NoteStore.cs`：

```csharp
using System;
using System.IO;
using System.Text;

namespace QuickNote.Core
{
    /// <summary>
    /// 按天把内容追加进 markdown 文件。整个过程是纯追加，写入前不读取文件内容。
    /// </summary>
    public sealed class NoteStore
    {
        public NoteStore(string notesDir)
        {
            if (string.IsNullOrWhiteSpace(notesDir))
                throw new ArgumentException("笔记目录不能为空", "notesDir");
            NotesDir = notesDir;
        }

        public string NotesDir { get; private set; }

        public string PathFor(DateTime when)
        {
            return Path.Combine(NotesDir, DateFormat.FileName(when) + ".md");
        }

        /// <summary>
        /// 追加一笔。内容全为空白时什么都不做（连文件都不创建），返回 false。
        /// </summary>
        public bool Append(string content, DateTime now)
        {
            if (content == null) return false;

            var normalized = content.Replace("\r\n", "\n").Replace('\r', '\n');
            if (normalized.Trim().Length == 0) return false;

            var text = normalized.TrimEnd('\n');
            var lines = text.Split('\n');

            var sb = new StringBuilder();
            var path = PathFor(now);
            var isNew = !File.Exists(path);

            if (isNew)
            {
                sb.Append(DateFormat.Heading(now)).Append('\n').Append('\n');
            }

            // 只有首行带时间戳前缀；后续行原样写入、不缩进，
            // 这样使用者自己写的 ## 标题与列表能正常渲染。
            sb.Append("- ").Append(DateFormat.Stamp(now)).Append(' ').Append(lines[0]).Append('\n');
            for (var i = 1; i < lines.Length; i++)
            {
                sb.Append(lines[i]).Append('\n');
            }
            sb.Append('\n');

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

        /// <summary>今天已有的记录条数，用于小条顶部的「今天已记 N 条」。</summary>
        public int CountEntries(DateTime day)
        {
            var path = PathFor(day);
            if (!File.Exists(path)) return 0;

            var count = 0;
            try
            {
                using (var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                using (var r = new StreamReader(fs, Encoding.UTF8))
                {
                    string line;
                    while ((line = r.ReadLine()) != null)
                    {
                        if (line.StartsWith("- ", StringComparison.Ordinal)) count++;
                    }
                }
            }
            catch (Exception ex)
            {
                throw new NoteStoreException("读取 " + path + " 失败：" + ex.Message, ex);
            }
            return count;
        }
    }
}
```

- [ ] **Step 5: 运行自检，确认全部通过**

```bash
dotnet run --project src/QuickNote.SelfCheck -c Debug
```

预期：`DateFormat` 与 `NoteStore` 全部 PASS，退出码 0。

- [ ] **Step 6: 提交**

```bash
git add src/QuickNote.Core/NoteStore.cs src/QuickNote.Core/NoteStoreException.cs src/QuickNote.SelfCheck
git commit -m "feat(core): NoteStore 按天追加写入"
```

---

### Task 3: AppConfig — 配置读写

**Files:**
- Create: `src/QuickNote.Core/AppConfig.cs`
- Create: `src/QuickNote.SelfCheck/AppConfigChecks.cs`
- Modify: `src/QuickNote.SelfCheck/Program.cs`

**Interfaces:**
- Consumes: 无
- Produces: `AppConfig` 的可读写字段 `NotesDir` / `HotkeyMode` / `DoubleTapMs` / `FallbackHotkey` / `Theme` / `AutoStart`；`AppConfig.Load(string path) → AppConfig`；`AppConfig.Save(string path)`；`AppConfig.DefaultNotesDir() → string`；`AppConfig.DefaultConfigPath() → string`；`AppConfig.AppDataDir() → string`

- [ ] **Step 1: 写会失败的自检**

`src/QuickNote.SelfCheck/AppConfigChecks.cs`：

```csharp
using System;
using System.IO;
using System.Text;
using QuickNote.Core;

namespace QuickNote.SelfCheck
{
    internal static class AppConfigChecks
    {
        public static void Run()
        {
            Check.Section("AppConfig");
            RoundTrip();
            MissingFileGivesDefaults();
            CorruptFileIsBackedUp();
            BadNumbersFallBackToDefault();
        }

        private static void RoundTrip()
        {
            using (var dir = new TempDir())
            {
                var path = Path.Combine(dir.Path, "config.ini");
                var c = new AppConfig();
                c.NotesDir = @"D:\Project\daliy_log\notes";
                c.HotkeyMode = "Combo";
                c.DoubleTapMs = 250;
                c.FallbackHotkey = "Ctrl+Alt+J";
                c.Theme = "Dark";
                c.AutoStart = false;
                c.Save(path);

                var back = AppConfig.Load(path);

                Check.Equal(back.NotesDir, @"D:\Project\daliy_log\notes", "NotesDir 往返一致");
                Check.Equal(back.HotkeyMode, "Combo", "HotkeyMode 往返一致");
                Check.Equal(back.DoubleTapMs, 250, "DoubleTapMs 往返一致");
                Check.Equal(back.FallbackHotkey, "Ctrl+Alt+J", "FallbackHotkey 往返一致");
                Check.Equal(back.Theme, "Dark", "Theme 往返一致");
                Check.Equal(back.AutoStart, false, "AutoStart 往返一致");
            }
        }

        private static void MissingFileGivesDefaults()
        {
            using (var dir = new TempDir())
            {
                var c = AppConfig.Load(Path.Combine(dir.Path, "不存在.ini"));

                Check.Equal(c.DoubleTapMs, 300, "文件不存在时用默认阈值 300ms");
                Check.Equal(c.HotkeyMode, "DoubleTapCtrl", "文件不存在时默认双击左 Ctrl");
                Check.Equal(c.FallbackHotkey, "Ctrl+Alt+N", "文件不存在时默认备用键");
                Check.Equal(c.Theme, "System", "文件不存在时默认跟随系统");
                Check.Equal(c.AutoStart, true, "文件不存在时默认开启自启");
                Check.That(c.NotesDir.Length > 0, "文件不存在时有默认笔记目录");
            }
        }

        private static void CorruptFileIsBackedUp()
        {
            using (var dir = new TempDir())
            {
                var path = Path.Combine(dir.Path, "config.ini");
                File.WriteAllText(path, "这一行根本不是配置\n还有这一行", Encoding.UTF8);

                var c = AppConfig.Load(path);

                Check.That(File.Exists(path + ".bad"), "损坏的配置被备份为 config.ini.bad");
                Check.Equal(c.DoubleTapMs, 300, "损坏后回退默认值");
            }
        }

        private static void BadNumbersFallBackToDefault()
        {
            using (var dir = new TempDir())
            {
                var path = Path.Combine(dir.Path, "config.ini");
                File.WriteAllText(path, "DoubleTapMs=abc\nTheme=Dark\n", Encoding.UTF8);

                var c = AppConfig.Load(path);

                Check.Equal(c.DoubleTapMs, 300, "非法数字回退默认值");
                Check.Equal(c.Theme, "Dark", "同一文件里合法的项仍然生效");
            }
        }
    }
}
```

在 `Program.cs` 中 `NoteStoreChecks.Run();` 之后加入：

```csharp
                AppConfigChecks.Run();
```

- [ ] **Step 2: 运行自检，确认失败**

```bash
dotnet build QuickNote.sln -c Debug
```

预期：编译失败，`AppConfig` 不存在（CS0246）。

- [ ] **Step 3: 实现 AppConfig**

`src/QuickNote.Core/AppConfig.cs`：

```csharp
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;

namespace QuickNote.Core
{
    /// <summary>
    /// 手写的 key=value 配置读写。不引第三方配置库——四行配置不值得多一个依赖。
    /// </summary>
    public sealed class AppConfig
    {
        public const string ModeDoubleTap = "DoubleTapCtrl";
        public const string ModeCombo = "Combo";

        public string NotesDir { get; set; }
        public string HotkeyMode { get; set; }
        public int DoubleTapMs { get; set; }
        public string FallbackHotkey { get; set; }
        public string Theme { get; set; }
        public bool AutoStart { get; set; }

        public AppConfig()
        {
            NotesDir = DefaultNotesDir();
            HotkeyMode = ModeDoubleTap;
            DoubleTapMs = 300;
            FallbackHotkey = "Ctrl+Alt+N";
            Theme = "System";
            AutoStart = true;
        }

        public static string AppDataDir()
        {
            return Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "QuickNote");
        }

        public static string DefaultConfigPath()
        {
            return Path.Combine(AppDataDir(), "config.ini");
        }

        public static string DefaultNotesDir()
        {
            return Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
                "QuickNote");
        }

        public static AppConfig Load(string path)
        {
            var cfg = new AppConfig();
            if (!File.Exists(path)) return cfg;

            string[] lines;
            try
            {
                lines = File.ReadAllLines(path, Encoding.UTF8);
            }
            catch (Exception)
            {
                BackupBad(path);
                return new AppConfig();
            }

            var parsed = new List<KeyValuePair<string, string>>();
            foreach (var raw in lines)
            {
                var line = raw.Trim();
                if (line.Length == 0 || line.StartsWith("#", StringComparison.Ordinal)) continue;

                var eq = line.IndexOf('=');
                if (eq <= 0)
                {
                    // 不是 key=value —— 整个文件视为损坏，回退默认值。
                    BackupBad(path);
                    return new AppConfig();
                }
                parsed.Add(new KeyValuePair<string, string>(
                    line.Substring(0, eq).Trim(), line.Substring(eq + 1).Trim()));
            }

            foreach (var kv in parsed)
            {
                switch (kv.Key)
                {
                    case "NotesDir":
                        if (kv.Value.Length > 0) cfg.NotesDir = kv.Value;
                        break;
                    case "HotkeyMode":
                        if (kv.Value == ModeDoubleTap || kv.Value == ModeCombo) cfg.HotkeyMode = kv.Value;
                        break;
                    case "DoubleTapMs":
                        {
                            int ms;
                            if (int.TryParse(kv.Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out ms)
                                && ms >= 50 && ms <= 2000)
                            {
                                cfg.DoubleTapMs = ms;
                            }
                            break;
                        }
                    case "FallbackHotkey":
                        if (kv.Value.Length > 0) cfg.FallbackHotkey = kv.Value;
                        break;
                    case "Theme":
                        if (kv.Value == "System" || kv.Value == "Light" || kv.Value == "Dark") cfg.Theme = kv.Value;
                        break;
                    case "AutoStart":
                        {
                            bool b;
                            if (bool.TryParse(kv.Value, out b)) cfg.AutoStart = b;
                            break;
                        }
                }
            }
            return cfg;
        }

        public void Save(string path)
        {
            var dir = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);

            var sb = new StringBuilder();
            sb.Append("# QuickNote 配置，改完重启生效\n");
            sb.Append("NotesDir=").Append(NotesDir).Append('\n');
            sb.Append("HotkeyMode=").Append(HotkeyMode).Append('\n');
            sb.Append("DoubleTapMs=").Append(DoubleTapMs.ToString(CultureInfo.InvariantCulture)).Append('\n');
            sb.Append("FallbackHotkey=").Append(FallbackHotkey).Append('\n');
            sb.Append("Theme=").Append(Theme).Append('\n');
            sb.Append("AutoStart=").Append(AutoStart ? "true" : "false").Append('\n');

            File.WriteAllText(path, sb.ToString(), new UTF8Encoding(false));
        }

        private static void BackupBad(string path)
        {
            try
            {
                if (File.Exists(path)) File.Copy(path, path + ".bad", true);
            }
            catch
            {
                // 备份失败不能妨碍程序带着默认值启动
            }
        }
    }
}
```

- [ ] **Step 4: 运行自检，确认全部通过**

```bash
dotnet run --project src/QuickNote.SelfCheck -c Debug
```

预期：三个分组全部 PASS，退出码 0。

- [ ] **Step 5: 提交**

```bash
git add src/QuickNote.Core/AppConfig.cs src/QuickNote.SelfCheck
git commit -m "feat(core): AppConfig 手写 key=value 配置读写"
```

---

### Task 4: InputRules 与 BarMode — Tab 与 Enter 的判定规则

**Files:**
- Create: `src/QuickNote.Core/BarMode.cs`
- Create: `src/QuickNote.Core/InputRules.cs`
- Create: `src/QuickNote.SelfCheck/InputRulesChecks.cs`
- Modify: `src/QuickNote.SelfCheck/Program.cs`

**Interfaces:**
- Consumes: 无
- Produces: `BarMode` 枚举（`Record` / `Search`）；`TabAction` 枚举（`SwitchToSearch` / `SwitchToRecord` / `Indent`）；`EnterAction` 枚举（`Save` / `Newline` / `Ignore`）；`InputRules.IndentWidth`（const int = 2）；`InputRules.DecideTab(BarMode, bool inputIsEmpty) → TabAction`；`InputRules.DecideEnter(bool isComposing, bool shiftHeld) → EnterAction`

本任务是 Review Focus 第 1 条（中文输入法组合态按 `Enter`）的落点。把 IME 组合状态做成一个**入参**而不是去读控件的实际状态，这条最难验证的路径就变成了一个可以离线断言的纯函数。

- [ ] **Step 1: 写会失败的自检**

`src/QuickNote.SelfCheck/InputRulesChecks.cs`：

```csharp
using QuickNote.Core;

namespace QuickNote.SelfCheck
{
    internal static class InputRulesChecks
    {
        public static void Run()
        {
            Check.Section("InputRules");

            Check.Equal(InputRules.DecideTab(BarMode.Record, true), TabAction.SwitchToSearch,
                "记录模式 + 输入框为空 + Tab → 切到搜索");
            Check.Equal(InputRules.DecideTab(BarMode.Record, false), TabAction.Indent,
                "记录模式 + 有内容 + Tab → 缩进");
            Check.Equal(InputRules.DecideTab(BarMode.Search, true), TabAction.SwitchToRecord,
                "搜索模式 + 空查询 + Tab → 切回记录");
            Check.Equal(InputRules.DecideTab(BarMode.Search, false), TabAction.SwitchToRecord,
                "搜索模式 + 有查询 + Tab → 仍切回记录");

            Check.Equal(InputRules.IndentWidth, 2, "缩进宽度为 2 个空格");

            Check.Equal(InputRules.DecideEnter(false, false), EnterAction.Save,
                "普通 Enter → 保存");
            Check.Equal(InputRules.DecideEnter(false, true), EnterAction.Newline,
                "Shift+Enter → 换行");
            Check.Equal(InputRules.DecideEnter(true, false), EnterAction.Ignore,
                "中文输入法组合态按 Enter → 忽略，不保存不关闭");
            Check.Equal(InputRules.DecideEnter(true, true), EnterAction.Ignore,
                "中文输入法组合态按 Shift+Enter → 也忽略，交给输入法处理");
        }
    }
}
```

在 `Program.cs` 中 `AppConfigChecks.Run();` 之后加入：

```csharp
                InputRulesChecks.Run();
```

- [ ] **Step 2: 运行自检，确认失败**

```bash
dotnet build QuickNote.sln -c Debug
```

预期：编译失败，`BarMode` / `InputRules` / `TabAction` / `EnterAction` 不存在（CS0246）。

- [ ] **Step 3: 实现**

`src/QuickNote.Core/BarMode.cs`：

```csharp
namespace QuickNote.Core
{
    /// <summary>小条的两种模式。</summary>
    public enum BarMode
    {
        Record,
        Search
    }
}
```

`src/QuickNote.Core/InputRules.cs`：

```csharp
namespace QuickNote.Core
{
    public enum TabAction
    {
        SwitchToSearch,
        SwitchToRecord,
        Indent
    }

    public enum EnterAction
    {
        Save,
        Newline,
        Ignore
    }

    /// <summary>
    /// 按键判定规则。故意做成纯函数：这几个分支是最容易出错、
    /// 又最难靠手动敲键盘验证的地方（尤其是输入法组合态）。
    /// </summary>
    public static class InputRules
    {
        public const int IndentWidth = 2;

        /// <summary>
        /// Tab 在文本框里本来是缩进键，直接占用会与写嵌套列表冲突。
        /// 所以记录模式下仅在输入框为空时切换模式——而按热键弹出小条时
        /// 输入框必然是空的，那正是唯一会想到「我要搜个东西」的时刻。
        /// </summary>
        public static TabAction DecideTab(BarMode mode, bool inputIsEmpty)
        {
            if (mode == BarMode.Search) return TabAction.SwitchToRecord;
            return inputIsEmpty ? TabAction.SwitchToSearch : TabAction.Indent;
        }

        /// <summary>
        /// 拼音候选框开着时 Enter 的语义是「上屏候选词」而非保存。
        /// 若被当作保存处理，使用者每选一次词就会保存并关闭小条一次，软件完全不可用。
        /// 正常情况下输入法会在组合状态吞掉按键、程序收不到；
        /// 这里再做一道防线，只认「非组合态」的 Enter。
        /// </summary>
        public static EnterAction DecideEnter(bool isComposing, bool shiftHeld)
        {
            if (isComposing) return EnterAction.Ignore;
            return shiftHeld ? EnterAction.Newline : EnterAction.Save;
        }
    }
}
```

- [ ] **Step 4: 运行自检，确认全部通过**

```bash
dotnet run --project src/QuickNote.SelfCheck -c Debug
```

预期：四个分组全部 PASS，退出码 0。

- [ ] **Step 5: 提交**

```bash
git add src/QuickNote.Core/BarMode.cs src/QuickNote.Core/InputRules.cs src/QuickNote.SelfCheck
git commit -m "feat(core): InputRules 的 Tab 与 Enter 判定规则"
```

---

### Task 5: DoubleTapDetector — 双击左 Ctrl 状态机

**Files:**
- Create: `src/QuickNote.Core/KeyAction.cs`
- Create: `src/QuickNote.Core/DoubleTapDetector.cs`
- Create: `src/QuickNote.SelfCheck/DoubleTapDetectorChecks.cs`
- Modify: `src/QuickNote.SelfCheck/Program.cs`

**Interfaces:**
- Consumes: 无
- Produces: `KeyAction` 枚举（`LeftCtrlDown` / `LeftCtrlUp` / `OtherKey`）；`DoubleTapDetector(int thresholdMs = 300)`；`DoubleTapDetector.KeyEvent(KeyAction action, DateTime now) → bool triggered`；`DoubleTapDetector.ThresholdMs → int`

- [ ] **Step 1: 写会失败的自检**

`src/QuickNote.SelfCheck/DoubleTapDetectorChecks.cs`：

```csharp
using System;
using QuickNote.Core;

namespace QuickNote.SelfCheck
{
    internal static class DoubleTapDetectorChecks
    {
        private static readonly DateTime T0 = new DateTime(2026, 10, 5, 14, 0, 0, 0);

        public static void Run()
        {
            Check.Section("DoubleTapDetector");
            TriggersWithinThreshold();
            IgnoresBeyondThreshold();
            CtrlCDoesNotTrigger();
            HoldingCtrlDoesNotTrigger();
            OtherKeyBetweenTapsDoesNotTrigger();
            FirstTapAloneDoesNotTrigger();
            TapAfterTriggerStartsFresh();
        }

        private static DoubleTapDetector New() { return new DoubleTapDetector(300); }

        private static void TriggersWithinThreshold()
        {
            var d = New();
            Check.That(!d.KeyEvent(KeyAction.LeftCtrlDown, T0), "第一次按下不触发");
            Check.That(!d.KeyEvent(KeyAction.LeftCtrlUp, T0.AddMilliseconds(50)), "第一次抬起不触发");
            Check.That(!d.KeyEvent(KeyAction.LeftCtrlDown, T0.AddMilliseconds(150)), "第二次按下不触发");
            Check.That(d.KeyEvent(KeyAction.LeftCtrlUp, T0.AddMilliseconds(180)),
                "两次轻点间隔 130ms（< 300ms）→ 第二次抬起时触发");
        }

        private static void IgnoresBeyondThreshold()
        {
            var d = New();
            d.KeyEvent(KeyAction.LeftCtrlDown, T0);
            d.KeyEvent(KeyAction.LeftCtrlUp, T0.AddMilliseconds(50));
            d.KeyEvent(KeyAction.LeftCtrlDown, T0.AddMilliseconds(600));
            Check.That(!d.KeyEvent(KeyAction.LeftCtrlUp, T0.AddMilliseconds(650)),
                "两次轻点间隔 600ms（> 300ms）→ 不触发");
        }

        private static void CtrlCDoesNotTrigger()
        {
            var d = New();
            d.KeyEvent(KeyAction.LeftCtrlDown, T0);
            d.KeyEvent(KeyAction.OtherKey, T0.AddMilliseconds(30));      // 期间按了 C
            d.KeyEvent(KeyAction.LeftCtrlUp, T0.AddMilliseconds(60));

            d.KeyEvent(KeyAction.LeftCtrlDown, T0.AddMilliseconds(120));
            d.KeyEvent(KeyAction.OtherKey, T0.AddMilliseconds(140));      // 又一次 Ctrl+C
            Check.That(!d.KeyEvent(KeyAction.LeftCtrlUp, T0.AddMilliseconds(170)),
                "两次 Ctrl+C 不触发（Ctrl 抬起前夹了其他键）");
        }

        private static void HoldingCtrlDoesNotTrigger()
        {
            var d = New();
            Check.That(!d.KeyEvent(KeyAction.LeftCtrlDown, T0), "长按初期不触发");
            Check.That(!d.KeyEvent(KeyAction.LeftCtrlDown, T0.AddMilliseconds(40)),
                "长按产生的重复 KeyDown 被忽略");
            Check.That(!d.KeyEvent(KeyAction.LeftCtrlDown, T0.AddMilliseconds(80)),
                "继续重复 KeyDown 仍被忽略");
            Check.That(!d.KeyEvent(KeyAction.LeftCtrlUp, T0.AddMilliseconds(1500)),
                "长按 1.5 秒后抬起不触发");
        }

        private static void OtherKeyBetweenTapsDoesNotTrigger()
        {
            var d = New();
            d.KeyEvent(KeyAction.LeftCtrlDown, T0);
            d.KeyEvent(KeyAction.LeftCtrlUp, T0.AddMilliseconds(50));
            d.KeyEvent(KeyAction.OtherKey, T0.AddMilliseconds(100));      // 中间敲了别的键
            d.KeyEvent(KeyAction.LeftCtrlDown, T0.AddMilliseconds(150));
            Check.That(!d.KeyEvent(KeyAction.LeftCtrlUp, T0.AddMilliseconds(180)),
                "两次轻点之间夹了其他键 → 不触发");
        }

        private static void FirstTapAloneDoesNotTrigger()
        {
            var d = New();
            d.KeyEvent(KeyAction.LeftCtrlDown, T0);
            Check.That(!d.KeyEvent(KeyAction.LeftCtrlUp, T0.AddMilliseconds(50)),
                "只轻点一次 Ctrl 不触发");
        }

        private static void TapAfterTriggerStartsFresh()
        {
            var d = New();
            d.KeyEvent(KeyAction.LeftCtrlDown, T0);
            d.KeyEvent(KeyAction.LeftCtrlUp, T0.AddMilliseconds(50));
            d.KeyEvent(KeyAction.LeftCtrlDown, T0.AddMilliseconds(150));
            Check.That(d.KeyEvent(KeyAction.LeftCtrlUp, T0.AddMilliseconds(180)), "先触发一次");

            // 紧接着第三次轻点不应立刻再触发一次——触发后状态必须清空
            d.KeyEvent(KeyAction.LeftCtrlDown, T0.AddMilliseconds(200));
            Check.That(!d.KeyEvent(KeyAction.LeftCtrlUp, T0.AddMilliseconds(230)),
                "触发后紧接着的第三次轻点不重复触发");
        }
    }
}
```

在 `Program.cs` 中 `InputRulesChecks.Run();` 之后加入：

```csharp
                DoubleTapDetectorChecks.Run();
```

- [ ] **Step 2: 运行自检，确认失败**

```bash
dotnet build QuickNote.sln -c Debug
```

预期：编译失败，`KeyAction` / `DoubleTapDetector` 不存在（CS0246）。

- [ ] **Step 3: 实现**

`src/QuickNote.Core/KeyAction.cs`：

```csharp
namespace QuickNote.Core
{
    /// <summary>
    /// 状态机的输入。注意 <see cref="OtherKey"/> 覆盖两种情况：
    /// 按下任何非左 Ctrl 的键，以及按下右 Ctrl——右 Ctrl 不参与双击检测，
    /// 但它的出现会打断一次待定的轻点序列。
    /// </summary>
    public enum KeyAction
    {
        LeftCtrlDown,
        LeftCtrlUp,
        OtherKey
    }
}
```

`src/QuickNote.Core/DoubleTapDetector.cs`：

```csharp
using System;

namespace QuickNote.Core
{
    /// <summary>
    /// 双击左 Ctrl 的判定。纯状态机：调用方喂它按键事件与时间戳，
    /// 它回答是否触发。完全不知道键盘钩子的存在，因此可以离线测试。
    /// </summary>
    public sealed class DoubleTapDetector
    {
        private readonly int _thresholdMs;
        private bool _ctrlDown;
        private bool _cleanTap;
        private DateTime? _lastTapAt;

        public DoubleTapDetector(int thresholdMs)
        {
            if (thresholdMs <= 0) throw new ArgumentOutOfRangeException("thresholdMs");
            _thresholdMs = thresholdMs;
        }

        public int ThresholdMs { get { return _thresholdMs; } }

        public bool KeyEvent(KeyAction action, DateTime now)
        {
            switch (action)
            {
                case KeyAction.LeftCtrlDown:
                    if (_ctrlDown) return false;   // 长按产生的重复 KeyDown，忽略
                    _ctrlDown = true;
                    _cleanTap = true;
                    return false;

                case KeyAction.LeftCtrlUp:
                    if (!_ctrlDown) return false;
                    _ctrlDown = false;
                    if (!_cleanTap) return false;  // Ctrl+C 之类，不是轻点

                    if (_lastTapAt.HasValue &&
                        (now - _lastTapAt.Value).TotalMilliseconds < _thresholdMs)
                    {
                        _lastTapAt = null;
                        return true;
                    }
                    _lastTapAt = now;
                    return false;

                default:
                    // 任何其他键都会打断待定的轻点序列，否则每个 Ctrl+C
                    // 都会和下一次 Ctrl 轻点凑成一次「双击」。
                    _cleanTap = false;
                    _lastTapAt = null;
                    return false;
            }
        }
    }
}
```

- [ ] **Step 4: 运行自检，确认全部通过**

```bash
dotnet run --project src/QuickNote.SelfCheck -c Debug
```

预期：五个分组全部 PASS，退出码 0。

- [ ] **Step 5: 提交**

```bash
git add src/QuickNote.Core/KeyAction.cs src/QuickNote.Core/DoubleTapDetector.cs src/QuickNote.SelfCheck
git commit -m "feat(core): DoubleTapDetector 双击左 Ctrl 状态机"
```

---

### Task 6: NoteSearcher — 流式全文搜索

**Files:**
- Create: `src/QuickNote.Core/SearchResult.cs`
- Create: `src/QuickNote.Core/NoteSearcher.cs`
- Create: `src/QuickNote.SelfCheck/NoteSearcherChecks.cs`
- Modify: `src/QuickNote.SelfCheck/Program.cs`

**Interfaces:**
- Consumes: `DateFormat.FileName`（Task 1）
- Produces: `SearchHit { string FilePath; DateTime Date; int LineNumber; string Line; string Context; }`；`SearchResult { IList<SearchHit> Hits; bool Truncated; int ScannedFiles; int UnreadableFiles; }`；`SearchResult.Empty`；`NoteSearcher.Search(string notesDir, string query, int limit, CancellationToken ct) → SearchResult`；`NoteSearcher.DefaultLimit`（const int = 200）

- [ ] **Step 1: 写会失败的自检**

`src/QuickNote.SelfCheck/NoteSearcherChecks.cs`：

```csharp
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading;
using QuickNote.Core;

namespace QuickNote.SelfCheck
{
    internal static class NoteSearcherChecks
    {
        public static void Run()
        {
            Check.Section("NoteSearcher");
            EmptyQueryReturnsNothing();
            CaseInsensitiveMatch();
            ChineseSubstringMatch();
            NewestFirst();
            ContextSkipsBlankAndNextEntry();
            TruncatesAtLimit();
            IgnoresNonMarkdownAndSubdirectories();
            UnreadableFileIsSkipped();
            CancellationStopsEarly();
        }

        private static void Write(string dir, string name, params string[] lines)
        {
            File.WriteAllText(Path.Combine(dir, name), string.Join("\n", lines) + "\n",
                new UTF8Encoding(false));
        }

        private static void EmptyQueryReturnsNothing()
        {
            using (var dir = new TempDir())
            {
                Write(dir.Path, "2026-10-05.md", "# 2026-10-05 星期一", "", "- 14:30 老王");
                var store = new NoteStore(dir.Path);

                var a = NoteSearcher.Search(dir.Path, "", NoteSearcher.DefaultLimit, CancellationToken.None);
                var b = NoteSearcher.Search(dir.Path, "   ", NoteSearcher.DefaultLimit, CancellationToken.None);
                var c = NoteSearcher.Search(dir.Path, null, NoteSearcher.DefaultLimit, CancellationToken.None);

                Check.Equal(a.Hits.Count, 0, "空查询不返回结果（而不是匹配所有行）");
                Check.Equal(a.ScannedFiles, 0, "空查询不扫描任何文件");
                Check.Equal(b.Hits.Count, 0, "纯空白查询不返回结果");
                Check.Equal(c.Hits.Count, 0, "null 查询不返回结果");
            }
        }

        private static void CaseInsensitiveMatch()
        {
            using (var dir = new TempDir())
            {
                Write(dir.Path, "2026-10-05.md", "- 14:30 字段名改成 Snake_Case 了");
                var r = NoteSearcher.Search(dir.Path, "snake", NoteSearcher.DefaultLimit, CancellationToken.None);

                Check.Equal(r.Hits.Count, 1, "小写查询命中大写内容");
                Check.Equal(r.Hits[0].Line, "- 14:30 字段名改成 Snake_Case 了", "命中行内容正确");
            }
        }

        private static void ChineseSubstringMatch()
        {
            using (var dir = new TempDir())
            {
                Write(dir.Path, "2026-10-05.md", "- 14:30 跟老王确认了接口字段");
                var r = NoteSearcher.Search(dir.Path, "老王", NoteSearcher.DefaultLimit, CancellationToken.None);

                Check.Equal(r.Hits.Count, 1, "中文子串命中");
            }
        }

        private static void NewestFirst()
        {
            using (var dir = new TempDir())
            {
                Write(dir.Path, "2026-08-01.md", "- 09:00 老王今天来");
                Write(dir.Path, "2026-10-05.md", "- 09:00 老王今天来");
                Write(dir.Path, "2026-09-10.md", "- 09:00 老王今天来");

                var r = NoteSearcher.Search(dir.Path, "老王", NoteSearcher.DefaultLimit, CancellationToken.None);

                Check.Equal(r.Hits.Count, 3, "三个文件都命中");
                Check.Equal(r.Hits[0].Date, new DateTime(2026, 10, 5), "最近的在最前");
                Check.Equal(r.Hits[1].Date, new DateTime(2026, 9, 10), "其次");
                Check.Equal(r.Hits[2].Date, new DateTime(2026, 8, 1), "最旧的在最后");
            }
        }

        private static void ContextSkipsBlankAndNextEntry()
        {
            using (var dir = new TempDir())
            {
                Write(dir.Path, "2026-10-05.md",
                    "# 2026-10-05 星期一",
                    "",
                    "- 14:30 跟老王确认了接口字段",
                    "他那边说字段名要改成 snake_case",
                    "",
                    "- 14:32 老王说要推迟",
                    "",
                    "- 14:40 结尾这条没有下文");

                var r = NoteSearcher.Search(dir.Path, "老王", NoteSearcher.DefaultLimit, CancellationToken.None);

                Check.Equal(r.Hits.Count, 3, "命中三条");
                Check.Equal(r.Hits[0].Context, "他那边说字段名要改成 snake_case",
                    "多行记录：上下文取紧随的续行");
                Check.Equal(r.Hits[1].Context, "",
                    "单行记录：下一非空行是另一条记录的时间戳，不留上下文");
                Check.Equal(r.Hits[2].Context, "", "文件末尾的记录不越界");
            }
        }

        private static void TruncatesAtLimit()
        {
            using (var dir = new TempDir())
            {
                var lines = new List<string>();
                for (var i = 0; i < 20; i++) lines.Add("- 14:30 老王第 " + i + " 次");
                Write(dir.Path, "2026-10-05.md", lines.ToArray());

                var r = NoteSearcher.Search(dir.Path, "老王", 5, CancellationToken.None);

                Check.Equal(r.Hits.Count, 5, "结果被截断到 limit");
                Check.That(r.Truncated, "标记为已截断");
            }
        }

        private static void IgnoresNonMarkdownAndSubdirectories()
        {
            using (var dir = new TempDir())
            {
                Write(dir.Path, "2026-10-05.md", "- 14:30 老王在笔记里");
                Write(dir.Path, "2026-10-05.txt", "- 14:30 老王在文本文件里");
                Write(dir.Path, "随便一个名字.md", "- 14:30 老王在取名不规范的文件里");

                var sub = Path.Combine(dir.Path, "子目录");
                Directory.CreateDirectory(sub);
                Write(sub, "2026-10-04.md", "- 14:30 老王在子目录里");

                var r = NoteSearcher.Search(dir.Path, "老王", NoteSearcher.DefaultLimit, CancellationToken.None);

                Check.Equal(r.Hits.Count, 1, "只扫顶层、只认 yyyy-MM-dd.md");
            }
        }

        private static void UnreadableFileIsSkipped()
        {
            using (var dir = new TempDir())
            {
                Write(dir.Path, "2026-10-04.md", "- 09:00 老王在好文件里");
                Write(dir.Path, "2026-10-05.md", "- 14:30 老王在被锁文件里");

                var locked = Path.Combine(dir.Path, "2026-10-05.md");
                using (var _ = new FileStream(locked, FileMode.Open, FileAccess.Read, FileShare.None))
                {
                    var r = NoteSearcher.Search(dir.Path, "老王", NoteSearcher.DefaultLimit, CancellationToken.None);

                    Check.Equal(r.Hits.Count, 1, "锁住的文件被跳过，其余文件仍正常出结果");
                    Check.Equal(r.UnreadableFiles, 1, "坏文件被计数");
                }
            }
        }

        private static void CancellationStopsEarly()
        {
            using (var dir = new TempDir())
            {
                for (var d = 1; d <= 30; d++)
                {
                    Write(dir.Path, string.Format("2026-09-{0:D2}.md", d), "- 09:00 老王");
                }

                var cts = new CancellationTokenSource();
                cts.Cancel();

                var threw = false;
                try { NoteSearcher.Search(dir.Path, "老王", NoteSearcher.DefaultLimit, cts.Token); }
                catch (OperationCanceledException) { threw = true; }

                Check.That(threw, "已取消的令牌立刻抛出 OperationCanceledException");
            }
        }
    }
}
```

在 `Program.cs` 中 `DoubleTapDetectorChecks.Run();` 之后加入：

```csharp
                NoteSearcherChecks.Run();
```

- [ ] **Step 2: 运行自检，确认失败**

```bash
dotnet build QuickNote.sln -c Debug
```

预期：编译失败，`NoteSearcher` / `SearchResult` / `SearchHit` 不存在（CS0246）。

- [ ] **Step 3: 实现 SearchResult**

`src/QuickNote.Core/SearchResult.cs`：

```csharp
using System;
using System.Collections.Generic;

namespace QuickNote.Core
{
    public sealed class SearchHit
    {
        public string FilePath { get; set; }
        public DateTime Date { get; set; }
        public int LineNumber { get; set; }
        public string Line { get; set; }

        /// <summary>紧随匹配行的续行；若下一非空行是另一条记录（以 "- " 开头）则为空串。</summary>
        public string Context { get; set; }
    }

    public sealed class SearchResult
    {
        public IList<SearchHit> Hits { get; set; }
        public bool Truncated { get; set; }
        public int ScannedFiles { get; set; }
        public int UnreadableFiles { get; set; }

        public SearchResult()
        {
            Hits = new List<SearchHit>();
        }

        public static SearchResult Empty()
        {
            return new SearchResult();
        }
    }
}
```

- [ ] **Step 4: 实现 NoteSearcher**

`src/QuickNote.Core/NoteSearcher.cs`：

```csharp
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using System.Threading;

namespace QuickNote.Core
{
    /// <summary>
    /// 全文搜索。不建索引——日记纯文本、体积小、增长慢，三年约一千个文件、
    /// 合计几 MB，全量扫描即可，而索引会常驻内存、直接违反内存约束。
    ///
    /// 流式读取：任何时刻内存中只有一个文件的内容加结果集。
    /// 调用方必须把它放在后台线程上跑：扫一千个文件要几百毫秒，
    /// 跑在 UI 线程上会连带全局键盘钩子一起卡住全系统键盘。
    /// </summary>
    public static class NoteSearcher
    {
        public const int DefaultLimit = 200;

        public static SearchResult Search(string notesDir, string query, int limit, CancellationToken ct)
        {
            var result = new SearchResult();

            if (string.IsNullOrEmpty(query) || query.Trim().Length == 0) return result;
            if (!Directory.Exists(notesDir)) return result;

            var needle = query.Trim();
            var files = new List<KeyValuePair<DateTime, string>>();

            foreach (var path in Directory.GetFiles(notesDir, "*.md"))
            {
                DateTime date;
                if (TryParseDate(Path.GetFileNameWithoutExtension(path), out date))
                {
                    files.Add(new KeyValuePair<DateTime, string>(date, path));
                }
            }

            // 按日期倒序，最近的在最前
            files.Sort((a, b) => b.Key.CompareTo(a.Key));

            foreach (var entry in files)
            {
                ct.ThrowIfCancellationRequested();
                result.ScannedFiles++;

                List<string> lines;
                try
                {
                    lines = ReadAllLines(entry.Value);
                }
                catch (Exception)
                {
                    result.UnreadableFiles++;
                    continue;
                }

                for (var i = 0; i < lines.Count; i++)
                {
                    if (lines[i].IndexOf(needle, StringComparison.OrdinalIgnoreCase) < 0) continue;

                    if (result.Hits.Count >= limit)
                    {
                        result.Truncated = true;
                        return result;
                    }

                    result.Hits.Add(new SearchHit
                    {
                        FilePath = entry.Value,
                        Date = entry.Key,
                        LineNumber = i + 1,
                        Line = lines[i],
                        Context = ContextFor(lines, i)
                    });
                }
            }

            return result;
        }

        /// <summary>
        /// 取匹配行的续行作为上下文。若下一非空行本身是一条新记录
        /// （以 "- " 开头），说明这条记录只有一行，就没有上下文可给。
        /// </summary>
        private static string ContextFor(List<string> lines, int index)
        {
            for (var j = index + 1; j < lines.Count; j++)
            {
                var candidate = lines[j];
                if (candidate.Trim().Length == 0) continue;
                if (candidate.StartsWith("- ", StringComparison.Ordinal)) return "";
                return candidate;
            }
            return "";
        }

        private static List<string> ReadAllLines(string path)
        {
            var lines = new List<string>();
            using (var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
            using (var reader = new StreamReader(fs, Encoding.UTF8))
            {
                string line;
                while ((line = reader.ReadLine()) != null) lines.Add(line);
            }
            return lines;
        }

        private static bool TryParseDate(string name, out DateTime date)
        {
            return DateTime.TryParseExact(name, DateFormat.FileNamePattern,
                CultureInfo.InvariantCulture, DateTimeStyles.None, out date);
        }
    }
}
```

- [ ] **Step 5: 运行自检，确认全部通过**

```bash
dotnet run --project src/QuickNote.SelfCheck -c Debug
```

预期：六个分组全部 PASS，退出码 0。

- [ ] **Step 6: 提交**

```bash
git add src/QuickNote.Core/SearchResult.cs src/QuickNote.Core/NoteSearcher.cs src/QuickNote.SelfCheck
git commit -m "feat(core): NoteSearcher 流式全文搜索"
```

---

### Task 7: 应用骨架 — 单实例、托盘、全局异常

**Files:**
- Create: `src/QuickNote/NativeMethods.cs`（本任务只放消息相关常量与 `RegisterWindowMessage` / `PostMessage`）
- Create: `src/QuickNote/Theme.cs`
- Create: `src/QuickNote/TrayIcon.cs`
- Create: `src/QuickNote/Program.cs`

**Interfaces:**
- Consumes: `AppConfig`（Task 3）
- Produces: `AppContext` 单例宿主 `QuickNoteApp`（`ApplicationContext` 子类）；`Theme.IsDarkMode() → bool`；`Theme.Palette Theme.Current() → Palette`；`NativeMethods.WM_APP_SHOW` / `WM_APP_EXIT`；`NativeMethods.RegisterWindowMessage(string)`；`NativeMethods.PostMessage(IntPtr, uint, IntPtr, IntPtr)`

- [ ] **Step 1: 写 P/Invoke 声明**

`src/QuickNote/NativeMethods.cs`（本任务先放这些，后续任务继续往同一文件追加）：

```csharp
using System;
using System.Runtime.InteropServices;

namespace QuickNote
{
    internal static class NativeMethods
    {
        /// <summary>自定义消息：另一个实例要求本实例弹出小条。</summary>
        internal const int WM_APP_SHOW = 0x8000 + 1;
        internal const int WM_APP_EXIT = 0x8000 + 2;

        internal const int WM_SETREDRAW = 0x000B;
        internal const int WM_KEYDOWN = 0x0100;
        internal const int WM_KEYUP = 0x0101;
        internal const int WM_SYSKEYDOWN = 0x0104;
        internal const int WM_SYSKEYUP = 0x0105;

        internal const int WM_IME_STARTCOMPOSITION = 0x010D;
        internal const int WM_IME_ENDCOMPOSITION = 0x010E;

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        internal static extern uint RegisterWindowMessage(string lpString);

        [DllImport("user32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool PostMessage(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam);

        [DllImport("user32.dll")]
        internal static extern IntPtr SendMessage(IntPtr hWnd, int msg, IntPtr wParam, IntPtr lParam);

        [DllImport("user32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool SetForegroundWindow(IntPtr hWnd);
    }
}
```

- [ ] **Step 2: 写主题**

`src/QuickNote/Theme.cs`：

```csharp
using System.Drawing;
using System.Drawing.Text;
using Microsoft.Win32;

namespace QuickNote
{
    internal sealed class Palette
    {
        public Color BarBack;
        public Color Border;
        public Color Text;
        public Color Muted;
        public Color Accent;      // 标题、列表符
        public Color Todo;        // - [ ] 待办
        public Color Selection;
        public Color Error;

        public Font Body;
        public Font HeadingFont;
        public Font Mono;
        public Font Small;
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
            var dark = ShouldUseDark(themeSetting);
            return dark ? Dark() : Light();
        }

        private static Palette Dark()
        {
            return new Palette
            {
                BarBack = Color.FromArgb(28, 28, 32),
                Border = Color.FromArgb(38, 255, 255, 255),
                Text = Color.FromArgb(232, 232, 234),
                Muted = Color.FromArgb(135, 135, 138),
                Accent = Color.FromArgb(124, 176, 255),
                Todo = Color.FromArgb(240, 180, 94),
                Selection = Color.FromArgb(60, 110, 168, 254),
                Error = Color.FromArgb(255, 120, 120),
                Body = new Font("Microsoft YaHei UI", 10.5f),
                HeadingFont = new Font("Microsoft YaHei UI", 10.5f, FontStyle.Bold),
                Mono = new Font("Consolas", 10f),
                Small = new Font("Microsoft YaHei UI", 8.5f)
            };
        }

        private static Palette Light()
        {
            return new Palette
            {
                BarBack = Color.FromArgb(250, 250, 252),
                Border = Color.FromArgb(30, 0, 0, 0),
                Text = Color.FromArgb(28, 28, 32),
                Muted = Color.FromArgb(122, 122, 126),
                Accent = Color.FromArgb(28, 96, 200),
                Todo = Color.FromArgb(176, 106, 16),
                Selection = Color.FromArgb(50, 60, 130, 230),
                Error = Color.FromArgb(190, 40, 40),
                Body = new Font("Microsoft YaHei UI", 10.5f),
                HeadingFont = new Font("Microsoft YaHei UI", 10.5f, FontStyle.Bold),
                Mono = new Font("Consolas", 10f),
                Small = new Font("Microsoft YaHei UI", 8.5f)
            };
        }
    }
}
```

- [ ] **Step 3: 写消息窗口**

`src/QuickNote/MessageWindow.cs`：

```csharp
using System;
using System.Windows.Forms;

namespace QuickNote
{
    /// <summary>
    /// 一个不可见的消息窗口。第二个实例启动时会向 HWND_BROADCAST 投递一个
    /// 由 RegisterWindowMessage 得来的自定义消息；已经有实例在跑的话由这里收到，
    /// 转成事件弹小条。没有这个窗口，广播就石沉大海，
    /// 表现是「双击 exe 什么也没发生」。
    /// </summary>
    internal sealed class MessageWindow : NativeWindow, IDisposable
    {
        private readonly uint _message;
        public event Action ShowRequested;

        public MessageWindow(uint message)
        {
            _message = message;
            CreateHandle(new CreateParams());
        }

        protected override void WndProc(ref Message m)
        {
            if ((uint)m.Msg == _message)
            {
                var h = ShowRequested;
                if (h != null) h();
            }
            base.WndProc(ref m);
        }

        public void Dispose()
        {
            DestroyHandle();
        }
    }
}
```

- [ ] **Step 4: 写托盘**

`src/QuickNote/TrayIcon.cs`：

```csharp
using System;
using System.Drawing;
using System.Windows.Forms;

namespace QuickNote
{
    internal sealed class TrayIcon : IDisposable
    {
        private readonly NotifyIcon _icon;
        private readonly ToolStripMenuItem _autoStartItem;

        public event Action ShowRequested;
        public event Action OpenTodayRequested;
        public event Action OpenNotesFolderRequested;
        public event Action SettingsRequested;
        public event Action AutoStartToggled;
        public event Action ExitRequested;

        public TrayIcon(bool autoStartChecked)
        {
            var menu = new ContextMenuStrip();
            menu.Items.Add("记一笔", null, delegate { Raise(ShowRequested); });
            menu.Items.Add("打开今天的日记", null, delegate { Raise(OpenTodayRequested); });
            menu.Items.Add("打开日记目录", null, delegate { Raise(OpenNotesFolderRequested); });
            menu.Items.Add(new ToolStripSeparator());

            _autoStartItem = new ToolStripMenuItem("开机自启") { Checked = autoStartChecked };
            _autoStartItem.Click += delegate { Raise(AutoStartToggled); };
            menu.Items.Add(_autoStartItem);

            menu.Items.Add("设置…", null, delegate { Raise(SettingsRequested); });
            menu.Items.Add(new ToolStripSeparator());
            menu.Items.Add("退出", null, delegate { Raise(ExitRequested); });

            _icon = new NotifyIcon
            {
                Icon = MakeIcon(),
                Text = "QuickNote",
                Visible = true,
                ContextMenuStrip = menu
            };
            _icon.MouseClick += delegate (object s, MouseEventArgs e)
            {
                if (e.Button == MouseButtons.Left) Raise(ShowRequested);
            };
        }

        public void SetAutoStartChecked(bool value) { _autoStartItem.Checked = value; }

        public void Balloon(string title, string text)
        {
            _icon.BalloonTipTitle = title;
            _icon.BalloonTipText = text;
            _icon.BalloonTipIcon = ToolTipIcon.Info;
            _icon.ShowBalloonTip(5000);
        }

        private static void Raise(Action a) { if (a != null) a(); }

        /// <summary>运行时画一个 16×16 的图标，避免为一张小图引入资源文件。</summary>
        private static Icon MakeIcon()
        {
            var bmp = new Bitmap(16, 16);
            using (var g = Graphics.FromImage(bmp))
            {
                g.Clear(Color.Transparent);
                using (var pen = new Pen(Color.FromArgb(110, 168, 254), 1.6f))
                using (var brush = new SolidBrush(Color.FromArgb(110, 168, 254)))
                {
                    g.DrawRectangle(pen, 2.5f, 1.5f, 11f, 13f);
                    g.FillRectangle(brush, 4.5f, 4.5f, 7f, 1.4f);
                    g.FillRectangle(brush, 4.5f, 7.5f, 7f, 1.4f);
                    g.FillRectangle(brush, 4.5f, 10.5f, 4.5f, 1.4f);
                }
            }
            return Icon.FromHandle(bmp.GetHicon());
        }

        public void Dispose()
        {
            _icon.Visible = false;
            _icon.Dispose();
        }
    }
}
```

- [ ] **Step 5: 写入口与单实例**

`src/QuickNote/Program.cs`：

```csharp
using System;
using System.IO;
using System.Threading;
using System.Windows.Forms;
using QuickNote.Core;

namespace QuickNote
{
    internal sealed class QuickNoteApp : ApplicationContext
    {
        private readonly AppConfig _config;
        private readonly TrayIcon _tray;
        private readonly MessageWindow _messageWindow;

        public QuickNoteApp()
        {
            _config = AppConfig.Load(AppConfig.DefaultConfigPath());

            var showMessage = NativeMethods.RegisterWindowMessage("QuickNote.Show");
            _messageWindow = new MessageWindow(showMessage);
            _messageWindow.ShowRequested += ShowBar;   // 第二个实例被启动时由这里弹出小条

            _tray = new TrayIcon(_config.AutoStart);
            _tray.ShowRequested += ShowBar;
            _tray.OpenTodayRequested += OpenToday;
            _tray.OpenNotesFolderRequested += OpenNotesFolder;
            _tray.ExitRequested += ExitApp;
        }

        private void ShowBar() { /* Task 8 起接入实际小条 */ }

        private void OpenToday()
        {
            var path = Path.Combine(_config.NotesDir, DateFormat.FileName(DateTime.Now) + ".md");
            if (!File.Exists(path)) File.WriteAllText(path, "", new System.Text.UTF8Encoding(false));
            Open(path);
        }

        private void OpenNotesFolder()
        {
            Directory.CreateDirectory(_config.NotesDir);
            Open(_config.NotesDir);
        }

        private static void Open(string path)
        {
            try { System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(path) { UseShellExecute = true }); }
            catch (Exception ex) { Log("打开失败 " + path + "：" + ex.Message); }
        }

        private void ExitApp()
        {
            // Task 11 会给它加上「有未保存草稿时先询问」。
            ExitThread();
        }

        public static void Log(string message)
        {
            try
            {
                Directory.CreateDirectory(AppConfig.AppDataDir());
                File.AppendAllText(Path.Combine(AppConfig.AppDataDir(), "error.log"),
                    DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + "  " + message + Environment.NewLine,
                    new System.Text.UTF8Encoding(false));
            }
            catch { /* 记日志失败绝不能反过来把程序搞崩 */ }
        }

        protected override void ExitThreadCore()
        {
            if (_tray != null) _tray.Dispose();
            if (_messageWindow != null) _messageWindow.Dispose();
            base.ExitThreadCore();
        }
    }

    internal static class Program
    {
        private static Mutex _mutex;

        [STAThread]
        private static void Main()
        {
            bool createdNew;
            _mutex = new Mutex(true, "QuickNote.SingleInstance", out createdNew);
            if (!createdNew)
            {
                // 已有实例在跑：请它弹出小条，然后自己退出。
                // 否则误双击 exe 会出现两个托盘图标和两个小条。
                var msg = NativeMethods.RegisterWindowMessage("QuickNote.Show");
                NativeMethods.PostMessage(new IntPtr(0xFFFF), msg, IntPtr.Zero, IntPtr.Zero);
                return;
            }

            Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);
            Application.ThreadException += delegate (object s, ThreadExceptionEventArgs e)
            {
                QuickNoteApp.Log("界面线程异常：" + e.Exception);
            };
            AppDomain.CurrentDomain.UnhandledException += delegate (object s, UnhandledExceptionEventArgs e)
            {
                QuickNoteApp.Log("未处理异常：" + e.ExceptionObject);
            };

            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new QuickNoteApp());

            GC.KeepAlive(_mutex);
        }
    }
}
```

- [ ] **Step 6: 编译并运行**

```bash
dotnet build QuickNote.sln -c Debug
dotnet run --project src/QuickNote -c Debug
```

预期：托盘出现图标；右键菜单可展开；点「打开日记目录」会创建并打开笔记目录；点「退出」退出。

再开一个终端跑同一个命令，预期第二个实例立刻退出，托盘图标不增加。若托盘图标变两个，说明单实例 Mutex 失效。

- [ ] **Step 7: 提交**

```bash
git add src/QuickNote
git commit -m "feat(app): 应用骨架、托盘、单实例与全局异常处理"
```

---

### Task 8: 小条外观 — 无边框窗口、定位、高度钳制

**Files:**
- Create: `src/QuickNote.Core/BarGeometry.cs`
- Create: `src/QuickNote.SelfCheck/BarGeometryChecks.cs`
- Modify: `src/QuickNote.SelfCheck/Program.cs`
- Modify: `src/QuickNote/NativeMethods.cs`（追加 DWM 与钩子相关声明）
- Create: `src/QuickNote/CaptureBar.cs`
- Modify: `src/QuickNote/Program.cs`

**Interfaces:**
- Consumes: `Theme`（Task 7）、`BarMode`（Task 4）
- Produces: `BarGeometry.MinLines`（const int = 3）、`BarGeometry.MaxLines`（const int = 8）、`BarGeometry.HeightFor(int lineCount, int lineHeight, int chromeHeight) → int`；`CaptureBar` 窗口（`ShowBar()` / `HideBar()`）

- [ ] **Step 1: 写会失败的自检**

`src/QuickNote.SelfCheck/BarGeometryChecks.cs`：

```csharp
using QuickNote.Core;

namespace QuickNote.SelfCheck
{
    internal static class BarGeometryChecks
    {
        public static void Run()
        {
            Check.Section("BarGeometry");

            const int lineHeight = 22;
            const int chrome = 90;   // 上下状态行 + 内边距

            Check.Equal(BarGeometry.HeightFor(1, lineHeight, chrome), chrome + 3 * lineHeight,
                "1 行内容时高度取下限 3 行");
            Check.Equal(BarGeometry.HeightFor(3, lineHeight, chrome), chrome + 3 * lineHeight,
                "3 行时正好等于下限");
            Check.Equal(BarGeometry.HeightFor(5, lineHeight, chrome), chrome + 5 * lineHeight,
                "5 行时按实际行数");
            Check.Equal(BarGeometry.HeightFor(8, lineHeight, chrome), chrome + 8 * lineHeight,
                "8 行时正好等于上限");
            Check.Equal(BarGeometry.HeightFor(9, lineHeight, chrome), chrome + 8 * lineHeight,
                "9 行时被上限钳制");
            Check.Equal(BarGeometry.HeightFor(500, lineHeight, chrome), chrome + 8 * lineHeight,
                "粘贴 500 行时仍被钳制在上限，不会撑满屏幕");
            Check.Equal(BarGeometry.HeightFor(0, lineHeight, chrome), chrome + 3 * lineHeight,
                "0 行时取下限");
        }
    }
}
```

在 `Program.cs` 中 `NoteSearcherChecks.Run();` 之后加入：

```csharp
                BarGeometryChecks.Run();
```

- [ ] **Step 2: 运行自检，确认失败**

```bash
dotnet build QuickNote.sln -c Debug
```

预期：编译失败，`BarGeometry` 不存在（CS0246）。

- [ ] **Step 3: 实现 BarGeometry**

`src/QuickNote.Core/BarGeometry.cs`：

```csharp
namespace QuickNote.Core
{
    /// <summary>
    /// 小条高度的纯计算。单独抽出来是因为「粘贴一大段文本」这条路径：
    /// 没有上限的话小条会一路长高直到遮住整个屏幕。
    /// </summary>
    public static class BarGeometry
    {
        public const int MinLines = 3;
        public const int MaxLines = 8;

        public static int HeightFor(int lineCount, int lineHeight, int chromeHeight)
        {
            var lines = lineCount;
            if (lines < MinLines) lines = MinLines;
            if (lines > MaxLines) lines = MaxLines;
            return chromeHeight + lines * lineHeight;
        }
    }
}
```

- [ ] **Step 4: 运行自检，确认通过**

```bash
dotnet run --project src/QuickNote.SelfCheck -c Debug
```

预期：七个分组全部 PASS，退出码 0。

- [ ] **Step 5: 追加 P/Invoke 声明**

在 `src/QuickNote/NativeMethods.cs` 的类中追加：

```csharp
        internal const int DWMWA_WINDOW_CORNER_PREFERENCE = 33;
        internal const int DWMWCP_ROUND = 2;
        internal const int CS_DROPSHADOW = 0x00020000;

        [DllImport("dwmapi.dll")]
        internal static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int attrValue, int attrSize);

        [StructLayout(LayoutKind.Sequential)]
        internal struct RECT
        {
            internal int Left, Top, Right, Bottom;
        }

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool GetCursorPos(out System.Drawing.Point lpPoint);

        [DllImport("user32.dll")]
        internal static extern IntPtr MonitorFromPoint(System.Drawing.Point pt, uint dwFlags);

        [DllImport("shcore.dll")]
        internal static extern int GetDpiForMonitor(IntPtr hmonitor, int dpiType, out uint dpiX, out uint dpiY);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter,
            int X, int Y, int cx, int cy, uint uFlags);
```

- [ ] **Step 6: 实现 CaptureBar 的窗口外壳**

`src/QuickNote/CaptureBar.cs`：

```csharp
using System;
using System.Drawing;
using System.Windows.Forms;
using QuickNote.Core;

namespace QuickNote
{
    /// <summary>
    /// 悬浮小条。无边框、置顶、不进任务栏、带投影与圆角。
    /// 本任务只做外壳与定位；语法高亮与业务在后续任务接入。
    /// </summary>
    internal sealed class CaptureBar : Form
    {
        private readonly Palette _palette;

        public CaptureBar(Palette palette)
        {
            _palette = palette;

            FormBorderStyle = FormBorderStyle.None;
            ShowInTaskbar = false;
            TopMost = true;
            StartPosition = FormStartPosition.Manual;
            BackColor = palette.BarBack;
            KeyPreview = true;
            DoubleBuffered = true;

            Padding = new Padding(14, 10, 14, 10);
            Width = 560;
            Height = BarGeometry.HeightFor(BarGeometry.MinLines, 22, 90);
        }

        protected override CreateParams CreateParams
        {
            get
            {
                var cp = base.CreateParams;
                cp.ClassStyle |= NativeMethods.CS_DROPSHADOW;   // 系统绘制的投影，免费
                return cp;
            }
        }

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            try
            {
                // Windows 11 让系统画圆角，比自绘 region 干净得多
                var pref = NativeMethods.DWMWCP_ROUND;
                NativeMethods.DwmSetWindowAttribute(Handle, NativeMethods.DWMWA_WINDOW_CORNER_PREFERENCE,
                    ref pref, sizeof(int));
            }
            catch { /* 非 Win11 上忽略 */ }
            Reposition();
        }

        /// <summary>定位到鼠标所在显示器工作区的上方三分之一处居中。</summary>
        public void Reposition()
        {
            var screen = Screen.FromPoint(Cursor.Position);
            var wa = screen.WorkingArea;
            Left = wa.Left + (wa.Width - Width) / 2;
            Top = wa.Top + wa.Height / 3;
        }

        public void ShowBar()
        {
            Reposition();
            Show();
            NativeMethods.SetForegroundWindow(Handle);
        }

        public void HideBar()
        {
            Hide();
        }

        protected override void OnDeactivate(EventArgs e)
        {
            base.OnDeactivate(e);
            HideBar();   // 失焦隐藏但不清空内容，防手滑
        }

        protected override bool ShowWithoutActivation { get { return false; } }
    }
}
```

在 `QuickNoteApp` 的构造函数里创建小条，并让 `ShowBar` 真正弹它：

```csharp
        private CaptureBar _bar;

        // 构造函数末尾：
        _bar = new CaptureBar(Theme.Current(_config.Theme));

        // 替换原来的 ShowBar 空实现：
        private void ShowBar()
        {
            if (_bar == null) return;
            _bar.ShowBar();
        }
```

- [ ] **Step 7: 手动验证外观**

```bash
dotnet run --project src/QuickNote -c Debug
```

从托盘菜单点「记一笔」，逐项确认：

- 小条出现在鼠标所在屏幕工作区上方三分之一处居中
- 无边框、有投影、圆角（Win11）
- 不在任务栏出现新条目
- 点屏幕上别处 → 小条消失

把窗口拖到第二显示器后重复一遍；并把系统缩放改成 150% 后重复一遍，位置与大小应仍然正确（这依赖 `app.manifest` 里的 PerMonitorV2）。

- [ ] **Step 8: 提交**

```bash
git add src/QuickNote src/QuickNote.Core/BarGeometry.cs src/QuickNote.SelfCheck
git commit -m "feat(app): 小条窗口外壳、定位与高度钳制"
```

---
### Task 9: Markdown 语法高亮

**Files:**
- Create: `src/QuickNote/BarTextBox.cs`
- Create: `src/QuickNote/MarkdownHighlighter.cs`
- Modify: `src/QuickNote/NativeMethods.cs`（追加 IME 常量）
- Modify: `src/QuickNote/CaptureBar.cs`

**Interfaces:**
- Consumes: `Palette`（Task 7）、`BarGeometry`（Task 8）
- Produces: `BarTextBox.IsComposing → bool`；`BarTextBox.TextChangedByUser` 事件；`MarkdownHighlighter.Apply(RichTextBox, Palette)`

本任务必须处理 spec 第 6 节点名的两个坑：**不能给 `RichTextBox.Text` 赋值**（会清空格式），以及重着色时要保存恢复光标与滚动位置、并用 `WM_SETREDRAW` 消除闪烁。不处理的话表现是「打字时光标乱跳」和「每敲一个字闪一下」。

- [ ] **Step 1: 写支持 IME 组合跟踪的文本框**

`src/QuickNote/BarTextBox.cs`：

```csharp
using System;
using System.Windows.Forms;

namespace QuickNote
{
    /// <summary>
    /// RichTextBox 子类。两件事：
    /// 1) 跟踪输入法组合状态 —— 拼音候选框开着时的 Enter 必须被忽略（见 InputRules）。
    ///    正常情况输入法会吞掉按键、程序收不到，这里再加一道防线。
    /// 2) 暴露一个不带重入的「文本变了」事件，避免重着色触发自身。
    /// </summary>
    internal sealed class BarTextBox : RichTextBox
    {
        private bool _composing;

        public bool IsComposing { get { return _composing; } }

        /// <summary>文本确实变化时触发（重着色改格式不会触发）。</summary>
        public event Action TextChangedByUser;

        public BarTextBox()
        {
            BorderStyle = BorderStyle.None;
            Multiline = true;
            WordWrap = true;
            ScrollBars = RichTextBoxScrollBars.Vertical;
            DetectUrls = false;
            AcceptsTab = false;          // Tab 由我们自己处理
            ShortcutsEnabled = true;
        }

        protected override void WndProc(ref Message m)
        {
            if (m.Msg == NativeMethods.WM_IME_STARTCOMPOSITION) _composing = true;
            else if (m.Msg == NativeMethods.WM_IME_ENDCOMPOSITION) _composing = false;
            base.WndProc(ref m);
        }

        protected override void OnTextChanged(EventArgs e)
        {
            base.OnTextChanged(e);
            var h = TextChangedByUser;
            if (h != null) h();
        }
    }
}
```

- [ ] **Step 2: 写高亮器**

`src/QuickNote/MarkdownHighlighter.cs`：

```csharp
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Text.RegularExpressions;
using System.Windows.Forms;

namespace QuickNote
{
    /// <summary>
    /// 整体重扫全文重新着色。内容只有几百字节，
    /// 增量高亮的复杂度不值得。绝不使用 RichTextBox.Text 赋值——那会清空全部格式。
    /// </summary>
    internal static class MarkdownHighlighter
    {
        private static readonly Regex HeadingLine = new Regex(@"^\s{0,3}#{1,6}\s", RegexOptions.Compiled);
        private static readonly Regex BulletMarker = new Regex(@"^(\s*)([-*+])(\s)", RegexOptions.Compiled);
        private static readonly Regex TodoMarker = new Regex(@"^(\s*)([-*+]\s\[[ xX]\])(\s)", RegexOptions.Compiled);
        private static readonly Regex QuoteLine = new Regex(@"^\s{0,3}>\s?", RegexOptions.Compiled);
        private static readonly Regex Bold = new Regex(@"\*\*[^*\n]+\*\*", RegexOptions.Compiled);
        private static readonly Regex Code = new Regex(@"`[^`\n]+`", RegexOptions.Compiled);

        public static void Apply(RichTextBox box, Palette p)
        {
            if (box.TextLength == 0) return;

            var selStart = box.SelectionStart;
            var selLength = box.SelectionLength;
            var firstVisible = box.GetCharIndexFromPosition(new Point(1, 1));

            NativeMethods.SendMessage(box.Handle, NativeMethods.WM_SETREDRAW, IntPtr.Zero, IntPtr.Zero);
            try
            {
                box.SelectAll();
                box.SelectionColor = p.Text;
                box.SelectionFont = p.Body;

                var text = box.Text;
                var lineStart = 0;
                while (lineStart <= text.Length)
                {
                    var nl = text.IndexOf('\n', lineStart);
                    var lineEnd = nl < 0 ? text.Length : nl;
                    ColorLine(box, text, lineStart, lineEnd, p);

                    if (nl < 0) break;
                    lineStart = nl + 1;
                }
            }
            finally
            {
                box.Select(selStart, selLength);
                NativeMethods.SendMessage(box.Handle, NativeMethods.WM_SETREDRAW, IntPtr.Zero + 1, IntPtr.Zero);
                box.Invalidate();
                // 把滚动位置放回去，否则重着色会把视图拽回顶部
                if (firstVisible >= 0 && firstVisible < box.TextLength)
                {
                    box.SelectionStart = firstVisible;
                    box.SelectionLength = 0;
                    box.ScrollToCaret();
                    box.SelectionStart = selStart;
                    box.SelectionLength = selLength;
                }
            }
        }

        private static void ColorLine(RichTextBox box, string text, int start, int end, Palette p)
        {
            var line = text.Substring(start, end - start);

            if (HeadingLine.IsMatch(line))
            {
                Paint(box, start, line.Length, p.Accent, p.HeadingFont);
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
                // 待办标记整段（含 "- [ ]"）用橙色，其余用正文色
                var markerStart = start + todo.Groups[1].Length;
                Paint(box, start, line.Length, p.Text, p.Body);
                Paint(box, markerStart, todo.Groups[2].Length, p.Todo, p.Body);
            }
            else
            {
                var bullet = BulletMarker.Match(line);
                if (bullet.Success)
                {
                    var markerStart = start + bullet.Groups[1].Length;
                    Paint(box, start, line.Length, p.Text, p.Body);
                    Paint(box, markerStart, bullet.Groups[2].Length, p.Accent, p.Body);
                }
            }

            foreach (Match m in Bold.Matches(line))
            {
                Paint(box, start + m.Index, m.Length, p.Text, p.HeadingFont);
            }
            foreach (Match m in Code.Matches(line))
            {
                Paint(box, start + m.Index, m.Length, p.Accent, p.Mono);
            }
        }

        private static void Paint(RichTextBox box, int start, int length, Color color, Font font)
        {
            if (length <= 0) return;
            box.Select(start, length);
            box.SelectionColor = color;
            box.SelectionFont = font;
        }
    }
}
```

- [ ] **Step 3: 接进小条**

修改 `src/QuickNote/CaptureBar.cs`：加一个 `_input` 字段并在构造函数里创建，替换原来的内边距布局：

```csharp
        private readonly BarTextBox _input;

        // 构造函数中，设置 Padding 之后：
        _input = new BarTextBox
        {
            Dock = DockStyle.Fill,
            BackColor = palette.BarBack,
            ForeColor = palette.Text,
            Font = palette.Body,
            AcceptsReturn = true
        };
        _input.TextChangedByUser += delegate { MarkdownHighlighter.Apply(_input, _palette); };
        Controls.Add(_input);
```

同时把 `BarGeometry.HeightFor` 的调用改成随行数变化：

```csharp
        private void FitToContent()
        {
            // 走 SetLineCount 而不是直接改 Height：尺寸常量是 96 DPI 的逻辑值，
            // CaptureBar 内部要乘上显示器缩放系数，绕过它就会在高 DPI 下偏小。
            SetLineCount(_input.Lines.Length);
        }
```

并在 `_input.TextChangedByUser` 的处理里追加 `FitToContent();`。

- [ ] **Step 4: 手动验证**

```bash
dotnet run --project src/QuickNote -c Debug
```

从托盘点「记一笔」，依次敲入并确认：

- `## 标题` → 整行变蓝加粗
- `- 列表项` → 连字符变蓝
- `- [ ] 待办` → `- [ ]` 变橙
- `**粗体**` → 变粗
- `` `代码` `` → 变等宽
- `>` 开头 → 变灰

关键的两条（这是本任务真正要验的）：

- **在文字中间插入字符，光标停在原处**，不会跳到行首或文末
- **每敲一个字符不闪屏**（若闪烁，检查 `WM_SETREDRAW` 的关闭/开启是否成对，以及 `Invalidate` 的位置）

再粘贴 20 行文本，确认小条高度停在上限、内部出现滚动条，没有撑满屏幕。

- [ ] **Step 5: 提交**

```bash
git add src/QuickNote
git commit -m "feat(app): markdown 语法高亮与 IME 组合状态跟踪"
```

---

### Task 10: 热键接线 — 全局钩子、双击触发、备用降级

**Files:**
- Modify: `src/QuickNote/NativeMethods.cs`（追加钩子与热键声明）
- Create: `src/QuickNote/KeyboardHook.cs`
- Create: `src/QuickNote/FallbackHotkey.cs`
- Create: `src/QuickNote/HotkeyManager.cs`
- Modify: `src/QuickNote/Program.cs`

**Interfaces:**
- Consumes: `DoubleTapDetector` / `KeyAction`（Task 5）、`AppConfig`（Task 3）
- Produces: `KeyboardHook.TryInstall() → bool`；`KeyboardHook.Dispose()`；`FallbackHotkey.TryRegister(string combo) → bool`；`HotkeyManager`，事件 `Activated`、`DegradedTo(string reason)`

- [ ] **Step 1: 追加 P/Invoke 声明**

在 `src/QuickNote/NativeMethods.cs` 中追加：

```csharp
        internal const int WH_KEYBOARD_LL = 13;
        internal const int VK_LCONTROL = 0xA2;
        internal const int WM_HOTKEY = 0x0312;

        internal const uint MOD_ALT = 0x0001;
        internal const uint MOD_CONTROL = 0x0002;
        internal const uint MOD_SHIFT = 0x0004;
        internal const uint MOD_WIN = 0x0008;
        internal const uint MOD_NOREPEAT = 0x4000;

        internal delegate IntPtr LowLevelKeyboardProc(int nCode, IntPtr wParam, IntPtr lParam);

        [StructLayout(LayoutKind.Sequential)]
        internal struct KBDLLHOOKSTRUCT
        {
            internal uint vkCode;
            internal uint scanCode;
            internal uint flags;
            internal uint time;
            internal IntPtr dwExtraInfo;
        }

        [DllImport("user32.dll", SetLastError = true)]
        internal static extern IntPtr SetWindowsHookEx(int idHook, LowLevelKeyboardProc lpfn, IntPtr hMod, uint dwThreadId);

        [DllImport("user32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool UnhookWindowsHookEx(IntPtr hhk);

        [DllImport("user32.dll", SetLastError = true)]
        internal static extern IntPtr CallNextHookEx(IntPtr hhk, int nCode, IntPtr wParam, IntPtr lParam);

        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        internal static extern IntPtr GetModuleHandle(string lpModuleName);

        [DllImport("user32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

        [DllImport("user32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool UnregisterHotKey(IntPtr hWnd, int id);
```

- [ ] **Step 2: 写键盘钩子**

`src/QuickNote/KeyboardHook.cs`：

```csharp
using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using QuickNote.Core;

namespace QuickNote
{
    /// <summary>
    /// 低级键盘钩子。回调里只做两件事：喂状态机、投递消息。
    /// 任何 IO、分配、异常都会变成使用者每次打字的延迟——
    /// 更糟的是，UI 线程卡顿会被放大成全系统键盘卡顿，
    /// 超过 LowLevelHooksTimeout（约 300ms）后 Windows 会静默摘掉钩子。
    /// </summary>
    internal sealed class KeyboardHook : IDisposable
    {
        private readonly DoubleTapDetector _detector;
        private readonly Action _onActivated;
        private readonly NativeMethods.LowLevelKeyboardProc _proc;   // 必须持有引用，否则会被 GC 回收
        private IntPtr _hookId = IntPtr.Zero;
        private long _maxTicks;

        public KeyboardHook(int thresholdMs, Action onActivated)
        {
            _detector = new DoubleTapDetector(thresholdMs);
            _onActivated = onActivated;
            _proc = HookProc;
        }

        public bool TryInstall()
        {
            var module = NativeMethods.GetModuleHandle(null);
            _hookId = NativeMethods.SetWindowsHookEx(NativeMethods.WH_KEYBOARD_LL, _proc, module, 0);
            return _hookId != IntPtr.Zero;
        }

        private IntPtr HookProc(int nCode, IntPtr wParam, IntPtr lParam)
        {
            if (nCode >= 0)
            {
                var start = Stopwatch.GetTimestamp();
                try
                {
                    Feed(wParam.ToInt32(), Marshal.PtrToStructure<NativeMethods.KBDLLHOOKSTRUCT>(lParam));
                }
                catch
                {
                    // 钩子回调里绝不能把异常抛出去
                }
                TrackCost(start);
            }
            return NativeMethods.CallNextHookEx(_hookId, nCode, wParam, lParam);
        }

        private void Feed(int msg, NativeMethods.KBDLLHOOKSTRUCT data)
        {
            var isDown = msg == NativeMethods.WM_KEYDOWN || msg == NativeMethods.WM_SYSKEYDOWN;
            var isUp = msg == NativeMethods.WM_KEYUP || msg == NativeMethods.WM_SYSKEYUP;
            if (!isDown && !isUp) return;

            KeyAction action;
            if (data.vkCode == NativeMethods.VK_LCONTROL)
            {
                action = isDown ? KeyAction.LeftCtrlDown : KeyAction.LeftCtrlUp;
            }
            else
            {
                if (!isDown) return;      // 其他键的抬起不关心
                action = KeyAction.OtherKey;
            }

            if (_detector.KeyEvent(action, DateTime.Now)) _onActivated();
        }

        private void TrackCost(long start)
        {
#if DEBUG
            var cost = Stopwatch.GetTimestamp() - start;
            if (cost > _maxTicks) _maxTicks = cost;
            _calls++;
            if (_calls % 1000 == 0)
            {
                var ms = _maxTicks * 1000.0 / Stopwatch.Frequency;
                Debug.WriteLine("[QuickNote] 钩子回调累计 " + _calls + " 次，单次最大 "
                    + ms.ToString("F3") + " ms");
                _maxTicks = 0;
            }
#endif
        }

#if DEBUG
        private int _calls;
#endif

        public void Dispose()
        {
            if (_hookId != IntPtr.Zero)
            {
                NativeMethods.UnhookWindowsHookEx(_hookId);
                _hookId = IntPtr.Zero;
            }
        }
    }
}
```

- [ ] **Step 3: 写备用热键**

`src/QuickNote/FallbackHotkey.cs`：

```csharp
using System;
using System.Windows.Forms;

namespace QuickNote
{
    /// <summary>
    /// RegisterHotKey 备用路径。钩子装不上时（被杀软拦、被安全策略禁）
    /// 用它兜底，否则失败模式是「热键没反应，使用者以为是自己的手抖」。
    /// </summary>
    internal sealed class FallbackHotkey : NativeWindow, IDisposable
    {
        private const int HotkeyId = 0x5154;
        private readonly Action _onActivated;
        private bool _registered;

        public FallbackHotkey(Action onActivated)
        {
            _onActivated = onActivated;
            CreateHandle(new CreateParams());
        }

        /// <summary>组合键字符串形如 "Ctrl+Alt+N"。注册失败返回 false。</summary>
        public bool TryRegister(string combo, out string error)
        {
            error = null;
            uint modifiers, vk;
            if (!TryParse(combo, out modifiers, out vk))
            {
                error = "无法识别的组合键：" + combo;
                return false;
            }

            if (_registered) NativeMethods.UnregisterHotKey(Handle, HotkeyId);
            _registered = NativeMethods.RegisterHotKey(Handle, HotkeyId,
                modifiers | NativeMethods.MOD_NOREPEAT, vk);

            if (!_registered)
            {
                error = "组合键 " + combo + " 已被其他程序占用";
                return false;
            }
            return true;
        }

        private static bool TryParse(string combo, out uint modifiers, out uint vk)
        {
            modifiers = 0;
            vk = 0;
            if (string.IsNullOrWhiteSpace(combo)) return false;

            var parts = combo.Split('+');
            for (var i = 0; i < parts.Length; i++)
            {
                var p = parts[i].Trim();
                if (p.Equals("Ctrl", StringComparison.OrdinalIgnoreCase)) modifiers |= NativeMethods.MOD_CONTROL;
                else if (p.Equals("Alt", StringComparison.OrdinalIgnoreCase)) modifiers |= NativeMethods.MOD_ALT;
                else if (p.Equals("Shift", StringComparison.OrdinalIgnoreCase)) modifiers |= NativeMethods.MOD_SHIFT;
                else if (p.Equals("Win", StringComparison.OrdinalIgnoreCase)) modifiers |= NativeMethods.MOD_WIN;
                else if (p.Length == 1)
                {
                    var c = char.ToUpperInvariant(p[0]);
                    if (c < 'A' || c > 'Z') return false;
                    vk = c;
                }
                else return false;
            }
            return modifiers != 0 && vk != 0;
        }

        protected override void WndProc(ref Message m)
        {
            if (m.Msg == NativeMethods.WM_HOTKEY && m.WParam.ToInt32() == HotkeyId) _onActivated();
            base.WndProc(ref m);
        }

        public void Dispose()
        {
            if (_registered)
            {
                NativeMethods.UnregisterHotKey(Handle, HotkeyId);
                _registered = false;
            }
            DestroyHandle();
        }
    }
}
```

- [ ] **Step 4: 写管理器，串起钩子与降级**

`src/QuickNote/HotkeyManager.cs`：

```csharp
using System;
using QuickNote.Core;

namespace QuickNote
{
    internal sealed class HotkeyManager : IDisposable
    {
        private readonly AppConfig _config;
        private KeyboardHook _hook;
        private FallbackHotkey _fallback;
        private Action _onActivated;

        /// <summary>钩子装不上、已降级到备用组合键时触发，参数是给使用者看的原因。</summary>
        public event Action<string> Degraded;

        public HotkeyManager(AppConfig config)
        {
            _config = config;
        }

        public void Start(Action onActivated)
        {
            _onActivated = onActivated;

            if (_config.HotkeyMode == AppConfig.ModeDoubleTap)
            {
                _hook = new KeyboardHook(_config.DoubleTapMs, onActivated);
                if (_hook.TryInstall()) return;

                _hook.Dispose();
                _hook = null;
                EnsureFallback("全局键盘钩子安装失败（可能被安全软件拦截），已改用备用组合键。");
            }
            else
            {
                EnsureFallback(null);
            }
        }

        private void EnsureFallback(string reason)
        {
            if (_fallback == null) _fallback = new FallbackHotkey(_onActivated);

            string error;
            if (!_fallback.TryRegister(_config.FallbackHotkey, out error))
            {
                if (Degraded != null) Degraded(error);
                return;
            }

            if (reason != null && Degraded != null) Degraded(reason);
        }

        public void ApplyConfigChange()
        {
            Dispose();
            Start(_onActivated);
        }

        public void Dispose()
        {
            if (_hook != null) { _hook.Dispose(); _hook = null; }
            if (_fallback != null) { _fallback.Dispose(); _fallback = null; }
        }
    }
}
```

- [ ] **Step 5: 接进应用**

在 `QuickNoteApp` 加上字段与接线：

```csharp
        private HotkeyManager _hotkeys;

        // 构造函数中，_tray 创建之后：
        _hotkeys = new HotkeyManager(_config);
        _hotkeys.Degraded += delegate (string reason)
        {
            _tray.Balloon("QuickNote 热键降级", reason);
        };
        _hotkeys.Start(ShowBar);

        // ExitThreadCore 中追加：
        if (_hotkeys != null) _hotkeys.Dispose();
```

- [ ] **Step 6: 手动验证**

```bash
dotnet run --project src/QuickNote -c Debug
```

逐项确认：

- **双击左 Ctrl** → 小条弹出
- **单击左 Ctrl（间隔 > 300ms）** → 不弹
- **长按左 Ctrl 两秒后松开** → 不弹
- **`Ctrl+C` 连按两次、每次间隔 200ms** → **绝不弹**（这是本任务最关键的一条）
- **`Ctrl+V`、`Ctrl+S`、`Ctrl+A` 各连按** → 不弹
- **右 Ctrl 双击** → 不弹
- 三分钟内持续打字，观察 Visual Studio 或 DebugView 里的钩子回调耗时输出，单次最大应远低于 1ms

- [ ] **Step 7: 提交**

```bash
git add src/QuickNote
git commit -m "feat(app): 全局键盘钩子、双击左 Ctrl 触发与备用降级"
```

---
### Task 11: 记录模式 — 保存、草稿保留、错误提示

**Files:**
- Modify: `src/QuickNote/CaptureBar.cs`
- Create: `src/QuickNote/BarChrome.cs`（顶部状态行与底部提示行）
- Modify: `src/QuickNote/Program.cs`

**Interfaces:**
- Consumes: `NoteStore`（Task 2）、`InputRules`（Task 4）、`BarGeometry`（Task 8）、`BarTextBox`（Task 9）
- Produces: `CaptureBar` 公开方法 `ShowBar()` / `HideBar()` / `ReloadDraft()`；属性 `Draft → string`

- [ ] **Step 1: 写顶部与底部状态行**

`src/QuickNote/BarChrome.cs`：

```csharp
using System.Drawing;
using System.Windows.Forms;

namespace QuickNote
{
    /// <summary>小条的上下两条辅助信息行。刻意做成极简：只有日期、条数与键位提示。</summary>
    internal sealed class BarChrome
    {
        public readonly Label Top;
        public readonly Label Bottom;

        public BarChrome(Palette p)
        {
            Top = NewLabel(p.Small, p.Muted);
            Bottom = NewLabel(p.Small, p.Muted);
            Top.Dock = DockStyle.Top;
            Top.Height = 20;
            Bottom.Dock = DockStyle.Bottom;
            Bottom.Height = 20;
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

- [ ] **Step 2: 把记录模式的键位与保存流程接进小条**

修改 `src/QuickNote/CaptureBar.cs`：加字段并在构造函数里装配上下状态行。

```csharp
        private readonly NoteStore _store;
        private readonly BarChrome _chrome;
        private string _draft = "";
        private Timer _errorReset;   // 仅在出错后短暂启用，平时不跑

        // 构造函数签名改为 CaptureBar(Palette palette, NoteStore store)
        _store = store;
        _chrome = new BarChrome(palette);

        // Padding 之后。_input 已在 Task 9 用 Controls.Add 加过（Dock=Fill），
        // 必须让它在 z-order 最前（最先加），上下两条才能在它之外停靠。
        Controls.Add(_chrome.Top);
        Controls.Add(_chrome.Bottom);
```

按键处理（覆盖 `OnKeyDown`，注意 `KeyPreview = true` 已设）：

```csharp
        protected override void OnKeyDown(KeyEventArgs e)
        {
            base.OnKeyDown(e);

            if (e.KeyCode == Keys.Enter)
            {
                var action = InputRules.DecideEnter(_input.IsComposing, e.Shift);
                if (action == EnterAction.Ignore) { e.SuppressKeyPress = true; return; }
                if (action == EnterAction.Newline) return;      // 交给控件插入换行
                e.SuppressKeyPress = true;
                Save();
                return;
            }

            if (e.KeyCode == Keys.Escape)
            {
                e.SuppressKeyPress = true;
                HideBar();     // 隐藏但不清空：写过三行东西误按一下全没了最劝退
                return;
            }

            if (e.KeyCode == Keys.Tab)
            {
                var tab = InputRules.DecideTab(BarMode.Record, _input.TextLength == 0);
                e.SuppressKeyPress = true;
                if (tab == TabAction.Indent) InsertIndent();
                // SwitchToSearch 由 Task 12 接管
                return;
            }
        }

        private void InsertIndent()
        {
            _input.SelectedText = new string(' ', InputRules.IndentWidth);
        }

        /// <summary>返回 true 表示已保存并清空。</summary>
        public bool Save()
        {
            var text = _input.Text;
            if (text.Trim().Length == 0)
            {
                HideBar();    // 空输入直接关掉，不创建文件、不留痕迹
                return false;
            }

            try
            {
                _store.Append(text, DateTime.Now);
                _input.Clear();
                _draft = "";
                _chrome.Top.Text = TopText();
                _chrome.Bottom.Text = "已保存";
                HideBar();
                return true;
            }
            catch (NoteStoreException ex)
            {
                // 最关键的一条：内容绝不清空。让使用者能把内容复制走。
                QuickNoteApp.Log(ex.Message);
                _chrome.Bottom.Text = "保存失败，内容已保留：" + ex.Message;
                _chrome.Bottom.ForeColor = _palette.Error;
                StartErrorReset();
                return false;
            }
        }

        private void StartErrorReset()
        {
            if (_errorReset == null)
            {
                _errorReset = new Timer { Interval = 6000 };
                _errorReset.Tick += delegate
                {
                    _errorReset.Stop();
                    _chrome.Bottom.ForeColor = _palette.Muted;
                    RefreshChrome();
                };
            }
            _errorReset.Stop();
            _errorReset.Start();
        }

        public string TopText()
        {
            var today = DateTime.Now;
            var count = 0;
            try { count = _store.CountEntries(today); }
            catch { /* 读不到就显示 0 条，不打扰使用者 */ }

            return DateFormat.Heading(today).Substring(2)
                + "　·　今天已记 " + count + " 条";
        }

        private void RefreshChrome()
        {
            _chrome.Top.Text = TopText();
            _chrome.Bottom.Text = "Enter 保存　·　Shift+Enter 换行　·　Tab 搜索";
        }

        public void OpenBar()
        {
            RefreshChrome();
            _input.Text = _draft;
            _input.SelectionStart = _input.TextLength;
            ShowBar();
            _input.Focus();
        }

        public void CloseBar()
        {
            _draft = _input.Text;      // Esc 与失焦都不清空草稿
            HideBar();
        }
```

把 Task 8 里 `OnDeactivate` 中的 `HideBar()` 改为 `CloseBar()`（Esc 是下面 `OnKeyDown` 里新加的，直接用 `CloseBar()`）；把 Task 7 里 `ShowBar` 的接线改为调 `OpenBar()`。

`QuickNoteApp` 中加字段并创建：

```csharp
        private readonly NoteStore _store;
        private CaptureBar _bar;

        // 构造函数中，_tray 创建之前：
        _store = new NoteStore(_config.NotesDir);
        _bar = new CaptureBar(Theme.Current(_config.Theme), _store);

        // 替换 Task 7 里的占位实现：
        private void ShowBar()
        {
            if (_bar != null) _bar.OpenBar();
        }
```

`ExitApp` 也在这里升级为「有草稿就先问」——直接退出会把使用者写了三行还没保存的东西丢掉：

```csharp
        private void ExitApp()
        {
            var draft = _bar == null ? "" : _bar.Draft;
            if (draft.Trim().Length > 0)
            {
                var answer = MessageBox.Show(
                    "还有没保存的内容：\n\n" + draft + "\n\n要退出吗？（退出后这些内容会丢失）",
                    "QuickNote", MessageBoxButtons.YesNo, MessageBoxIcon.Warning);
                if (answer != DialogResult.Yes) return;
            }
            ExitThread();
        }
```

`CaptureBar` 需要暴露草稿属性：

```csharp
        public string Draft { get { return _input.Text; } }
```

- [ ] **Step 3: 手动验证**

```bash
dotnet run --project src/QuickNote -c Debug
```

逐项确认：

- 双击左 Ctrl → 小条弹出，输入几个字 → `Enter` → 小条消失，`notes\2026-10-05.md` 出现新记录，顶部条数 +1
- 再打一次 → `Shift+Enter` → 换行且小条长高 → `Enter` 保存
- 打三行 → 按 `Esc` → 小条消失 → 再次唤起 → **三行内容还在**（草稿保留）
- 打几个字 → 点屏幕上别处 → 小条消失 → 再次唤起 → **内容还在**
- 清空输入框 → 直接 `Enter` → 小条消失，**没有产生新文件、没有新记录**
- **中文输入法**：切到拼音，输入「laowang」，候选框开着时按 `Enter` → **只上屏候选词，小条不消失**；再按一次 `Enter`（候选框已关）→ 这次才保存。这条不过，整个软件对中文使用者就是不可用的
- 用 Typora 打开当天的文件并保持打开，再记一笔 → 仍能写入成功，Typora 里刷新能看到新内容
- 把 `notes` 目录改成只读后再记一笔 → 小条底部显示红字提示、**内容没被清空**、能手动复制走

- [ ] **Step 4: 提交**

```bash
git add src/QuickNote
git commit -m "feat(app): 记录模式保存流程、草稿保留与写入失败提示"
```

---

### Task 12: 搜索模式 — SearchController 与结果列表

**Files:**
- Create: `src/QuickNote/SearchController.cs`
- Create: `src/QuickNote/SearchResultsView.cs`
- Modify: `src/QuickNote/CaptureBar.cs`
- Modify: `src/QuickNote/NativeMethods.cs`（追加 `SHOpenFolderAndSelectItems` 不需要，改用 Process.Start）

**Interfaces:**
- Consumes: `NoteSearcher` / `SearchResult` / `SearchHit`（Task 6）、`InputRules`（Task 4）、`BarMode`（Task 4）
- Produces: `SearchController(Control ui, string notesDir, Action<SearchResult> onResult, Action onStarted, int debounceMs = 200)`；`SearchController.Query(string)`；`SearchController.Cancel()`；`SearchResultsView`（`SetResult(SearchResult, string query)` / `MoveSelection(int)` / `SelectedHit → SearchHit` / 事件 `Activated`）

- [ ] **Step 1: 写 SearchController**

`src/QuickNote/SearchController.cs`：

```csharp
using System;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using QuickNote.Core;

namespace QuickNote
{
    /// <summary>
    /// 把「防抖、取消上一次扫描、结果回到 UI 线程」这堆易错的并发细节
    /// 从界面代码里隔离出去。界面只负责给查询词、把结果画出来，不碰线程。
    ///
    /// 扫描必须在后台：扫一千个文件要几百毫秒，跑在 UI 线程上会连带
    /// 全局键盘钩子一起卡住全系统键盘。
    ///
    /// 防抖用 Task.Delay 而不是 UI 定时器——定时器只在搜索期间存活，
    /// 待机时不存在任何计时器。
    /// </summary>
    internal sealed class SearchController : IDisposable
    {
        private readonly Control _ui;
        private readonly string _notesDir;
        private readonly Action<SearchResult> _onResult;
        private readonly Action _onStarted;
        private CancellationTokenSource _cts;
        private readonly int _debounceMs;

        public SearchController(Control ui, string notesDir,
            Action<SearchResult> onResult, Action onStarted, int debounceMs = 200)
        {
            _ui = ui;
            _notesDir = notesDir;
            _onResult = onResult;
            _onStarted = onStarted;
            _debounceMs = debounceMs;
        }

        public void Query(string text)
        {
            Cancel();

            if (string.IsNullOrEmpty(text) || text.Trim().Length == 0)
            {
                _onResult(SearchResult.Empty());
                return;
            }

            var cts = new CancellationTokenSource();
            _cts = cts;
            _onStarted();

            var query = text.Trim();
            var dir = _notesDir;
            var limit = NoteSearcher.DefaultLimit;
            var debounce = _debounceMs;

            Task.Run(async delegate
            {
                try
                {
                    await Task.Delay(debounce, cts.Token).ConfigureAwait(false);

                    var result = NoteSearcher.Search(dir, query, limit, cts.Token);
                    if (cts.IsCancellationRequested) return;

                    if (_ui.IsHandleCreated && !_ui.IsDisposed)
                    {
                        _ui.BeginInvoke((Action)delegate
                        {
                            if (!cts.IsCancellationRequested) _onResult(result);
                        });
                    }
                }
                catch (OperationCanceledException) { /* 被新的查询取代，正常 */ }
                catch (Exception ex) { QuickNoteApp.Log("搜索失败：" + ex); }
            });
        }

        public void Cancel()
        {
            if (_cts != null)
            {
                _cts.Cancel();
                _cts.Dispose();
                _cts = null;
            }
        }

        public void Dispose() { Cancel(); }
    }
}
```

- [ ] **Step 2: 写结果列表控件**

`src/QuickNote/SearchResultsView.cs`：

```csharp
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;
using QuickNote.Core;

namespace QuickNote
{
    /// <summary>
    /// 自绘结果列表。每条三行：日期、匹配行（命中处高亮）、上下文行。
    /// 用自绘而不是 ListBox，是为了在一条里画出三种字号与一种高亮。
    /// </summary>
    internal sealed class SearchResultsView : Control
    {
        private SearchResult _result = SearchResult.Empty();
        private int _selected;
        private readonly Palette _p;
        private string _query = "";
        private const int RowHeight = 62;

        public event Action<SearchHit> Activated;

        public SearchResultsView(Palette p)
        {
            _p = p;
            DoubleBuffered = true;
            SetStyle(ControlStyles.ResizeRedraw | ControlStyles.OptimizedDoubleBuffer |
                     ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint, true);
            BackColor = p.BarBack;
        }

        public SearchHit SelectedHit
        {
            get
            {
                if (_result.Hits.Count == 0) return null;
                return _result.Hits[_selected];
            }
        }

        public void SetResult(SearchResult result, string query)
        {
            _result = result ?? SearchResult.Empty();
            _query = query ?? "";
            _selected = 0;
            Invalidate();
        }

        public void MoveSelection(int delta)
        {
            if (_result.Hits.Count == 0) return;
            _selected += delta;
            if (_selected < 0) _selected = 0;
            if (_selected >= _result.Hits.Count) _selected = _result.Hits.Count - 1;
            Invalidate();
        }

        public void ActivateSelected()
        {
            var hit = SelectedHit;
            if (hit != null && Activated != null) Activated(hit);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.Clear(_p.BarBack);

            if (_result.Hits.Count == 0)
            {
                TextRenderer.DrawText(g, "没有找到匹配", _p.Small,
                    new Point(12, 12), _p.Muted);
                return;
            }

            var lastVisible = Math.Min(_result.Hits.Count, Height / RowHeight);
            for (var i = 0; i < lastVisible; i++)
            {
                var y = i * RowHeight;
                if (i == _selected)
                {
                    using (var b = new SolidBrush(_p.Selection))
                        g.FillRectangle(b, 0, y, Width, RowHeight - 2);
                }

                var hit = _result.Hits[i];
                TextRenderer.DrawText(g, hit.Date.ToString("MM-dd dddd"), _p.Small,
                    new Point(10, y + 4), _p.Muted);

                DrawMatchLine(g, hit, new Rectangle(10, y + 21, Width - 20, 20));

                if (!string.IsNullOrEmpty(hit.Context))
                {
                    TextRenderer.DrawText(g, hit.Context, _p.Small,
                        new Rectangle(10, y + 40, Width - 20, 18), _p.Muted,
                        TextFormatFlags.EndEllipsis);
                }
            }
        }

        /// <summary>
        /// 逐段画出匹配行，命中处用强调色加底纹。按「命中前的部分 → 命中 → 之后的部分」
        /// 三段依次量宽绘制，用 EndEllipsis 保证超长行不越界、不丢字。
        /// </summary>
        private void DrawMatchLine(Graphics g, SearchHit hit, Rectangle box)
        {
            var line = hit.Line;
            var query = _query;

            var at = box.X;
            var remaining = box.Width;

            if (string.IsNullOrEmpty(query))
            {
                TextRenderer.DrawText(g, line, _p.Body,
                    new Rectangle(box.X, box.Y, box.Width, box.Height), _p.Text,
                    TextFormatFlags.EndEllipsis);
                return;
            }

            var index = line.IndexOf(query, StringComparison.OrdinalIgnoreCase);
            if (index < 0)
            {
                TextRenderer.DrawText(g, line, _p.Body,
                    new Rectangle(box.X, box.Y, box.Width, box.Height), _p.Text,
                    TextFormatFlags.EndEllipsis);
                return;
            }

            var before = line.Substring(0, index);
            var matched = line.Substring(index, query.Length);
            var after = line.Substring(index + query.Length);

            var beforeWidth = TextRenderer.MeasureText(g, before, _p.Body).Width;
            var matchedWidth = TextRenderer.MeasureText(g, matched, _p.Body).Width;

            if (beforeWidth < remaining)
            {
                TextRenderer.DrawText(g, before, _p.Body,
                    new Rectangle(at, box.Y, remaining, box.Height), _p.Text,
                    TextFormatFlags.NoPadding);
            }
            at += beforeWidth;
            remaining -= beforeWidth;

            if (remaining > 0)
            {
                using (var b = new SolidBrush(_p.Selection))
                    g.FillRectangle(b, at - 1, box.Y, Math.Min(matchedWidth + 2, remaining), box.Height);
                TextRenderer.DrawText(g, matched, _p.Body,
                    new Rectangle(at, box.Y, remaining, box.Height), _p.Todo,
                    TextFormatFlags.NoPadding);
                at += matchedWidth;
                remaining -= matchedWidth;
            }

            if (remaining > 0 && after.Length > 0)
            {
                TextRenderer.DrawText(g, after, _p.Body,
                    new Rectangle(at, box.Y, remaining, box.Height), _p.Text,
                    TextFormatFlags.EndEllipsis | TextFormatFlags.NoPadding);
            }
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            var index = e.Y / RowHeight;
            if (index >= 0 && index < _result.Hits.Count)
            {
                _selected = index;
                Invalidate();
                ActivateSelected();
            }
            base.OnMouseDown(e);
        }
    }
}
```

- [ ] **Step 3: 把搜索模式接进小条**

在 `CaptureBar` 中：

```csharp
        private BarMode _mode = BarMode.Record;
        private SearchController _search;
        private SearchResultsView _results;
        private Panel _searchRow;
        private TextBox _queryBox;
        private Label _searchStatus;
        private string _lastQuery = "";
        private SearchResult _lastResult = SearchResult.Empty();

        // 构造函数中，_store 之后：
        _search = new SearchController(this, _store.NotesDir, OnSearchResult, OnSearchStarted);
        BuildSearchControls();

        private void BuildSearchControls()
        {
            _searchStatus = new Label
            {
                Font = _palette.Small,
                ForeColor = _palette.Muted,
                TextAlign = ContentAlignment.MiddleRight,
                Dock = DockStyle.Right,
                Width = 200
            };

            _queryBox = new TextBox
            {
                BorderStyle = BorderStyle.None,
                BackColor = _palette.BarBack,
                ForeColor = _palette.Text,
                Font = _palette.Body,
                Dock = DockStyle.Fill
            };
            _queryBox.TextChanged += delegate
            {
                _lastQuery = _queryBox.Text;
                _search.Query(_lastQuery);
            };
            _queryBox.KeyDown += OnSearchKeyDown;

            // 查询框与右侧状态在同一行：先加 Fill 再加 Right，
            // 停靠顺序由 z-order 决定，Right 后加才会贴右边而不是吃掉整行。
            _searchRow = new Panel { Dock = DockStyle.Top, Height = 26, Visible = false };
            _searchRow.Controls.Add(_queryBox);
            _searchRow.Controls.Add(_searchStatus);

            _results = new SearchResultsView(_palette) { Dock = DockStyle.Fill, Visible = false };
            _results.Activated += OpenHit;

            // Fill 必须最先加：WinForms 从 z-order 末尾往前停靠，
            // 先加的 Fill 最后停靠，才能正确占据上下两条剩下的空间。
            Controls.Add(_results);
            Controls.Add(_searchRow);
        }

        private void OnSearchStarted()
        {
            _searchStatus.Text = "搜索中…";
        }

        private void OnSearchResult(SearchResult result)
        {
            _lastResult = result;
            _results.SetResult(result, _lastQuery);
            _searchStatus.Text = result.Hits.Count + " 条"
                + (result.Truncated ? "（已截断）" : "")
                + "　·　扫描 " + result.ScannedFiles + " 个文件"
                + (result.UnreadableFiles > 0 ? "，跳过 " + result.UnreadableFiles + " 个无法读取" : "");
        }

        private void OnSearchKeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Down) { _results.MoveSelection(1); e.SuppressKeyPress = true; return; }
            if (e.KeyCode == Keys.Up) { _results.MoveSelection(-1); e.SuppressKeyPress = true; return; }
            if (e.KeyCode == Keys.Enter) { _results.ActivateSelected(); e.SuppressKeyPress = true; return; }
            if (e.KeyCode == Keys.Tab || e.KeyCode == Keys.Escape)
            {
                e.SuppressKeyPress = true;
                SwitchTo(BarMode.Record);
            }
        }

        private void SwitchTo(BarMode mode)
        {
            _mode = mode;
            var searching = mode == BarMode.Search;

            _input.Visible = !searching;
            _results.Visible = searching;
            _searchRow.Visible = searching;

            if (searching)
            {
                Width = 600;
                Height = 470;
                Reposition();
                _queryBox.Text = _lastQuery;          // 沿用上次查询词，避免反复输入
                _queryBox.Focus();
                _queryBox.SelectionStart = _queryBox.TextLength;
                _results.SetResult(_lastResult, _lastQuery);
            }
            else
            {
                Width = 560;
                FitToContent();
                _input.Focus();
            }
            RefreshChrome();
        }

        public string Draft { get { return _input.Text; } }

        private static void OpenHit(SearchHit hit)
        {
            try
            {
                System.Diagnostics.Process.Start(
                    new System.Diagnostics.ProcessStartInfo(hit.FilePath) { UseShellExecute = true });
            }
            catch (Exception ex) { QuickNoteApp.Log("打开 " + hit.FilePath + " 失败：" + ex.Message); }
        }
```

把记录模式里 `Tab` 的处理从「只缩进」改为调用 `SwitchTo`：

```csharp
            if (e.KeyCode == Keys.Tab)
            {
                var tab = InputRules.DecideTab(BarMode.Record, _input.TextLength == 0);
                e.SuppressKeyPress = true;
                if (tab == TabAction.Indent) InsertIndent();
                else if (tab == TabAction.SwitchToSearch) SwitchTo(BarMode.Search);
                return;
            }
```

把 `CloseBar` 改为同时丢弃搜索状态：

```csharp
        public void CloseBar()
        {
            _draft = _input.Text;              // 记录模式草稿保留
            _search.Cancel();
            _lastQuery = "";                   // 搜索词与结果在小条隐藏时丢弃
            _lastResult = SearchResult.Empty();
            _queryBox.Text = "";
            _results.SetResult(_lastResult, "");
            if (_mode == BarMode.Search) SwitchTo(BarMode.Record);
            HideBar();
        }

        public void OpenBar()
        {
            SwitchTo(BarMode.Record);          // 默认进记录模式，不记忆上次
            RefreshChrome();
            _input.Text = _draft;
            _input.SelectionStart = _input.TextLength;
            ShowBar();
            _input.Focus();
        }
```

`RefreshChrome` 按模式给出不同提示：

```csharp
        private void RefreshChrome()
        {
            if (_mode == BarMode.Search)
            {
                _chrome.Top.Text = "搜索日记";
                _chrome.Bottom.Text = "↑↓ 选择　·　Enter 打开　·　Tab 返回";
            }
            else
            {
                _chrome.Top.Text = TopText();
                _chrome.Bottom.Text = "Enter 保存　·　Shift+Enter 换行　·　Tab 搜索";
            }
        }
```

- [ ] **Step 4: 手动验证**

```bash
dotnet run --project src/QuickNote -c Debug
```

先在 `notes\` 里造几百个文件（可以直接复制当天文件并改文件名日期），然后逐项确认：

- 唤起小条 → 按 `Tab` → 切到搜索模式，查询框获得焦点
- 输入「老王」→ 结果列表出现，每条显示日期、匹配行、上下文行
- `↑` `↓` 移动选中，`Enter` 用默认编辑器打开对应日期文件
- 按 `Tab` 或 `Esc` → 切回记录模式
- **切到搜索前先打几个字，切回来草稿还在**
- 在搜索框里**快速连打**（模拟输入「老王」三个字）→ 结果不乱序、不闪回旧结果
- **搜索期间持续打键盘**（在别的窗口打字）→ 无卡顿感；若明显卡顿，说明扫描跑到了 UI 线程上
- 输入一个不存在的词 → 显示「没有找到匹配」
- 清空搜索框 → 结果清空，不显示全部内容
- 关掉小条再打开 → 搜索词被清空（符合「小条隐藏时丢弃」）
- 任务管理器观察内存：连续搜索十次，待机内存应回到基线，不累积增长

- [ ] **Step 5: 提交**

```bash
git add src/QuickNote
git commit -m "feat(app): 搜索模式、结果列表与后台扫描调度"
```

---
### Task 13: 开机自启与设置对话框

**Files:**
- Create: `src/QuickNote/AutoStart.cs`
- Create: `src/QuickNote/SettingsDialog.cs`
- Modify: `src/QuickNote/Program.cs`
- Modify: `src/QuickNote/TrayIcon.cs`（若菜单项需要动态刷新）

**Interfaces:**
- Consumes: `AppConfig`（Task 3）、`HotkeyManager`（Task 10）、`Theme`（Task 7）
- Produces: `AutoStart.IsEnabled() → bool`；`AutoStart.Enable()`；`AutoStart.Disable()`；`AutoStart.EnsurePathCurrent() → bool`；`SettingsDialog(AppConfig, Action onApplied)`

- [ ] **Step 1: 写自启管理**

`src/QuickNote/AutoStart.cs`：

```csharp
using System;
using System.Reflection;
using Microsoft.Win32;

namespace QuickNote
{
    /// <summary>
    /// 注册表 Run 键自启。不需要管理员权限，而且会出现在任务管理器的
    /// 「启动」选项卡里——使用者能自己看到、自己禁用，这是优点不是缺点。
    /// </summary>
    internal static class AutoStart
    {
        private const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
        private const string ValueName = "QuickNote";

        private static string ExePath()
        {
            return Assembly.GetEntryAssembly().Location;
        }

        public static bool IsEnabled()
        {
            try
            {
                using (var key = Registry.CurrentUser.OpenSubKey(RunKey, false))
                {
                    if (key == null) return false;
                    return key.GetValue(ValueName) != null;
                }
            }
            catch { return false; }
        }

        public static void Enable()
        {
            using (var key = Registry.CurrentUser.OpenSubKey(RunKey, true))
            {
                if (key == null) return;
                key.SetValue(ValueName, "\"" + ExePath() + "\"");
            }
        }

        public static void Disable()
        {
            using (var key = Registry.CurrentUser.OpenSubKey(RunKey, true))
            {
                if (key == null) return;
                if (key.GetValue(ValueName) != null) key.DeleteValue(ValueName, false);
            }
        }

        /// <summary>
        /// 注册表里存的是 exe 的绝对路径。程序挪了位置后自启会静默失效，
        /// 所以每次启动都自查一遍，不一致就改回来。
        /// 返回 true 表示做了修正。
        /// </summary>
        public static bool EnsurePathCurrent()
        {
            try
            {
                if (!IsEnabled()) return false;

                using (var key = Registry.CurrentUser.OpenSubKey(RunKey, true))
                {
                    if (key == null) return false;
                    var stored = key.GetValue(ValueName) as string;
                    var expected = "\"" + ExePath() + "\"";
                    if (string.Equals(stored, expected, StringComparison.OrdinalIgnoreCase)) return false;

                    key.SetValue(ValueName, expected);
                    return true;
                }
            }
            catch { return false; }
        }
    }
}
```

- [ ] **Step 2: 写设置对话框**

`src/QuickNote/SettingsDialog.cs`：

```csharp
using System;
using System.Drawing;
using System.Windows.Forms;
using QuickNote.Core;

namespace QuickNote
{
    internal sealed class SettingsDialog : Form
    {
        private readonly AppConfig _config;
        private readonly RadioButton _doubleTap;
        private readonly RadioButton _combo;
        private readonly NumericUpDown _threshold;
        private readonly TextBox _fallback;
        private readonly TextBox _notesDir;
        private readonly ComboBox _theme;

        public SettingsDialog(AppConfig config)
        {
            _config = config;

            Text = "QuickNote 设置";
            FormBorderStyle = FormBorderStyle.FixedDialog;
            StartPosition = FormStartPosition.CenterScreen;
            MaximizeBox = false;
            MinimizeBox = false;
            ClientSize = new Size(430, 296);
            Font = new Font("Microsoft YaHei UI", 9f);

            _doubleTap = new RadioButton
            {
                Text = "双击左 Ctrl",
                Location = new Point(16, 16),
                Width = 200,
                Checked = config.HotkeyMode == AppConfig.ModeDoubleTap
            };
            _combo = new RadioButton
            {
                Text = "只用下面的组合键（不装全局键盘钩子）",
                Location = new Point(16, 42),
                Width = 320,
                Checked = config.HotkeyMode == AppConfig.ModeCombo
            };

            var thresholdLabel = new Label
            {
                Text = "双击判定窗口（毫秒）",
                Location = new Point(16, 78),
                Width = 170
            };
            _threshold = new NumericUpDown
            {
                Location = new Point(200, 76),
                Width = 80,
                Minimum = 50,
                Maximum = 2000,
                Increment = 10,
                Value = Math.Min(2000, Math.Max(50, config.DoubleTapMs))
            };

            var fallbackLabel = new Label
            {
                Text = "备用组合键",
                Location = new Point(16, 114),
                Width = 170
            };
            _fallback = new TextBox
            {
                Location = new Point(200, 111),
                Width = 168,
                ReadOnly = true,
                Text = config.FallbackHotkey,
                BackColor = SystemColors.Window
            };
            _fallback.KeyDown += OnFallbackKeyDown;
            _fallback.GotFocus += delegate { _fallback.Text = "请按下组合键…"; };

            var dirLabel = new Label { Text = "日记目录", Location = new Point(16, 150), Width = 170 };
            _notesDir = new TextBox
            {
                Location = new Point(200, 147),
                Width = 130,
                Text = config.NotesDir
            };
            var browse = new Button { Text = "…", Location = new Point(336, 145), Width = 32 };
            browse.Click += delegate
            {
                using (var dlg = new FolderBrowserDialog { SelectedPath = _notesDir.Text })
                {
                    if (dlg.ShowDialog(this) == DialogResult.OK) _notesDir.Text = dlg.SelectedPath;
                }
            };

            var themeLabel = new Label { Text = "主题", Location = new Point(16, 186), Width = 80 };
            _theme = new ComboBox
            {
                Location = new Point(200, 183),
                Width = 130,
                DropDownStyle = ComboBoxStyle.DropDownList
            };
            _theme.Items.AddRange(new object[] { "System", "Light", "Dark" });
            _theme.SelectedItem = config.Theme;
            if (_theme.SelectedIndex < 0) _theme.SelectedIndex = 0;

            var ok = new Button { Text = "确定", Location = new Point(240, 244), Width = 80, DialogResult = DialogResult.OK };
            var cancel = new Button { Text = "取消", Location = new Point(328, 244), Width = 80, DialogResult = DialogResult.Cancel };

            ok.Click += delegate { Apply(); };
            AcceptButton = ok;
            CancelButton = cancel;

            Controls.AddRange(new Control[]
            {
                _doubleTap, _combo, thresholdLabel, _threshold,
                fallbackLabel, _fallback, dirLabel, _notesDir, browse,
                themeLabel, _theme, ok, cancel
            });
        }

        /// <summary>
        /// 按下组合键即录制。必须避开 Ctrl+Space 与 Ctrl+Shift——
        /// 那是中文输入法的切换热键，抢占会导致输入法行为错乱。
        /// </summary>
        private void OnFallbackKeyDown(object sender, KeyEventArgs e)
        {
            e.SuppressKeyPress = true;

            if (e.KeyCode == Keys.ControlKey || e.KeyCode == Keys.Menu || e.KeyCode == Keys.ShiftKey)
                return;   // 只按了修饰键，继续等主键

            if (e.KeyCode == Keys.Space)
            {
                MessageBox.Show(this, "Ctrl+Space 是中文输入法的切换热键，不能占用。",
                    "QuickNote", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            var parts = new System.Collections.Generic.List<string>();
            if ((e.Modifiers & Keys.Control) != 0) parts.Add("Ctrl");
            if ((e.Modifiers & Keys.Alt) != 0) parts.Add("Alt");
            if ((e.Modifiers & Keys.Shift) != 0) parts.Add("Shift");
            if (parts.Count == 0)
            {
                MessageBox.Show(this, "至少要带一个修饰键（Ctrl / Alt / Shift / Win）。",
                    "QuickNote", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            parts.Add(e.KeyCode.ToString());
            _fallback.Text = string.Join("+", parts.ToArray());
        }

        private void Apply()
        {
            _config.HotkeyMode = _doubleTap.Checked ? AppConfig.ModeDoubleTap : AppConfig.ModeCombo;
            _config.DoubleTapMs = (int)_threshold.Value;
            if (_fallback.Text.IndexOf('+') > 0) _config.FallbackHotkey = _fallback.Text;
            _config.NotesDir = _notesDir.Text.Trim();
            _config.Theme = (string)_theme.SelectedItem;
            _config.Save(AppConfig.DefaultConfigPath());
        }
    }
}
```

这里只显式拦了 `Ctrl+Space`。`Ctrl+Shift` 不需要拦：组合键必须带一个非修饰主键，而 `Ctrl+Shift` 单独按下根本不会产生 `KeyDown` 事件，构造不出来。

- [ ] **Step 3: 接进应用**

在 `QuickNoteApp` 中：

```csharp
        private readonly NoteStore _store;   // 若 Task 11 已加则复用

        // 构造函数中，_hotkeys.Start(...) 之后：
        AutoStart.EnsurePathCurrent();
        if (_config.AutoStart && !AutoStart.IsEnabled()) AutoStart.Enable();
        if (!_config.AutoStart && AutoStart.IsEnabled()) AutoStart.Disable();
```

托盘接线：

```csharp
        _tray.SettingsRequested += delegate
        {
            using (var dlg = new SettingsDialog(_config))
            {
                if (dlg.ShowDialog() != DialogResult.OK) return;
            }
            _store = new NoteStore(_config.NotesDir);
            _bar.SetStore(_store);
            _hotkeys.ApplyConfigChange();
            _bar.ApplyTheme(Theme.Current(_config.Theme));
            _tray.Balloon("QuickNote", "设置已保存");
        };

        _tray.AutoStartToggled += delegate
        {
            _config.AutoStart = !_config.AutoStart;
            if (_config.AutoStart) AutoStart.Enable(); else AutoStart.Disable();
            _config.Save(AppConfig.DefaultConfigPath());
            _tray.SetAutoStartChecked(_config.AutoStart);
        };
```

`CaptureBar` 需要两个新方法：

```csharp
        public void SetStore(NoteStore store)
        {
            _store = store;
            _search.Dispose();
            _search = new SearchController(this, store.NotesDir, OnSearchResult, OnSearchStarted);
            RefreshChrome();
        }

        public void ApplyTheme(Palette palette)
        {
            _palette = palette;
            BackColor = palette.BarBack;
            _input.BackColor = palette.BarBack;
            _input.ForeColor = palette.Text;
            _input.Font = palette.Body;
            _queryBox.BackColor = palette.BarBack;
            _queryBox.ForeColor = palette.Text;
            _queryBox.Font = palette.Body;
            MarkdownHighlighter.Apply(_input, palette);
            Invalidate(true);
        }
```

把 `_palette` 字段由 `readonly` 改为可变。

- [ ] **Step 4: 手动验证**

```bash
dotnet run --project src/QuickNote -c Debug
```

逐项确认：

- 托盘右键 →「设置…」→ 对话框打开，各项显示当前值
- 把「日记目录」改到一个新目录 → 确定 → 记一笔 → 新目录里出现当天的文件
- 把主题改成 `Light` → 小条变浅色
- 把热键模式改成「组合键」→ 确定 → **双击左 Ctrl 不再弹出**，`Ctrl+Alt+N` 弹出
- 改回「双击左 Ctrl」→ 双击恢复
- 托盘右键 →「开机自启」取消勾选 → 打开任务管理器「启动」选项卡，`QuickNote` 条目消失；重新勾上 → 条目回来
- 把 `bin\QuickNote\Debug\QuickNote.exe` 复制到别处、手动把注册表 `Run\QuickNote` 的值改成那个错误路径，然后启动原 exe → 注册表里的值被自动改回正确路径

- [ ] **Step 5: 提交**

```bash
git add src/QuickNote
git commit -m "feat(app): 开机自启、路径自查与设置对话框"
```

---

### Task 14: 验收 — 内存实测与手测清单

**Files:**
- Create: `docs/superpowers/plans/2026-10-05-quicknote-acceptance.md`

**Interfaces:**
- Consumes: 全部
- Produces: 一份填写完毕的验收记录

本任务不改产品代码。它的产出是**证据**：spec 第 2 节九条验收标准的实测结果。

- [ ] **Step 1: 发布正式产物**

```bash
dotnet build QuickNote.sln -c Release
```

预期：`bin\QuickNote\Release\QuickNote.exe` 生成，同目录下只有 exe、`QuickNote.Core.dll` 和 `.config`，**没有任何第三方 DLL**。若出现 `Microsoft.NETFramework.ReferenceAssemblies` 相关的包，说明 `PrivateAssets="all"` 没生效，回 Task 1 检查。

- [ ] **Step 2: 配置真实笔记目录**

启动 exe → 托盘「设置…」→ 日记目录改为 `D:\Project\daliy_log\notes` → 确定。

确认 `D:\Project\daliy_log\notes` 已存在，记一笔能在里面看到。

- [ ] **Step 3: 从干净启动测内存与冷启动**

1. 退出 QuickNote（托盘 → 退出）
2. 打开任务管理器 → 详细信息，确认没有 `QuickNote.exe` 残留
3. 启动 exe，用秒表量到托盘图标出现的时间
4. 什么都不做，等 60 秒让它稳定
5. 在任务管理器里找 `QuickNote.exe`，记下「内存（活动的专用工作集）」

预期：冷启动 < 1 秒；稳定后内存 **≤ 35MB**。

若超标，按 spec 第 13 节的排查顺序：确认没引入第三方依赖 → 检查是否有后台定时器还在跑 → 检查 RichTextBox 是否持有过大缓冲 → 检查搜索结果集是否被长期持有。

- [ ] **Step 4: 测搜索的内存不累积**

在 `notes\` 里放够几百个文件（复制当天文件并改成不同日期），然后：

1. 连续搜索十个不同的词，每个都等结果出来
2. 关掉小条，回到待机
3. 再读一次内存

预期：与 Step 3 的基线相比**没有累积增长**。

- [ ] **Step 5: 逐条跑完手测清单**

把 spec 第 12 节「手动清单」的十一项逐条走一遍，每项记录通过与否。清单里最关键的仍然是这条：

> 中文输入法组合状态下按 `Enter` 只上屏候选词，不触发保存、小条不消失。需覆盖：拼音组合中按 Enter 选词、组合中按 Shift+Enter、组合中按 Esc

- [ ] **Step 6: 写验收记录**

`docs/superpowers/plans/2026-10-05-quicknote-acceptance.md`：

```markdown
# QuickNote 验收记录

日期：<填写>
发布产物：bin\QuickNote\Release\QuickNote.exe

## 验收标准

| # | 标准 | 实测 | 结论 |
|---|---|---|---|
| 1 | 待机私有工作集 ≤ 35MB | <实测值> MB | |
| 2 | 冷启动到托盘就绪 < 1 秒 | <实测值> 秒 | |
| 3 | 双击左 Ctrl 弹出，Ctrl+C/V/S 不误触发 | | |
| 4 | 中文输入法在小条内正常 | | |
| 5 | 强杀进程后当天内容完整可读 | | |
| 6 | notes 下文件在 Typora / VS Code 渲染正常 | | |
| 7 | 钩子回调平均耗时微秒级 | <实测值> ms | |
| 8 | 搜索期间 UI 不阻塞，全系统键盘不卡 | | |
| 9 | 连续搜索十次后待机内存回到基线 | <实测值> MB | |

## 手动清单

| 项 | 结论 | 备注 |
|---|---|---|
| 钩子生效且不与其他软件冲突 | | |
| 中文输入法组合态按 Enter 只上屏候选词 | | |
| 中英文输入法切换、候选框定位正常 | | |
| 多显示器与 DPI 缩放下的位置正确 | | |
| 失焦隐藏、Esc 保留草稿 | | |
| Tab 三个分支（空框切搜索 / 有内容缩进 / 搜索中切回） | | |
| 切到搜索再切回，草稿仍在 | | |
| 快速连打搜索词结果不错乱，扫描期间打字不卡 | | |
| 真实规模 notes 上搜索无卡顿 | | |
| 搜索结果 Enter 正确打开对应日期文件 | | |
| 开机自启生效；移动 exe 后路径自动修正 | | |
| 托盘菜单各项 | | |

## 未通过项与处理

<逐条记录，或写「无」>
```

- [ ] **Step 7: 提交**

```bash
git add docs/superpowers/plans/2026-10-05-quicknote-acceptance.md
git commit -m "docs: QuickNote 验收记录"
```

---

## 完成标准

全部十四项任务的验收记录均为通过。若有未通过项，回到对应任务修复后重跑 Task 14 的相关步骤。
