# KSP Launch Countdown - 发展路线图

> 本文档规划 KSP Launch Countdown 模组的未来发展方向和新功能。
> 路线图按优先级和复杂度分为三个阶段：近期（v0.2）、中期（v0.3~v0.5）、远期（v1.0+）。
> 每个功能标注了优先级、依赖关系和预期工作量。

---

## 当前版本：v0.1.0（已完成）

### 已实现功能

| 功能 | 状态 | 说明 |
|------|------|------|
| 基本发射倒计时 | ✅ | 隐藏UI → SAS → 满油门 → 播放音频 → 分级 → 恢复UI |
| 单段音频模式 | ✅ | 一个.ogg文件完成整个倒计时 |
| 多段音频模式(p1/p2) | ✅ | p1倒计时部分 + p2点火后部分 |
| 预设语音包管理 | ✅ | 扫描Lauch Voice目录，自动识别单段/多段 |
| "先启动发动机再分离" | ✅ | 可配置延迟的二次分级 |
| IMGUI控制菜单 | ✅ | 预设选择、Launch/Cancel按钮 |
| ApplicationLauncher工具栏 | ✅ | 飞行场景工具栏按钮 + Ctrl+L快捷键 |
| preset.cfg配置文件 | ✅ | 延迟时间参数配置 |

---

## 🎯 第一阶段：v0.2 — 体验优化与稳定性

**目标**：提升日常使用体验，修复边缘情况，让模组更"好用"

### v0.2.1 — 倒计时HUD显示

**优先级**：🔴 高 | **依赖**：无 | **工作量**：小

在游戏画面上叠加显示倒计时数字和状态文字，类似真实航天发射的倒计时时钟。

```
┌─────────────────────────────┐
│                             │
│       T - 00:07             │   ← 大字体倒计时数字
│                             │
│    ▶ LAUNCH SEQUENCE        │   ← 当前阶段状态
│                             │
│    [SAS: ON] [THROTTLE:100%]│   ← 系统状态指示
└─────────────────────────────┘
```

**技术方案**：
- 使用 `OnGUI` 在屏幕中央绘制倒计时文字
- 从 `CountdownController` 暴露当前倒计时状态（剩余时间、当前步骤）
- 支持位置、大小、透明度配置
- 倒计时结束后自动淡出

**涉及文件**：
- 新建 `CountdownHUD.cs` — HUD渲染组件
- 修改 `CountdownController.cs` — 添加状态暴露接口
- 修改 `CountdownMenu.cs` — 添加HUD开关选项

---

### v0.2.2 — 音量控制

**优先级**：🔴 高 | **依赖**：无 | **工作量**：小 | **状态**：✅ 已完成

在菜单中添加音量滑块，实时调节倒计时语音音量并自动保存。

**功能点**：
- 全局音量滑块（0%~100%）
- 记忆上次使用的音量设置（保存到当前存档配置文件）
- 本次实现**不包含**音频预览功能

**技术方案**：
- 在 `CountdownMenu` 中添加 `GUILayout.HorizontalSlider`
- `AudioPlayer` 添加 `Volume` 属性并实时应用
- 新增 `SettingsManager.cs` 管理持久化设置（使用ConfigNode保存到 `saves/<存档名>/KSPLaunchCountdown/Settings.cfg`）

**涉及文件**：
- 修改 `AudioPlayer.cs` — 添加音量属性
- 修改 `CountdownMenu.cs` — 添加音量滑块
- 新建 `SettingsManager.cs` — 设置持久化管理

---

### v0.2.3 — 发射前安全检查

**优先级**：🟡 中 | **依赖**：无 | **工作量**：小

点击Launch前自动检查飞船状态，防止误操作。

**状态**：✅ 已完成

**检查项**：
- [x] 是否在发射台（地面高度 < 100m 且速度接近0）
- [x] 是否已有正在进行的倒计时（防重复启动）
- [x] 芯一级发动机是否启动（如果启动则油门为0，播放完成后油门加满）

**失败处理**：
- 在菜单窗口内显示黄色警告文本
- 列出未通过的项目
- 提供"强制发射"复选框（跳过检查）

**实现说明**：
- 原计划使用 `PopupDialog.SpawnPopupDialog` 弹出独立对话框，但为避免KSP版本间API签名差异导致的编译兼容性问题，
  改为在 `CountdownMenu` 窗口内直接显示警告和强制发射选项，交互更直观。

**技术方案**：
- 在 `CountdownMenu.OnLaunchClicked()` 中调用 `LaunchSafetyChecker.PerformCheck()`
- 新建 `LaunchSafetyChecker.cs` 封装各项检查
- 检查结果传入 `CountdownController.StartCountdown(preset, safetyResult)` 以决定油门策略

**涉及文件**：
- 新建 `LaunchSafetyChecker.cs` — 安全检查器
- 修改 `CountdownController.cs` — 根据检查结果选择油门策略
- 修改 `LaunchSequence.cs` — 支持设置任意油门值（包括0油门）
- 修改 `CountdownMenu.cs` — 显示警告和强制发射选项

---

### v0.2.4 — 自定义倒计时起始时间

**优先级**：🟡 中 | **依赖**：无 | **工作量**：小

允许用户设置倒计时的起始秒数，而非固定从音频开头开始。

**功能点**：
- 菜单中添加起始时间输入框（默认T-10秒）
- 支持常用预设快速选择：T-5、T-10、T-15、T-30、T-60
- 起始时间仅影响HUD显示，不影响音频播放时机

**技术方案**：
- 在 `CountdownPreset` 中添加 `StartFromSecond` 字段
- 或在菜单UI中作为临时覆盖参数
- HUD根据设置的起始时间递减显示

**涉及文件**：
- 修改 `CountdownMenu.cs` — 添加时间选择UI
- 修改 `CountdownController.cs` — 接收起始时间参数
- 修改 `CountdownHUD.cs` — 根据起始时间显示

---

## 🚀 第二阶段：v0.3 ~ v0.5 — 功能增强

**目标**：从"能用的倒计时工具"进化为"专业的发射自动化助手"

### v0.3.1 — 多级火箭序列编辑器

**优先级**：🔴 高 | **依赖**：v0.2.x | **工作量**：大

为多级火箭提供可视化的发射序列编辑界面，用户可以自定义每一级的操作。

**核心概念**：
将发射过程分解为多个"阶段"(Phase)，每个阶段包含一组"动作"(Action)：

```
Phase 1: 倒计时 (T-10 ~ T-0)
  ├── Action: HideUI
  ├── Action: EnableSAS
  ├── Action: SetThrottle(1.0)
  └── Action: PlayAudio("p1")

Phase 2: 点火升空 (T+0 ~ T+10)
  ├── Action: StageSeparation
  ├── Action: PlayAudio("p2")
  └── Action: Wait(3.0)

Phase 3: 程序转弯 (T+10 ~ T+60)
  ├── Action: SetSASMode(prograde)
  └── Action: FairingSeparation (可选)
```

**UI设计**：
- 列表形式展示所有阶段和动作
- 支持拖拽排序
- 点击展开/折叠每个阶段的动作列表
- 每个动作可配置参数（如Wait的时间、SetThrottle的值）
- 内置常用模板（单级火箭、两级火箭、航天飞机等）

**数据格式**（存储在preset.cfg中扩展）：
```ini
COUNTDOWN_PRESET
{
    singleStageDelay = 2.0
    multiStageDelay = 0.3

    PHASE
    {
        name = 倒计时
        action = HIDE_UI
        action = ENABLE_SAS
        action = SET_THROTTLE,1.0
        action = PLAY_AUDIO,p1
    }
    PHASE
    {
        name = 点火
        action = STAGE
        action = PLAY_AUDIO,p2
        action = WAIT,3.0
    }
}
```

**涉及文件**：
- 新建 `LaunchSequenceEditor.cs` — 序列编辑器UI
- 新建 `Phase.cs` / `Action.cs` — 阶段和动作数据模型
- 重构 `CountdownController.cs` — 改为执行动态序列
- 重构 `PresetManager.cs` — 支持读取PHASE节点

---

### v0.3.2 — 发射中止(Abort)系统

**优先级**：🟡 中 | **依赖**：v0.2.3 | **工作量**：中

模拟真实航天的发射中止程序，在紧急情况下安全终止发射。

**中止模式**：

| 模式 | 触发条件 | 操作序列 |
|------|---------|---------|
| 正常中止 | 用户点击Abort按钮或在倒计时中按Escape | 关闭发动机 → 保持SAS → 恢复UI |
| 自动中止 | 检测到严重故障（如发动机点火失败） | 关闭发动机 → 启动逃逸塔（如有）→ 播放警报音频 |
| 安全分离 | 中止后的清理操作 | 分离载荷舱 → 打开降落伞 |

**功能点**：
- Abort按钮（红色醒目按钮，位于Launch旁边）
- 中止音频播放（警报声）
- 可配置的中止动作序列
- 中止日志记录（记录中止原因和时间）

**技术方案**：
- 在 `CountdownController` 中添加 `AbortCountdown()` 方法
- 新建 `AbortSequence.cs` 定义中止动作
- 新建 `AbortAudio.ogg` 警报音频（或复用现有音频）

**涉及文件**：
- 新建 `AbortSequence.cs` — 中止序列执行器
- 修改 `CountdownController.cs` — 添加中止逻辑
- 修改 `CountdownMenu.cs` — 添加Abort按钮
- 修改 `AudioPlayer.cs` — 支持警报音频

---

### v0.3.3 — 发射统计与历史记录

**优先级**：🟢 低 | **依赖**：v0.2.x | **工作量**：中

记录每次发射的关键数据，供玩家回顾和分析。

**记录的数据**：
- 发射时间戳
- 使用的预设名称
- 倒计时总耗时
- 分级次数
- 是否发生中止
- 发射时的飞船质量、零件数

**UI展示**：
- 在菜单中添加"历史记录"标签页
- 表格形式列出历史发射
- 点击某条记录查看详情
- 支持导出为文本文件

**技术方案**：
- 新建 `LaunchHistory.cs` 数据模型
- 使用KSP的 `SaveGame` 机制或独立文件存储
- 按存档区分历史记录

**涉及文件**：
- 新建 `LaunchHistory.cs` — 历史记录数据模型
- 新建 `HistoryViewer.cs` — 历史查看UI
- 修改 `CountdownMenu.cs` — 添加历史标签页

---

### v0.4.1 — Toolbar / ClickThrough Blocker 兼容

**优先级**：🟡 中 | **依赖**：无 | **工作量**：小

与其他常用模组的工具栏系统兼容。

**兼容目标**：
- [Blizzy's Toolbar](https://forum.kerbalspaceprogram.com/index.php?/topic/87972-/) — 更灵活的工具栏模组
- [Click Through Blocker](https://forum.kerbalspaceprogram.com/index.php?/topic/110609-/) — UI点击穿透修复

**技术方案**：
- 条件编译或运行时检测Toolbar是否安装
- 如果检测到Toolbar，注册Toolbar按钮而非ApplicationLauncher按钮
- 使用CTB的API确保菜单窗口不会阻挡底层点击

**涉及文件**：
- 修改 `ToolbarButton.cs` — 添加Toolbar兼容逻辑
- 修改 `CountdownMenu.cs` — 集成CTB API

---

### v0.4.2 — ModuleManager配置补丁支持

**优先级**：🟡 中 | **依赖**：无 | **工作量**：小

通过ModuleManager的PATCH机制允许其他模组修改本模组的配置。

**支持的PATCH类型**：
- 添加/修改/删除预设
- 修改默认参数（延迟时间、音量等）
- 注册新的动作类型

**示例MM patch**：
```ini
@PRESSET[DFH-1]:FOR[SomeOtherMod]
{
    @singleStageDelay = 3.0     // 修改延迟时间
}
```

**技术方案**：
- 在 `KSPLaunchCountdownMod.Start()` 中处理MM PATCH
- 将预设列表暴露为可被MM修改的对象

**涉及文件**：
- 修改 `KSPLaunchCountdownMod.cs` — MM PATCH处理
- 修改 `PresetManager.cs` — 暴露可修改接口

---

### v0.5.1 — 多语言支持(i18n)

**优先级**：🟢 低 | **依赖**：v0.3.x | **工作量**：中

支持界面语言的切换，方便非中文/英文玩家使用。

**状态**：✅ 已完成（当前仅实现中/英/俄三种语言）

**支持语言**：
- 中文（简体） ✅
- English ✅
- Русский（俄语） ✅
- 日本語（日语） 📋 待后续补充
- 中文繁体 📋 待后续补充

**本地化范围**：
- 菜单UI中的所有文字
- 安全检查警告文本
- 强制发射/取消按钮文本
- HUD显示的文字（如后续实现HUD）
- 日志输出仍使用中文（便于中文开发者调试）

**技术方案**：
- 新建 `Localization.cs` 本地化管理器
- 语言文件使用ConfigNode格式（与KSP原生一致）
- 语言文件放在 `GameData/KSPLaunchCountdown/Localization/` 目录
- 默认跟随KSP语言设置，当前语言缺失时回退到英文

**目录结构**：
```
Localization/
├── zh-cn.cfg    # 中文（简体）
├── en-us.cfg    # 英文
└── ru-ru.cfg    # 俄语
```

**涉及文件**：
- 新建 `Localization.cs` — 本地化系统
- 新建 `Localization/` 目录及语言文件
- 修改 `CountdownMenu.cs` — 替换硬编码字符串为Localize()调用
- 修改 `LaunchSafetyChecker.cs` / `CountdownController.cs` — 使用本地化键生成警告文本

---

## 🌟 第三阶段：v1.0+ — 高级特性

**目标**：成为KSP社区中最专业、最完整的发射倒计时解决方案

### v1.0.0 — 文本转语音(TTS)引擎

**优先级**：🟡 中 | **依赖**：v0.5.x | **工作量**：大

集成TTS引擎，允许用户自定义倒计时语音内容，无需录制音频文件。

**功能点**：
- 输入倒计时文本脚本，自动生成语音
- 支持多种TTS后端：
  - Windows SAPI（系统自带，零依赖）
  - eSpeak NG（开源跨平台）
  - 在线TTS API（需网络连接）
- 支持SSML标记语言（控制语速、停顿、音调等）
- 生成的音频缓存到本地避免重复生成

**文本脚本示例**：
```yaml
countdown_script:
  - text: "All stations report."
    delay: 0
  - text: "T-minus 10 seconds."
    delay: 2
  - text: "9... 8... 7..."
    speed: 1.2  # 加速播报
  - text: "Main engine start."
    pitch: 0.8 # 降低音调
  - text: "Liftoff! We have liftoff!"
    volume: 1.2 # 提高音量
```

**技术方案**：
- 新建 `TTSEngine.cs` TTS抽象层
- 新建 `TTSBackendSAPI.cs` Windows后端
- 新建 `TTSBackendeSpeak.cs` eSpeak后端
- 新建 `ScriptParser.cs` 解析文本脚本
- 集成到现有的 `AudioPlayer` 流程中

**涉及文件**：
- 新建 `TTS/` 目录存放所有TTS相关代码
- 修改 `PresetManager.cs` — 识别TTS脚本文件(.yml/.txt)
- 修改 `CountdownMenu.cs` — 添加TTS设置面板

---

### v1.1.0 — 网络多人同步倒计时

**优先级**：🟢 低 | **依赖**：v1.0.x | **工作量**：很大

配合 [Luna Multiplayer](https://github.com/LunaMultiplayer/LunaMultiplayer) 等多人模组，实现多人联机时的同步倒计时。

**功能点**：
- 主机发起倒计时，所有客户端同步显示和执行
- 延迟补偿算法（不同玩家的网络延迟不同）
- 角色权限控制（谁可以发起/取消倒计时）
- 同步HUD显示（所有人看到相同的倒计时数字）

**技术挑战**：
- 网络消息协议设计
- 时钟同步（NTP或自定义同步算法）
- 断线重连和状态恢复
- 与LMP/DarkMultiplayer的API集成

**涉及文件**：
- 新建 `NetworkSync/` 目录
- 新建 `SyncProtocol.cs` 同步协议
- 修改 `CountdownController.cs` — 网络模式分支

---

### v1.2.0 — 发射台特效集成

**优先级**：🟢 低 | **依赖**：v0.3.x | **工作量**：中

在倒计时关键节点触发视觉特效，增强沉浸感。

**特效类型**：
- 倒计时数字闪烁/缩放动画
- 点火时的火焰粒子效果（调用SmokeScreen或其他粒子模组API）
- 发射时的震动/画面抖动
- 分离时的碎片特效

**技术方案**：
- 使用Unity ParticleSystem创建基础特效
- 检测SmokeScreen/Particle FX等模组是否安装并利用其API
- 特效配置存储在preset.cfg中

**涉及文件**：
- 新建 `EffectsManager.cs` 特效管理器
- 新建 `Effect.cs` 特效基类
- 修改 `CountdownController.cs` — 在关键节点触发特效

---

### v1.3.0 — 配置编辑器GUI

**优先级**：🟢 低 | **依赖**：v0.3.1 | **工作量**：中

提供一个图形化的配置编辑器，替代手动编辑cfg文件。

**功能点**：
- 在太空中心场景打开设置窗口
- 可视化编辑预设配置
- 拖拽式序列编辑器（v0.3.1的GUI版本）
- 实时预览音频
- 导入/导出预设包（zip格式分享给其他玩家）

**技术方案**：
- 使用KSP的 `AddSettingsWindow` API注册设置页面
- 或在主菜单添加独立的设置窗口
- 复用 `LaunchSequenceEditor` 的组件

**涉及文件**：
- 新建 `SettingsWindow.cs` — 设置窗口
- 修改 `KSPLaunchCountdownMod.cs` — 添加设置入口

---

## 📊 功能优先级总览

```
优先级    功能                    版本    状态
━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
 🔴 高    倒计时HUD显示           v0.2.1  📋 规划中
 🔴 高    音量控制（无预览）      v0.2.2  ✅ 已完成
 🔴 高    多级火箭序列编辑器      v0.3.1  📋 规划中
 🟡 中    发射前安全检查          v0.2.3  ✅ 已完成
 🟡 中    自定义倒计时起始时间    v0.2.4  📋 规划中
 🟡 中    发射中止系统            v0.3.2  📋 规划中
 🟡 中    Toolbar/CTB兼容         v0.4.1  📋 规划中
 🟡 中    ModuleManager补丁支持   v0.4.2  📋 规划中
 🟡 中    TTS文本转语音           v1.0.0  📋 规划中
 🟢 低    发射统计与历史记录      v0.3.3  📋 规划中
 🟢 低    多语言i18n（中英俄）    v0.5.1  ✅ 已完成
 🟢 low    发射台特效集成          v1.2.0  📋 规划中
 🟢 Low    配置编辑器GUI          v1.3.0  📋 规划中
 🟢 Low    网络多人同步           v1.1.0  📋 规划中
```

## ⏱️ 时间线预估

```
2026 Q3 ┃ v0.2.x  体验优化
        ┃ ├ v0.2.1 倒计时HUD
        ┃ ├ v0.2.2 音量控制
        ┃ ├ v0.2.3 安全检查
        ┃ └ v0.2.4 自定义起始时间
        ┃
2026 Q4 ┃ v0.3.x  功能增强
        ┃ ├ v0.3.1 序列编辑器（重点功能）
        ┃ ├ v0.3.2 发射中止系统
        ┃ └ v0.3.3 发射统计
        ┃
2027 Q1 ┃ v0.4.x  兼容性
        ┃ ├ v0.4.1 Toolbar/CTB
        ┃ └ v0.4.2 ModuleManager
        ┃
2027 Q2 ┃ v0.5.x  国际化
        ┃ └ v0.5.1 多语言支持
        ┃
2027 Q3+┃ v1.0.x  高级特性
        ┃ ├ v1.0.0 TTS引擎
        ┃ ├ v1.1.0 多人同步
        ┃ ├ v1.2.0 特效集成
        └ └ v1.3.0 配置编辑器
```

> 注：以上时间线仅为规划参考，实际进度取决于开发资源和社区反馈。

## 💡 社区贡献方向

以下是一些适合社区贡献者参与的方向：

| 方向 | 难度 | 说明 |
|------|------|------|
| 新增语音包 | 🟢 简单 | 录制ogg音频，放入Lauch Voice目录即可 |
| 新增语言翻译 | 🟢 简单 | 编写 Localization/*.cfg 语言文件 |
| HUD皮肤/主题 | 🟡 中等 | 为CountdownHUD添加新的视觉样式 |
| 新的动作类型 | 🟡 中等 | 为序列编辑器扩展新的Action |
| TTS后端插件 | 🔴 困难 | 实现新的TTS引擎后端 |

---

## ❓ 待定事项（待社区反馈确认）

以下功能的可行性需要进一步讨论：

- [ ] **Realism Overhaul集成**：RO改变了大量游戏机制（推力、燃料等），需要适配。
- [ ] **RP-0/RP-1职业生涯集成**：与职业 progression 模组联动，倒计时可能影响职业进度。

---

*本文档会随着开发进展持续更新。欢迎通过 Issues 和 Discussions 提出功能建议！*
