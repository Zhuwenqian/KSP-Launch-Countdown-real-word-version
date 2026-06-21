/**
 * PresetManager.cs - KSP1 发射倒计时预设语音包管理器
 *
 * 用途：管理倒计时预设（语音包），负责扫描、加载和查询可用的倒计时语音包。
 * 每个语音包对应 Lauch Voice 目录下的一个子目录，子目录名即为预设名称。
 *
 * 目录结构示例：
 *   GameData/KSPLaunchCountdown/Lauch Voice/
 *   ├── DFH-1/                  <- 预设名称 "DFH-1"（东风一号）
 *   │   ├── DFH-1.ogg           <- 单段音频模式
 *   │   └── preset.cfg          <- 预设配置文件（可选）
 *   ├── Shenzhou Series/        <- 预设名称 "Shenzhou Series"
 *   │   ├── Shenzhou Series-p1.ogg  <- 多段音频模式：p1=倒计时部分
 *   │   ├── Shenzhou Series-p2.ogg  <- 多段音频模式：p2=点火后部分
 *   │   └── preset.cfg          <- 预设配置文件（可选）
 *   └── Atlas/
 *       └── Atlas.ogg
 *
 * 多段音频说明：
 *   当目录中存在 XXX-p1.ogg 和 XXX-p2.ogg 时，识别为多段音频模式：
 *   - p1: 倒计时部分，播放完毕后执行分级
 *   - p2: 分级后继续播放的部分（如"点火"、"升空"等语音）
 *   单段模式：目录中只有 XXX.ogg，播放完毕后执行分级
 *
 * 配置文件 preset.cfg 格式（ModuleManager兼容）：
 *   COUNTDOWN_PRESET
 *   {
 *       singleStageDelay = 2.0              // 单段模式下第二次分级的延迟（秒）
 *       multiStageDelay = 0.3               // 多段模式下第二次分级的延迟（秒）
 *   }
 *
 *   注意：startEngineBeforeSeparation 选项仅在UI上勾选，不写入配置文件。
 *   配置文件中的延迟时间参数仅在UI勾选"先启动发动机再分离"时才生效，
 *   因为不同火箭的分级模式各不相同，延迟时间需要按语音包/火箭类型调整。
 *
 * 依赖：
 *   - Assembly-CSharp.dll (KSP核心，提供KSPUtil、ConfigNode等)
 *   - UnityEngine.CoreModule.dll (Unity核心，提供Debug日志)
 */

using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace KSPLaunchCountdown
{
    /// <summary>
    /// 倒计时预设数据类
    /// 每个预设对应一个语音包，包含预设名称、音频路径和配置信息
    /// </summary>
    public class CountdownPreset
    {
        /// <summary>预设名称，对应语音包目录名，如 "DFH-1"</summary>
        public string Name { get; set; }

        /// <summary>预设目录的完整路径（GameData下的相对路径）</summary>
        public string RelativePath { get; set; }

        /// <summary>第一段音频路径（GameDatabase格式，不含扩展名）
        /// 单段模式：唯一的音频文件路径
        /// 多段模式：p1音频路径（倒计时部分）</summary>
        public string AudioFilePath { get; set; }

        /// <summary>第二段音频路径（GameDatabase格式，不含扩展名）
        /// 多段模式：p2音频路径（点火后部分）
        /// 单段模式：null</summary>
        public string AudioFilePath2 { get; set; }

        /// <summary>是否为多段音频模式（p1/p2分段）</summary>
        public bool IsMultiSegment => AudioFilePath2 != null;

        /// <summary>是否启用"先启动发动机再分离"功能
        /// 此选项仅在UI上勾选控制，不写入配置文件
        /// 启用后，分级操作会执行两次：
        /// - 单段模式：音频结束后第一次分级，等待singleStageDelay秒后第二次分级
        /// - 多段模式：p1结束后第一次分级，p2开始后等待multiStageDelay秒第二次分级</summary>
        public bool StartEngineBeforeSeparation { get; set; } = false;

        /// <summary>单段模式下第二次分级的延迟时间（秒）
        /// 可调整参数：增大此值让发动机有更多时间启动，减小此值更快执行第二次分级</summary>
        public float SingleStageDelay { get; set; } = 2.0f;

        /// <summary>多段模式下第二次分级的延迟时间（秒）
        /// 可调整参数：p2开始播放后等待多久执行第二次分级</summary>
        public float MultiStageDelay { get; set; } = 0.3f;
    }

    /// <summary>
    /// 预设语音包管理器
    /// 负责扫描GameData下的语音包目录，加载预设信息和配置，提供查询接口
    /// </summary>
    public class PresetManager
    {
        /// <summary>日志标签</summary>
        private const string LOG_TAG = "[KSPLaunchCountdown]";

        /// <summary>语音包在GameData下的相对路径</summary>
        private const string VOICE_BASE_PATH = "KSPLaunchCountdown/Lauch Voice";

        /// <summary>预设配置文件名</summary>
        private const string PRESET_CONFIG_FILE = "preset.cfg";

        /// <summary>ConfigNode节点名称，用于KSP的ConfigNode解析</summary>
        private const string CONFIG_NODE_NAME = "COUNTDOWN_PRESET";

        /// <summary>已加载的预设列表</summary>
        private readonly List<CountdownPreset> presets = new List<CountdownPreset>();

        /// <summary>获取已加载的预设数量</summary>
        public int PresetCount => presets.Count;

        /// <summary>
        /// 加载所有预设
        /// 扫描语音包基础目录下的所有子目录，检测音频文件模式（单段/多段），
        /// 加载配置文件（如果存在）
        /// </summary>
        public void LoadPresets()
        {
            presets.Clear();

            string basePath = KSPUtil.ApplicationRootPath + "GameData/" + VOICE_BASE_PATH;
            Debug.Log($"{LOG_TAG} 扫描语音包目录: {basePath}");

            if (!Directory.Exists(basePath))
            {
                Debug.LogWarning($"{LOG_TAG} 语音包目录不存在: {basePath}");
                return;
            }

            try
            {
                string[] subDirs = Directory.GetDirectories(basePath);
                foreach (string dir in subDirs)
                {
                    string dirName = new DirectoryInfo(dir).Name;
                    string presetRelativePath = VOICE_BASE_PATH + "/" + dirName;

                    // 查找目录中的所有.ogg文件
                    string[] oggFiles = Directory.GetFiles(dir, "*.ogg");
                    if (oggFiles.Length == 0)
                    {
                        Debug.LogWarning($"{LOG_TAG} 预设 '{dirName}' 目录中没有找到.ogg音频文件，跳过");
                        continue;
                    }

                    var preset = new CountdownPreset
                    {
                        Name = dirName,
                        RelativePath = presetRelativePath
                    };

                    // 检测多段音频模式：查找 *-p1.ogg 和 *-p2.ogg 文件
                    string p1File = null;
                    string p2File = null;
                    string singleFile = null;

                    foreach (string oggFile in oggFiles)
                    {
                        string fileName = Path.GetFileNameWithoutExtension(oggFile);
                        if (fileName.EndsWith("-p1", StringComparison.OrdinalIgnoreCase))
                        {
                            p1File = oggFile;
                        }
                        else if (fileName.EndsWith("-p2", StringComparison.OrdinalIgnoreCase))
                        {
                            p2File = oggFile;
                        }
                        else if (singleFile == null)
                        {
                            // 单段模式：取第一个非p1/p2的文件
                            singleFile = oggFile;
                        }
                    }

                    // 判断音频模式
                    if (p1File != null && p2File != null)
                    {
                        // 多段音频模式
                        preset.AudioFilePath = presetRelativePath + "/" + Path.GetFileNameWithoutExtension(p1File);
                        preset.AudioFilePath2 = presetRelativePath + "/" + Path.GetFileNameWithoutExtension(p2File);
                        Debug.Log($"{LOG_TAG} 已加载预设(多段): {preset.Name} -> p1={preset.AudioFilePath}, p2={preset.AudioFilePath2}");
                    }
                    else
                    {
                        // 单段音频模式
                        if (p1File != null && p2File == null)
                        {
                            // 只有p1没有p2，当作单段处理
                            Debug.LogWarning($"{LOG_TAG} 预设 '{dirName}' 只有p1没有p2，当作单段音频处理");
                            preset.AudioFilePath = presetRelativePath + "/" + Path.GetFileNameWithoutExtension(p1File);
                        }
                        else
                        {
                            preset.AudioFilePath = presetRelativePath + "/" + Path.GetFileNameWithoutExtension(singleFile);
                        }
                        Debug.Log($"{LOG_TAG} 已加载预设(单段): {preset.Name} -> {preset.AudioFilePath}");
                    }

                    // 加载配置文件（如果存在）
                    LoadPresetConfig(preset, dir);

                    presets.Add(preset);
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"{LOG_TAG} 扫描语音包目录失败: {ex.Message}");
            }

            Debug.Log($"{LOG_TAG} 共加载 {presets.Count} 个预设");
        }

        /// <summary>
        /// 加载预设配置文件
        /// 读取语音包目录下的 preset.cfg 文件，解析延迟时间配置
        /// 配置文件使用KSP的ConfigNode格式，与ModuleManager兼容
        /// 
        /// 配置文件格式：
        /// COUNTDOWN_PRESET
        /// {
        ///     singleStageDelay = 2.0
        ///     multiStageDelay = 0.3
        /// }
        /// 
        /// 注意：startEngineBeforeSeparation 仅在UI上勾选，不写入配置文件。
        /// 延迟时间参数仅在UI勾选"先启动发动机再分离"时才生效。
        /// </summary>
        /// <param name="preset">要加载配置的预设对象</param>
        /// <param name="directoryPath">预设目录的完整路径</param>
        private void LoadPresetConfig(CountdownPreset preset, string directoryPath)
        {
            string configPath = Path.Combine(directoryPath, PRESET_CONFIG_FILE);
            if (!File.Exists(configPath))
            {
                // 无配置文件，使用默认值
                return;
            }

            try
            {
                ConfigNode config = ConfigNode.Load(configPath);
                if (config == null)
                {
                    Debug.LogWarning($"{LOG_TAG} 预设 '{preset.Name}' 的配置文件解析失败");
                    return;
                }

                ConfigNode node = config.GetNode(CONFIG_NODE_NAME);
                if (node == null)
                {
                    if (config.name == CONFIG_NODE_NAME)
                    {
                        node = config;
                    }
                    else
                    {
                        Debug.LogWarning($"{LOG_TAG} 预设 '{preset.Name}' 配置文件中未找到{CONFIG_NODE_NAME}节点");
                        return;
                    }
                }

                // singleStageDelay: 单段模式下第二次分级的延迟（秒）
                float singleDelay = 0f;
                if (node.TryGetValue("singleStageDelay", ref singleDelay))
                {
                    preset.SingleStageDelay = Mathf.Max(0.1f, singleDelay);
                    Debug.Log($"{LOG_TAG} 预设 '{preset.Name}' 配置: singleStageDelay={preset.SingleStageDelay}");
                }

                // multiStageDelay: 多段模式下第二次分级的延迟（秒）
                float multiDelay = 0f;
                if (node.TryGetValue("multiStageDelay", ref multiDelay))
                {
                    preset.MultiStageDelay = Mathf.Max(0.05f, multiDelay);
                    Debug.Log($"{LOG_TAG} 预设 '{preset.Name}' 配置: multiStageDelay={preset.MultiStageDelay}");
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"{LOG_TAG} 加载预设 '{preset.Name}' 配置文件失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 获取所有预设名称列表
        /// </summary>
        public List<string> GetPresetNames()
        {
            var names = new List<string>(presets.Count);
            foreach (var preset in presets)
            {
                names.Add(preset.Name);
            }
            return names;
        }

        /// <summary>
        /// 根据预设名称获取预设的音频文件路径（GameDatabase格式，不含扩展名）
        /// </summary>
        public string GetPresetAudioPath(string name)
        {
            foreach (var preset in presets)
            {
                if (preset.Name == name)
                {
                    return preset.AudioFilePath;
                }
            }
            return null;
        }

        /// <summary>
        /// 根据索引获取预设名称
        /// </summary>
        public string GetPresetNameByIndex(int index)
        {
            if (index >= 0 && index < presets.Count)
            {
                return presets[index].Name;
            }
            return null;
        }

        /// <summary>
        /// 根据预设名称获取预设对象
        /// </summary>
        public CountdownPreset GetPreset(string name)
        {
            foreach (var preset in presets)
            {
                if (preset.Name == name)
                {
                    return preset;
                }
            }
            return null;
        }

        /// <summary>
        /// 根据索引获取预设对象
        /// </summary>
        public CountdownPreset GetPresetByIndex(int index)
        {
            if (index >= 0 && index < presets.Count)
            {
                return presets[index];
            }
            return null;
        }
    }
}
