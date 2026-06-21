# KSP1 发射倒计时系统 - 实施计划

## 概述

在KSP1飞行场景的工具栏添加模组按钮，点击后弹出菜单，支持选择倒计时Preset（预设语音包），选择后点击Launch执行自动发射序列：开启SAS → 满油门 → 隐藏UI → 播放倒计时音频 → 音频结束后分离一级启动发动机 → 3秒后恢复UI。

## 当前状态分析

- **项目骨架**：已有 `KSPLaunchCountdownMod.cs` 入口类，仅含日志输出，`[KSPAddon(KSPAddon.Startup.MainMenu, false)]`
- **音频资源**：`Lauch Voice/DFH-1/DFH-1.ogg`（单个完整倒计时音频文件）
- **项目引用**：已包含所有必要的KSP和Unity DLL引用（AudioModule、IMGUIModule、UIModule等）
- **ModuleManager**：`ModuleManager.4.2.3.dll` 已放入KSP DLL目录，但尚未在csproj中引用
- **按钮图标**：无，需代码生成临时图标

## 设计决策

1. **场景加载时机**：改为 `KSPAddon.Startup.Flight`，因为工具栏按钮只在飞行场景需要
2. **UI方式**：使用Unity IMGUI（OnGUI + GUILayout.Window），因为需要自定义Preset选择列表和Launch按钮，比PopupDialog更灵活
3. **音频播放**：使用 `WWW` 加载ogg + `AudioSource` 播放，通过协程等待播放结束
4. **Preset管理**：扫描 `Lauch Voice/` 下的子目录作为Preset，每个子目录名即Preset名，目录内的ogg文件为该Preset的倒计时音频
5. **自动发射序列**：使用协程（Coroutine）实现异步序列控制
6. **UI隐藏**：使用 `UIMasterController.Instance.Hide()/Show()`
7. **模块化设计**：将功能拆分为多个类，各司其职

## 文件结构规划

```
src/KSPLaunchCountdown/
├── KSPLaunchCountdownMod.cs    # 模组入口类（修改：改为Flight场景，协调各模块）
├── ToolbarButton.cs            # 工具栏按钮管理（新增）
├── CountdownMenu.cs            # 倒计时菜单UI（新增）
├── CountdownController.cs      # 倒计时执行控制器（新增）
├── PresetManager.cs            # 预设语音包管理器（新增）
├── AudioPlayer.cs              # 音频播放器（新增）
└── LaunchSequence.cs           # 发射序列执行器（新增）
```

## 各文件详细设计

### 1. KSPLaunchCountdownMod.cs（修改）

**变更**：
- `[KSPAddon(KSPAddon.Startup.MainMenu, false)]` → `[KSPAddon(KSPAddon.Startup.Flight, false)]`
- 在 `Start()` 中初始化所有子模块
- 在 `OnDestroy()` 中清理所有子模块
- 作为各模块的协调中心

**关键逻辑**：
```csharp
void Start()
{
    // 初始化预设管理器
    presetManager = new PresetManager();
    presetManager.LoadPresets();

    // 初始化音频播放器
    audioPlayer = gameObject.AddComponent<AudioPlayer>();

    // 初始化发射序列执行器
    launchSequence = gameObject.AddComponent<LaunchSequence>();

    // 初始化倒计时控制器
    countdownController = gameObject.AddComponent<CountdownController>();
    countdownController.Initialize(audioPlayer, launchSequence);

    // 初始化菜单
    countdownMenu = gameObject.AddComponent<CountdownMenu>();
    countdownMenu.Initialize(presetManager, countdownController);

    // 初始化工具栏按钮
    toolbarButton = gameObject.AddComponent<ToolbarButton>();
    toolbarButton.Initialize(countdownMenu);
}
```

### 2. ToolbarButton.cs（新增）

**用途**：管理KSP ApplicationLauncher工具栏按钮的注册、图标生成、点击响应

**关键实现**：
- 监听 `GameEvents.onGUIApplicationLauncherReady` 事件
- 使用代码生成38x38像素临时图标（橙色圆形+白色倒计时符号）
- 点击按钮时切换菜单显示/隐藏
- 仅在飞行场景显示（`AppScenes.FLIGHT`）
- `OnDestroy` 时移除按钮和事件

**依赖**：`ApplicationLauncher`, `GameEvents`, `Texture2D`

### 3. CountdownMenu.cs（新增）

**用途**：倒计时控制菜单的UI绘制和交互

**关键实现**：
- 使用 `OnGUI` + `GUILayout.Window` 绘制菜单窗口
- 菜单内容：
  - 标题："发射倒计时"
  - Preset选择：下拉列表或滚动列表显示可用预设
  - Launch按钮：点击后触发倒计时
  - 取消按钮：取消正在进行的倒计时
- 使用 `HighLogic.UISkin` 保持KSP风格
- 窗口可拖拽（`GUI.DragWindow()`）

**依赖**：`PresetManager`, `CountdownController`

### 4. CountdownController.cs（新增）

**用途**：倒计时的核心控制逻辑，协调音频播放和发射序列

**关键实现**：
- `StartCountdown(string presetName)` 方法：
  1. 通知菜单关闭
  2. 隐藏UI（`UIMasterController.Instance.Hide()`）
  3. 开启SAS
  4. 设置满油门
  5. 播放倒计时音频
  6. 等待音频播放结束
  7. 分离一级（`StageManager.ActivateNextStage()`）
  8. 等待3秒
  9. 恢复UI（`UIMasterController.Instance.Show()`）
- 使用协程实现异步序列
- `CancelCountdown()` 方法：停止音频、恢复UI

**依赖**：`AudioPlayer`, `LaunchSequence`

### 5. PresetManager.cs（新增）

**用途**：管理倒计时预设（语音包）

**关键实现**：
- `LoadPresets()`：扫描GameData下 `KSPLaunchCountdown/Lauch Voice/` 的子目录
- 每个子目录名即为Preset名称
- `GetPresetNames()`：返回所有可用Preset名称列表
- `GetPresetAudioPath(string name)`：返回指定Preset的音频文件路径
- 支持后续扩展（一个Preset可包含多个音频文件）

**数据结构**：
```csharp
class CountdownPreset
{
    string Name;           // 预设名称，如 "DFH-1"
    string DirectoryPath;  // 预设目录路径
    string AudioFilePath;  // 音频文件路径（.ogg）
}
```

### 6. AudioPlayer.cs（新增）

**用途**：音频加载和播放控制

**关键实现**：
- `LoadAndPlay(string relativePath)`：使用WWW加载ogg并播放
- `Stop()`：停止当前播放
- `IsPlaying`：当前是否正在播放
- `OnAudioFinished`：音频播放结束的事件回调
- 使用协程等待WWW加载和播放完成
- `AudioSource` 设置 `spatialBlend = 0f`（2D音频，无距离衰减）

**依赖**：`UnityEngine.AudioModule`, `UnityEngine.CoreModule`

### 7. LaunchSequence.cs（新增）

**用途**：执行发射操作（SAS、油门、分级）

**关键实现**：
- `EnableSAS()`：开启SAS稳定模式
- `SetFullThrottle()`：设置满油门
- `ActivateNextStage()`：激活下一级（分离+启动发动机）
- 所有操作前检查 `FlightGlobals.ActiveVessel != null`

**依赖**：`FlightGlobals`, `FlightInputHandler`, `StageManager`

## KSPLaunchCountdown.csproj 修改

- 添加 `ModuleManager.4.2.3.dll` 引用（如果需要）
- 注意：ModuleManager通常作为独立模组运行，不一定需要编译时引用

## 音频资源部署

KSP模组的音频文件需要放在GameData目录下才能被访问。部署时目录结构：
```
GameData/KSPLaunchCountdown/
├── KSPLaunchCountdown.dll
├── Textures/              # 按钮图标纹理（后续替换）
└── Lauch Voice/           # 语音包目录
    └── DFH-1/
        └── DFH-1.ogg
```

**注意**：代码中使用 `KSPUtil.ApplicationRootPath + "GameData/KSPLaunchCountdown/Lauch Voice/"` 来定位音频文件。

## 实施步骤

### 步骤1：修改入口类 KSPLaunchCountdownMod.cs
- 改为Flight场景加载
- 添加子模块初始化和清理逻辑
- 更新文件头部注释

### 步骤2：创建 PresetManager.cs
- 实现预设扫描和加载
- 实现预设查询接口

### 步骤3：创建 AudioPlayer.cs
- 实现WWW加载ogg音频
- 实现AudioSource播放控制
- 实现播放结束检测

### 步骤4：创建 LaunchSequence.cs
- 实现SAS控制
- 实现油门控制
- 实现分级操作

### 步骤5：创建 CountdownController.cs
- 实现倒计时序列协程
- 协调UI隐藏/恢复
- 协调音频播放和发射操作

### 步骤6：创建 CountdownMenu.cs
- 实现Preset选择UI
- 实现Launch/Cancel按钮
- 实现窗口拖拽

### 步骤7：创建 ToolbarButton.cs
- 实现ApplicationLauncher按钮注册
- 实现临时图标生成
- 实现按钮点击响应

### 步骤8：更新 readme/功能更新.md
- 记录本次功能更新

### 步骤9：编译验证
- 执行 `dotnet build` 确保编译通过

## 验证步骤

1. 编译成功，无错误和警告
2. 代码逻辑审查：
   - 工具栏按钮在飞行场景正确显示
   - 菜单正确显示Preset列表
   - 选择Preset后点击Launch执行完整序列
   - 倒计时音频播放期间UI隐藏
   - 音频结束后立即分级
   - 3秒后UI恢复
3. 资源清理：场景切换/模组卸载时无内存泄漏

## 假设与约束

1. **DFH-1.ogg** 是一段完整的倒计时音频，播放完毕即代表倒计时结束
2. **分级操作**：使用 `StageManager.ActivateNextStage()` 等效按空格键，这是最通用的方式
3. **油门控制**：通过 `FlightInputHandler.state.mainThrottle` 设置，需在 `OnFlyByWire` 回调中持续设置以确保生效
4. **UI隐藏**：使用 `UIMasterController.Instance.Hide()` 隐藏所有KSP UI
5. **音频路径**：基于 `KSPUtil.ApplicationRootPath` 构建绝对路径
6. **ModuleManager**：暂不引用，如后续需要配置文件支持再添加
