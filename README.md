<div align="center">
  <img src="icon/MuseSlice_logo.svg" alt="MuseSlice Logo" width="400"/>

  # MuseSlice

  > 💡 轻量级快速笔记工具 —— 双击 Ctrl，把一闪而过的念头留下来
</div>

## ✨ 为什么是 MuseSlice

记一笔的成本，决定了你会不会真的去记。MuseSlice 把这条链路压到最短：**双击 Ctrl → 打字 → Enter**，结束。

**极致轻量**

- 📦 安装包不到 1 MB，常驻托盘，不吃内存不占屏
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
