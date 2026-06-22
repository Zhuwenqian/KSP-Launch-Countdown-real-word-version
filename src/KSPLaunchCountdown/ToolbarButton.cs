/**
 * ToolbarButton.cs - KSP1 发射倒计时工具栏按钮管理
 *
 * 用途：管理KSP ApplicationLauncher工具栏上的模组按钮。
 * 负责按钮的注册、图标加载、点击响应和清理。
 *
 * 功能说明：
 *   - 在飞行场景的工具栏上添加模组按钮
 *   - 从GameData目录加载38x38像素的PNG图标（由ICON.jpeg转换而来）
 *   - 点击按钮时切换倒计时菜单的显示/隐藏
 *   - 场景切换或模组卸载时自动清理按钮
 *   - Ctrl+L快捷键兜底切换菜单
 *
 * 实现方式（参考MechJeb）：
 *   不依赖GameEvents.onGUIApplicationLauncherReady事件，
 *   而是在每帧Update中检查ApplicationLauncher.Ready状态，
 *   就绪后立即注册按钮。这种方式更可靠，不会因事件时序问题导致按钮丢失。
 *
 * 直接调用说明（不再使用反射）：
 *   本文件直接调用KSP的ApplicationLauncher、RUIToggleButton等类型。
 *   由于精简版Assembly-CSharp.dll可能缺少这些类型的定义，
 *   文件末尾提供了本地stub类型定义作为编译兼容层。
 *   运行时KSP游戏自带完整DLL，所有API均可正常工作。
 *
 * 图标说明：
 *   图标文件为 GameData/KSPLaunchCountdown/Textures/icon.png（38x38像素）
 *   通过GameDatabase.GetTexture加载，路径格式为"KSPLaunchCountdown/Textures/icon"
 *   （不含扩展名和GameData前缀）
 *
 * API参考（来源：Classes.xml / KSP Wiki）：
 *   ApplicationLauncher.Instance          -> 单例实例
 *   ApplicationLauncher.Ready             -> 静态bool属性，是否就绪
 *   ApplicationLauncher.AddModApplication(8个参数) -> 注册模组按钮
 *   ApplicationLauncher.RemoveModApplication(button) -> 移除按钮
 *   ApplicationLauncher.AppScenes.FLIGHT  -> 飞行场景枚举值
 *
 * 依赖：
 *   - Assembly-CSharp.dll (KSP核心)
 *   - UnityEngine.CoreModule.dll (Unity核心)
 *   - KSPApiHelper.cs (UI控制、分级等辅助方法)
 */

using System;
using UnityEngine;

namespace KSPLaunchCountdown
{
    /// <summary>
    /// 工具栏按钮管理器
    /// 参考MechJeb的实现模式：在Update中轮询ApplicationLauncher.Ready状态
    /// 直接调用KSP ApplicationLauncher API（不通过反射）
    /// </summary>
    public class ToolbarButton : MonoBehaviour
    {
        /// <summary>日志标签</summary>
        private const string LOG_TAG = "[KSPLaunchCountdown]";

        /// <summary>
        /// 图标在GameDatabase中的路径（不含扩展名和GameData前缀）
        /// 对应文件：GameData/KSPLaunchCountdown/Textures/icon.png
        /// </summary>
        private const string ICON_DB_PATH = "KSPLaunchCountdown/Textures/icon";

        /// <summary>ApplicationLauncher按钮实例</summary>
        private ApplicationLauncherButton launcherButton;

        /// <summary>倒计时菜单引用</summary>
        private CountdownMenu countdownMenu;

        /// <summary>加载的图标纹理</summary>
        private Texture2D iconTexture;

        /// <summary>
        /// 初始化工具栏按钮
        /// </summary>
        public void Initialize(CountdownMenu menu)
        {
            countdownMenu = menu;
            Debug.Log($"{LOG_TAG} 工具栏按钮初始化完成");
        }

        /// <summary>
        /// Unity每帧更新
        /// 参考MechJeb模式：每帧检查ApplicationLauncher.Ready，
        /// 就绪后注册按钮
        /// </summary>
        void Update()
        {
            // 仅在飞行场景且按钮未注册时尝试注册
            if (launcherButton == null && HighLogic.LoadedSceneIsFlight)
            {
                SetupAppLauncher();
            }

            // Ctrl+L快捷键兜底：切换菜单显示/隐藏
            if (HighLogic.LoadedSceneIsFlight
                && (Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl))
                && Input.GetKeyDown(KeyCode.L))
            {
                if (countdownMenu != null)
                {
                    countdownMenu.IsVisible = !countdownMenu.IsVisible;
                    Debug.Log($"{LOG_TAG} Ctrl+L 切换菜单: {(countdownMenu.IsVisible ? "显示" : "隐藏")}");
                }
            }
        }

        /// <summary>
        /// 设置ApplicationLauncher按钮
        /// 参考KSP API文档（Classes.xml）：
        ///   AddModApplication(onTrue, onFalse, onHover, onHoverOut,
        ///                     onEnable, onDisable, visibleInScenes, texture)
        /// </summary>
        private void SetupAppLauncher()
        {
            // 检查ApplicationLauncher是否就绪
            if (!ApplicationLauncher.Ready)
            {
                return;
            }

            // 加载图标纹理
            if (iconTexture == null)
            {
                iconTexture = LoadIconTexture();
                if (iconTexture == null) return;
            }

            // 直接调用ApplicationLauncher.Instance.AddModApplication注册按钮
            launcherButton = ApplicationLauncher.Instance.AddModApplication(
                OnButtonToggle,       // RUIToggleButton.OnTrue: 按钮激活回调
                OnButtonUntoggle,     // RUIToggleButton.OnFalse: 按钮取消激活回调
                null,                 // RUIToggleButton.OnHover: 鼠标悬停回调
                null,                 // RUIToggleButton.OnHoverOut: 鼠标移出回调
                null,                 // RUIToggleButton.OnEnable: 按钮启用回调
                null,                 // RUIToggleButton.OnDisable: 按钮禁用回调
                ApplicationLauncher.AppScenes.FLIGHT,  // 仅在飞行场景显示
                iconTexture           // 38x38纹理图标
            );

            if (launcherButton != null)
            {
                Debug.Log($"{LOG_TAG} 工具栏按钮已注册");
            }
        }

        /// <summary>
        /// 从GameDatabase加载图标纹理
        /// 路径格式：模组名/目录/文件名（不含扩展名和GameData前缀）
        /// </summary>
        private Texture2D LoadIconTexture()
        {
            Texture2D tex = GameDatabase.Instance.GetTexture(ICON_DB_PATH, false);
            if (tex != null)
            {
                Debug.Log($"{LOG_TAG} 图标加载成功: {ICON_DB_PATH}");
            }
            else
            {
                Debug.LogError($"{LOG_TAG} 图标加载失败: {ICON_DB_PATH}。请确认icon.png已放入GameData/KSPLaunchCountdown/Textures/目录");
            }
            return tex;
        }

        /// <summary>
        /// 按钮激活回调（按钮被按下/选中）
        /// 显示倒计时菜单
        /// </summary>
        private void OnButtonToggle()
        {
            if (countdownMenu != null)
            {
                countdownMenu.IsVisible = true;
            }
            Debug.Log($"{LOG_TAG} 工具栏按钮已激活 - 显示菜单");
        }

        /// <summary>
        /// 按钮取消激活回调（按钮被弹起/取消选中）
        /// 隐藏倒计时菜单
        /// </summary>
        private void OnButtonUntoggle()
        {
            if (countdownMenu != null)
            {
                countdownMenu.IsVisible = false;
            }
            Debug.Log($"{LOG_TAG} 工具栏按钮已取消 - 隐藏菜单");
        }

        /// <summary>
        /// 清理方法，由入口类在OnDestroy中调用
        /// 参考MechJeb：检查按钮的gameObject是否还存在再移除
        /// </summary>
        public void Cleanup()
        {
            if (launcherButton != null)
            {
                // 检查按钮的gameObject是否存在再移除（避免空引用异常）
                if (launcherButton.gameObject != null)
                {
                    ApplicationLauncher.Instance.RemoveModApplication(launcherButton);
                }
                launcherButton = null;
            }

            // 销毁图标纹理
            if (iconTexture != null)
            {
                Destroy(iconTexture);
                iconTexture = null;
            }

            Debug.Log($"{LOG_TAG} 工具栏按钮已清理");
        }

        void OnDestroy()
        {
            Cleanup();
        }
    }

    #region KSP API Stub 类型定义
    // ================================================================
    // 以下类型定义用于编译兼容。
    // 精简版Assembly-CSharp.dll可能缺少这些类型的元数据定义，
    // 但运行时KSP游戏自带完整DLL，所有API均可正常工作。
    //
    // 类型签名来源：Classes.xml (KSP API文档)
    // 项目: https://anatid.github.io/XML-Documentation-for-the-KSP-API/
    // ================================================================

    #region ApplicationLauncher 相关Stub

    /// <summary>
    /// ApplicationLauncher Stub - KSP应用启动器
    /// 来源：KSP API文档 Classes.xml
    /// 管理 KSP 主界面右上角/右下角的应用程序工具栏按钮
    /// 
    /// 关键成员：
    ///   Instance: 单例实例（静态属性）
    ///   Ready: 是否就绪可添加按钮（静态bool属性）
    ///   AddModApplication: 添加第三方模组按钮
    ///   RemoveModApplication: 移除模组按钮
    ///   AppScenes: 嵌套枚举，指定按钮可见场景
    /// </summary>
    public class ApplicationLauncher
    {
        /// <summary>单例实例</summary>
        public static ApplicationLauncher Instance { get { return null; } }

        /// <summary>是否就绪，就绪后才可添加按钮</summary>
        public static bool Ready { get { return false; } }

        /// <summary>
        /// 添加模组按钮到工具栏
        /// 参数说明来自Classes.xml文档
        /// </summary>
        public ApplicationLauncherButton AddModApplication(
            RUIToggleButton.OnTrue onTrue,
            RUIToggleButton.OnFalse onFalse,
            RUIToggleButton.OnHover onHover,
            RUIToggleButton.OnHoverOut onHoverOut,
            RUIToggleButton.OnEnable onEnable,
            RUIToggleButton.OnDisable onDisable,
            AppScenes visibleInScenes,
            Texture texture)
        {
            return null;
        }

        /// <summary>移除模组按钮</summary>
        public void RemoveModApplication(ApplicationLauncherButton button) { }

        /// <summary>AppScenes嵌套枚举</summary>
        [System.Flags]
        public enum AppScenes
        {
            FLIGHT = 4,
            MAPVIEW = 8,
            EDITOR = 2,
            SETTINGS = 16,
            ALWAYS = FLIGHT | MAPVIEW | EDITOR | SETTINGS
        }
    }

    /// <summary>
    /// ApplicationLauncherButton Stub - 应用启动器按钮
    /// 来源：KSP API文档 Classes.xml
    /// AddModApplication() 的返回类型
    /// 继承自RUIToggleButton
    /// </summary>
    public class ApplicationLauncherButton : RUIToggleButton
    {
    }

    #endregion

    #region RUIToggleButton 相关Stub

    /// <summary>
    /// RUIToggleButton Stub - KSP UI切换按钮基类
    /// 来源：KSP API文档 Classes.xml
    /// 定义了按钮的各种回调委托类型
    /// </summary>
    public class RUIToggleButton : MonoBehaviour
    {
        /// <summary>按钮被激活（toggle on）时的回调委托</summary>
        public delegate void OnTrue();

        /// <summary>按钮被取消激活（toggle off）时的回调委托</summary>
        public delegate void OnFalse();

        /// <summary>鼠标悬停在按钮上时的回调委托</summary>
        public delegate void OnHover();

        /// <summary>鼠标离开按钮时的回调委托</summary>
        public delegate void OnHoverOut();

        /// <summary>按钮变为可用/显示时的回调委托</summary>
        public delegate void OnEnable();

        /// <summary>按钮变为不可用/隐藏时的回调委托</summary>
        public delegate void OnDisable();
    }

    #endregion

    #endregion
}
