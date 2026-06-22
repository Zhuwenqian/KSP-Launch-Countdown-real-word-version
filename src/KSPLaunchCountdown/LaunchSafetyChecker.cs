/**
 * LaunchSafetyChecker.cs - KSP1 发射倒计时安全检查器
 *
 * 用途：在倒计时开始前检查飞船状态，防止误操作导致发射失败。
 * 检查项包括：
 *   1. 是否在发射台（地面高度 < 100m 且速度接近0）
 *   2. 是否已有正在进行的倒计时
 *   3. 飞船电量是否充足（当前电量 < 总电量 1/20 时拒绝发射）
 *   4. 芯一级发动机是否已启动
 *
 * 对于发动机已启动的情况，模组采用"先点火后放行"策略：
 *   - 倒计时期间保持油门为0
 *   - 倒计时音频播放结束后只加满油门，不执行自动分级
 *   - 由玩家手动控制后续分级操作
 *
 * 设计说明：
 *   - 所有阈值参数在类内集中定义，便于统一调整
 *   - 检查结果以对象形式返回，包含是否通过、失败原因和特殊状态标志
 *   - 记录 LowElectricCharge 标志，供 CountdownController 在 SAS 开启失败时区分 MJ 控制和停电
 *   - 检查失败时由 CountdownMenu 在窗口内显示警告和"强制发射"复选框
 *
 * 依赖：
 *   - Assembly-CSharp.dll (KSP核心，提供Vessel、PartResource等)
 *   - UnityEngine.CoreModule.dll (Unity核心，提供Vector3、Debug等)
 */

using System.Collections.Generic;
using UnityEngine;

namespace KSPLaunchCountdown
{
    /// <summary>
    /// 发射前安全检查结果
    /// 封装检查是否通过、失败原因列表和特殊状态标志
    /// </summary>
    public class SafetyCheckResult
    {
        /// <summary>检查是否全部通过</summary>
        public bool IsSafe { get; set; } = true;

        /// <summary>失败原因列表（仅当IsSafe为false时有效）</summary>
        public List<string> FailureReasons { get; set; } = new List<string>();

        /// <summary>
        /// 芯一级发动机是否已经启动
        /// 如果为true，倒计时期间应保持油门为0，音频结束后再加满油门
        /// </summary>
        public bool EngineAlreadyRunning { get; set; } = false;

        /// <summary>
        /// 是否电量不足（低于总电量的1/20）
        /// 用于SAS开启失败时区分MJ控制和停电
        /// </summary>
        public bool LowElectricCharge { get; set; } = false;

        /// <summary>添加失败原因</summary>
        public void AddFailure(string reason)
        {
            IsSafe = false;
            FailureReasons.Add(reason);
        }
    }

    /// <summary>
    /// 发射前安全检查器
    /// 负责在倒计时开始前验证飞船是否满足发射条件
    /// </summary>
    public static class LaunchSafetyChecker
    {
        /// <summary>日志标签</summary>
        private const string LOG_TAG = "[KSPLaunchCountdown]";

        /// <summary>
        /// 判断在发射台的最大高度阈值（米）
        /// 可调整参数：增大此值允许在更高位置启动倒计时
        /// </summary>
        private const float MAX_LAUNCHPAD_ALTITUDE = 100f;

        /// <summary>
        /// 判断速度接近0的阈值（米/秒）
        /// 可调整参数：增大此值允许在微动状态下启动倒计时
        /// </summary>
        private const float MAX_LAUNCHPAD_VELOCITY = 0.5f;

        /// <summary>
        /// 电量不足阈值（占总电量的比例）
        /// 默认 1/20 = 0.05，即电量低于5%时拒绝发射
        /// 可调整参数：增大此值会让发射前电量要求更严格
        /// </summary>
        private const float LOW_ELECTRIC_CHARGE_RATIO = 0.05f;

        /// <summary>
        /// 执行发射前安全检查
        /// </summary>
        /// <param name="vessel">当前活跃飞船</param>
        /// <param name="isCountingDown">是否已有正在进行的倒计时</param>
        /// <param name="localization">本地化系统，用于生成失败原因文本</param>
        /// <returns>安全检查结果</returns>
        public static SafetyCheckResult PerformCheck(Vessel vessel, bool isCountingDown, Localization localization)
        {
            SafetyCheckResult result = new SafetyCheckResult();

            if (vessel == null)
            {
                // 没有活跃飞船属于基础环境检查失败
                result.AddFailure("No active vessel");
                Debug.LogWarning($"{LOG_TAG} 安全检查失败：无活跃飞船");
                return result;
            }

            // 检查1：是否已有正在进行的倒计时
            if (isCountingDown)
            {
                result.AddFailure(localization.GetString(Localization.Keys.SafetyCheckAlreadyCountingDown));
                Debug.LogWarning($"{LOG_TAG} 安全检查失败：已有正在进行的倒计时");
            }

            // 检查2：是否在发射台
            // 使用 vessel.radarAltitude 获取雷达高度（相对于地面的高度）
            // 使用 vessel.srf_velocity.magnitude 获取地表速度
            float altitude = (float)vessel.radarAltitude;
            float velocity = (float)vessel.srf_velocity.magnitude;

            if (altitude > MAX_LAUNCHPAD_ALTITUDE || velocity > MAX_LAUNCHPAD_VELOCITY)
            {
                result.AddFailure(localization.GetString(Localization.Keys.SafetyCheckNotOnLaunchPad));
                Debug.LogWarning($"{LOG_TAG} 安全检查失败：高度={altitude:F1}m, 速度={velocity:F1}m/s");
            }

            // 检查3：飞船电量是否充足
            // 当前电量小于总电量的1/20时拒绝发射
            if (!HasEnoughElectricCharge(vessel))
            {
                result.LowElectricCharge = true;
                result.AddFailure(localization.GetString(Localization.Keys.SafetyCheckLowElectricCharge));
                Debug.LogWarning($"{LOG_TAG} 安全检查失败：飞船电量不足");
            }

            // 检查4：芯一级发动机是否已启动
            // 通过检查所有ModuleEngines的当前推力来判断
            // 如果任意发动机正在产生推力，则认为发动机已启动
            result.EngineAlreadyRunning = IsAnyEngineRunning(vessel);
            if (result.EngineAlreadyRunning)
            {
                // 发动机已启动不是失败条件，而是特殊状态
                // 记录日志以便调试
                Debug.Log($"{LOG_TAG} 安全检查：芯一级发动机已启动，将采用点火后放行策略");
            }

            return result;
        }

        /// <summary>
        /// 检查飞船电量是否充足
        /// 当前电量小于总电量的1/20时返回false
        /// </summary>
        /// <param name="vessel">当前活跃飞船</param>
        /// <returns>电量是否充足</returns>
        private static bool HasEnoughElectricCharge(Vessel vessel)
        {
            if (vessel == null) return false;

            double currentCharge = 0.0;
            double maxCharge = 0.0;

            // 遍历所有部件，累加 ElectricCharge 资源的当前值和最大值
            foreach (Part part in vessel.Parts)
            {
                if (part.Resources == null) continue;

                foreach (PartResource resource in part.Resources)
                {
                    if (resource.resourceName == "ElectricCharge")
                    {
                        currentCharge += resource.amount;
                        maxCharge += resource.maxAmount;
                    }
                }
            }

            // 没有电池/太阳能板时 maxCharge 为0，此时跳过电量检查
            if (maxCharge <= 0.0)
            {
                return true;
            }

            double ratio = currentCharge / maxCharge;
            Debug.Log($"{LOG_TAG} 飞船电量: {currentCharge:F1}/{maxCharge:F1} ({ratio:P0})");

            return ratio >= LOW_ELECTRIC_CHARGE_RATIO;
        }

        /// <summary>
        /// 检查飞船上是否有任何发动机正在产生推力
        /// </summary>
        /// <param name="vessel">当前活跃飞船</param>
        /// <returns>是否有发动机正在运行</returns>
        private static bool IsAnyEngineRunning(Vessel vessel)
        {
            if (vessel == null) return false;

            // 遍历飞船的所有部件，查找 ModuleEngines 模块
            foreach (Part part in vessel.Parts)
            {
                foreach (PartModule module in part.Modules)
                {
                    // ModuleEngines 代表液体/固体火箭发动机
                    // ModuleEnginesFX 是其衍生类型，使用 is 可同时匹配
                    if (module is ModuleEngines engine)
                    {
                        // 检查发动机是否正在产生推力（大于一个很小的阈值）
                        if (engine.isOperational && engine.finalThrust > 0.01f)
                        {
                            return true;
                        }
                    }
                }
            }

            return false;
        }
    }
}
