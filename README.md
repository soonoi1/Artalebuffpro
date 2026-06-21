# 枫之谷大合集 Buff/巡逻辅助助手 (ArtaleProBuff)

这是一款专门为《枫之谷》及《MapleStory Worlds》（如 Artale 模式）开发的高级自动化 Buff 轮播与巡逻挂机辅助小工具。项目基于 **.NET 8.0 WPF** 开发，深度整合了 Windows 原生 API 模拟输入、WinRT 硬件加速画面抓取与 OCR 识别，并集成了基于 RSA 非对称加密的机器码授权激活机制。

---

## 📖 核心功能特性

1. **自动 Buff 轮播与混合按键**
   - 支持自定义多组按键（如 F1-F12、A-Z 等），独立配置触发周期与随机波动时间。
   - 支持常规单点模式及长按模式，确保不同技能释放的时长要求。
   - 独占（互斥）按键：触发时自动暂停其他常规按键的执行。

2. **巡逻挂机移动机制**
   - **多组顺序轮播**：支持配置多组不同的移动策略（如左右移动时长、中途停留时间、组后间隔）。
   - **定时循环组 (抢占/强制打断)**：可将任意组别设置为“定时循环”（可配置间隔秒数与重复执行次数）。当到达设定时刻时，系统会**强制替换/打断**当前正在运行的常规动作（并在打断瞬间安全释放方向按键防卡死），执行完该定时组的设定次数后，自动返回常规第一组继续循环。
   - **移动技能互斥**：支持巡逻移动时临时暂停其他技能按键触发的可选项，避免技能硬直影响走位。
   - **随机波动防检测**：所有的移动与停顿时间均支持随机百分比波动，模拟人工真实操作。

3. **智能经验值监测 (防符文/静止检测)**
   - **手动划区截图**：用户可手选游戏视窗中的 EXP 经验条区域。
   - **硬件加速兼容 (DWM Capture)**：引入 Win32 `PrintWindow` 捕获机制，专门应对 Unity 引擎硬件加速导致的画面静止问题。
   - **3倍图像缩放超分**：在 OCR 前自动将图片放大 3 倍，完美避开 Windows 原生 OCR 引擎的长宽 40px 物理大小限制，实现 100% 准确度。
   - **时速分析**：自动统计每分钟及每 10 分钟经验增长率，并显示本次运行的累计增长值。
   - **超时保护**：可设置判定阈值（秒）。若超时无经验变化，可执行“全局按键暂停”或“强制物理关闭游戏窗口”。

4. **安全保护与保密机制**
   - **机器码绑定 (HWID)**：软件在启动时自动读取系统 MachineGuid 并结合 SHA256 算法生成全球唯一的 16 位机器码。
   - **RSA 非对称加密激活**：用户须提供机器码，由作者利用专用“激活码生成器”（使用 RSA 私钥签名）生成对应的激活码。客户端内置 RSA 公钥进行非对称签名校验，杜绝逆向破解与二次分发传播。

---

## 🚀 软件安装与运行指南

### 1. 版本选择与环境要求
- **无需激活版 (`ArtaleProBuff_无需激活版.exe`)**：双击即可直接使用，跳过了任何激活码和机器码的校验逻辑，方便内部测试或信任分发。
- **安全激活版 (`ArtaleProBuff.exe`)**：需要绑定用户机器码，配合激活码生成器方可运行，适用于公开分发和权限受限场景。
- **环境要求**：
  - **操作系统**：Windows 10 / Windows 11 (以支持 WinRT DWM 截图组件)
  - **运行时环境**：[.NET 8.0 Desktop Runtime](https://dotnet.microsoft.com/download/dotnet/8.0) (若您电脑未安装，运行程序时会自动引导下载安装)

### 2. 安全激活版激活步骤 (如果您分发的是 `ArtaleProBuff.exe`)
1. 双击打开根目录下的 **[ArtaleProBuff.exe](file:///d:/VSC/HHH/ArtaleProBuff.exe)**。
2. 若是首次启动，程序会弹出“🔑 软件激活”窗口。
3. 点击“**复制**”按钮获取您的“机器码”。
4. 将机器码发送给软件作者，作者通过唯一的 **[ActivationGenerator.exe](file:///d:/VSC/HHH/ActivationGenerator.exe)** 生成对应的激活码（作者在本地保留此生成器即可，无需发给普通用户）。
5. 在软件输入框中粘贴激活码，点击“**激活软件**”即可完成绑定！

---

## 🛠️ 开发者开发指南 & 技术路径

项目源码结构清晰、轻量化，旨在为后续继续优化的 Agent 或其他 AI 辅助大模型提供友好的调整路径：

### 1. 核心技术架构 (Technical Stack)
- **框架**：WPF (Windows Presentation Foundation) / .NET 8.0 (C#)
- **UI库**：`WPF-UI` (Modern styling & Mica background)
- **窗口过滤**：通过 Windows `EnumWindows` 和 `GetWindowText` 动态刷新匹配活动的游戏视窗。
- **按键模拟**：
  - **前台模式**：使用 Windows `SendInput` API。
  - **后台模式**：使用 Win32 `PostMessage` 将物理扫描码发送给特定子窗口，不抢占鼠标键盘焦点的同时保证防检测静默模拟。
- **图像捕捉**：通过 `PrintWindow(hwnd, memDC, PW_CLIENTONLY | PW_RENDERFULLCONTENT)` 从 DWM 重定向重构客户区渲染层。
- **OCR 模块**：通过 native 桥接 `Windows.Media.Ocr.OcrEngine`，无需依赖任何臃肿的第三方本地 OCR 库。

### 2. 授权激活设计逻辑 (Licensing Architecture)
- **ActivationGenerator 项目**：
  - 位于 [ActivationGenerator](file:///d:/VSC/HHH/ActivationGenerator/) 目录中。
  - 内置唯一的 RSA 私钥（XML 字符串）。
  - 读取用户机器码、有效期后，计算生成包含 16 字节 Payload (MachineCode + ExpirationDate) 和 128 字节数字签名的 144 字节二进制数据，转为 Base64 后输出。
- **客户端校验**：
  - 位于 [LicenseManager.cs](file:///d:/VSC/HHH/LicenseManager.cs) 中。
  - 内置唯一的 RSA 公钥。
  - 启动时通过 `App.xaml.cs` 调用 `LicenseManager.IsActivated()`，检验本地 `license.lic` 是否存在、RSA 签名是否完好、HWID 是否匹配、是否在有效期内。

### 3. 项目编译与打包发布说明
- **发布生成主程序**（框架依赖单文件，体积约 10MB）：
  ```powershell
  dotnet publish -c Release -r win-x64 --self-contained false -p:PublishSingleFile=true
  ```
  生成路径：`bin\Release\net8.0-windows10.0.19041.0\win-x64\publish\ArtaleProBuff.exe`
  
- **发布生成激活码生成器**：
  ```powershell
  dotnet publish ActivationGenerator/ActivationGenerator.csproj -c Release -r win-x64 --self-contained false -p:PublishSingleFile=true
  ```
  生成路径：`ActivationGenerator\bin\Release\net8.0\win-x64\publish\ActivationGenerator.exe`

---

## 📈 维护与优化方向

未来需要由 Agent 或 AI 大模型接手进行的改进方向：
1. **OCR 数值纠错升级**：增强 `SanitizeOcrText` 的正则清洗规则，目前已针对 `l`/`i` -> `1`, `o` -> `0` 做了上下文邻接过滤，可以扩展对特殊字体的过滤。
2. **多开窗口隔离**：将后台绑定的 `_cachedBgHwnd` 扩展为多进程列表，实现一台电脑多开游戏窗口独立挂机和独立监控。
3. **云激活服务整合**：如果不再满足于单机 HWID RSA 离线校验，可以将 `LicenseManager` 的 `VerifyActivationCode` 替换为发起 HTTP 网络请求校验，实现远程封禁激活码、封禁用户机器码等功能。
