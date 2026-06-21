/**
 * PresetManager.cs - KSP1 发射倒计时预设语音包管理器
 *
 * 用途：管理倒计时预设（语音包），负责扫描、加载和查询可用的倒计时语音包。
 * 每个语音包对应 Lauch Voice 目录下的一个子目录，子目录名即为预设名称，
 * 目录内的 .ogg 文件为该预设的倒计时音频。
 *
 * 目录结构示例：
 *   GameData/KSPLaunchCountdown/Lauch Voice/
 *   ├── DFH-1/          <- 预设名称 "DFH-1"（东风一号）
 *   │   └── DFH-1.ogg   <- 该预设的倒计时音频
 *   └── Atlas/          <- 预设名称 "Atlas"（后续扩展）
 *       └── Atlas.ogg
 *
 * 依赖：
 *   - Assembly-CSharp.dll (KSP核心，提供KSPUtil获取游戏路径)
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
    /// 每个预设对应一个语音包，包含预设名称、目录路径和音频文件路径
    /// </summary>
    public class CountdownPreset
    {
        /// <summary>预设名称，对应语音包目录名，如 "DFH-1"</summary>
        public string Name { get; set; }

        /// <summary>预设目录的完整路径（GameData下的相对路径）</summary>
        public string RelativePath { get; set; }

        /// <summary>预设的音频文件完整路径（用于GameDatabase加载）</summary>
        public string AudioFilePath { get; set; }
    }

    /// <summary>
    /// 预设语音包管理器
    /// 负责扫描GameData下的语音包目录，加载预设信息，提供查询接口
    /// </summary>
    public class PresetManager
    {
        /// <summary>日志标签，用于KSP日志输出</summary>
        private const string LOG_TAG = "[KSPLaunchCountdown]";

        /// <summary>
        /// 语音包在GameData下的相对路径
        /// 注意：目录名 "Lauch Voice" 保持了与项目资源目录一致的拼写
        /// </summary>
        private const string VOICE_BASE_PATH = "KSPLaunchCountdown/Lauch Voice";

        /// <summary>已加载的预设列表</summary>
        private readonly List<CountdownPreset> presets = new List<CountdownPreset>();

        /// <summary>获取已加载的预设数量</summary>
        public int PresetCount => presets.Count;

        /// <summary>
        /// 加载所有预设
        /// 扫描语音包基础目录下的所有子目录，每个子目录视为一个预设
        /// 每个子目录中的第一个.ogg文件作为该预设的音频文件
        /// </summary>
        public void LoadPresets()
        {
            presets.Clear();

            // 构建语音包目录的完整路径
            // KSPUtil.ApplicationRootPath 返回KSP安装根目录路径（以/结尾）
            string basePath = KSPUtil.ApplicationRootPath + "GameData/" + VOICE_BASE_PATH;

            Debug.Log($"{LOG_TAG} 扫描语音包目录: {basePath}");

            // 检查目录是否存在
            if (!Directory.Exists(basePath))
            {
                Debug.LogWarning($"{LOG_TAG} 语音包目录不存在: {basePath}");
                return;
            }

            try
            {
                // 扫描子目录，每个子目录为一个预设
                string[] subDirs = Directory.GetDirectories(basePath);
                foreach (string dir in subDirs)
                {
                    string dirName = new DirectoryInfo(dir).Name;
                    string presetRelativePath = VOICE_BASE_PATH + "/" + dirName;

                    // 查找目录中的.ogg文件
                    string[] oggFiles = Directory.GetFiles(dir, "*.ogg");
                    if (oggFiles.Length == 0)
                    {
                        Debug.LogWarning($"{LOG_TAG} 预设 '{dirName}' 目录中没有找到.ogg音频文件，跳过");
                        continue;
                    }

                    // 使用找到的第一个.ogg文件作为该预设的音频
                    string oggFile = oggFiles[0];
                    // 构建GameDatabase加载用的路径（不含扩展名）
                    string audioRelativePath = presetRelativePath + "/" + Path.GetFileNameWithoutExtension(oggFile);

                    var preset = new CountdownPreset
                    {
                        Name = dirName,
                        RelativePath = presetRelativePath,
                        AudioFilePath = audioRelativePath
                    };

                    presets.Add(preset);
                    Debug.Log($"{LOG_TAG} 已加载预设: {preset.Name} -> {preset.AudioFilePath}");
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"{LOG_TAG} 扫描语音包目录失败: {ex.Message}");
            }

            Debug.Log($"{LOG_TAG} 共加载 {presets.Count} 个预设");
        }

        /// <summary>
        /// 获取所有预设名称列表
        /// </summary>
        /// <returns>预设名称列表，如 ["DFH-1", "Atlas"]</returns>
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
        /// <param name="name">预设名称，如 "DFH-1"</param>
        /// <returns>音频文件的相对路径（GameDatabase格式），未找到返回null</returns>
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
        /// <param name="index">预设索引（从0开始）</param>
        /// <returns>预设名称，索引越界返回null</returns>
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
        /// <param name="name">预设名称</param>
        /// <returns>预设对象，未找到返回null</returns>
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
    }
}
