# RO（Realism Overhaul）点火延迟适配实施计划

## 1. 概述（Summary）

为 KSP Launch Countdown 模组添加 Realism Overhaul（RO）环境自动识别与专用延迟支持。

核心目标：
- 安装 RO 时，自动使用更长的分级延迟，避免 RO 发动机推力尚未建立就过早分离。
- 未安装 RO 时，完全保持现有 Stock 行为不变。
- 每个语音包可在 `preset.cfg` 中独立配置 RO 专用延迟，同时提供全局 RO 延迟倍率滑块做微调。

本次变更基于用户确认的方案：
- 配置方式：每个语音包独立配置 RO 延迟 + 全局 RO 延迟倍率微调。
- RO 模式：自动检测 `GameData/RealismOverhaul` 目录是否存在。
- 示例默认值：`roSingleStageDelay = 5.0`、`roMultiStageDelay = 3.0`。

## 2. 当前状态分析（Current State）

已阅读的关键文件：
- `preset.cfg.example`：当前仅含 `singleStageDelay = 2.0`、`multiStageDelay = 0.3`。
- `PresetManager.cs`：`CountdownPreset` 类定义上述两个延迟字段，并在 `LoadPresetConfig()` 中解析。
- `CountdownController.cs`：在 4 处使用 `preset.SingleStageDelay` / `preset.MultiStageDelay` 控制第二次分级/放行延迟。
- `CountdownMenu.cs`：提供预设选择、"先启动发动机再分离"复选框、音量滑块。
- `SettingsManager.cs`：仅持久化 `CountdownVolume`。
- `Localization.cs` 与 `zh-cn.cfg`/`en-us.cfg`/`ru-ru.cfg`：现有键不足以描述 RO 模式。
- `readme/功能更新.md`：当前 249 行，超过 200 行，需要裁剪最旧的条目。
- `README.md`：`preset.cfg` 示例与参数表需要同步更新。

当前代码中没有任何第三方模组适配器，RO 适配将是首个独立适配模块。

## 3. 设计决策（Design Decisions）

1. **RO 检测方式**：检查 `GameData/RealismOverhaul` 目录是否存在。目录存在即认为玩家处于 RO 环境。不依赖反射 RO 程序集，避免版本兼容性问题。
2. **延迟生效方式**：
   - 未检测到 RO：使用 `singleStageDelay` / `multiStageDelay`（Stock 值）。
   - 检测到 RO：使用 `roSingleStageDelay` / `roMultiStageDelay`，再乘以全局 `RODelayMultiplier`。
3. **全局微调范围**：`0.1x ~ 3.0x`，默认值 `1.0x`，按存档持久化。
4. **数值下限保护**：单段延迟最小 `0.1s`，多段延迟最小 `0.05s`，防止配置错误导致即时分级。
5. **UI 展示**：仅在检测到 RO 时，在音量滑块下方显示 "RO 模式已启用" 标签与延迟倍率滑块；未安装 RO 时不显示，避免干扰 Stock 玩家。
6. **模块划分**：新增 `ROAdapter.cs` 负责 RO 检测；`CountdownPreset` 增加有效延迟计算方法；`CountdownController` 仅负责按有效延迟执行。

## 4. 具体变更（Proposed Changes）

### 4.1 新增 `src/KSPLaunchCountdown/ROAdapter.cs`

**用途**：RO 环境检测与适配器入口，提供静态只读属性供其他模块查询。

**内容**：
- 文件头部详细注释说明用途、检测原理、依赖。
- 静态属性 `IsROInstalled`：首次访问时检测并缓存结果。
- 私有方法 `DetectRO()`：通过 `Directory.Exists` 检查 `KSPUtil.ApplicationRootPath/GameData/RealismOverhaul`。
- 日志输出检测结果，便于调试。

### 4.2 修改 `src/KSPLaunchCountdown/PresetManager.cs`

**变更点**：
1. 在 `CountdownPreset` 中新增：
   - `RoSingleStageDelay`（默认 `5.0f`）
   - `RoMultiStageDelay`（默认 `3.0f`）
2. 新增公共方法：
   - `float GetEffectiveSingleStageDelay(bool roInstalled, float multiplier)`
   - `float GetEffectiveMultiStageDelay(bool roInstalled, float multiplier)`
   - 方法内部根据 `roInstalled` 选择 Stock 或 RO 基准值，乘以倍率并截断下限。
3. 在 `LoadPresetConfig()` 中新增对 `roSingleStageDelay`、`roMultiStageDelay` 的解析，使用 `Mathf.Max` 保护下限。
4. 更新文件头部注释，说明新增 RO 字段用途与默认值含义。
5. 更新 `preset.cfg` 格式注释示例，加入 RO 字段说明。

### 4.3 修改 `src/KSPLaunchCountdown/SettingsManager.cs`

**变更点**：
1. 新增常量 `RO_DELAY_MULTIPLIER_KEY = "RODelayMultiplier"`，默认值 `1.0f`。
2. 新增私有字段 `roDelayMultiplier`。
3. 新增公共属性 `RODelayMultiplier`（`get`/`set`），`set` 时使用 `Mathf.Clamp(value, 0.1f, 3.0f)` 并立即保存。
4. 在 `Load()` 中读取该值，解析失败时使用默认值。
5. 在 `Save()` 中写入该值。
6. 更新文件头部注释，说明新增全局 RO 延迟倍率设置。

### 4.4 修改 `src/KSPLaunchCountdown/CountdownController.cs`

**变更点**：
1. 新增私有字段 `settingsManager`。
2. 修改 `Initialize()` 签名，注入 `SettingsManager` 实例：
   - `public void Initialize(AudioPlayer player, LaunchSequence sequence, Localization loc, SettingsManager settings)`
3. 在 4 处使用延迟的地方，替换为：
   - `float delay = preset.GetEffectiveSingleStageDelay(ROAdapter.IsROInstalled, settingsManager.RODelayMultiplier);`
   - `float delay = preset.GetEffectiveMultiStageDelay(ROAdapter.IsROInstalled, settingsManager.RODelayMultiplier);`
4. 在日志中增加 RO 模式与倍率信息，方便排查。
5. 更新文件头部注释，说明已支持 RO 环境自动适配。

### 4.5 修改 `src/KSPLaunchCountdown/KSPLaunchCountdownMod.cs`

**变更点**：
1. 在 `Start()` 中实例化 `ROAdapter`（触发一次检测，或调用其初始化方法）。
2. 将 `settingsManager` 注入 `countdownController.Initialize(...)`。
3. 更新文件头部注释，说明新增 RO 适配模块。

### 4.6 修改 `src/KSPLaunchCountdown/CountdownMenu.cs`

**变更点**：
1. 在音量滑块下方、安全检查区域上方，新增条件绘制：
   - 若 `ROAdapter.IsROInstalled` 为 true，显示 "RO 模式已启用" 标签。
   - 显示 RO 延迟倍率滑块（`0.1x ~ 3.0x`），绑定到 `settingsManager.RODelayMultiplier`。
2. 调整 `windowRect` 默认高度，确保新增控件不溢出。
3. 更新文件头部注释与窗口布局示意图。

### 4.7 修改 `src/KSPLaunchCountdown/Localization.cs`

**变更点**：
1. 在 `Keys` 静态类中新增：
   - `ROModeEnabled = "#KSPLaunchCountdown_ROModeEnabled"`
   - `RODelayMultiplierLabel = "#KSPLaunchCountdown_RODelayMultiplierLabel"`

### 4.8 修改多语言配置文件

**文件**：
- `GameData/KSPLaunchCountdown/Localization/zh-cn.cfg`
- `GameData/KSPLaunchCountdown/Localization/en-us.cfg`
- `GameData/KSPLaunchCountdown/Localization/ru-ru.cfg`

**变更点**：
- 新增上述两个键的翻译：
  - zh-cn：`RO 模式已启用`、`RO 延迟倍数: {0}x`
  - en-us：`RO Mode Enabled`、`RO Delay Multiplier: {0}x`
  - ru-ru：`Режим RO включён`、`Множитель задержки RO: {0}x`（若无法确认俄文术语，保留英文作为回退）

### 4.9 修改 `preset.cfg.example`

**变更后内容**：

```ini
COUNTDOWN_PRESET
{
    // Stock 环境下的延迟
    singleStageDelay = 2.0
    multiStageDelay = 0.3

    // RO（Realism Overhaul）环境下的延迟
    // 安装 RO 后自动生效，发动机点火到满推力需要更长时间
    roSingleStageDelay = 5.0
    roMultiStageDelay = 3.0
}
```

## 5. 测试用例（10 条）

| 编号 | 前置条件 | 输入 | 预期结果 |
|------|----------|------|----------|
| TC-01 | RO 未安装 | `singleStageDelay=2.0`，倍率=1.0 | 单段模式使用 2.0s 延迟 |
| TC-02 | RO 已安装 | `roSingleStageDelay=5.0`，倍率=1.0 | 单段模式使用 5.0s 延迟 |
| TC-03 | RO 已安装 | `roSingleStageDelay=5.0`，倍率=0.5 | 单段模式使用 2.5s 延迟 |
| TC-04 | RO 已安装 | `roSingleStageDelay=5.0`，倍率=2.0 | 单段模式使用 10.0s 延迟 |
| TC-05 | RO 已安装 | preset.cfg 未配置 RO 字段 | 使用默认值 5.0s / 3.0s |
| TC-06 | RO 未安装 | `multiStageDelay=0.3`，倍率任意 | 多段模式使用 0.3s 延迟，倍率无效 |
| TC-07 | RO 已安装 | `roMultiStageDelay=3.0`，倍率=1.5 | 多段模式使用 4.5s 延迟 |
| TC-08 | RO 已安装 | 玩家将倍率拖到 0.05 | 倍率被截断到 0.1，延迟按 0.1x 计算 |
| TC-09 | RO 已安装 | 玩家将倍率拖到 5.0 | 倍率被截断到 3.0，延迟按 3.0x 计算 |
| TC-10 | RO 已安装 | 在存档 A 设置倍率 1.5，切换至存档 B 后改为 0.8，再切回存档 A | 存档 A 倍率仍为 1.5，存档 B 为 0.8 |

## 6. 文档更新

### 6.1 `readme/功能更新.md`

- 在文件最前面新增本次功能更新条目。
- 当前文件 249 行，超过 200 行限制，需删除最旧的 2 条（"初始化 KSP1 模组开发环境"和"实现发射倒计时核心功能"），使总长度回到 200 行以内。

### 6.2 `README.md`

- 更新 `preset.cfg format` 示例代码块，加入 `roSingleStageDelay` 与 `roMultiStageDelay` 说明。
- 更新 `Preset Level Parameters` 参数表，新增 RO 字段、全局倍率说明。
- 更新中文版对应段落。

### 6.3 修改过的 C# 文件头部注释

- 所有本次修改的 `.cs` 文件（`ROAdapter.cs`、`PresetManager.cs`、`SettingsManager.cs`、`CountdownController.cs`、`KSPLaunchCountdownMod.cs`、`CountdownMenu.cs`、`Localization.cs`）头部注释需同步更新，说明本次 RO 适配相关变更。

### 6.4 开发/实施文档

- 本计划文件（`.trae/documents/ro-ignition-delay-adapter-plan.md`）作为实施文档已包含本次变更。
- 项目若存在其他开发文档，需检查其配置参数描述是否与本实现一致；如不一致则同步更新。

## 7. 假设与风险（Assumptions & Risks）

**假设**：
- 玩家安装 RO 时，`GameData/RealismOverhaul` 目录必然存在。
- RO 发动机推力建立时间更长，因此更长的延迟确实能解决落回砸坏引擎的问题；若玩家使用特别复杂的 RO 火箭，仍可通过全局倍率进一步延长。

**风险与缓解**：
- **误检**：某些玩家可能手动放置同名空目录。误检后果仅是用更长延迟，不会破坏功能，影响可控。
- **未检**：若 RO 安装路径变化或未来 RO 改名，检测会失败。缓解方案：保留 Stock 行为作为回退，并在日志中记录检测结果便于排查。
- **UI 空间**：新增滑块可能使窗口变高。缓解方案：适当增大 `windowRect` 高度，并只在 RO 安装时显示。
- **向后兼容**：旧的 `preset.cfg` 没有 RO 字段时会使用默认值，不影响现有语音包。

## 8. 实施顺序

1. 新增 `ROAdapter.cs` 并确保能正确检测 RO。
2. 修改 `PresetManager.cs` 添加 RO 字段与有效延迟计算方法。
3. 修改 `SettingsManager.cs` 添加全局倍率持久化。
4. 修改 `CountdownController.cs` 使用有效延迟。
5. 修改 `KSPLaunchCountdownMod.cs` 注入 SettingsManager。
6. 修改 `Localization.cs` 与三个语言文件。
7. 修改 `CountdownMenu.cs` 增加 RO 状态显示与倍率滑块。
8. 更新 `preset.cfg.example`。
9. 更新 `readme/功能更新.md`、`README.md` 与所有相关文件头部注释。
10. 编译并运行测试用例验证。
