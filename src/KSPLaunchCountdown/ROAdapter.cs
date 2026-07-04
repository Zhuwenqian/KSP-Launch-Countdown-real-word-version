/**
 * ROAdapter.cs - KSP1 发射倒计时 RO（Realism Overhaul）适配器
 *
 * 用途：检测玩家是否安装了 Realism Overhaul（RO）模组，并为其他模块提供统一的查询接口。
 * RO 环境下火箭发动机从点火到达到额定推力需要更长的时间，若仍按 Stock 的短延迟执行第二次分级，
 * 火箭可能在推力尚未建立时分离，导致落回发射台损坏引擎。本适配器用于在运行时识别 RO 环境，
 * 从而让倒计时控制器自动切换到更长的 RO 专用延迟。
 *
 * 检测原理：
 *   检查 KSP 游戏根目录下的 GameData/RealismOverhaul 目录是否存在。
 *   该目录是 RO 模组的安装标识，只要玩家通过 CKAN 或手动正确安装 RO，此目录必然存在。
 *   采用目录检测而非反射 RO 程序集，可避免 RO 版本升级导致的兼容性问题。
 *
 * 使用方式：
 *   其他模块直接访问 ROAdapter.IsROInstalled 静态属性即可获取检测结果。
 *   首次访问时会执行一次检测并缓存结果，后续访问直接返回缓存值，避免重复 IO。
 *
 * 可调整参数：
 *   - RO_DIRECTORY_NAME：RO 模组在 GameData 下的目录名，默认 "RealismOverhaul"。
 *     若未来 RO 安装结构发生变化，可修改此常量适配新的目录名。
 *
 * 依赖：
 *   - Assembly-CSharp.dll (KSP核心，提供KSPUtil)
 *   - UnityEngine.CoreModule.dll (Unity核心，提供Debug日志)
 *   - System.dll (.NET核心，提供IO与Nullable支持)
 */

using System.IO;
using UnityEngine;

namespace KSPLaunchCountdown
{
    /// <summary>
    /// RO 环境检测适配器
    /// 提供静态方法检测 Realism Overhaul 是否安装，并缓存检测结果
    /// </summary>
    public static class ROAdapter
    {
        /// <summary>日志标签</summary>
        private const string LOG_TAG = "[KSPLaunchCountdown]";

        /// <summary>
        /// RO 模组在 GameData 下的目录名
        /// 可调整参数：若 RO 安装目录结构变化，修改此值即可适配
        /// </summary>
        private const string RO_DIRECTORY_NAME = "RealismOverhaul";

        /// <summary>
        /// RO 检测结果的缓存
        /// 使用可空布尔避免重复检测，首次访问时赋值
        /// </summary>
        private static bool? isROInstalled;

        /// <summary>
        /// 获取当前是否安装了 RO 模组
        /// 首次访问时执行目录检测，之后返回缓存结果
        /// </summary>
        public static bool IsROInstalled
        {
            get
            {
                if (!isROInstalled.HasValue)
                {
                    isROInstalled = DetectRO();
                }
                return isROInstalled.Value;
            }
        }

        /// <summary>
        /// 强制重新检测 RO 安装状态
        /// 主要用于调试或场景切换后需要刷新状态的场景
        /// </summary>
        public static void Refresh()
        {
            isROInstalled = DetectRO();
        }

        /// <summary>
        /// 执行 RO 安装检测
        /// 通过检查 GameData/RealismOverhaul 目录是否存在来判断
        /// </summary>
        /// <returns>是否检测到 RO 安装</returns>
        private static bool DetectRO()
        {
            try
            {
                string roPath = Path.Combine(
                    KSPUtil.ApplicationRootPath,
                    "GameData",
                    RO_DIRECTORY_NAME
                );

                bool exists = Directory.Exists(roPath);
                Debug.Log($"{LOG_TAG} RO 环境检测: 路径={roPath}, 结果={exists}");
                return exists;
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"{LOG_TAG} RO 环境检测失败: {ex.Message}，按未安装处理");
                return false;
            }
        }
    }
}
