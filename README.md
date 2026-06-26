# KSP Launch Countdown - KSP1 发射倒计时模组

[![KSP Version](https://img.shields.io/badge/KSP-1.12x-blue)](https://www.kerbalspaceprogram.com/)
[![.NET Framework](https://img.shields.io/badge/.NET-4.7.2-purple)](https://docs.microsoft.com/en-us/dotnet/framework/)
[![License](https://img.shields.io/badge/License-GPL%20v3-blue)](./LICENSE)

## 📖 项目简介

**KSP Launch Countdown** 是一个为 Kerbal Space Program (KSP) 1 开发的游戏模组，提供专业的发射倒计时功能。该模组能够模拟真实航天发射的倒计时流程，包括倒计时语音播报、自动执行发射序列（开启SAS、设置满油门、分级点火）等操作。

### ✨ 核心特性

- **🎯 专业倒计时流程**：完整的发射倒计时序列，模拟真实航天发射程序
- **🔊 多语音包支持**：内置多种预设语音包（长征一号、长征二号F、土星五号、航天飞机、星舰、西昌、文昌），支持自定义扩展
- **🎵 双音频模式**：
  - 单段模式：一个音频文件完成整个倒计时
  - 多段模式（p1/p2）：p1播放倒计时，分级后继续播放p2（如"点火"、"升空"语音）
- **⚙️ 智能发射控制**：
  - 自动开启SAS稳定系统（最多尝试3次，失败后根据电量判断MJ控制或停电）
  - 自动设置并保持满油门
  - 支持标准分级和"先启动发动机再分离"两种模式
  - 智能识别发动机已点火状态（即使油门为0），倒计时期间保持0油门，音频结束后按需分级
- **🎨 友好用户界面**：
  - IMGUI风格的控制菜单
  - ApplicationLauncher工具栏集成
  - Ctrl+L快捷键快速切换
- **🔊 音量控制**：菜单内音量滑块，0%~100% 实时调节并自动保存
- **🌍 多语言支持**：内置简体中文、英文、俄文界面文本
- **🛡️ 发射前安全检查**：
  - 自动检查发射台状态、倒计时冲突、发动机状态
  - 电量检查：电量低于总电量 5% 时拒绝发射
  - SAS 智能判断：尝试 3 次开启 SAS，失败后根据电量区分 MJ 控制（继续执行）或停电（中止）
  - 未通过时提供强制发射选项
- **📦 易于扩展**：模块化设计，支持添加自定义语音包和配置

## 🚀 功能演示

### 发射流程示例（单段音频模式）

```
时间轴：
T-0s   ┃ 点击Launch按钮
       ┃ ↓
T+0s   ┃ 隐藏游戏UI → 开启SAS → 设置满油门
       ┃ ↓
T+0.1s ┃ 开始播放倒计时语音（如"10、9、8...1、点火"）
       ┃ ↓
T+Xs   ┃ 音频播放结束 → 执行分级（分离+启动发动机）
       ┃ [若启用"先启动发动机再分离"]
       ┃ 等待延迟（可配置）→ 第二次分级
       ┃ ↓
T+X+3s ┃ 恢复游戏UI → 释放油门保持 → 完成✓
```

### 发射流程示例（多段音频模式）

```
时间轴：
T-0s   ┃ 点击Launch按钮
       ┃ ↓
T+0s   ┃ 隐藏游戏UI → 开启SAS → 设置满油门
       ┃ ↓
T+0.1s ┃ 播放p1音频（倒计时部分："10、9、8...1"）
       ┃ ↓
T+Xs   ┃ p1结束 → 执行第一次分级
       ┃ ↓
T+Xs   ┃ 播放p2音频（点火后部分："点火！升空！"）
       ┃ [若启用"先启动发动机再分离"]
       ┃ p2开始后等待延迟 → 第二次分级
       ┃ ↓
T+Ys   ┃ p2结束 → 等待3秒
       ┃ ↓
T+Y+3s ┃ 恢复游戏UI → 完成✓
```

## 📁 项目结构

```
KSP Launch Countdown/
├── src/                                    # 源代码目录
│   └── KSPLaunchCountdown/
│       ├── KSPLaunchCountdownMod.cs        # 🔷 模组入口类（主控制器）
│       ├── CountdownController.cs          # ⏱️ 倒计时控制器（核心逻辑）
│       ├── CountdownMenu.cs                # 🖥️ 倒计时菜单UI
│       ├── LaunchSequence.cs               # 🚀 发射序列执行器
│       ├── PresetManager.cs                # 📦 预设语音包管理器
│       ├── AudioPlayer.cs                  # 🔊 音频播放器
│       ├── ToolbarButton.cs                # 🔘 工具栏按钮管理
│       ├── KSPApiHelper.cs                 # 🔧 KSP API辅助类
│       └── KSPLaunchCountdown.csproj       # 📋 项目配置文件
│
├── GameData/KSPLaunchCountdown/            # 🎮 游戏资源目录（部署到KSP）
│   ├── Lauch Voice/                        # 🎤 语音包目录
│   │   ├── LM-1(70s Jiuquan)/              #    长征一号（70年代酒泉）语音包
│   │   │   ├── LM-1(70s Jiuquan).ogg       #    单段音频
│   │   │   └── preset.cfg                  #    配置文件
│   │   ├── LM-2F(Jiuquan)/                 #    长征二号F（酒泉）语音包
│   │   │   ├── LM-2F(Jiuquan).ogg          #    单段音频
│   │   │   └── preset.cfg
│   │   ├── Saturn V/                       #    土星五号语音包
│   │   ├── Space shuttle/                  #    航天飞机语音包（多段音频）
│   │   │   ├── Space shuttle-p1.ogg        #    p1: 倒计时部分
│   │   │   ├── Space shuttle-p2.ogg        #    p2: 点火后部分
│   │   │   └── preset.cfg
│   │   ├── Starship/                       #    星舰语音包（多段音频）
│   │   │   ├── Starship-p1.ogg             #    p1: 倒计时部分
│   │   │   ├── Starship-p2.ogg             #    p2: 点火后部分
│   │   │   └── preset.cfg
│   │   ├── Wenchang/                       #    文昌发射场语音包
│   │   │   ├── Wenchang.ogg                #    单段音频
│   │   │   └── preset.cfg
│   │   └── Xichang/                        #    西昌发射场语音包
│   │       ├── Xichang.ogg                 #    单段音频
│   │       └── preset.cfg
│   ├── Localization/                       # 🌍 多语言文件目录
│   │   ├── zh-cn.cfg                       #    简体中文
│   │   ├── en-us.cfg                       #    英文
│   │   └── ru-ru.cfg                       #    俄文
│   └── Textures/
│       └── icon.png                        # 🖼️ 工具栏图标 (38x38)
│
├── build/                                  # 📦 编译输出目录
│   └── Release/net472/
│       ├── KSPLaunchCountdown.dll          # 编译后的模组DLL
│       └── KSPLaunchCountdown.pdb          # 调试符号文件
│
├── readme/                                 # 📚 文档目录
│   └── 功能更新.md                         #    功能更新日志
│
├── LICENSE                                 # 📄 许可证文件
├── KSPLaunchCountdown.sln                  # Visual Studio解决方案
└── Classes.xml                             # KSP API参考文档
```

## 🛠️ 技术架构

### 系统架构图

```
┌─────────────────────────────────────────────────────────────┐
│                    KSPLaunchCountdownMod                     │
│                      （入口类 / 主控制器）                      │
├─────────────────────────────────────────────────────────────┤
│  ┌─────────────┐  ┌──────────────┐  ┌────────────────────┐  │
│  │PresetManager│  │ AudioPlayer  │  │  LaunchSequence    │  │
│  │  预设管理器  │  │  音频播放器   │  │  发射序列执行器     │  │
│  └──────┬──────┘  └──────┬───────┘  └────────┬───────────┘  │
│         │                │                    │              │
│         ▼                ▼                    ▼              │
│  ┌─────────────────────────────────────────────────────┐    │
│  │              CountdownController                     │    │
│  │              倒计时控制器（协调器）                     │    │
│  └────────────────────────┬────────────────────────────┘    │
│                           │                                  │
│                           ▼                                  │
│  ┌─────────────────────────────────────────────────────┐    │
│  │               CountdownMenu                          │    │
│  │               倒计时菜单UI                            │    │
│  └────────────────────────┬────────────────────────────┘    │
│                           │                                  │
│                           ▼                                  │
│  ┌─────────────────────────────────────────────────────┐    │
│  │               ToolbarButton                          │    │
│  │               工具栏按钮                              │    │
│  └─────────────────────────────────────────────────────┘    │
└─────────────────────────────────────────────────────────────┘
         │
         ▼
┌─────────────────────────────────────────────────────────────┐
│                       KSPApiHelper                          │
│                  KSP API辅助类（反射调用）                     │
└─────────────────────────────────────────────────────────────┘
```

### 核心模块说明

| 模块 | 文件 | 职责 |
|------|------|------|
| **KSPLaunchCountdownMod** | `KSPLaunchCountdownMod.cs` | 模组主入口，初始化所有子模块，管理生命周期 |
| **CountdownController** | `CountdownController.cs` | 倒计时核心逻辑，协调音频播放和发射序列的时序 |
| **CountdownMenu** | `CountdownMenu.cs` | IMGUI界面，提供预设选择、设置选项和发射控制按钮 |
| **LaunchSequence** | `LaunchSequence.cs` | 执行飞船操作：SAS、油门控制、分级激活 |
| **PresetManager** | `PresetManager.cs` | 扫描和管理语音包预设，加载配置文件 |
| **AudioPlayer** | `AudioPlayer.cs` | 加载和播放.ogg音频文件，支持异步回调 |
| **ToolbarButton** | `ToolbarButton.cs` | 管理ApplicationLauncher工具栏按钮 |
| **KSPApiHelper** | `KSPApiHelper.cs` | 封装KSP API调用，处理反射和兼容性问题 |
| **SettingsManager** | `SettingsManager.cs` | 管理全局设置（音量）的加载和保存 |
| **Localization** | `Localization.cs` | 多语言文本加载和翻译 |
| **LaunchSafetyChecker** | `LaunchSafetyChecker.cs` | 发射前安全检查 |

## 📦 安装指南

### 方法一：手动安装（推荐）

1. **下载最新版本**
   - 从 [Releases](../../releases) 页面下载最新的 `KSPLaunchCountdown.zip`

2. **解压到KSP安装目录**
   ```
   Kerbal Space Program/
   └── GameData/
       └── KSPLaunchCountdown/     ← 将整个文件夹复制到这里
           ├── Lauch Voice/
           │   ├── LM-1(70s Jiuquan)/
           │   ├── LM-2F(Jiuquan)/
           │   ├── Saturn V/
           │   ├── Space shuttle/
           │   ├── Starship/
           │   ├── Wenchang/
           │   └── Xichang/
           ├── Textures/
           │   └── icon.png
           └── KSPLaunchCountdown.dll  ← 从 build/Release/net472/ 复制
   ```

3. **启动游戏**
   - 启动KSP，进入飞行场景
   - 在工具栏右侧找到模组图标（自定义图标）
   - 点击图标打开倒计时控制菜单

### 方法二：从源码编译

#### 前置要求

- **IDE**: Visual Studio 2019+ 或 JetBrains Rider
- **.NET SDK**: .NET Framework 4.7.2 开发包
- **KSP DLL**: 将KSP安装目录下的以下DLL复制到项目根目录的 `KSP DLL/` 文件夹：
  - `KSP_x64_Data/Managed/Assembly-CSharp.dll`
  - `KSP_x64_Data/Managed/Assembly-CSharp-firstpass.dll`
  - `KSP_x64_Data/Managed/KSPAssets.dll`
  - `KSP_x64_Data/Managed/UnityEngine.dll`
  - 以及其他Unity DLL（详见 `.csproj` 文件）

#### 编译步骤

```bash
# 1. 克隆仓库
git clone <repository-url>
cd "KSP Launch Countdown"

# 2. 打开解决方案
# 使用Visual Studio打开 KSPLaunchCountdown.sln

# 3. 还原NuGet包（如果有）
dotnet restore

# 4. 编译Release版本
dotnet build -c Release

# 5. 部署
# 将 build/Release/net472/KSPLaunchCountdown.dll 复制到
# KSP GameData/KSPLaunchCountdown/ 目录下
```

## 🎮 使用教程

### 基本使用流程

1. **进入飞行场景**
   - 在太空船装配间(VAB)建造好火箭
   - 发射到发射台（Launch Pad）

2. **打开倒计时菜单**
   - 方式一：点击工具栏上的模组图标
   - 方式二：按快捷键 `Ctrl + L`

3. **选择预设语音包**
   - 从下拉列表选择想要的倒计时语音：
     - **LM-1(70s Jiuquan)**: 长征一号（70年代酒泉）中文语音
     - **LM-2F(Jiuquan)**: 长征二号F（酒泉）中文语音
     - **Saturn V**: 土星五号英文语音
     - **Space shuttle**: 航天飞机英文语音（多段）
     - **Starship**: SpaceX星舰英文语音（多段）
     - **Wenchang**: 文昌发射场中文语音
     - **Xichang**: 西昌发射场中文语音

4. **配置选项（可选）**
   - ☑️ **先启动发动机再分离**：适用于需要先点火再分离的火箭
     - 正常流程：启用后会执行两次分级操作，中间有可配置的延迟时间
     - 若芯一级发动机已点火：倒计时期间保持0油门，音频结束后加满油门；
       若勾选此项，则按延迟执行分级；若未勾选，则不自动分级，由玩家手动控制
   - **音量滑块**：拖动调节倒计时语音音量（0%~100%），设置自动保存到当前存档

5. **开始倒计时**
   - 点击 **Launch** 按钮
   - 系统会自动执行发射前安全检查
   - 如果检查未通过（如不在发射台），窗口内会显示警告，勾选"强制发射"后可继续
   - 观看自动执行的发射序列！

6. **取消倒计时（如需要）**
   - 在倒计时过程中点击 **Cancel** 按钮
   - 或再次点击工具栏图标关闭菜单

### 高级功能

#### 自定义语音包

在 `GameData/KSPLaunchCountdown/Lauch Voice/` 目录下创建新文件夹：

```
Lauch Voice/
└── My Rocket/              ← 新语音包名称
    ├── My Rocket.ogg      ← 单段模式：完整倒计时语音
    └── preset.cfg         ← 可选配置文件
```

**多段音频格式：**

```
Lauch Voice/
└── My Rocket/
    ├── My Rocket-p1.ogg   ← p1: 倒计时部分（"10, 9, 8... 1"）
    ├── My Rocket-p2.ogg   ← p2: 分级后部分（"Ignition! Liftoff!"）
    └── preset.cfg
```

**preset.cfg 配置文件格式：**

```ini
COUNTDOWN_PRESET
{
    // 单段模式下第二次分级的延迟时间（秒）
    // 仅当勾选"先启动发动机再分离"时生效
    singleStageDelay = 2.0

    // 多段模式下第二次分级的延迟时间（秒）
    // p2开始播放后等待多久执行第二次分级
    multiStageDelay = 0.3
}
```

> **注意**：`startEngineBeforeSeparation` 选项仅在UI上勾选控制，不写入配置文件。因为不同火箭的分级模式各不相同，此选项应由玩家根据当前火箭手动选择。

## ⚙️ 配置参数说明

### 全局参数（代码中定义）

| 参数 | 位置 | 默认值 | 说明 |
|------|------|--------|------|
| `UI_RESTORE_DELAY` | CountdownController.cs | 3.0秒 | 分级后等待恢复UI的延迟时间 |
| `audioSource.volume` | AudioPlayer.cs | 1.0 | 倒计时语音音量（0.0~1.0） |

### 存档级别参数（saves/<存档名>/KSPLaunchCountdown/Settings.cfg）

| 参数 | 类型 | 默认值 | 说明 |
|------|------|--------|------|
| `CountdownVolume` | float | 1.0 | 倒计时语音音量（0.0~1.0） |

### 预设级别参数（preset.cfg）

| 参数 | 类型 | 默认值 | 说明 |
|------|------|--------|------|
| `singleStageDelay` | float | 2.0秒 | 单段模式第二次分级延迟 |
| `multiStageDelay` | float | 0.3秒 | 多段模式第二次分级延迟 |

## 🔧 开发指南

### 技术栈

- **语言**: C# 9.0
- **目标框架**: .NET Framework 4.7.2
- **游戏引擎**: Unity 2019.4.x（KSP1内置）
- **构建工具**: MSBuild / dotnet CLI
- **UI框架**: Unity IMGUI (Immediate Mode GUI)

### 关键设计决策

#### 1. 分级方式：keybd_event 模拟空格键

**原因**：
- KSP使用Unity旧输入系统，会正常接收OS层面的键盘事件
- 这是最可靠的分级方式，走KSP完整的输入处理流程
- 相比直接调用API，能正确触发所有相关事件和动画

**实现位置**: [KSPApiHelper.cs#L50-L65](src/KSPLaunchCountdown/KSPApiHelper.cs#L50-L65)

```csharp
// 使用Windows API keybd_event模拟空格键输入
keybd_event(0x20, 0, 0, UIntPtr.Zero);   // 按下空格键
keybd_event(0x20, 0, 2, UIntPtr.Zero);   // 释放空格键
```

#### 2. 油门保持机制：OnFlyByWire 回调

**原因**：
- `FlightInputHandler.state.mainThrottle` 每帧会被重置
- 需要在 `OnFlyByWire` 回调中持续设置才能保持油门
- 这是KSP模组控制油门的标准做法

**实现位置**: [LaunchSequence.cs#L80-L95](src/KSPLaunchCountdown/LaunchSequence.cs#L80-L95)

#### 3. 工具栏按钮注册：直接调用 KSP.UI.Screens.ApplicationLauncher

**原因**：
- `ApplicationLauncher` 实际位于 `KSP.UI.Screens` 命名空间
- 参考 `StagingAndEngineConfig.cs.example` 的实现模式
- 直接调用真实的 `ApplicationLauncher.Instance.AddModApplication()` 和 `RemoveModApplication()`
- 不依赖 `GameEvents.onGUIApplicationLauncherReady` 事件
- 在每帧 `Update` 中检查 `ApplicationLauncher.Instance != null`，就绪后立即注册按钮
- 更可靠，不会因事件时序问题导致按钮丢失

**实现位置**: [ToolbarButton.cs#L81-L147](src/KSPLaunchCountdown/ToolbarButton.cs#L81-L147)

#### 4. 反射调用的必要性

**原因**：
- 大部分 KSP API 可直接引用本地 KSP DLL 中的类型
- 已补充 `UnityEngine.AnimationModule.dll` 引用，使 `ApplicationLauncherButton` 可直接编译
- 对于本地 DLL 中仍缺少的类型或方法（如 `GameEvents.onHideUI` 的 `Fire()`），使用反射

**直接调用的方法**:
- `KSP.UI.Screens.ApplicationLauncher.AddModApplication()` / `RemoveModApplication()`

**仍使用反射的方法**:
- `GameEvents.onHideUI.Fire()` / `GameEvents.onShowUI.Fire()`: 通过反射触发

### 添加新功能的步骤

1. **确定功能所属模块**
   - UI相关 → `CountdownMenu.cs`
   - 发射逻辑 → `CountdownController.cs` 或 `LaunchSequence.cs`
   - 音频相关 → `AudioPlayer.cs`
   - 预设管理 → `PresetManager.cs`

2. **遵循现有代码模式**
   - 使用协程（Coroutine）处理异步操作
   - 使用事件（Event/Delegate）实现模块间通信
   - 保持日志输出格式一致 `[KSPLaunchCountdown]`

3. **测试注意事项**
   - 必须在KSP飞行场景测试
   - 检查Debug日志确认执行流程
   - 测试取消操作的资源清理

## 🐛 故障排除

### 常见问题

#### Q1: 工具栏上没有看到模组图标

**可能原因**:
- DLL未正确放置到GameData目录
- 图标文件缺失或路径错误
- 场景切换后GameDatabase尚未就绪（模组会自动重试，一般几帧后恢复）

**解决方法**:
1. 确认 `KSPLaunchCountdown.dll` 在 `GameData/KSPLaunchCountdown/` 目录下
2. 确认 `icon.png` 在 `GameData/KSPLaunchCountdown/Textures/` 目录下
3. 检查KSP调试日志中的 `[KSPLaunchCountdown]` 信息
4. 如果日志显示"图标加载失败...将在下一帧重试"，属于正常现象，等待1~2秒即可
5. 持续失败时尝试重启游戏让GameDatabase重新扫描资源

#### Q2: 倒计时语音没有播放

**可能原因**:
- 音频文件格式错误（必须是.ogg）
- 音频文件路径不正确
- GameDatabase未扫描到音频文件

**解决方法**:
1. 确认音频文件为OGG Vorbis格式（.ogg扩展名）
2. 确认文件位于正确的语音包子目录
3. 查看KSP日志中的错误信息
4. 尝试重启游戏让GameDatabase重新扫描资源

#### Q3: 分级操作没有生效

**可能原因**:
- 当前不在飞行场景
- 没有活跃的飞船
- keybd_event被安全软件拦截

**解决方法**:
1. 确认已在发射台且火箭已准备就绪
2. 检查是否有其他程序拦截键盘模拟
3. 尝试手动按空格键确认KSP响应正常

#### Q4: 编译时报错找不到KSP类型

**可能原因**:
- KSP DLL未正确引用
- 缺少必要的Unity模块DLL

**解决方法**:
1. 确认 `KSP DLL/` 目录包含所有必需的DLL
2. 检查 `.csproj` 文件中的引用路径是否正确
3. 确保DLL版本与KSP安装版本匹配

### 日志查看

KSP模组的所有日志都带有 `[KSPLaunchCountdown]` 前缀，可在以下位置查看：

- **Windows**: `%APPDATA%/Unity/Logs/Player.log`
- **macOS**: `~/Library/Logs/Unity/Player.log`
- **Linux**: `~/.config/unity3d/Squad/Kerbal Space Program/Player.log`

使用日志过滤器搜索：
```
[KSP Launch Countdown]
```

## 📊 测试用例

### 正常发射流程测试

| 用例编号 | 输入条件 | 操作 | 预期结果 |
|---------|---------|------|---------|
| TC-01 | 飞行场景，有活跃飞船，选择LM-1(70s Jiuquan)预设 | 点击Launch | 隐藏UI→开SAS→满油门→播放LM-1(70s Jiuquan).ogg→分级→恢复UI |
| TC-02 | 飞行场景，选择Starship预设（多段） | 点击Launch | 隐藏UI→开SAS→满油门→播放p1→分级→播放p2→恢复UI |
| TC-03 | 选择Space shuttle预设（多段） | 点击Launch | 隐藏UI→开SAS→满油门→播放p1→分级→播放p2→恢复UI |
| TC-04 | 火箭电量 < 5% | 点击Launch | 安全检查失败，显示警告，禁止发射（除非勾选强制发射） |
| TC-05 | 不在发射台 | 点击Launch | 安全检查失败，显示警告 |
| TC-06 | 芯一级发动机已点火，未勾选"先启动发动机再分离" | 点击Launch | 倒计时期间0油门→音频结束后加满油门→不自动分级 |
| TC-07 | 芯一级发动机已点火，勾选"先启动发动机再分离" | 点击Launch | 倒计时期间0油门→音频结束后加满油门→按延迟分级 |
| TC-08 | 倒计时进行中 | 点击Cancel | 倒计时取消，UI恢复 |
| TC-09 | SAS被MechJeb控制且电量正常 | 点击Launch | 尝试3次开启SAS失败，判断为MJ控制，继续正常分级 |
| TC-10 | SAS无法开启且电量 < 5% | 点击Launch | 尝试3次开启SAS失败，判断为停电，中止发射 |

### 取消与边界测试

| 用例编号 | 输入条件 | 操作 | 预期结果 |
|---------|---------|------|---------|
| TC-11 | 倒计时进行中 | 点击Cancel | 停止音频→释放油门→立即恢复UI |
| TC-12 | 未选择预设 | 点击Launch | 无反应，控制台警告"无可用预设" |
| TC-13 | 不在飞行场景 | 尝试开始倒计时 | 控制台警告"不在飞行场景" |
| TC-14 | 快捷键Ctrl+L | 按下 | 切换菜单显示/隐藏状态 |
| TC-15 | 多次快速点击Launch | 连续点击 | 只有第一次有效，后续忽略（防重复） |
| TC-16 | 场景切换时倒计时进行中 | 切换到太空中心 | 自动清理资源，无异常 |

## 📈 版本历史

详见 [readme/功能更新.md](./readme/功能更新.md)

### 主要里程碑

- **2026-06-21**: v0.1.0 - 实现发射倒计时核心功能
  - 单段/多段音频支持
  - 完整发射序列（SAS、油门、分级）
  - 预设管理系统
  - UI菜单和工具栏集成

- **2026-06-21**: v0.0.1 - 初始化开发环境
  - 项目结构搭建
  - KSP DLL引用配置
  - 基础模组框架

## 🤝 贡献指南

我们欢迎社区贡献！请遵循以下步骤：

1. **Fork 本仓库**
2. **创建特性分支** (`git checkout -b feature/AmazingFeature`)
3. **提交更改** (`git commit -m 'Add some AmazingFeature'`)
4. **推送到分支** (`git push origin feature/AmazingFeature`)
5. **提交Pull Request**

### 代码规范

- 所有代码注释使用中文
- 每个文件开头必须包含详细的功能说明注释
- 遵循现有的命名约定和代码风格
- 新增功能需要在 `readme/功能更新.md` 中记录

## 📄 许可证

本项目基于 [GPL v3 License](./LICENSE) 开源。

```
GNU GENERAL PUBLIC LICENSE
Version 3, 29 June 2007

Copyright (C) 2026 KSP Launch Countdown Contributors

This program is free software: you can redistribute it and/or modify
it under the terms of the GNU General Public License as published by
the Free Software Foundation, either version 3 of the License, or
(at your option) any later version.

This program is distributed in the hope that it will be useful,
but WITHOUT ANY WARRANTY; without even the implied warranty of
MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the
GNU General Public License for more details.
```

## 🙏 致谢

- **KSP Community** - 提供了优秀的游戏平台和活跃的模组开发生态
- **MechJeb** - 提供了工具栏按钮实现的参考模式
- **KSP API Documentation Project** - 提供了详尽的API文档（Classes.xml）
- **Unity Technologies** - 提供了强大的游戏引擎

## 📞 联系方式

- **问题反馈**: 请通过 [Issues](../../issues) 提交
- **功能建议**: 请通过 [Discussions](../../discussions) 讨论
- **开发文档**: 查看 [.trae/documents/ksp-launch-countdown-plan.md](.trae/documents/ksp-launch-countdown-plan.md)

---

<div align="center">

**Made with ❤️ for Kerbal Space Program**

*为坎巴拉太空计划打造的专业发射体验*

</div>
