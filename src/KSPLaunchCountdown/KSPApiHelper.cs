/**
 * KSPApiHelper.cs - KSP1 API辅助类
 *
 * 用途：封装KSP API调用，分为直接调用和反射调用两部分。
 *   - 可直接引用的类型：UI控制、分级、UISkin、工具栏按钮等
 *   - 需反射调用的类型（精简DLL中缺失或无法直接引用）：GameEvents事件注册
 *
 * 直接调用（通过正常 using 引用）：
 *   - ApplicationLauncher / ApplicationLauncherButton → ToolbarButton.cs 直接调用
 *   - 命名空间：KSP.UI.Screens
 *   - 说明：已补充 UnityEngine.AnimationModule 引用，解决 ApplicationLauncherButton
 *     的间接依赖，因此无需再通过 stub 或反射调用。
 *
 * 仍保留反射的部分：
 *   - GameEvents.onHideUI / onShowUI 的 Fire() 调用（EventData反射）
 *
 * 可直接引用的类型（本地 KSP DLL 中存在）：
 *   GameEvents, EventVoid, HighLogic, FlightGlobals, FlightInputHandler,
 *   KSPActionGroup, FlightCtrlState, KSPAddon, GameDatabase, KSPUtil,
 *   PopupDialog, KSP.UI.UIMasterController,
 *   KSP.UI.Screens.ApplicationLauncher, KSP.UI.Screens.ApplicationLauncherButton
 *
 * 依赖：
 *   - Assembly-CSharp.dll (KSP核心)
 *   - UnityEngine.CoreModule.dll (Unity核心)
 *   - UnityEngine.AnimationModule.dll (ApplicationLauncherButton 的间接依赖)
 */

using System;
using System.Reflection;
using UnityEngine;

namespace KSPLaunchCountdown
{
    /// <summary>
    /// KSP API辅助类
    /// 封装需要特殊处理的KSP API调用
    /// </summary>
    public static class KSPApiHelper
    {
        /// <summary>日志标签</summary>
        private const string LOG_TAG = "[KSPLaunchCountdown]";

        #region Staging 相关

        /// <summary>
        /// 激活下一级（等效按空格键）
        /// 通过Windows API keybd_event模拟空格键输入
        /// KSP使用Unity旧输入系统，会正常接收OS层面的键盘事件
        /// 这是最可靠的分级方式，走KSP完整的输入处理流程
        /// </summary>
        public static void ActivateNextStage()
        {
            try
            {
                // 使用keybd_event模拟空格键
                // VK_SPACE = 0x20, KEYEVENTF_KEYUP = 0x0002
                keybd_event(0x20, 0, 0, UIntPtr.Zero);   // 按下空格键
                keybd_event(0x20, 0, 2, UIntPtr.Zero);   // 释放空格键
                Debug.Log($"{LOG_TAG} 模拟空格键分级成功");
            }
            catch (Exception ex)
            {
                Debug.LogError($"{LOG_TAG} 模拟空格键分级失败: {ex.Message}");
            }
        }

        /// <summary>
        /// Windows API keybd_event函数声明
        /// 在OS层面发送键盘事件，Unity旧输入系统会正常接收
        /// 参数：bVk=虚拟键码, bScan=扫描码(可忽略), dwFlags=事件标志, dwExtraInfo=附加信息
        /// </summary>
        [System.Runtime.InteropServices.DllImport("user32.dll")]
        private static extern void keybd_event(byte bVk, byte bScan, uint dwFlags, UIntPtr dwExtraInfo);

        #endregion

        #region UI控制相关

        /// <summary>
        /// 隐藏KSP游戏UI
        /// 通过反射调用 GameEvents.onHideUI.Fire()
        /// </summary>
        public static void HideUI()
        {
            FireGameEvent("onHideUI", "UI已隐藏（通过onHideUI）");
        }

        /// <summary>
        /// 显示KSP游戏UI
        /// 通过反射调用 GameEvents.onShowUI.Fire()
        /// </summary>
        public static void ShowUI()
        {
            FireGameEvent("onShowUI", "UI已恢复（通过onShowUI）");
        }

        /// <summary>
        /// 触发GameEvents事件（通过反射调用Fire方法）
        /// 用于onHideUI/onShowUI等无参数事件
        /// </summary>
        private static void FireGameEvent(string eventName, string successLog)
        {
            var field = typeof(GameEvents).GetField(eventName,
                BindingFlags.Public | BindingFlags.Static);
            if (field == null)
            {
                Debug.LogWarning($"{LOG_TAG} {eventName}事件不可用");
                return;
            }

            object eventData = field.GetValue(null);
            if (eventData == null) return;

            var fireMethod = eventData.GetType().GetMethod("Fire",
                BindingFlags.Public | BindingFlags.Instance);
            fireMethod?.Invoke(eventData, null);
            Debug.Log($"{LOG_TAG} {successLog}");
        }

        /// <summary>
        /// 检查UI是否已隐藏
        /// 使用KSP.UI.UIMasterController（精简DLL中存在）
        /// </summary>
        public static bool IsUIHidden()
        {
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
            var skinObj = HighLogic.UISkin as object;
            if (skinObj == null) return null;

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
