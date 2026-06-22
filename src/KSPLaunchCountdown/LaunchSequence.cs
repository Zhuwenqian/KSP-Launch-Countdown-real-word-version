/**
 * LaunchSequence.cs - KSP1 发射倒计时发射序列执行器
 *
 * 用途：执行发射操作序列，包括开启SAS、设置油门、激活下一级（分离+启动发动机）。
 * 提供独立的发射操作方法，供倒计时控制器按序列调用。
 * 支持设置任意油门值（0.0~1.0），用于正常发射和"先点火后放行"策略。
 *
 * 操作说明：
 *   - EnableSAS(): 开启SAS稳定模式，防止火箭在发射过程中翻滚
 *   - SetFullThrottle(): 设置满油门，通过OnFlyByWire回调持续保持
 *   - ActivateNextStage(): 激活下一级，等效于按空格键，触发分离和发动机点火
 *
 * 兼容性说明：
 *   由于精简版Assembly-CSharp.dll缺少StageManager、FlightInputHandler等类型定义，
 *   本类通过KSPApiHelper反射辅助类调用KSP API，确保编译兼容性。
 *   运行时KSP游戏自带完整DLL，所有API均可正常工作。
 *
 * 依赖：
 *   - Assembly-CSharp.dll (KSP核心，提供FlightGlobals、FlightInputHandler、
 *     StageManager、Vessel、KSPActionGroup等)
 *   - UnityEngine.CoreModule.dll (Unity核心，提供MonoBehaviour、Debug日志)
 *   - KSPApiHelper.cs (KSP API反射辅助类)
 */

using UnityEngine;

namespace KSPLaunchCountdown
{
    /// <summary>
    /// 发射序列执行器
    /// 负责执行发射相关的飞船操作：SAS、油门、分级
    /// 挂载到GameObject上，通过OnFlyByWire回调持续控制油门
    /// </summary>
    public class LaunchSequence : MonoBehaviour
    {
        /// <summary>日志标签</summary>
        private const string LOG_TAG = "[KSPLaunchCountdown]";

        /// <summary>是否需要持续保持油门状态</summary>
        private bool holdThrottle = false;

        /// <summary>
        /// 当前应保持的油门目标值（0.0~1.0）
        /// 用于在OnFlyByWire回调中持续保持该油门值
        /// </summary>
        private float targetThrottle = 1.0f;

        /// <summary>
        /// 开启SAS（稳定性增强系统）
        /// 设置为稳定模式，防止火箭在发射过程中翻滚
        /// </summary>
        public void EnableSAS()
        {
            Vessel vessel = FlightGlobals.ActiveVessel;
            if (vessel == null)
            {
                Debug.LogWarning($"{LOG_TAG} 无法开启SAS：当前无活跃飞船");
                return;
            }

            // 开启SAS
            vessel.ActionGroups.SetGroup(KSPActionGroup.SAS, true);
            Debug.Log($"{LOG_TAG} SAS已开启");
        }

        /// <summary>
        /// 设置指定油门值
        /// 通过FlightInputHandler设置油门值，并通过OnFlyByWire回调持续保持
        /// 参数范围 0.0（关闭）到 1.0（满油门）
        /// </summary>
        /// <param name="throttle">油门值（0.0~1.0）</param>
        public void SetThrottle(float throttle)
        {
            Vessel vessel = FlightGlobals.ActiveVessel;
            if (vessel == null)
            {
                Debug.LogWarning($"{LOG_TAG} 无法设置油门：当前无活跃飞船");
                return;
            }

            // 限制油门值在合法范围内
            float clampedThrottle = Mathf.Clamp01(throttle);

            // 立即设置油门
            FlightInputHandler.state.mainThrottle = clampedThrottle;

            // 注册OnFlyByWire回调，持续保持油门
            // 原因：FlightInputHandler.state每帧会被重置，
            // 需要在OnFlyByWire回调中持续设置才能保持油门
            holdThrottle = true;
            targetThrottle = clampedThrottle;

            Debug.Log($"{LOG_TAG} 油门已设置为 {clampedThrottle:P0}");
        }

        /// <summary>
        /// 设置满油门
        /// 是SetThrottle(1.0f)的快捷方式
        /// </summary>
        public void SetFullThrottle()
        {
            SetThrottle(1.0f);
        }

        /// <summary>
        /// 设置零油门
        /// 是SetThrottle(0.0f)的快捷方式
        /// 用于发动机已启动时倒计时期间保持推力为0
        /// </summary>
        public void SetZeroThrottle()
        {
            SetThrottle(0.0f);
        }

        /// <summary>
        /// 释放油门保持
        /// 取消OnFlyByWire回调中的油门保持，允许玩家手动控制油门
        /// </summary>
        public void ReleaseThrottleHold()
        {
            holdThrottle = false;
            Debug.Log($"{LOG_TAG} 油门保持已释放");
        }

        /// <summary>
        /// 激活下一级
        /// 等效于按空格键，触发当前级的分离器和下一级的发动机点火
        /// 这是KSP发射的标准操作方式
        /// </summary>
        public void ActivateNextStage()
        {
            Vessel vessel = FlightGlobals.ActiveVessel;
            if (vessel == null)
            {
                Debug.LogWarning($"{LOG_TAG} 无法分级：当前无活跃飞船");
                return;
            }

            // 使用StageManager激活下一级（通过反射）
            KSPApiHelper.ActivateNextStage();
            Debug.Log($"{LOG_TAG} 已激活下一级（分级+点火）");
        }

        /// <summary>
        /// OnFlyByWire回调
        /// 当飞船需要处理飞行输入时被调用（每帧）
        /// 用于持续保持油门状态，防止被其他输入覆盖
        /// </summary>
        /// <param name="state">飞行控制状态，包含油门、姿态等输入值</param>
        private void OnFlyByWire(FlightCtrlState state)
        {
            if (holdThrottle)
            {
                state.mainThrottle = targetThrottle;
            }
        }

        /// <summary>
        /// Unity生命周期方法，每帧调用
        /// 用于注册/注销OnFlyByWire回调
        /// </summary>
        void Update()
        {
            Vessel vessel = FlightGlobals.ActiveVessel;

            if (holdThrottle && vessel != null)
            {
                // 注册OnFlyByWire回调（每帧注册确保不丢失）
                // 注意：KSP内部可能会清除回调列表，所以需要持续注册
                vessel.OnFlyByWire -= OnFlyByWire;  // 先移除避免重复注册
                vessel.OnFlyByWire += OnFlyByWire;
            }
        }

        /// <summary>
        /// Unity生命周期方法，在对象销毁时清理
        /// 移除OnFlyByWire回调，释放油门保持
        /// </summary>
        void OnDestroy()
        {
            Vessel vessel = FlightGlobals.ActiveVessel;
            if (vessel != null)
            {
                vessel.OnFlyByWire -= OnFlyByWire;
            }
            holdThrottle = false;
        }
    }
}
