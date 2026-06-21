/**
 * KSPApiHelper.cs - KSP1 API反射辅助类
 *
 * 用途：封装对KSP核心API的反射调用，解决精简版Assembly-CSharp.dll
 * 缺少类型定义导致的编译问题。运行时KSP游戏自带完整DLL，所有API均可正常调用。
 *
 * 封装的API：
 *   - ApplicationLauncher: 工具栏按钮管理
 *   - StageManager: 分级管理
 *   - UIMasterController: UI显示/隐藏控制
 *   - HighLogic: 全局状态和UI皮肤
 *   - FlightInputHandler: 飞行输入控制
 *   - GameEvents: 游戏事件系统
 *
 * 依赖：
 *   - Assembly-CSharp.dll (KSP核心，精简版即可，运行时使用完整版)
 *   - UnityEngine.CoreModule.dll (Unity核心)
 */

using System;
using System.Reflection;
using UnityEngine;

namespace KSPLaunchCountdown
{
    /// <summary>
    /// KSP API反射辅助类
    /// 提供对KSP核心API的类型解析和方法调用封装，
    /// 兼容精简版Assembly-CSharp.dll编译环境
    /// </summary>
    public static class KSPApiHelper
    {
        /// <summary>日志标签</summary>
        private const string LOG_TAG = "[KSPLaunchCountdown]";

        /// <summary>缓存已解析的KSP程序集</summary>
        private static Assembly kspAssembly;

        /// <summary>
        /// 获取KSP核心程序集（Assembly-CSharp）
        /// </summary>
        private static Assembly GetKspAssembly()
        {
            if (kspAssembly != null) return kspAssembly;

            // 查找Assembly-CSharp程序集
            foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
            {
                if (asm.GetName().Name == "Assembly-CSharp")
                {
                    kspAssembly = asm;
                    return kspAssembly;
                }
            }

            Debug.LogError($"{LOG_TAG} 未找到Assembly-CSharp程序集");
            return null;
        }

        /// <summary>
        /// 根据类型名称解析KSP类型
        /// </summary>
        /// <param name="typeName">类型全名（含命名空间），如 "ApplicationLauncher"</param>
        /// <returns>解析到的Type，未找到返回null</returns>
        private static Type ResolveType(string typeName)
        {
            var asm = GetKspAssembly();
            if (asm == null) return null;

            Type type = asm.GetType(typeName);
            if (type == null)
            {
                // 尝试KSP命名空间
                type = asm.GetType("KSP." + typeName);
            }
            return type;
        }

        #region ApplicationLauncher 相关

        /// <summary>ApplicationLauncher类型缓存</summary>
        private static Type appLauncherType;

        /// <summary>获取ApplicationLauncher类型</summary>
        public static Type GetApplicationLauncherType()
        {
            if (appLauncherType == null)
            {
                appLauncherType = ResolveType("ApplicationLauncher");
            }
            return appLauncherType;
        }

        /// <summary>
        /// 获取ApplicationLauncher单例实例
        /// </summary>
        /// <returns>ApplicationLauncher实例，失败返回null</returns>
        public static object GetApplicationLauncherInstance()
        {
            var type = GetApplicationLauncherType();
            if (type == null) return null;

            var instanceProp = type.GetProperty("Instance",
                BindingFlags.Public | BindingFlags.Static);
            return instanceProp?.GetValue(null);
        }

        /// <summary>
        /// 检查ApplicationLauncher是否就绪
        /// </summary>
        public static bool IsApplicationLauncherReady()
        {
            var type = GetApplicationLauncherType();
            if (type == null) return false;

            var readyProp = type.GetProperty("Ready",
                BindingFlags.Public | BindingFlags.Static);
            if (readyProp == null) return false;

            return (bool)readyProp.GetValue(null);
        }

        /// <summary>
        /// 添加模组按钮到ApplicationLauncher
        /// </summary>
        /// <param name="onToggle">激活回调</param>
        /// <param name="onUntoggle">取消激活回调</param>
        /// <param name="onHover">悬停回调</param>
        /// <param name="onHoverOut">悬停移出回调</param>
        /// <param name="onEnable">启用回调</param>
        /// <param name="onDisable">禁用回调</param>
        /// <param name="visibleInScenes">显示场景（AppScenes枚举值）</param>
        /// <param name="texture">按钮图标纹理</param>
        /// <returns>按钮对象（ApplicationLauncherButton实例），失败返回null</returns>
        public static object AddModApplication(
            Action onToggle, Action onUntoggle,
            Action onHover, Action onHoverOut,
            Action onEnable, Action onDisable,
            int visibleInScenes, Texture2D texture)
        {
            var instance = GetApplicationLauncherInstance();
            if (instance == null) return null;

            var type = GetApplicationLauncherType();
            var method = type.GetMethod("AddModApplication", BindingFlags.Public | BindingFlags.Instance);
            if (method == null)
            {
                Debug.LogError($"{LOG_TAG} 未找到AddModApplication方法");
                return null;
            }

            // 获取AppScenes枚举类型
            Type appScenesType = ResolveType("ApplicationLauncher+AppScenes");
            if (appScenesType == null)
            {
                // 尝试嵌套类型
                appScenesType = GetApplicationLauncherType().GetNestedType("AppScenes");
            }

            object scenesValue = appScenesType != null
                ? Enum.ToObject(appScenesType, visibleInScenes)
                : visibleInScenes;

            try
            {
                var result = method.Invoke(instance, new object[]
                {
                    onToggle, onUntoggle, onHover, onHoverOut,
                    onEnable, onDisable, scenesValue, texture
                });
                return result;
            }
            catch (Exception ex)
            {
                Debug.LogError($"{LOG_TAG} AddModApplication调用失败: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// 移除模组按钮
        /// </summary>
        /// <param name="button">按钮对象（ApplicationLauncherButton实例）</param>
        public static void RemoveModApplication(object button)
        {
            if (button == null) return;

            var instance = GetApplicationLauncherInstance();
            if (instance == null) return;

            var type = GetApplicationLauncherType();
            var method = type.GetMethod("RemoveModApplication", BindingFlags.Public | BindingFlags.Instance);
            if (method == null)
            {
                Debug.LogError($"{LOG_TAG} 未找到RemoveModApplication方法");
                return;
            }

            try
            {
                method.Invoke(instance, new object[] { button });
            }
            catch (Exception ex)
            {
                Debug.LogError($"{LOG_TAG} RemoveModApplication调用失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 获取AppScenes.FLIGHT的枚举值
        /// </summary>
        public static int GetAppScenesFlight()
        {
            Type appScenesType = GetApplicationLauncherType()?.GetNestedType("AppScenes");
            if (appScenesType == null) return 4; // FLIGHT通常是4

            var names = Enum.GetNames(appScenesType);
            var values = Enum.GetValues(appScenesType);
            for (int i = 0; i < names.Length; i++)
            {
                if (names[i] == "FLIGHT")
                {
                    return (int)values.GetValue(i);
                }
            }
            return 4; // 默认值
        }

        #endregion

        #region GameEvents 相关

        /// <summary>
        /// 注册ApplicationLauncher就绪事件
        /// </summary>
        /// <param name="callback">回调方法</param>
        public static void AddOnLauncherReadyEvent(Action callback)
        {
            var gameEventsType = ResolveType("GameEvents");
            if (gameEventsType == null)
            {
                Debug.LogError($"{LOG_TAG} 未找到GameEvents类型");
                return;
            }

            var readyEvent = gameEventsType.GetEvent("onGUIApplicationLauncherReady",
                BindingFlags.Public | BindingFlags.Static);
            if (readyEvent == null)
            {
                Debug.LogError($"{LOG_TAG} 未找到onGUIApplicationLauncherReady事件");
                return;
            }

            // GameEvents使用EventData<T>，需要通过反射注册
            var readyField = gameEventsType.GetField("onGUIApplicationLauncherReady",
                BindingFlags.Public | BindingFlags.Static);
            if (readyField == null) return;

            object eventData = readyField.GetValue(null);
            if (eventData == null) return;

            // EventData<T>.Add(callback)
            var addMethod = eventData.GetType().GetMethod("Add", BindingFlags.Public | BindingFlags.Instance);
            if (addMethod != null)
            {
                addMethod.Invoke(eventData, new object[] { callback });
            }
        }

        /// <summary>
        /// 注销ApplicationLauncher就绪事件
        /// </summary>
        /// <param name="callback">回调方法</param>
        public static void RemoveOnLauncherReadyEvent(Action callback)
        {
            var gameEventsType = ResolveType("GameEvents");
            if (gameEventsType == null) return;

            var readyField = gameEventsType.GetField("onGUIApplicationLauncherReady",
                BindingFlags.Public | BindingFlags.Static);
            if (readyField == null) return;

            object eventData = readyField.GetValue(null);
            if (eventData == null) return;

            var removeMethod = eventData.GetType().GetMethod("Remove", BindingFlags.Public | BindingFlags.Instance);
            if (removeMethod != null)
            {
                removeMethod.Invoke(eventData, new object[] { callback });
            }
        }

        /// <summary>
        /// 注册ApplicationLauncher销毁事件
        /// </summary>
        /// <param name="callback">回调方法</param>
        public static void AddOnLauncherDestroyedEvent(Action callback)
        {
            var gameEventsType = ResolveType("GameEvents");
            if (gameEventsType == null) return;

            var destroyedField = gameEventsType.GetField("onGUIApplicationLauncherDestroyed",
                BindingFlags.Public | BindingFlags.Static);
            if (destroyedField == null) return;

            object eventData = destroyedField.GetValue(null);
            if (eventData == null) return;

            var addMethod = eventData.GetType().GetMethod("Add", BindingFlags.Public | BindingFlags.Instance);
            if (addMethod != null)
            {
                addMethod.Invoke(eventData, new object[] { callback });
            }
        }

        /// <summary>
        /// 注销ApplicationLauncher销毁事件
        /// </summary>
        /// <param name="callback">回调方法</param>
        public static void RemoveOnLauncherDestroyedEvent(Action callback)
        {
            var gameEventsType = ResolveType("GameEvents");
            if (gameEventsType == null) return;

            var destroyedField = gameEventsType.GetField("onGUIApplicationLauncherDestroyed",
                BindingFlags.Public | BindingFlags.Static);
            if (destroyedField == null) return;

            object eventData = destroyedField.GetValue(null);
            if (eventData == null) return;

            var removeMethod = eventData.GetType().GetMethod("Remove", BindingFlags.Public | BindingFlags.Instance);
            if (removeMethod != null)
            {
                removeMethod.Invoke(eventData, new object[] { callback });
            }
        }

        #endregion

        #region StageManager 相关

        /// <summary>
        /// 激活下一级（等效按空格键）
        /// </summary>
        public static void ActivateNextStage()
        {
            var stageManagerType = ResolveType("StageManager");
            if (stageManagerType == null)
            {
                Debug.LogError($"{LOG_TAG} 未找到StageManager类型");
                return;
            }

            var method = stageManagerType.GetMethod("ActivateNextStage",
                BindingFlags.Public | BindingFlags.Static);
            if (method == null)
            {
                // 尝试实例方法
                var instanceProp = stageManagerType.GetProperty("Instance",
                    BindingFlags.Public | BindingFlags.Static);
                if (instanceProp == null) return;

                object instance = instanceProp.GetValue(null);
                if (instance == null) return;

                method = stageManagerType.GetMethod("ActivateNextStage",
                    BindingFlags.Public | BindingFlags.Instance);
                method?.Invoke(instance, null);
            }
            else
            {
                method.Invoke(null, null);
            }
        }

        #endregion

        #region UIMasterController 相关

        /// <summary>
        /// 隐藏KSP游戏UI
        /// </summary>
        public static void HideUI()
        {
            var uiMasterType = ResolveType("UIMasterController");
            if (uiMasterType == null)
            {
                // 尝试KSP.UI命名空间
                uiMasterType = ResolveType("KSP.UI.UIMasterController");
            }
            if (uiMasterType == null)
            {
                Debug.LogWarning($"{LOG_TAG} 未找到UIMasterController类型，尝试GameEvents方式");
                // 备用方案：通过GameEvents隐藏UI
                FireGameEvent("onHideUI");
                return;
            }

            var instanceProp = uiMasterType.GetProperty("Instance",
                BindingFlags.Public | BindingFlags.Static);
            if (instanceProp == null) return;

            object instance = instanceProp.GetValue(null);
            if (instance == null) return;

            var hideMethod = uiMasterType.GetMethod("Hide", BindingFlags.Public | BindingFlags.Instance);
            hideMethod?.Invoke(instance, null);
        }

        /// <summary>
        /// 显示KSP游戏UI
        /// </summary>
        public static void ShowUI()
        {
            var uiMasterType = ResolveType("UIMasterController");
            if (uiMasterType == null)
            {
                uiMasterType = ResolveType("KSP.UI.UIMasterController");
            }
            if (uiMasterType == null)
            {
                Debug.LogWarning($"{LOG_TAG} 未找到UIMasterController类型，尝试GameEvents方式");
                FireGameEvent("onShowUI");
                return;
            }

            var instanceProp = uiMasterType.GetProperty("Instance",
                BindingFlags.Public | BindingFlags.Static);
            if (instanceProp == null) return;

            object instance = instanceProp.GetValue(null);
            if (instance == null) return;

            var showMethod = uiMasterType.GetMethod("Show", BindingFlags.Public | BindingFlags.Instance);
            showMethod?.Invoke(instance, null);
        }

        /// <summary>
        /// 检查UI是否已隐藏
        /// </summary>
        public static bool IsUIHidden()
        {
            var uiMasterType = ResolveType("UIMasterController");
            if (uiMasterType == null) return false;

            var instanceProp = uiMasterType.GetProperty("Instance",
                BindingFlags.Public | BindingFlags.Static);
            if (instanceProp == null) return false;

            object instance = instanceProp.GetValue(null);
            if (instance == null) return false;

            var isHiddenProp = uiMasterType.GetProperty("isHidden",
                BindingFlags.Public | BindingFlags.Instance);
            if (isHiddenProp == null) return false;

            return (bool)isHiddenProp.GetValue(instance);
        }

        #endregion

        #region HighLogic 相关

        /// <summary>
        /// 获取HighLogic.UISkin（GUISkin）
        /// </summary>
        /// <returns>GUISkin实例，失败返回null</returns>
        public static GUISkin GetUISkin()
        {
            var highLogicType = ResolveType("HighLogic");
            if (highLogicType == null) return null;

            var skinProp = highLogicType.GetProperty("UISkin",
                BindingFlags.Public | BindingFlags.Static);
            if (skinProp == null) return null;

            var skinObj = skinProp.GetValue(null);
            if (skinObj is GUISkin guiSkin) return guiSkin;

            // 如果返回的是UISkinDef类型，尝试转换
            if (skinObj != null)
            {
                // UISkinDef有skin属性返回GUISkin
                var skinField = skinObj.GetType().GetProperty("skin",
                    BindingFlags.Public | BindingFlags.Instance);
                if (skinField != null)
                {
                    return skinField.GetValue(skinObj) as GUISkin;
                }
            }

            return null;
        }

        /// <summary>
        /// 检查当前是否在飞行场景
        /// </summary>
        public static bool IsFlightScene()
        {
            var highLogicType = ResolveType("HighLogic");
            if (highLogicType == null) return false;

            var prop = highLogicType.GetProperty("LoadedSceneIsFlight",
                BindingFlags.Public | BindingFlags.Static);
            if (prop == null) return false;

            return (bool)prop.GetValue(null);
        }

        #endregion

        #region FlightInputHandler 相关

        /// <summary>
        /// 设置油门值
        /// </summary>
        /// <param name="throttle">油门值（0.0~1.0）</param>
        public static void SetThrottle(float throttle)
        {
            var fihType = ResolveType("FlightInputHandler");
            if (fihType == null) return;

            var stateProp = fihType.GetProperty("state",
                BindingFlags.Public | BindingFlags.Static);
            if (stateProp == null) return;

            object state = stateProp.GetValue(null);
            if (state == null) return;

            var throttleProp = state.GetType().GetProperty("mainThrottle",
                BindingFlags.Public | BindingFlags.Instance);
            if (throttleProp == null) return;

            throttleProp.SetValue(state, throttle);
        }

        #endregion

        #region 通用辅助

        /// <summary>
        /// 触发GameEvents中的无参数事件
        /// </summary>
        /// <param name="eventName">事件字段名</param>
        private static void FireGameEvent(string eventName)
        {
            var gameEventsType = ResolveType("GameEvents");
            if (gameEventsType == null) return;

            var eventField = gameEventsType.GetField(eventName,
                BindingFlags.Public | BindingFlags.Static);
            if (eventField == null) return;

            object eventData = eventField.GetValue(null);
            if (eventData == null) return;

            var fireMethod = eventData.GetType().GetMethod("Fire",
                BindingFlags.Public | BindingFlags.Instance);
            fireMethod?.Invoke(eventData, null);
        }

        #endregion
    }
}
