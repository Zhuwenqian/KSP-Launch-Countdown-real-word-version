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
 * 关键技术点：
 *   - KSP的GameEvents字段类型为EventDataVoid（继承自EventData<EventVoid>），
 *     其Add/Remove方法期望EventVoid.OnEvent委托类型，而非System.Action。
 *     两者签名相同（无参void），但类型不同，需通过Delegate.CreateDelegate转换。
 *   - AddModApplication的回调参数类型为RUIToggleButton.OnToggle，
 *     同样需要从System.Action转换。
 *
 * 依赖：
 *   - Assembly-CSharp.dll (KSP核心，精简版即可，运行时使用完整版)
 *   - UnityEngine.CoreModule.dll (Unity核心)
 */

using System;
using System.Linq;
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
        /// 依次尝试：全名 → KSP命名空间 → 遍历程序集所有类型
        /// </summary>
        private static Type ResolveType(string typeName)
        {
            var asm = GetKspAssembly();
            if (asm == null) return null;

            // 尝试直接全名
            Type type = asm.GetType(typeName);
            if (type != null) return type;

            // 尝试KSP命名空间
            type = asm.GetType("KSP." + typeName);
            if (type != null) return type;

            // 遍历程序集所有类型，按名称匹配
            foreach (var t in asm.GetTypes())
            {
                if (t.Name == typeName)
                {
                    return t;
                }
            }

            return null;
        }

        /// <summary>
        /// 将System.Action转换为KSP的委托类型
        /// KSP使用自定义委托类型（如EventVoid.OnEvent、RUIToggleButton.OnToggle），
        /// 它们的签名与System.Action相同（无参void），但类型不同。
        /// 通过Delegate.CreateDelegate创建目标委托类型的实例。
        /// </summary>
        /// <param name="action">System.Action回调</param>
        /// <param name="targetDelegateType">目标委托类型</param>
        /// <returns>转换后的委托实例</returns>
        private static Delegate ConvertActionToDelegate(Action action, Type targetDelegateType)
        {
            if (action == null) return null;
            // 从Action的MethodInfo创建目标委托类型的实例
            return Delegate.CreateDelegate(targetDelegateType, action.Target, action.Method);
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
        public static object GetApplicationLauncherInstance()
        {
            var type = GetApplicationLauncherType();
            if (type == null) return null;

            // 先尝试属性
            var instanceProp = type.GetProperty("Instance",
                BindingFlags.Public | BindingFlags.Static);
            if (instanceProp != null)
            {
                return instanceProp.GetValue(null);
            }

            // 再尝试字段
            var instanceField = type.GetField("Instance",
                BindingFlags.Public | BindingFlags.Static);
            return instanceField?.GetValue(null);
        }

        /// <summary>
        /// 检查ApplicationLauncher是否就绪
        /// </summary>
        public static bool IsApplicationLauncherReady()
        {
            var type = GetApplicationLauncherType();
            if (type == null) return false;

            // 先尝试属性
            var readyProp = type.GetProperty("Ready",
                BindingFlags.Public | BindingFlags.Static);
            if (readyProp != null)
            {
                return (bool)readyProp.GetValue(null);
            }

            // 再尝试字段
            var readyField = type.GetField("Ready",
                BindingFlags.Public | BindingFlags.Static);
            if (readyField != null)
            {
                return (bool)readyField.GetValue(null);
            }

            return false;
        }

        /// <summary>
        /// 添加模组按钮到ApplicationLauncher
        /// AddModApplication的回调参数类型为RUIToggleButton.OnToggle，
        /// 需要从System.Action转换。
        /// </summary>
        /// <returns>按钮对象（ApplicationLauncherButton实例），失败返回null</returns>
        public static object AddModApplication(
            Action onToggle, Action onUntoggle,
            Action onHover, Action onHoverOut,
            Action onEnable, Action onDisable,
            int visibleInScenes, Texture2D texture)
        {
            var instance = GetApplicationLauncherInstance();
            if (instance == null)
            {
                Debug.LogWarning($"{LOG_TAG} ApplicationLauncher实例不存在");
                return null;
            }

            var type = GetApplicationLauncherType();

            // 查找AddModApplication方法
            var method = type.GetMethod("AddModApplication", BindingFlags.Public | BindingFlags.Instance);
            if (method == null)
            {
                Debug.LogError($"{LOG_TAG} 未找到AddModApplication方法");
                return null;
            }

            // 获取回调参数的委托类型（RUIToggleButton.OnToggle）
            var parameters = method.GetParameters();
            Type callbackDelegateType = parameters[0].ParameterType;

            // 将Action转换为KSP的委托类型
            Delegate onToggleDel = ConvertActionToDelegate(onToggle, callbackDelegateType);
            Delegate onUntoggleDel = ConvertActionToDelegate(onUntoggle, callbackDelegateType);
            Delegate onHoverDel = ConvertActionToDelegate(onHover, callbackDelegateType);
            Delegate onHoverOutDel = ConvertActionToDelegate(onHoverOut, callbackDelegateType);
            Delegate onEnableDel = ConvertActionToDelegate(onEnable, callbackDelegateType);
            Delegate onDisableDel = ConvertActionToDelegate(onDisable, callbackDelegateType);

            // 获取AppScenes枚举类型
            Type appScenesType = type.GetNestedType("AppScenes");
            if (appScenesType == null)
            {
                appScenesType = ResolveType("ApplicationLauncher+AppScenes");
            }

            object scenesValue = appScenesType != null
                ? Enum.ToObject(appScenesType, visibleInScenes)
                : visibleInScenes;

            try
            {
                var result = method.Invoke(instance, new object[]
                {
                    onToggleDel, onUntoggleDel, onHoverDel, onHoverOutDel,
                    onEnableDel, onDisableDel, scenesValue, texture
                });
                Debug.Log($"{LOG_TAG} AddModApplication调用成功");
                return result;
            }
            catch (Exception ex)
            {
                Debug.LogError($"{LOG_TAG} AddModApplication调用失败: {ex.Message}\n{ex.InnerException}");
                return null;
            }
        }

        /// <summary>
        /// 移除模组按钮
        /// </summary>
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
            return 4;
        }

        #endregion

        #region GameEvents 相关

        /// <summary>
        /// 在GameEvents类型中查找指定名称的字段
        /// 同时尝试字段和属性，支持多种命名风格
        /// </summary>
        /// <param name="gameEventsType">GameEvents类型</param>
        /// <param name="fieldName">字段名</param>
        /// <returns>字段/属性的值（EventData对象），未找到返回null</returns>
        private static object FindGameEventField(Type gameEventsType, string fieldName)
        {
            // 尝试作为字段获取
            var field = gameEventsType.GetField(fieldName,
                BindingFlags.Public | BindingFlags.Static);
            if (field != null)
            {
                return field.GetValue(null);
            }

            // 尝试作为属性获取
            var prop = gameEventsType.GetProperty(fieldName,
                BindingFlags.Public | BindingFlags.Static);
            if (prop != null)
            {
                return prop.GetValue(null);
            }

            // 尝试模糊匹配：遍历所有静态字段，查找名称包含关键字的
            foreach (var f in gameEventsType.GetFields(BindingFlags.Public | BindingFlags.Static))
            {
                if (f.Name.IndexOf(fieldName, StringComparison.OrdinalIgnoreCase) >= 0 ||
                    fieldName.IndexOf(f.Name, StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    Debug.Log($"{LOG_TAG} 模糊匹配事件字段: {f.Name} (搜索: {fieldName})");
                    return f.GetValue(null);
                }
            }

            // 尝试按类型匹配：查找EventDataVoid类型的字段
            // EventDataVoid是KSP中无参数事件的类型
            var eventDataType = ResolveType("EventDataVoid");
            if (eventDataType != null)
            {
                foreach (var f in gameEventsType.GetFields(BindingFlags.Public | BindingFlags.Static))
                {
                    if (f.FieldType == eventDataType || f.FieldType.Name == "EventDataVoid")
                    {
                        Debug.Log($"{LOG_TAG} 按类型匹配事件字段: {f.Name}");
                        return f.GetValue(null);
                    }
                }
            }

            return null;
        }

        /// <summary>
        /// 获取EventData的Add/Remove方法所需的委托类型
        /// KSP的EventData<T>.Add方法期望特定的委托类型（如EventVoid.OnEvent），
        /// 需要通过反射获取该类型并转换。
        /// </summary>
        /// <param name="eventData">EventData对象</param>
        /// <returns>Add方法第一个参数的委托类型</returns>
        private static Type GetEventDelegateType(object eventData)
        {
            var addMethod = eventData.GetType().GetMethod("Add", BindingFlags.Public | BindingFlags.Instance);
            if (addMethod != null)
            {
                var parameters = addMethod.GetParameters();
                if (parameters.Length > 0)
                {
                    return parameters[0].ParameterType;
                }
            }
            return null;
        }

        /// <summary>
        /// 注册GameEvents中的事件
        /// 自动处理委托类型转换（System.Action → EventVoid.OnEvent）
        /// </summary>
        /// <param name="fieldName">事件字段名</param>
        /// <param name="callback">回调方法</param>
        private static void AddGameEvent(string fieldName, Action callback)
        {
            var gameEventsType = ResolveType("GameEvents");
            if (gameEventsType == null)
            {
                Debug.LogError($"{LOG_TAG} 未找到GameEvents类型");
                return;
            }

            object eventData = FindGameEventField(gameEventsType, fieldName);
            if (eventData == null)
            {
                Debug.LogError($"{LOG_TAG} 未找到事件: {fieldName}");
                // 调试：列出GameEvents中所有字段名
                Debug.Log($"{LOG_TAG} GameEvents可用字段:");
                foreach (var f in gameEventsType.GetFields(BindingFlags.Public | BindingFlags.Static))
                {
                    Debug.Log($"  {f.Name} ({f.FieldType.Name})");
                }
                return;
            }

            // 获取Add方法期望的委托类型
            Type delegateType = GetEventDelegateType(eventData);
            if (delegateType == null)
            {
                Debug.LogError($"{LOG_TAG} 无法确定事件 {fieldName} 的委托类型");
                return;
            }

            // 将Action转换为目标委托类型
            Delegate convertedCallback = ConvertActionToDelegate(callback, delegateType);

            var addMethod = eventData.GetType().GetMethod("Add", BindingFlags.Public | BindingFlags.Instance);
            if (addMethod != null)
            {
                try
                {
                    addMethod.Invoke(eventData, new object[] { convertedCallback });
                    Debug.Log($"{LOG_TAG} 已注册事件: {fieldName}");
                }
                catch (Exception ex)
                {
                    Debug.LogError($"{LOG_TAG} 注册事件 {fieldName} 失败: {ex.Message}");
                }
            }
        }

        /// <summary>
        /// 注销GameEvents中的事件
        /// </summary>
        /// <param name="fieldName">事件字段名</param>
        /// <param name="callback">回调方法</param>
        private static void RemoveGameEvent(string fieldName, Action callback)
        {
            var gameEventsType = ResolveType("GameEvents");
            if (gameEventsType == null) return;

            object eventData = FindGameEventField(gameEventsType, fieldName);
            if (eventData == null) return;

            // 获取Remove方法期望的委托类型
            Type delegateType = GetEventDelegateType(eventData);
            if (delegateType == null) return;

            Delegate convertedCallback = ConvertActionToDelegate(callback, delegateType);

            var removeMethod = eventData.GetType().GetMethod("Remove", BindingFlags.Public | BindingFlags.Instance);
            if (removeMethod != null)
            {
                try
                {
                    removeMethod.Invoke(eventData, new object[] { convertedCallback });
                }
                catch (Exception ex)
                {
                    Debug.LogError($"{LOG_TAG} 注销事件 {fieldName} 失败: {ex.Message}");
                }
            }
        }

        /// <summary>
        /// 注册ApplicationLauncher就绪事件
        /// </summary>
        public static void AddOnLauncherReadyEvent(Action callback)
        {
            AddGameEvent("onGUIApplicationLauncherReady", callback);
        }

        /// <summary>
        /// 注销ApplicationLauncher就绪事件
        /// </summary>
        public static void RemoveOnLauncherReadyEvent(Action callback)
        {
            RemoveGameEvent("onGUIApplicationLauncherReady", callback);
        }

        /// <summary>
        /// 注册ApplicationLauncher销毁事件
        /// </summary>
        public static void AddOnLauncherDestroyedEvent(Action callback)
        {
            AddGameEvent("onGUIApplicationLauncherDestroyed", callback);
        }

        /// <summary>
        /// 注销ApplicationLauncher销毁事件
        /// </summary>
        public static void RemoveOnLauncherDestroyedEvent(Action callback)
        {
            RemoveGameEvent("onGUIApplicationLauncherDestroyed", callback);
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

            // 尝试静态方法
            var method = stageManagerType.GetMethod("ActivateNextStage",
                BindingFlags.Public | BindingFlags.Static);
            if (method != null)
            {
                method.Invoke(null, null);
                return;
            }

            // 尝试实例方法
            var instanceProp = stageManagerType.GetProperty("Instance",
                BindingFlags.Public | BindingFlags.Static);
            if (instanceProp != null)
            {
                object inst = instanceProp.GetValue(null);
                if (inst != null)
                {
                    method = stageManagerType.GetMethod("ActivateNextStage",
                        BindingFlags.Public | BindingFlags.Instance);
                    method?.Invoke(inst, null);
                    return;
                }
            }

            // 尝试字段获取实例
            var instanceField = stageManagerType.GetField("Instance",
                BindingFlags.Public | BindingFlags.Static);
            if (instanceField != null)
            {
                object inst = instanceField.GetValue(null);
                if (inst != null)
                {
                    method = stageManagerType.GetMethod("ActivateNextStage",
                        BindingFlags.Public | BindingFlags.Instance);
                    method?.Invoke(inst, null);
                }
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
                // 备用方案：通过GameEvents隐藏UI
                FireGameEvent("onHideUI");
                return;
            }

            var instance = GetStaticInstance(uiMasterType);
            if (instance == null) return;

            var hideMethod = uiMasterType.GetMethod("Hide", BindingFlags.Public | BindingFlags.Instance);
            if (hideMethod != null)
            {
                hideMethod.Invoke(instance, null);
            }
            else
            {
                // 备用方案
                FireGameEvent("onHideUI");
            }
        }

        /// <summary>
        /// 显示KSP游戏UI
        /// </summary>
        public static void ShowUI()
        {
            var uiMasterType = ResolveType("UIMasterController");
            if (uiMasterType == null)
            {
                FireGameEvent("onShowUI");
                return;
            }

            var instance = GetStaticInstance(uiMasterType);
            if (instance == null) return;

            var showMethod = uiMasterType.GetMethod("Show", BindingFlags.Public | BindingFlags.Instance);
            if (showMethod != null)
            {
                showMethod.Invoke(instance, null);
            }
            else
            {
                FireGameEvent("onShowUI");
            }
        }

        /// <summary>
        /// 检查UI是否已隐藏
        /// </summary>
        public static bool IsUIHidden()
        {
            var uiMasterType = ResolveType("UIMasterController");
            if (uiMasterType == null) return false;

            var instance = GetStaticInstance(uiMasterType);
            if (instance == null) return false;

            // 尝试isHidden属性
            var isHiddenProp = uiMasterType.GetProperty("isHidden",
                BindingFlags.Public | BindingFlags.Instance);
            if (isHiddenProp != null)
            {
                return (bool)isHiddenProp.GetValue(instance);
            }

            // 尝试IsHidden属性
            isHiddenProp = uiMasterType.GetProperty("IsHidden",
                BindingFlags.Public | BindingFlags.Instance);
            if (isHiddenProp != null)
            {
                return (bool)isHiddenProp.GetValue(instance);
            }

            return false;
        }

        #endregion

        #region HighLogic 相关

        /// <summary>
        /// 获取HighLogic.UISkin（GUISkin）
        /// </summary>
        public static GUISkin GetUISkin()
        {
            var highLogicType = ResolveType("HighLogic");
            if (highLogicType == null) return null;

            // 尝试属性
            var skinProp = highLogicType.GetProperty("UISkin",
                BindingFlags.Public | BindingFlags.Static);
            if (skinProp == null)
            {
                // 尝试字段
                var skinField = highLogicType.GetField("UISkin",
                    BindingFlags.Public | BindingFlags.Static);
                if (skinField != null)
                {
                    var skinObj = skinField.GetValue(null);
                    return TryConvertToGUISkin(skinObj);
                }
                return null;
            }

            var skinObj2 = skinProp.GetValue(null);
            return TryConvertToGUISkin(skinObj2);
        }

        /// <summary>
        /// 尝试将对象转换为GUISkin
        /// 处理HighLogic.UISkin返回UISkinDef类型的情况
        /// </summary>
        private static GUISkin TryConvertToGUISkin(object skinObj)
        {
            if (skinObj == null) return null;

            // 直接是GUISkin
            if (skinObj is GUISkin guiSkin) return guiSkin;

            // UISkinDef类型，有skin属性返回GUISkin
            var skinProp = skinObj.GetType().GetProperty("skin",
                BindingFlags.Public | BindingFlags.Instance);
            if (skinProp != null)
            {
                return skinProp.GetValue(skinObj) as GUISkin;
            }

            // 尝试字段
            var skinField = skinObj.GetType().GetField("skin",
                BindingFlags.Public | BindingFlags.Instance);
            if (skinField != null)
            {
                return skinField.GetValue(skinObj) as GUISkin;
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

            // 尝试属性
            var prop = highLogicType.GetProperty("LoadedSceneIsFlight",
                BindingFlags.Public | BindingFlags.Static);
            if (prop != null)
            {
                return (bool)prop.GetValue(null);
            }

            // 尝试字段
            var field = highLogicType.GetField("LoadedSceneIsFlight",
                BindingFlags.Public | BindingFlags.Static);
            if (field != null)
            {
                return (bool)field.GetValue(null);
            }

            return false;
        }

        #endregion

        #region FlightInputHandler 相关

        /// <summary>
        /// 设置油门值
        /// </summary>
        public static void SetThrottle(float throttle)
        {
            var fihType = ResolveType("FlightInputHandler");
            if (fihType == null) return;

            // 尝试属性获取state
            var stateProp = fihType.GetProperty("state",
                BindingFlags.Public | BindingFlags.Static);
            object state = null;
            if (stateProp != null)
            {
                state = stateProp.GetValue(null);
            }
            else
            {
                // 尝试字段
                var stateField = fihType.GetField("state",
                    BindingFlags.Public | BindingFlags.Static);
                if (stateField != null)
                {
                    state = stateField.GetValue(null);
                }
            }

            if (state == null) return;

            // 设置mainThrottle
            var throttleProp = state.GetType().GetProperty("mainThrottle",
                BindingFlags.Public | BindingFlags.Instance);
            if (throttleProp != null)
            {
                throttleProp.SetValue(state, throttle);
            }
            else
            {
                var throttleField = state.GetType().GetField("mainThrottle",
                    BindingFlags.Public | BindingFlags.Instance);
                throttleField?.SetValue(state, throttle);
            }
        }

        #endregion

        #region 通用辅助

        /// <summary>
        /// 获取类型的静态实例（通过Instance属性或字段）
        /// </summary>
        private static object GetStaticInstance(Type type)
        {
            // 尝试属性
            var instanceProp = type.GetProperty("Instance",
                BindingFlags.Public | BindingFlags.Static);
            if (instanceProp != null)
            {
                return instanceProp.GetValue(null);
            }

            // 尝试字段
            var instanceField = type.GetField("Instance",
                BindingFlags.Public | BindingFlags.Static);
            return instanceField?.GetValue(null);
        }

        /// <summary>
        /// 触发GameEvents中的无参数事件
        /// </summary>
        private static void FireGameEvent(string eventName)
        {
            var gameEventsType = ResolveType("GameEvents");
            if (gameEventsType == null) return;

            object eventData = FindGameEventField(gameEventsType, eventName);
            if (eventData == null) return;

            var fireMethod = eventData.GetType().GetMethod("Fire",
                BindingFlags.Public | BindingFlags.Instance);
            fireMethod?.Invoke(eventData, null);
        }

        #endregion
    }
}
