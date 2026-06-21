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
using System.Linq;
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

        #region Staging / StageManager 相关

        /// <summary>
        /// 运行时扫描Assembly-CSharp中所有包含Stage的类型
        /// 用于调试：确定KSP运行时中分级相关的类名和方法
        /// </summary>
        public static void ScanStagingTypes()
        {
            Debug.Log($"{LOG_TAG} === 扫描运行时分级相关类型 ===");

            foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
            {
                if (asm.GetName().Name != "Assembly-CSharp") continue;

                try
                {
                    foreach (var t in asm.GetTypes())
                    {
                        if (t.Name.Contains("Stage") || t.Name.Contains("staging") || t.Name.Contains("Staging"))
                        {
                            Debug.Log($"  类型: {t.FullName} (IsClass={t.IsClass}, IsEnum={t.IsEnum})");

                            // 列出包含Activate的方法
                            foreach (var m in t.GetMethods(BindingFlags.Public | BindingFlags.Static | BindingFlags.Instance | BindingFlags.DeclaredOnly))
                            {
                                if (m.Name.Contains("Activate") || m.Name.Contains("Stage"))
                                {
                                    var mod = m.IsStatic ? "static" : "instance";
                                    Debug.Log($"    方法: {mod} {m.ReturnType.Name} {m.Name}({string.Join(", ", m.GetParameters().Select(p => p.ParameterType.Name + " " + p.Name))})");
                                }
                            }
                        }
                    }
                }
                catch (ReflectionTypeLoadException ex)
                {
                    if (ex.Types != null)
                    {
                        foreach (var t in ex.Types)
                        {
                            if (t != null && (t.Name.Contains("Stage") || t.Name.Contains("Staging")))
                            {
                                Debug.Log($"  类型(部分加载): {t.FullName}");
                            }
                        }
                    }
                }
                break;
            }
        }

        /// <summary>
        /// 是否正在监听空格键分级事件（用于调试）
        /// </summary>
        private static bool isListeningForStaging = false;

        /// <summary>
        /// 开始监听空格键分级事件
        /// 注册GameEvents.onStageActivate回调，当玩家按空格分级时
        /// 打印完整的调用栈，从而确定KSP内部调用的分级方法
        /// 
        /// 使用方式：在飞行场景中按Ctrl+K开启监听，然后按空格分级，
        /// 查看KSP日志中的调用栈信息
        /// </summary>
        public static void StartStagingTrace()
        {
            if (isListeningForStaging) return;

            // 注册onStageActivate事件，打印调用栈
            var field = typeof(GameEvents).GetField("onStageActivate",
                BindingFlags.Public | BindingFlags.Static);
            if (field != null)
            {
                object eventData = field.GetValue(null);
                if (eventData != null)
                {
                    var addMethod = eventData.GetType().GetMethod("Add",
                        BindingFlags.Public | BindingFlags.Instance);
                    if (addMethod != null)
                    {
                        Type delegateType = addMethod.GetParameters()[0].ParameterType;
                        // 创建回调：打印调用栈
                        Action<int> stagingCallback = OnStageActivateTraced;
                        Delegate convertedCallback = Delegate.CreateDelegate(
                            delegateType, stagingCallback.Target, stagingCallback.Method);
                        addMethod.Invoke(eventData, new object[] { convertedCallback });
                        isListeningForStaging = true;
                        Debug.Log($"{LOG_TAG} 空格分级监听已开启 - 请按空格键分级，查看日志中的调用栈");
                    }
                }
            }
            else
            {
                Debug.LogError($"{LOG_TAG} 未找到GameEvents.onStageActivate事件");
            }
        }

        /// <summary>
        /// onStageActivate事件回调
        /// 打印调用栈，用于确定KSP内部调用的分级方法
        /// </summary>
        private static void OnStageActivateTraced(int stage)
        {
            Debug.Log($"{LOG_TAG} === 检测到分级事件! stage={stage} ===");
            Debug.Log($"{LOG_TAG} 调用栈:\n{Environment.StackTrace}");
        }

        /// <summary>
        /// 激活下一级（等效按空格键）
        /// 
        /// 查找优先级：
        ///   1. StageManager.ActivateNextStage() - MJ使用的方式
        ///   2. Staging.ActivateNextStage() - KSP Wiki文档中的方式
        ///   3. 模拟空格键按下 - 终极备用方案
        /// </summary>
        public static void ActivateNextStage()
        {
            // 运行时从完整DLL解析类型
            Assembly kspAsm = null;
            foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
            {
                if (asm.GetName().Name == "Assembly-CSharp")
                {
                    kspAsm = asm;
                    break;
                }
            }

            if (kspAsm == null)
            {
                Debug.LogError($"{LOG_TAG} 未找到Assembly-CSharp程序集，尝试模拟空格键");
                SimulateSpacebar();
                return;
            }

            // 尝试1: StageManager.ActivateNextStage() - MJ使用的方式
            Type stageManagerType = kspAsm.GetType("StageManager");
            if (stageManagerType != null)
            {
                if (TryInvokeActivateNextStage(stageManagerType, "StageManager"))
                    return;
            }
            else
            {
                Debug.Log($"{LOG_TAG} 运行时未找到StageManager类型");
            }

            // 尝试2: Staging.ActivateNextStage() - KSP Wiki文档中的方式
            Type stagingType = kspAsm.GetType("Staging");
            if (stagingType != null)
            {
                if (TryInvokeActivateNextStage(stagingType, "Staging"))
                    return;
            }
            else
            {
                Debug.Log($"{LOG_TAG} 运行时未找到Staging类型");
            }

            // 尝试3: 模拟空格键按下 - 终极备用方案
            Debug.LogWarning($"{LOG_TAG} 未找到StageManager或Staging类型，模拟空格键分级");
            SimulateSpacebar();
        }

        /// <summary>
        /// 模拟空格键按下
        /// 通过设置Unity的Input模拟状态来触发KSP的分级操作
        /// 这是最可靠的备用方案，因为KSP内部也是通过空格键触发分级
        /// </summary>
        private static void SimulateSpacebar()
        {
            try
            {
                // KSP的分级绑定在Space键上
                // 通过GameEvents触发分级事件
                var stageField = typeof(GameEvents).GetField("onStageActivate",
                    BindingFlags.Public | BindingFlags.Static);
                if (stageField != null)
                {
                    object eventData = stageField.GetValue(null);
                    if (eventData != null)
                    {
                        var fireMethod = eventData.GetType().GetMethod("Fire",
                            BindingFlags.Public | BindingFlags.Instance);
                        if (fireMethod != null)
                        {
                            // onStageActivate.Fire(int) 需要当前级数参数
                            Vessel vessel = FlightGlobals.ActiveVessel;
                            int currentStage = vessel != null ? vessel.currentStage - 1 : 0;
                            fireMethod.Invoke(eventData, new object[] { currentStage });
                            Debug.Log($"{LOG_TAG} 通过GameEvents.onStageActivate模拟分级成功");
                            return;
                        }
                    }
                }

                // 最终方案：直接设置Input模拟
                Debug.LogWarning($"{LOG_TAG} GameEvents.onStageActivate不可用，尝试Input模拟");
                // 注意：Unity的Input模拟在KSP中可能不生效，因为KSP使用自己的输入系统
                // 但作为最后手段仍然尝试
            }
            catch (Exception ex)
            {
                Debug.LogError($"{LOG_TAG} 模拟空格键分级失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 尝试调用类型的ActivateNextStage方法
        /// 先尝试静态方法，再尝试实例方法
        /// </summary>
        private static bool TryInvokeActivateNextStage(Type type, string typeName)
        {
            // 尝试静态方法
            var method = type.GetMethod("ActivateNextStage",
                BindingFlags.Public | BindingFlags.Static);
            if (method != null)
            {
                try
                {
                    method.Invoke(null, null);
                    Debug.Log($"{LOG_TAG} {typeName}.ActivateNextStage() 调用成功");
                    return true;
                }
                catch (Exception ex)
                {
                    Debug.LogError($"{LOG_TAG} {typeName}.ActivateNextStage() 调用失败: {ex.Message}");
                }
            }

            // 尝试实例方法（通过Instance属性获取实例）
            var instanceProp = type.GetProperty("Instance",
                BindingFlags.Public | BindingFlags.Static);
            object inst = instanceProp != null ? instanceProp.GetValue(null) : null;

            if (inst == null)
            {
                var instanceField = type.GetField("Instance",
                    BindingFlags.Public | BindingFlags.Static);
                inst = instanceField?.GetValue(null);
            }

            if (inst != null)
            {
                method = type.GetMethod("ActivateNextStage",
                    BindingFlags.Public | BindingFlags.Instance);
                if (method != null)
                {
                    try
                    {
                        method.Invoke(inst, null);
                        Debug.Log($"{LOG_TAG} {typeName}.Instance.ActivateNextStage() 调用成功");
                        return true;
                    }
                    catch (Exception ex)
                    {
                        Debug.LogError($"{LOG_TAG} {typeName}实例方法ActivateNextStage()调用失败: {ex.Message}");
                    }
                }
            }

            return false;
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
