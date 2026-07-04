/**
 * SettingsManager.cs - KSP1 发射倒计时设置管理器
 *
 * 用途：负责管理模组的全局设置持久化。
 * 当前管理的设置项包括：
 *   - 倒计时语音音量
 *   - RO（Realism Overhaul）延迟倍率（仅按存档保存，安装 RO 时生效）
 * 设置保存在当前存档目录下的独立配置文件中：
 *   saves/<存档名>/KSPLaunchCountdown/Settings.cfg
 * 确保按存档分别保存，兼容 Windows/Linux/Docker。
 *
 * 设计说明：
 *   - 使用 KSP 的 ConfigNode 作为配置格式，与 KSP 原生配置系统兼容。
 *   - 所有路径采用相对路径，基于 KSPUtil.ApplicationRootPath 构建。
 *   - 设置读取失败时使用默认值，保证模组在任意场景下可用。
 *   - RODelayMultiplier 用于全局微调 RO 环境下的分级延迟，范围 0.1x ~ 3.0x。
 *
 * 依赖：
 *   - Assembly-CSharp.dll (KSP核心，提供KSPUtil、ConfigNode、HighLogic等)
 *   - UnityEngine.CoreModule.dll (Unity核心，提供Debug日志)
 */

using System.IO;
using UnityEngine;

namespace KSPLaunchCountdown
{
    /// <summary>
    /// 设置管理器
    /// 负责加载、保存和提供全局设置项访问。
    /// 这是一个纯逻辑类，不需要挂载到GameObject。
    /// </summary>
    public class SettingsManager
    {
        /// <summary>日志标签</summary>
        private const string LOG_TAG = "[KSPLaunchCountdown]";

        /// <summary>设置文件在当前存档下的相对路径</summary>
        private const string SETTINGS_RELATIVE_PATH = "KSPLaunchCountdown/Settings.cfg";

        /// <summary>ConfigNode根节点名称</summary>
        private const string CONFIG_NODE_NAME = "KSPLaunchCountdownSettings";

        /// <summary>配置节点中音量字段的键名</summary>
        private const string VOLUME_KEY = "CountdownVolume";

        /// <summary>配置节点中 RO 延迟倍率字段的键名</summary>
        private const string RO_DELAY_MULTIPLIER_KEY = "RODelayMultiplier";

        /// <summary>
        /// 默认音量（0.0~1.0）
        /// 可调整：修改此值改变模组首次加载时的默认音量
        /// </summary>
        private const float DEFAULT_VOLUME = 1.0f;

        /// <summary>
        /// 默认 RO 延迟倍率（1.0x）
        /// 可调整：修改此值改变安装 RO 后首次加载时的默认倍率
        /// </summary>
        private const float DEFAULT_RO_DELAY_MULTIPLIER = 1.0f;

        /// <summary>
        /// 当前倒计时语音音量
        /// 取值范围 0.0（静音）到 1.0（最大音量）
        /// </summary>
        private float countdownVolume = DEFAULT_VOLUME;

        /// <summary>
        /// 当前 RO 延迟倍率
        /// 取值范围 0.1x ~ 3.0x，安装 RO 时用于全局微调分级延迟
        /// </summary>
        private float roDelayMultiplier = DEFAULT_RO_DELAY_MULTIPLIER;

        /// <summary>
        /// 获取设置文件的完整路径
        /// 路径格式：KSP根目录/saves/<当前存档名>/KSPLaunchCountdown/Settings.cfg
        /// </summary>
        private string GetSettingsFilePath()
        {
            string saveFolder = HighLogic.SaveFolder;
            if (string.IsNullOrEmpty(saveFolder))
            {
                // 如果当前没有活跃存档，使用默认存档目录
                saveFolder = "default";
            }

            return Path.Combine(
                KSPUtil.ApplicationRootPath,
                "saves",
                saveFolder,
                SETTINGS_RELATIVE_PATH
            );
        }

        /// <summary>
        /// 初始化设置管理器，从存档配置中加载设置
        /// </summary>
        public void Load()
        {
            try
            {
                string filePath = GetSettingsFilePath();
                if (!File.Exists(filePath))
                {
                    Debug.Log($"{LOG_TAG} 设置文件不存在，使用默认音量: {DEFAULT_VOLUME:F2}");
                    countdownVolume = DEFAULT_VOLUME;
                    return;
                }

                ConfigNode rootNode = ConfigNode.Load(filePath);
                if (rootNode == null)
                {
                    Debug.LogWarning($"{LOG_TAG} 设置文件解析失败，使用默认音量");
                    countdownVolume = DEFAULT_VOLUME;
                    return;
                }

                ConfigNode settingsNode = rootNode.GetNode(CONFIG_NODE_NAME);
                if (settingsNode == null)
                {
                    if (rootNode.name == CONFIG_NODE_NAME)
                    {
                        settingsNode = rootNode;
                    }
                    else
                    {
                        Debug.LogWarning($"{LOG_TAG} 设置文件中未找到 {CONFIG_NODE_NAME} 节点");
                        countdownVolume = DEFAULT_VOLUME;
                        return;
                    }
                }

                // 读取音量配置，若不存在或解析失败则使用默认值
                string valueStr = settingsNode.GetValue(VOLUME_KEY);
                if (float.TryParse(valueStr, System.Globalization.NumberStyles.Float,
                    System.Globalization.CultureInfo.InvariantCulture, out float parsedVolume))
                {
                    countdownVolume = Mathf.Clamp01(parsedVolume);
                }
                else
                {
                    countdownVolume = DEFAULT_VOLUME;
                }

                // 读取 RO 延迟倍率配置，若不存在或解析失败则使用默认值
                string roMultiplierStr = settingsNode.GetValue(RO_DELAY_MULTIPLIER_KEY);
                if (float.TryParse(roMultiplierStr, System.Globalization.NumberStyles.Float,
                    System.Globalization.CultureInfo.InvariantCulture, out float parsedMultiplier))
                {
                    roDelayMultiplier = Mathf.Clamp(parsedMultiplier, 0.1f, 3.0f);
                }
                else
                {
                    roDelayMultiplier = DEFAULT_RO_DELAY_MULTIPLIER;
                }

                Debug.Log($"{LOG_TAG} 设置加载完成，音量: {countdownVolume:F2}, RO延迟倍率: {roDelayMultiplier:F2}x");
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"{LOG_TAG} 加载设置失败: {ex.Message}，使用默认值");
                countdownVolume = DEFAULT_VOLUME;
                roDelayMultiplier = DEFAULT_RO_DELAY_MULTIPLIER;
            }
        }

        /// <summary>
        /// 保存当前设置到存档配置
        /// </summary>
        public void Save()
        {
            try
            {
                string filePath = GetSettingsFilePath();
                string directoryPath = Path.GetDirectoryName(filePath);

                // 确保设置文件所在目录存在
                if (!Directory.Exists(directoryPath))
                {
                    Directory.CreateDirectory(directoryPath);
                }

                ConfigNode settingsNode = new ConfigNode(CONFIG_NODE_NAME);
                settingsNode.SetValue(VOLUME_KEY, countdownVolume.ToString("F3"), true);
                settingsNode.SetValue(RO_DELAY_MULTIPLIER_KEY, roDelayMultiplier.ToString("F2"), true);

                ConfigNode rootNode = new ConfigNode();
                rootNode.AddNode(settingsNode);
                rootNode.Save(filePath);

                Debug.Log($"{LOG_TAG} 设置已保存，音量: {countdownVolume:F2}, RO延迟倍率: {roDelayMultiplier:F2}x");
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"{LOG_TAG} 保存设置失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 获取或设置倒计时语音音量
        /// 取值范围 0.0（静音）到 1.0（最大音量）
        /// </summary>
        public float CountdownVolume
        {
            get => countdownVolume;
            set
            {
                countdownVolume = Mathf.Clamp01(value);
                // 设置变更后立即保存，避免玩家重启游戏后丢失
                Save();
            }
        }

        /// <summary>
        /// 获取或设置 RO（Realism Overhaul）延迟倍率
        /// 取值范围 0.1x ~ 3.0x，默认值 1.0x
        /// 安装 RO 时，所有语音包的 RO 专用延迟会乘以该倍率作为最终延迟
        /// 未安装 RO 时，此设置不会影响 Stock 延迟
        /// </summary>
        public float RODelayMultiplier
        {
            get => roDelayMultiplier;
            set
            {
                roDelayMultiplier = Mathf.Clamp(value, 0.1f, 3.0f);
                // 设置变更后立即保存，避免玩家重启游戏后丢失
                Save();
            }
        }
    }
}
