/**
 * LaunchSequence.cs - KSP1 发射倒计时发射序列执行器
 *
 * 用途：执行发射操作序列，包括开启SAS、设置满油门、激活下一级（分离+启动发动机）。
 * 提供独立的发射操作方法，供倒计时控制器按序列调用。
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

        /// <summary>是否需要持续保持满油门状态</summary>
        private bool holdFullThrottle = false;

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
        /// 设置满油门
        /// 通过FlightInputHandler设置油门值为1.0（满油门），
        /// 同时注册OnFlyByWire回调持续保持油门状态，
        /// 防止游戏或其他模组重置油门值
        /// </summary>
        public void SetFullThrottle()
        {
            Vessel vessel = FlightGlobals.ActiveVessel;
            if (vessel == null)
            {
                Debug.LogWarning($"{LOG_TAG} 无法设置油门：当前无活跃飞船");
                return;
            }

            // 立即设置满油门（通过反射）
            KSPApiHelper.SetThrottle(1.0f);

            // 注册OnFlyByWire回调，持续保持满油门
            // 原因：FlightInputHandler.state每帧会被重置，
            // 需要在OnFlyByWire回调中持续设置才能保持油门
            holdFullThrottle = true;

            Debug.Log($"{LOG_TAG} 油门已设置为满");
        }

        /// <summary>
        /// 释放油门保持
        /// 取消OnFlyByWire回调中的油门保持，允许玩家手动控制油门
        /// </summary>
        public void ReleaseThrottleHold()
        {
            holdFullThrottle = false;
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
            if (holdFullThrottle)
            {
                state.mainThrottle = 1.0f;
            }
        }

        /// <summary>
        /// Unity生命周期方法，每帧调用
        /// 用于注册/注销OnFlyByWire回调
        /// </summary>
        void Update()
        {
            Vessel vessel = FlightGlobals.ActiveVessel;

            if (holdFullThrottle && vessel != null)
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
            holdFullThrottle = false;
        }
    }
}
