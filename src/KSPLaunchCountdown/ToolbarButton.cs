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
 * 兼容性说明：
 *   由于精简版Assembly-CSharp.dll缺少ApplicationLauncherButton等类型定义，
 *   本类通过KSPApiHelper反射辅助类调用KSP API，确保编译兼容性。
 *   运行时KSP游戏自带完整DLL，所有API均可正常工作。
 *
 * 图标说明：
 *   图标文件为 GameData/KSPLaunchCountdown/Textures/icon.png（38x38像素）
 *   通过GameDatabase.GetTexture加载，路径格式为"KSPLaunchCountdown/Textures/icon"
 *   （不含扩展名和GameData前缀）
 *
 * 依赖：
 *   - Assembly-CSharp.dll (KSP核心，提供ApplicationLauncher、GameEvents、GameDatabase等)
 *   - UnityEngine.CoreModule.dll (Unity核心，提供Texture2D、Color等)
 *   - KSPApiHelper.cs (KSP API反射辅助类)
 */

using System;
using UnityEngine;

namespace KSPLaunchCountdown
{
    /// <summary>
    /// 工具栏按钮管理器
    /// 参考MechJeb的实现模式：在Update中轮询ApplicationLauncher.Ready状态
    /// 通过KSPApiHelper反射调用KSP API，兼容精简版DLL
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

        /// <summary>
        /// ApplicationLauncher按钮实例
        /// 使用object类型存储，通过KSPApiHelper操作
        /// </summary>
        private object launcherButton;

        /// <summary>倒计时菜单引用，用于切换菜单显示</summary>
        private CountdownMenu countdownMenu;

        /// <summary>加载的图标纹理</summary>
        private Texture2D iconTexture;

        /// <summary>
        /// 初始化工具栏按钮，注入倒计时菜单引用
        /// </summary>
        /// <param name="menu">倒计时菜单实例</param>
        public void Initialize(CountdownMenu menu)
        {
            countdownMenu = menu;
            Debug.Log($"{LOG_TAG} 工具栏按钮初始化完成");
        }

        /// <summary>
        /// Unity每帧更新
        /// 参考MechJeb模式：每帧检查ApplicationLauncher.Ready状态，
        /// 就绪后注册按钮。不依赖onGUIApplicationLauncherReady事件。
        /// </summary>
        void Update()
        {
            // 仅在飞行场景且按钮未注册时尝试注册
            if (launcherButton == null && HighLogic.LoadedSceneIsFlight)
            {
                SetupAppLauncher();
            }

            // Ctrl+L快捷键兜底：切换菜单显示/隐藏
            // 当工具栏按钮无法显示时，可通过此快捷键操作
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
        /// 参考MechJeb的SetupAppLauncher方法：
        ///   1. 检查ApplicationLauncher.Ready
        ///   2. 就绪后调用AddModApplication注册按钮
        /// </summary>
        private void SetupAppLauncher()
        {
            // 检查ApplicationLauncher是否就绪（参考MJ: if (!ApplicationLauncher.Ready) return;）
            if (!KSPApiHelper.IsApplicationLauncherReady())
            {
                return;
            }

            // 加载图标纹理
            if (iconTexture == null)
            {
                iconTexture = LoadIconTexture();
            }

            // 添加模组按钮到ApplicationLauncher（通过反射）
            // 参考MJ: ApplicationLauncher.Instance.AddModApplication(
            //   ShowHideMasterWindow, ShowHideMasterWindow, null, null, null, null,
            //   ApplicationLauncher.AppScenes.ALWAYS, mjButtonTexture);
            int flightScenes = KSPApiHelper.GetAppScenesFlight();
            launcherButton = KSPApiHelper.AddModApplication(
                OnButtonToggle,       // 激活回调（按钮被按下）
                OnButtonUntoggle,     // 取消激活回调（按钮被弹起）
                null,                 // 鼠标悬停回调
                null,                 // 鼠标移出回调
                null,                 // 按钮启用回调
                null,                 // 按钮禁用回调
                flightScenes,         // 仅在飞行场景显示
                iconTexture           // 按钮图标
            );

            if (launcherButton != null)
            {
                Debug.Log($"{LOG_TAG} 工具栏按钮已注册");
            }
        }

        /// <summary>
        /// 从GameDatabase加载图标纹理
        /// 图标文件：GameData/KSPLaunchCountdown/Textures/icon.png
        /// GameDatabase在游戏启动时自动扫描GameData目录下的PNG文件
        /// 路径格式：模组名/目录/文件名（不含扩展名和GameData前缀）
        /// </summary>
        /// <returns>图标纹理，加载失败返回null</returns>
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
        /// 参考MechJeb的ClearButtons方法：
        ///   检查按钮的gameObject是否还存在再移除
        /// </summary>
        public void Cleanup()
        {
            // 参考MJ: if (mjButton != null && mjButton.gameObject != null)
            // 通过反射检查按钮的gameObject是否存在
            if (launcherButton != null)
            {
                bool gameObjectExists = true;
                try
                {
                    var goProp = launcherButton.GetType().GetProperty("gameObject",
                        System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
                    if (goProp != null)
                    {
                        var go = goProp.GetValue(launcherButton);
                        if (go == null) gameObjectExists = false;
                    }
                }
                catch
                {
                    // 无法检查，假设存在
                }

                if (gameObjectExists)
                {
                    KSPApiHelper.RemoveModApplication(launcherButton);
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

        /// <summary>
        /// Unity生命周期方法，在对象销毁时清理
        /// 确保即使Cleanup未被显式调用也能清理资源
        /// </summary>
        void OnDestroy()
        {
            Cleanup();
        }
    }
}
