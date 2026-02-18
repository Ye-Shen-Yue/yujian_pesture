using UnityEngine;

namespace YuJian.Core
{
    /// <summary>
    /// 输入桥接 - 从GestureDataBuffer读取数据并分发到各子系统
    /// 每帧从环形缓冲区读取最新手势帧，转换为Unity事件
    /// </summary>
    public class InputBridge : MonoBehaviour
    {
        private Network.GestureDataBuffer _buffer;
        private int _logCounter;  // 控制日志频率

        /// <summary>最新的手势帧数据（供其他系统直接读取）</summary>
        public GestureFrame LatestFrame { get; private set; }

        /// <summary>左手数据</summary>
        public HandData LeftHand { get; private set; }

        /// <summary>右手数据</summary>
        public HandData RightHand { get; private set; }

        /// <summary>是否有有效数据</summary>
        public bool HasData => LatestFrame != null;

        public void Initialize(Network.GestureDataBuffer buffer)
        {
            _buffer = buffer;
        }

        private void Update()
        {
            if (_buffer == null) return;

            var frame = _buffer.GetLatest();
            if (frame == null) return;

            LatestFrame = frame;

            // 调试日志（每60帧输出一次，避免刷屏）
            _logCounter++;
            if (_logCounter % 60 == 1)
            {
                Debug.Log($"[InputBridge] seq={frame.Sequence} hands={frame.Hands?.Count ?? 0} gestures={frame.Gestures?.Count ?? 0}");
            }

            // 更新左右手引用
            LeftHand = null;
            RightHand = null;
            if (frame.Hands != null)
            {
                foreach (var hand in frame.Hands)
                {
                    if (hand.Id == "Left") LeftHand = hand;
                    else if (hand.Id == "Right") RightHand = hand;
                }
            }

            // 发布手势帧事件
            EventBus.Publish(new GestureFrameEvent { Frame = frame });

            // 发布各个手势事件
            if (frame.Gestures != null)
            {
                foreach (var gesture in frame.Gestures)
                {
                    if (gesture.Type != GestureType.None)
                    {
                        Debug.Log($"[InputBridge] 手势事件: {gesture.Type} hand={gesture.HandId} conf={gesture.Confidence:F2}");
                    }
                    EventBus.Publish(new GestureDetectedEvent
                    {
                        Type = gesture.Type,
                        HandId = gesture.HandId,
                        Confidence = gesture.Confidence,
                        Params = gesture.Params,
                    });
                }
            }
        }

        /// <summary>
        /// 将归一化掌心坐标(0~1)映射到世界空间
        /// </summary>
        public Vector3 PalmToWorldPosition(Vector3 palmPos, float depth = 5f)
        {
            // 将归一化坐标映射到摄像机视锥
            var cam = Camera.main;
            if (cam == null) return Vector3.zero;

            // x: 0~1 → 屏幕左到右
            // y: 0~1 → 屏幕上到下 (翻转)
            // z: 用于深度偏移
            float screenX = palmPos.x * Screen.width;
            float screenY = (1f - palmPos.y) * Screen.height;
            Vector3 screenPoint = new Vector3(screenX, screenY, depth + palmPos.z * 2f);

            return cam.ScreenToWorldPoint(screenPoint);
        }
    }
}
