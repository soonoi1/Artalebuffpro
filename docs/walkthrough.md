# 找王辅助工具开发总结与使用说明 (Walkthrough)

找王辅助小工具已顺利开发并成功编译为独立的 `.exe` 程序，同时也对悬浮窗 UI 进行了大幅度的美化升级，使其拥有极佳的游戏 HUD 视觉表现。

---

## 交付文件清单

1. **编译后的独立可执行文件**：[ArtaleBossHelper.exe](file:///C:/Users/suhao/.gemini/antigravity/scratch/artale_boss_helper/dist/ArtaleBossHelper.exe)
   - 已将 Python 解释器、Tkinter 图形界面、keyboard 模块、Windows OCR 组件和 OpenCV 预处理库打包为单一 `.exe` 文件。
   - **双击即可直接运行**，无需安装 Python 环境或配置任何库文件！
2. **主程序源码**：[artale_boss_helper.py](file:///C:/Users/suhao/.gemini/antigravity/scratch/artale_boss_helper/artale_boss_helper.py)
   - 精细设计的窗口定位、防重记算法和升级版 UI 的 Python 源码。
3. **单元测试与逻辑校验脚本**：[test_helper_logic.py](file:///C:/Users/suhao/.gemini/antigravity/scratch/artale_boss_helper/test_helper_logic.py)
4. **用户指南**：[README.md](file:///C:/Users/suhao/.gemini/antigravity/scratch/artale_boss_helper/README.md)

---

## UI 视觉设计升级

为了让辅助工具的视觉感更加精致，并与《冒险岛阿尔泰服》的游戏界面完美融合，我们对 UI 进行了全面重构：

- **暗黑极客质感卡片**：使用 `#18181b`（深碳黑）作为窗口主体卡片，边缘采用 `1.5px` 粗细的霓虹科技线描边。
- **动态呼吸渐变霓虹色**：悬浮窗的主题色彩会根据当前的状态进行实时智能切换：
  - **默认状态 (< 10 只)**：使用**冰晶蓝** (`#00D2FF`) 描边和文字。
  - **蓄势待发 (>= 10 只)**：使用**荧光绿** (`#39FF14`) 描边和文字，提醒您 Boss 即将刷新。
  - **Boss 降临警告**：使用**霓虹红** (`#FF3131`) 描边，且主警告字样 `⚠️ BOSS SPAWNED! ⚠️` 会以 `400ms` 的频率**红白交替高频闪烁**，保证您第一眼能看见！
- **可视化动态进度条**：卡片中内置了一个精致的水平进度条（横条），随着击杀怪物数量从 0 累加至 10，进度条会像游戏血条一样向右推满，色彩与当前状态同步。
- **状态指示灯**：卡片右上角显示连接状态。未打开游戏时显示黄点 `● 未检测到游戏`，正常工作时显示绿点 `● 监测运行中`，暂停时显示橙点 `● 监测已暂停`。
- **小字脚标提示**：底部显示当前热键的快捷备忘，以及窗口锁定的安全提示，防止您忘记热键。

---

## 验证与测试结果

我们对编译出的 `ArtaleBossHelper.exe` 进行了冷启动和运行监控测试：

- **进程成功载入与执行**：
  在 Windows 交互桌面下成功调用了 `ArtaleBossHelper.exe --mock` 进行仿真测试，系统成功拉起两个对应进程（主程序及解析进程），并在后台顺利运行，未产生任何缺少 DLL 或 WinRT 环境的兼容性崩溃。
  ```
  Handles  NPM(K)    PM(K)      WS(K)     CPU(s)     Id  SI ProcessName
  -------  ------    -----      -----     ------     --  -- -----------
       97       8     1744       7608       2.38  53084   1 ArtaleBossHelper
      141      15    24300      32928       0.50 180008   1 ArtaleBossHelper
  ```

---

## 玩家使用指南

### 1. 运行方式
- 推荐直接双击运行 [ArtaleBossHelper.exe](file:///C:/Users/suhao/.gemini/antigravity/scratch/artale_boss_helper/dist/ArtaleBossHelper.exe)。
- 或者在终端运行测试模式（仿真演示）：
  ```bash
  ArtaleBossHelper.exe --mock
  ```

### 2. 热键与操作说明
1. **摆放位置 (首次使用)**：默认悬浮窗会显示在屏幕左上角。
   - 按下 **`F9`** 解锁位置。
   - 此时悬浮窗会显示出背景底色，并且右下角显示 `[已解锁 - 拖拽定位]`。
   - 您可以直接**按住悬浮窗的任意位置进行拖拽**，将其挪动到您喜欢的游戏界面角落。
   - 移动完毕后，再次按下 **`F9`** 锁定。此时背景会消失（仅保留悬浮窗卡片本身），并自动恢复**鼠标完全穿透**模式。
2. **打怪与换线**：
   - 击杀小怪时，悬浮窗数字和进度条会自动增长。
   - 换线时，按下 **`F8`**，计数器自动重置为 `0 / 10`，方便您开启新一轮的计数。
3. **暂停监测**：
   - 如果您需要暂时切出游戏挂机或者做别的事情，可以按下 **`F10`** 暂停 OCR 截屏，防止误判。
