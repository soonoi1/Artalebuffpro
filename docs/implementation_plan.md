# 冒险岛阿尔泰亚服找王辅助工具设计方案 (MapleStory Artale Boss-Hunting Helper)

该小工具旨在帮助玩家在《冒险岛阿尔泰（Artale）》中自动统计击杀怪物数量，并在 Boss 刷新时进行醒目提示。

## 核心功能

1. **游戏窗口定位**：自动查找“MapleStory Worlds-Artale”游戏窗口，无论其移动或缩放，都精确确定其客户端区域。
2. **实时 OCR 检测**：
   - 每隔 0.5 秒截取游戏窗口的战斗日志区域（右下角）及 Boss 提示区域（屏幕中央）。
   - 使用 Windows 10/11 内置的高性能本地 OCR 引擎进行文字识别，识别“已獲得經驗值”（统计击杀）和“能感受到邪惡的氣息。”（Boss 提示）。
3. **智能去重计数**：
   - 根据 OCR 结果的每行文字及其垂直位置（Y坐标）进行运动追踪，过滤掉滚动和重复识别的旧日志，确保击杀数量不漏记、不重记。
4. **游戏悬浮窗 (Overlay)**：
   - 采用 Tkinter 制作的无边框、半透明、始终置顶的悬浮窗，在游戏画面上实时显示当前击杀数。
   - **锁定模式（默认）**：鼠标点击穿透，完全不影响游戏操作。
   - **设置模式**：可拖动改变悬浮窗位置。
   - **计数颜色**：未满 10 只显示默认颜色；满 10 只变成绿色；Boss 刷新变成红色。
5. **快捷键支持**：
   - `F8`：重置计数器（并清除 Boss 警告状态），换线时一键归零。
   - `F9`：切换锁定/解锁悬浮窗位置。
   - `F10`：暂停/恢复监测。

---

## 用户确认事项 / User Review Required

> [!IMPORTANT]
> **Windows OCR 语言包依赖**
> 本工具使用 Windows 内置的 OCR 接口，默认使用系统语言。
> 阿尔泰服游戏为繁体中文。经测试，在简体中文 Windows 系统上，内置的 `zh-Hans` (简体中文) 引擎能非常完美且高精度地识别出繁体字“獲”、“惡”、“氣”等，无需额外安装繁体中文语言包。
> 如果后续运行中发现漏记，我们可以通过 PowerShell 命令一键安装繁体中文 OCR 语言包。

---

## 开放性问题 / Open Questions

> [!NOTE]
> **悬浮窗默认位置**
> 悬浮窗默认会在游戏窗口的左上角附近生成，您可以通过按 `F9` 解锁它，然后用鼠标拖动到屏幕上的任意位置（例如游戏血条上方或空闲区域），再次按 `F9` 锁定并启用点击穿透。您对这种交互设计是否满意？

---

## 拟作出的更改 / Proposed Changes

### 项目目录结构

我们将新建一个项目目录：`C:\Users\suhao\.gemini\antigravity\scratch\artale_boss_helper\`，并在其中创建以下文件：

#### [NEW] [artale_boss_helper.py](file:///C:/Users/suhao/.gemini/antigravity/scratch/artale_boss_helper/artale_boss_helper.py)
主程序文件，包含游戏窗口定位、屏幕截图与预处理、OCR 触发逻辑、计数过滤算法、Tkinter 悬浮窗 UI 和全局热键注册。

#### [NEW] [README.md](file:///C:/Users/suhao/.gemini/antigravity/scratch/artale_boss_helper/README.md)
用户使用说明书，介绍悬浮窗状态指示、快捷键用法和常见问题排查。

---

## 验证计划 / Verification Plan

### 自动化与仿真测试
- 在无游戏运行的情况下，使用之前提取的截图（`uploaded_image_0_1781783191456.jpg` 和 `uploaded_image_1_1781783191456.png`）运行模拟测试，验证 OCR 能够正确识别“已獲得經驗值”和“能感受到邪惡的氣息”，并正确计数和改变悬浮窗颜色。

### 手动测试
- 启动游戏，运行脚本，手动击杀小怪，确认计数器累加。
- 确认换线后按 `F8` 能正常复位。
- 确认 Boss 出现时屏幕变红。
