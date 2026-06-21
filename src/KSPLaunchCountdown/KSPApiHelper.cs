/**
 * KSPApiHelper.cs - KSP1 API反射辅助类
 *
 * 用途：封装对精简版Assembly-CSharp.dll中缺失类型的反射调用。
 * 运行时KSP游戏自带完整DLL，所有API均可正常调用。
 *
 * 精简DLL中缺失的类型（需反射）：
 *   - ApplicationLauncher / ApplicationLauncherButton: 工具栏按钮管理
 *   - StageManager: 分级管理
 *   - RUIToggleButton.OnToggle: 按钮回调委托类型
 *
 * 精简DLL中存在的类型（可直接引用）：
 *   GameEvents, EventVoid, HighLogic, FlightGlobals, FlightInputHandler,
 *   KSPActionGroup, FlightCtrlState, KSPAddon, GameDatabase, KSPUtil,
 *   PopupDialog, KSP.UI.UIMasterController
 *
 * 关键技术点：
 *   - KSP的EventVoid.Add()期望EventVoid.OnEvent委托类型，而非System.Action。
 *     两者签名相同（无参void），需通过Delegate.CreateDelegate转换。
 *   - AddModApplication的回调参数类型为RUIToggleButton.OnToggle，
 *     同样需要从System.Action转换。
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
    /// 仅封装精简版DLL中缺失的类型，其他类型可直接引用
    /// </summary>
    public static class KSPApiHelper
    {
        /// <summary>日志标签</summary>
        private const string LOG_TAG = "[KSPLaunchCountdown]";

        #region ApplicationLauncher 相关

        /// <summary>ApplicationLauncher类型缓存</summary>
        private static Type appLauncherType;

        /// <summary>获取ApplicationLauncher类型（运行时从完整DLL解析）</summary>
        public static Type GetApplicationLauncherType()
        {
            if (appLauncherType == null)
            {
                // 运行时KSP加载了完整版Assembly-CSharp.dll
                foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
                {
                    if (asm.GetName().Name == "Assembly-CSharp")
                    {
                        appLauncherType = asm.GetType("ApplicationLauncher");
                        break;
                    }
                }
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

            var instanceProp = type.GetProperty("Instance",
                BindingFlags.Public | BindingFlags.Static);
            if (instanceProp != null)
            {
                return instanceProp.GetValue(null);
            }

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

            var readyProp = type.GetProperty("Ready",
                BindingFlags.Public | BindingFlags.Static);
            if (readyProp != null)
            {
                return (bool)readyProp.GetValue(null);
            }

            var readyField = type.GetField("Ready",
                BindingFlags.Public | BindingFlags.Static);
            if (readyField != null)
            {
                return (bool)readyField.GetValue(null);
            }

            return false;
        }

        /// <summary>
        /// 将System.Action转换为KSP的委托类型
        /// KSP使用自定义委托类型（如EventVoid.OnEvent、RUIToggleButton.OnToggle），
        /// 签名与System.Action相同（无参void），但类型不同。
        /// 通过Delegate.CreateDelegate创建目标委托类型的实例。
        /// </summary>
        private static Delegate ConvertActionToDelegate(Action action, Type targetDelegateType)
        {
            if (action == null) return null;
            return Delegate.CreateDelegate(targetDelegateType, action.Target, action.Method);
        }

        /// <summary>
        /// 添加模组按钮到ApplicationLauncher
        /// AddModApplication的回调参数类型为RUIToggleButton.OnToggle（精简DLL中缺失），
        /// 需要从System.Action转换。
        /// </summary>
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
        /// 注册GameEvents中的事件
        /// 直接使用typeof(GameEvents)（精简DLL中存在），处理委托类型转换
        /// </summary>
        private static void AddGameEvent(string fieldName, Action callback)
        {
            // 直接使用GameEvents类型（精简DLL中存在）
            var field = typeof(GameEvents).GetField(fieldName,
                BindingFlags.Public | BindingFlags.Static);
            if (field == null)
            {
                Debug.LogError($"{LOG_TAG} GameEvents中未找到字段: {fieldName}");
                return;
            }

            object eventData = field.GetValue(null);
            if (eventData == null)
            {
                Debug.LogError($"{LOG_TAG} GameEvents.{fieldName} 值为null");
                return;
            }

            // 获取Add方法期望的委托类型（如EventVoid.OnEvent）
            var addMethod = eventData.GetType().GetMethod("Add", BindingFlags.Public | BindingFlags.Instance);
            if (addMethod == null)
            {
                Debug.LogError($"{LOG_TAG} 事件 {fieldName} 没有Add方法");
                return;
            }

            Type delegateType = addMethod.GetParameters()[0].ParameterType;
            Delegate convertedCallback = ConvertActionToDelegate(callback, delegateType);

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

        /// <summary>
        /// 注销GameEvents中的事件
        /// </summary>
        private static void RemoveGameEvent(string fieldName, Action callback)
        {
            var field = typeof(GameEvents).GetField(fieldName,
                BindingFlags.Public | BindingFlags.Static);
            if (field == null) return;

            object eventData = field.GetValue(null);
            if (eventData == null) return;

            var removeMethod = eventData.GetType().GetMethod("Remove", BindingFlags.Public | BindingFlags.Instance);
            if (removeMethod == null) return;

            Type delegateType = removeMethod.GetParameters()[0].ParameterType;
            Delegate convertedCallback = ConvertActionToDelegate(callback, delegateType);

            try
            {
                removeMethod.Invoke(eventData, new object[] { convertedCallback });
            }
            catch (Exception ex)
            {
                Debug.LogError($"{LOG_TAG} 注销事件 {fieldName} 失败: {ex.Message}");
            }
        }

        #endregion

        #region StageManager 相关

        /// <summary>
        /// 激活下一级（等效按空格键）
        /// 参考MechJeb的ImmediateStage方法，直接调用StageManager.ActivateNextStage()
        /// StageManager在精简DLL中缺失，需通过反射从运行时完整DLL中获取
        /// 注意：GameEvents.StageManager是事件容器（嵌套类），与顶层StageManager不同
        /// </summary>
        public static void ActivateNextStage()
        {
            // 运行时从完整DLL解析StageManager类型
            Type stageManagerType = null;
            foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
            {
                if (asm.GetName().Name == "Assembly-CSharp")
                {
                    stageManagerType = asm.GetType("StageManager");
                    break;
                }
            }

            if (stageManagerType == null)
            {
                Debug.LogError($"{LOG_TAG} 未找到StageManager类型");
                return;
            }

            // 参考MJ: StageManager.ActivateNextStage()
            // ActivateNextStage是静态方法
            var method = stageManagerType.GetMethod("ActivateNextStage",
                BindingFlags.Public | BindingFlags.Static);
            if (method != null)
            {
                try
                {
                    method.Invoke(null, null);
                    Debug.Log($"{LOG_TAG} StageManager.ActivateNextStage() 调用成功");
                    return;
                }
                catch (Exception ex)
                {
                    Debug.LogError($"{LOG_TAG} StageManager.ActivateNextStage() 调用失败: {ex.Message}");
                }
            }

            // 备用方案：尝试实例方法（通过Instance属性获取实例）
            var instanceProp = stageManagerType.GetProperty("Instance",
                BindingFlags.Public | BindingFlags.Static);
            object inst = instanceProp != null ? instanceProp.GetValue(null) : null;

            if (inst == null)
            {
                var instanceField = stageManagerType.GetField("Instance",
                    BindingFlags.Public | BindingFlags.Static);
                inst = instanceField?.GetValue(null);
            }

            if (inst != null)
            {
                method = stageManagerType.GetMethod("ActivateNextStage",
                    BindingFlags.Public | BindingFlags.Instance);
                if (method != null)
                {
                    try
                    {
                        method.Invoke(inst, null);
                        Debug.Log($"{LOG_TAG} StageManager.Instance.ActivateNextStage() 调用成功");
                    }
                    catch (Exception ex)
                    {
                        Debug.LogError($"{LOG_TAG} 实例方法ActivateNextStage()调用失败: {ex.Message}");
                    }
                }
            }
        }

        #endregion

        #region UI控制相关

        /// <summary>
        /// 隐藏KSP游戏UI
        /// 使用GameEvents.onHideUI（精简DLL中存在GameEvents类型）
        /// </summary>
        public static void HideUI()
        {
            // 通过GameEvents.onHideUI隐藏UI
            var field = typeof(GameEvents).GetField("onHideUI",
                BindingFlags.Public | BindingFlags.Static);
            if (field != null)
            {
                object eventData = field.GetValue(null);
                if (eventData != null)
                {
                    var fireMethod = eventData.GetType().GetMethod("Fire",
                        BindingFlags.Public | BindingFlags.Instance);
                    fireMethod?.Invoke(eventData, null);
                    Debug.Log($"{LOG_TAG} UI已隐藏（通过onHideUI）");
                    return;
                }
            }

            Debug.LogWarning($"{LOG_TAG} onHideUI事件不可用");
        }

        /// <summary>
        /// 显示KSP游戏UI
        /// 使用GameEvents.onShowUI（精简DLL中存在GameEvents类型）
        /// </summary>
        public static void ShowUI()
        {
            var field = typeof(GameEvents).GetField("onShowUI",
                BindingFlags.Public | BindingFlags.Static);
            if (field != null)
            {
                object eventData = field.GetValue(null);
                if (eventData != null)
                {
                    var fireMethod = eventData.GetType().GetMethod("Fire",
                        BindingFlags.Public | BindingFlags.Instance);
                    fireMethod?.Invoke(eventData, null);
                    Debug.Log($"{LOG_TAG} UI已恢复（通过onShowUI）");
                    return;
                }
            }

            Debug.LogWarning($"{LOG_TAG} onShowUI事件不可用");
        }

        /// <summary>
        /// 检查UI是否已隐藏
        /// 使用KSP.UI.UIMasterController（精简DLL中存在）
        /// </summary>
        public static bool IsUIHidden()
        {
            // KSP.UI.UIMasterController在精简DLL中存在
            var instanceProp = typeof(KSP.UI.UIMasterController).GetProperty("Instance",
                BindingFlags.Public | BindingFlags.Static);
            if (instanceProp == null) return false;

            object instance = instanceProp.GetValue(null);
            if (instance == null) return false;

            var isHiddenProp = instance.GetType().GetProperty("isHidden",
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
        /// HighLogic在精简DLL中存在，但UISkin属性返回UISkinDef类型
        /// </summary>
        public static GUISkin GetUISkin()
        {
            // HighLogic.UISkin返回UISkinDef，需要从中提取GUISkin
            var skinObj = HighLogic.UISkin as object;
            if (skinObj == null) return null;

            // UISkinDef类型，有skin属性返回GUISkin
            var skinProp = skinObj.GetType().GetProperty("skin",
                BindingFlags.Public | BindingFlags.Instance);
            if (skinProp != null)
            {
                return skinProp.GetValue(skinObj) as GUISkin;
            }

            var skinField = skinObj.GetType().GetField("skin",
                BindingFlags.Public | BindingFlags.Instance);
            if (skinField != null)
            {
                return skinField.GetValue(skinObj) as GUISkin;
            }

            return null;
        }

        #endregion
    }
}
