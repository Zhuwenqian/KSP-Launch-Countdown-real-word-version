/**
 * Localization.cs - KSP1 发射倒计时本地化系统
 *
 * 用途：提供模组界面的多语言支持。
 * 通过读取 GameData/KSPLaunchCountdown/Localization/ 目录下的语言配置文件，
 * 根据当前游戏语言或用户选择显示对应的文本。
 *
 * 支持语言：
 *   - 简体中文 (zh-cn)
 *   - 英文 (en-us)
 *   - 俄文 (ru-ru)
 *
 * 语言文件格式（ConfigNode）：
 *   Localization
 *   {
 *       zh-cn
 *       {
 *           #KSPLaunchCountdown_WindowTitle = 发射倒计时控制
 *           ...
 *       }
 *       en-us
 *       {
 *           #KSPLaunchCountdown_WindowTitle = Launch Countdown Control
 *           ...
 *       }
 *   }
 *
 * 设计说明：
 *   - 每个文本使用唯一键（如 #KSPLaunchCountdown_WindowTitle）
 *   - 键名前缀 # 符合 KSP 本地化系统约定
 *   - 若当前语言无对应翻译，自动回退到英文，再回退到键名本身
 *   - 所有路径采用相对路径，兼容 Windows/Linux/Docker
 *
 * 依赖：
 *   - Assembly-CSharp.dll (KSP核心，提供KSPUtil、ConfigNode、Localizer)
 *   - UnityEngine.CoreModule.dll (Unity核心，提供Debug日志)
 */

using System.Collections.Generic;
using System.IO;
using UnityEngine;
using KSP;

namespace KSPLaunchCountdown
{
    /// <summary>
    /// 本地化管理器
    /// 负责加载语言文件并根据当前语言提供翻译文本
    /// </summary>
    public class Localization
    {
        /// <summary>日志标签</summary>
        private const string LOG_TAG = "[KSPLaunchCountdown]";

        /// <summary>语言文件在GameData下的相对路径</summary>
        private const string LOCALIZATION_BASE_PATH = "KSPLaunchCountdown/Localization";

        /// <summary>语言文件扩展名</summary>
        private const string LANGUAGE_FILE_EXTENSION = ".cfg";

        /// <summary>ConfigNode根节点名称</summary>
        private const string CONFIG_ROOT_NODE = "Localization";

        /// <summary>回退语言（当当前语言缺少翻译时使用）</summary>
        private const string FALLBACK_LANGUAGE = "en-us";

        /// <summary>当前语言代码（如 zh-cn、en-us、ru-ru）</summary>
        private string currentLanguage;

        /// <summary>已加载的翻译字典：键 -> 翻译文本</summary>
        private readonly Dictionary<string, string> translations = new Dictionary<string, string>();

        /// <summary>
        /// 预定义的文本键列表
        /// 集中管理所有可翻译文本，避免硬编码字符串散落各处
        /// </summary>
        public static class Keys
        {
            public const string WindowTitle = "#KSPLaunchCountdown_WindowTitle";
            public const string SelectPreset = "#KSPLaunchCountdown_SelectPreset";
            public const string NoPresetsFound = "#KSPLaunchCountdown_NoPresetsFound";
            public const string StartEngineBeforeSeparation = "#KSPLaunchCountdown_StartEngineBeforeSeparation";
            public const string LaunchButton = "#KSPLaunchCountdown_LaunchButton";
            public const string CancelButton = "#KSPLaunchCountdown_CancelButton";
            public const string VolumeLabel = "#KSPLaunchCountdown_VolumeLabel";
            public const string SafetyCheckFailedTitle = "#KSPLaunchCountdown_SafetyCheckFailedTitle";
            public const string SafetyCheckNotOnLaunchPad = "#KSPLaunchCountdown_SafetyCheckNotOnLaunchPad";
            public const string SafetyCheckAlreadyCountingDown = "#KSPLaunchCountdown_SafetyCheckAlreadyCountingDown";
            public const string SafetyCheckEngineAlreadyRunning = "#KSPLaunchCountdown_SafetyCheckEngineAlreadyRunning";
            public const string ForceLaunchButton = "#KSPLaunchCountdown_ForceLaunchButton";
            public const string AbortLaunchButton = "#KSPLaunchCountdown_AbortLaunchButton";
        }

        /// <summary>
        /// 初始化本地化系统
        /// 自动检测KSP当前语言并加载对应的语言文件
        /// </summary>
        public void Initialize()
        {
            // 获取KSP当前语言设置，默认英文
            currentLanguage = GetCurrentLanguage();
            Debug.Log($"{LOG_TAG} 当前语言: {currentLanguage}");

            LoadLanguageFiles();
        }

        /// <summary>
        /// 获取当前KSP语言代码
        /// KSP的Localizer.CurrentLanguage返回类似"zh-cn"、"en-us"的代码
        /// </summary>
        private string GetCurrentLanguage()
        {
            try
            {
                // 通过反射访问 KSP.Localization.Localizer.CurrentLanguage
                // 因为精简版DLL可能不包含 Localizer 类型
                var localizerType = System.Type.GetType("KSP.Localization.Localizer, Assembly-CSharp");
                if (localizerType != null)
                {
                    var currentLanguageProperty = localizerType.GetProperty("currentLanguage",
                        System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
                    if (currentLanguageProperty != null)
                    {
                        string lang = currentLanguageProperty.GetValue(null) as string;
                        if (!string.IsNullOrEmpty(lang))
                        {
                            return lang.ToLowerInvariant();
                        }
                    }
                }
            }
            catch (System.Exception ex)
            {
                Debug.LogWarning($"{LOG_TAG} 获取KSP语言失败: {ex.Message}，使用默认语言");
            }

            return FALLBACK_LANGUAGE;
        }

        /// <summary>
        /// 加载所有语言文件
        /// 扫描 Localization/ 目录下的 .cfg 文件，解析当前语言和回退语言的翻译
        /// </summary>
        private void LoadLanguageFiles()
        {
            translations.Clear();

            string basePath = KSPUtil.ApplicationRootPath + "GameData/" + LOCALIZATION_BASE_PATH;
            if (!Directory.Exists(basePath))
            {
                Debug.LogWarning($"{LOG_TAG} 本地化目录不存在: {basePath}");
                return;
            }

            try
            {
                string[] cfgFiles = Directory.GetFiles(basePath, "*" + LANGUAGE_FILE_EXTENSION);
                foreach (string file in cfgFiles)
                {
                    LoadLanguageFile(file);
                }
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"{LOG_TAG} 扫描语言文件失败: {ex.Message}");
            }

            Debug.Log($"{LOG_TAG} 本地化加载完成，共 {translations.Count} 条翻译");
        }

        /// <summary>
        /// 加载单个语言文件
        /// 只解析当前语言节点和回退语言节点中的键值对
        /// </summary>
        /// <param name="filePath">语言文件完整路径</param>
        private void LoadLanguageFile(string filePath)
        {
            try
            {
                ConfigNode rootNode = ConfigNode.Load(filePath);
                if (rootNode == null)
                {
                    Debug.LogWarning($"{LOG_TAG} 语言文件加载失败: {filePath}");
                    return;
                }

                ConfigNode localizationNode = rootNode.GetNode(CONFIG_ROOT_NODE);
                if (localizationNode == null)
                {
                    // 如果根节点就是 Localization，直接用它
                    if (rootNode.name == CONFIG_ROOT_NODE)
                    {
                        localizationNode = rootNode;
                    }
                    else
                    {
                        Debug.LogWarning($"{LOG_TAG} 语言文件中未找到 {CONFIG_ROOT_NODE} 节点: {filePath}");
                        return;
                    }
                }

                // 优先加载当前语言
                LoadLanguageNode(localizationNode, currentLanguage);
                // 然后加载回退语言（用于补充缺失的翻译）
                if (currentLanguage != FALLBACK_LANGUAGE)
                {
                    LoadLanguageNode(localizationNode, FALLBACK_LANGUAGE);
                }
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"{LOG_TAG} 加载语言文件失败 {filePath}: {ex.Message}");
            }
        }

        /// <summary>
        /// 从ConfigNode中加载指定语言节点的所有键值对
        /// 已存在的键不会被覆盖（保证当前语言优先）
        /// </summary>
        private void LoadLanguageNode(ConfigNode localizationNode, string language)
        {
            ConfigNode langNode = localizationNode.GetNode(language);
            if (langNode == null) return;

            foreach (ConfigNode.Value value in langNode.values)
            {
                // 键名通常以 # 开头，符合KSP本地化约定
                string key = value.name;
                if (!translations.ContainsKey(key))
                {
                    translations[key] = value.value;
                }
            }
        }

        /// <summary>
        /// 获取指定键的翻译文本
        /// 如果找不到翻译，先回退到英文，再回退到键名本身
        /// </summary>
        /// <param name="key">文本键（如 #KSPLaunchCountdown_WindowTitle）</param>
        /// <returns>翻译后的文本</returns>
        public string GetString(string key)
        {
            if (string.IsNullOrEmpty(key)) return string.Empty;

            if (translations.TryGetValue(key, out string value))
            {
                return value;
            }

            // 未找到翻译时返回键名（方便开发者发现缺失的翻译）
            Debug.LogWarning($"{LOG_TAG} 缺少翻译键: {key}");
            return key;
        }

        /// <summary>
        /// 获取指定键的翻译文本（支持格式化参数）
        /// </summary>
        public string GetString(string key, params object[] args)
        {
            string template = GetString(key);
            try
            {
                return string.Format(template, args);
            }
            catch (System.Exception)
            {
                return template;
            }
        }
    }
}
