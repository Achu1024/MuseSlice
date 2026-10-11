<div align="center">
  <img src="icon/MuseSlice_logo.svg" alt="MuseSlice Logo" width="400"/>

  # MuseSlice

  > 💡 为"只想记一笔"而生的轻量备忘工具 —— 双击 Ctrl，把一闪而过的念头留下来

  [**⬇️ 下载安装**](https://github.com/Achu1024/MuseSlice/releases/latest) · [功能](#-功能一览) · [数据在哪](#-数据在哪) · [从源码构建](#-从源码构建)
</div>

## 为什么是 MuseSlice

你是否也经历过这样的场景？

同时开着 Obsidian、Notion、系统记事本，电脑风扇已经开始狂转。只是想记一个临时想法，却要先在 Obsidian 里选文件夹、在 Notion 里选数据库、在记事本里纠结要不要保存 —— 记录这件事本身，变成了一道选择题。

而当你终于决定"先随便记一下"之后，真正的麻烦才刚开始：三个月后，那个临时文件夹里只剩一个叫「新建文件夹」的子文件夹，以及两份你已经不记得是什么的文件。

**MuseSlice 选择做减法。**

> **不管理文件，只管理想法。**
> 没有文件夹层级，没有数据库结构，没有"这个应该放哪里"的犹豫。打开即写，写完即存，用最轻的方式捕捉每一个值得留下的念头。

> **Markdown 原生。**
> 语法简洁、纯文本可移植、跨平台无障碍。你写下的每一行，都不被任何私有格式绑架。

> **为 AI 时代而生。**
> 纯 Markdown 输出意味着 MuseSlice 天然适合接入任何 AI 工具或 agent 工作流。无论是后续做内容聚合、语义检索，还是让 agent 自动读取和处理你的备忘，格式层面零障碍。

## ✨ 功能一览

记一笔的成本，决定了你会不会真的去记。MuseSlice 把这条链路压到最短：**双击 Ctrl → 打字 → Enter**，结束。

**极致轻量**

- 📦 安装包 2 MB 出头，常驻托盘，不吃内存不占屏
- ⚡ 纯追加写入，不读全文、不解析格式，落盘就是毫秒级
- 🧩 无数据库、无云同步、无账号 —— 你的笔记就是一堆 `.md` 文件

**顺手到无感**

- ⌨️ **双击 `Ctrl`** 唤出输入栏，不打断手上的事
- 📝 `Enter` 即存，没有"保存"按钮这个概念
- 📅 自动按日期归档成 Markdown，天然可被 Obsidian / Logseq 接管
- ✅ 每行自动成为一条待办，`- [ ]` 勾选框语法直接可用
- 🔍 记得写过但忘了哪天？在输入条里按 `Tab` 直接搜历史笔记

## 🎯 适用场景

| 场景 | 用法 |
|------|------|
| 灵感捕获 | 写代码卡壳时冒出个念头，双击 Ctrl 记录，继续敲代码 |
| 会议速记 | 一边听一边记，一行一件事，散会直接变待办清单 |
| 每日复盘 | 一天结束打开当天的 `.md`，做过什么一目了然 |

## 🚀 安装

到 [**Releases**](https://github.com/Achu1024/MuseSlice/releases/latest) 下载最新的 `MuseSlice-Setup-x.y.z.exe`，双击安装。

- 安装程序会**默认勾选**"创建桌面快捷方式"，取消勾选则只进开始菜单
- 每用户安装，装到 `%LOCALAPPDATA%\Programs\MuseSlice`，**不需要管理员权限**
- 装完自动启动，之后它就安静地待在托盘里

### 上手三步

1. 双击 **`Ctrl`** —— 唤出输入框
2. 输入内容，`Enter` 保存（`Esc` 直接关掉）
3. 想翻旧账就按 `Tab` 进搜索；想调热键、换保存位置就右键托盘图标 →「设置」

## 📁 数据在哪

| 内容 | 位置 |
|------|------|
| 日记 | `我的文档\MuseSlice\YYYY-MM-DD.md` |
| 配置 | `%APPDATA%\MuseSlice\config.ini` |

纯文本、纯本地。想备份就拷文件夹，想迁移就直接带走。

## ⚙️ 托盘菜单

右键托盘图标：

- ✏️ 记一笔（等同双击 Ctrl）
- 📂 打开今天的日记
- 🗂️ 打开日记目录
- ⚡ 开机自启开关
- ⚙️ 设置 —— 热键模式与双击判定窗口、备用组合键、日记目录、主题、提示音
- ❌ 退出

## 🛠️ 从源码构建

需要 .NET SDK（项目目标框架 `net48`）。

```bash
git clone https://github.com/Achu1024/MuseSlice.git
cd MuseSlice

# 只编译程序
dotnet build MuseSlice.sln -c Release
# 产物：bin/MuseSlice/Release/MuseSlice.exe

# 打安装包（需要 Inno Setup 6：winget install JRSoftware.InnoSetup）
powershell -ExecutionPolicy Bypass -File build-installer.ps1
# 产物：dist/MuseSlice-Setup-0.1.0.exe
```

### 项目结构

```
src/MuseSlice.Core/       核心逻辑：存储、搜索、几何、配置
src/MuseSlice/            WinForms 界面：输入条、托盘、设置
src/MuseSlice.SelfCheck/  自检用例，不依赖测试框架
installer/                Inno Setup 安装脚本
tools/make_icon.py        从品牌图生成多尺寸 .ico
```

改完代码跑一遍自检：

```bash
dotnet run --project src/MuseSlice.SelfCheck -c Release
```

## 📄 许可证

本项目采用开源许可证，欢迎提交 Issue 和 PR。

---

**设计理念**：好的笔记工具应该让你忘记它的存在，只在需要时瞬间出现。
