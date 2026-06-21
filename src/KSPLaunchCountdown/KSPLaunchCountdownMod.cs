/**
 * KSPLaunchCountdownMod.cs - KSP1 发射倒计时模组入口类
 *
 * 用途：KSP1模组的主入口点，继承自KSP的KSPAddon特性标记类。
 * 该类负责模组的初始化、生命周期管理和资源清理。
 * 作为KSP模组开发环境的验证基础，确保项目结构、引用和编译配置正确。
 *
 * 依赖：
 *   - Assembly-CSharp.dll (KSP核心，提供KSPAddon、MonoBehaviour等)
 *   - UnityEngine.CoreModule.dll (Unity核心，提供MonoBehaviour基类)
 *
 * KSP加载机制：
 *   KSP通过 [KSPAddon] 特性自动发现和加载模组类，
 *   无需手动注册，只需标注特性并继承 MonoBehaviour 即可。
 */

using UnityEngine;
using KSP;

namespace KSPLaunchCountdown
{
    /// <summary>
    /// 模组主入口类
    /// KSPAddon特性参数说明：
    ///   - KSPAddon.Startup.MainMenu: 模组加载时机，在主菜单加载时启动
    ///     可选值：Flight, TrackingStation, SpaceCentre, Editor 等
    ///   - false: 是否只实例化一次（false表示每次场景加载都创建新实例）
    /// </summary>
    [KSPAddon(KSPAddon.Startup.MainMenu, false)]
    public class KSPLaunchCountdownMod : MonoBehaviour
    {
        /// <summary>
        /// Unity生命周期方法，在对象首次创建时调用（仅一次）
        /// 用于初始化模组
        /// </summary>
        void Awake()
        {
            Debug.Log("[KSPLaunchCountdown] 模组已加载 - 发射倒计时准备就绪");
        }

        /// <summary>
        /// Unity生命周期方法，在对象启用时调用
        /// 用于注册事件和启动功能
        /// </summary>
        void Start()
        {
            Debug.Log("[KSPLaunchCountdown] 模组已启动");
        }

        /// <summary>
        /// Unity生命周期方法，每帧调用
        /// 用于更新倒计时逻辑和UI显示
        /// </summary>
        void Update()
        {
            // 倒计时逻辑将在此实现
        }

        /// <summary>
        /// Unity生命周期方法，在对象销毁时调用
        /// 用于清理资源、注销事件等
        /// </summary>
        void OnDestroy()
        {
            Debug.Log("[KSPLaunchCountdown] 模组已卸载");
        }
    }
}
