<div align="center">
  <img src="icon/MuseSlice_logo.svg" alt="MuseSlice Logo" width="400"/>
  
  # MuseSlice
  
  > 💡 轻量级快速笔记工具 - 随时捕获灵光一现的想法
</div>

## ✨ 核心特性

**极致轻量** - 无需打开完整应用，全局热键瞬间唤出输入栏
- 🚀 常驻系统托盘，占用资源极少
- ⚡ 纯追加式写入，毫秒级响应
- 📦 单文件启动，开箱即用

**便捷高效** - 专注于快速记录，摒弃一切繁琐操作
- ⌨️ 双击 `Ctrl` 快速唤出输入栏
- 📝 输入即保存，无需手动操作
- 📅 自动按日期整理为 Markdown 文件
- ✅ 智能识别待办事项格式

## 🎯 使用场景

- **灵感捕获**：写代码时突然有个想法？按下热键，记录，继续工作
- **快速备忘**：会议中需要记个事项？唤出输入栏，一秒搞定
- **待办管理**：每行自动转换为待办事项，支持勾选框语法

## 🚀 快速开始

1. 下载并运行 `MuseSlice.exe`
2. 程序自动常驻系统托盘
3. **双击 `Ctrl` 键**唤出输入栏
4. 输入内容后按 `Enter` 保存，按 `Esc` 关闭

## 📁 笔记存储

- 默认保存位置：`我的文档\MuseSlice`
- 文件格式：Markdown（`.md`）
- 命名规则：`YYYY-MM-DD.md`（按日期自动分类）
- 每条记录自动添加时间戳

## ⚙️ 配置选项

右键系统托盘图标可以：
- 📂 打开今日笔记
- 🗂️ 打开笔记文件夹
- ⚡ 切换开机自启动
- 🎨 自定义热键和界面

## 🛠️ 技术栈

- **框架**：.NET / WinForms
- **语言**：C#
- **架构**：轻量化设计，三层结构
  - `MuseSlice.Core`：核心逻辑
  - `MuseSlice`：UI 界面
  - `MuseSlice.SelfCheck`：自检测试

## 💻 开发构建

```bash
# 克隆项目
git clone https://github.com/Achu1024/MuseSlice.git

# 编译项目
dotnet build MuseSlice.sln -c Release

# 运行程序
dotnet run --project src/MuseSlice
```

## 📄 许可证

本项目采用开源许可证，欢迎贡献代码和反馈建议。

---

**设计理念**：好的笔记工具应该让你忘记它的存在，只在需要时瞬间出现。MuseSlice 就是这样一个工具 —— 轻量、快速、不打扰。
