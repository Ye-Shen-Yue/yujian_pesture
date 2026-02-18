using UnityEngine;
using System.Collections.Generic;

namespace YuJian.Core
{
    // ========== 枚举定义 ==========

    /// <summary>游戏阶段</summary>
    public enum GamePhase
    {
        Idle = 0,
        KaiZhen = 1,   // 启阵
        BuZhen = 2,    // 布阵
        YuJian = 3,    // 御剑
        PoZhen = 4,    // 破阵
    }

    /// <summary>手势类型（与Python端protocol.py对应）</summary>
    public enum GestureType
    {
        None = 0,
        Grab = 1,
        Push = 2,
        Pull = 3,
        Rotate = 4,
        Pinch = 5,
        Launch = 10,
        OpenArray = 20,
        CloseArray = 21,
        FormCircle = 22,
        PointPlace = 23,
        WanJian = 30,      // 万剑归宗
    }

    /// <summary>剑体状态</summary>
    public enum SwordState
    {
        Dormant,        // 休眠（对象池中）
        Summoning,      // 召唤中（飞入动画）
        InFormation,    // 阵中（保持阵位）
        FreeControl,    // 自由控制（跟随手势）
        Returning,      // 归位中（飞回阵位）
        Dissolving,     // 消散中（破阵特效）
    }

    // ========== 数据结构 ==========

    /// <summary>手部数据（反序列化自Python消息）</summary>
    [System.Serializable]
    public class HandData
    {
        public string Id;                // "Left" or "Right"
        public Vector3[] Landmarks;      // 21个3D关键点
        public Vector3 PalmPosition;     // 掌心位置
        public Vector3 PalmVelocity;     // 掌心速度
        public Vector3 PalmNormal;       // 掌心法向量
        public int[] FingerStates;       // 五指伸展状态
        public float Confidence;
    }

    /// <summary>手势事件数据</summary>
    [System.Serializable]
    public class GestureData
    {
        public GestureType Type;
        public string HandId;
        public float Confidence;
        public Dictionary<string, object> Params;
    }

    /// <summary>完整手势帧（一帧的所有数据）</summary>
    [System.Serializable]
    public class GestureFrame
    {
        public int Version;
        public int Sequence;
        public float Timestamp;
        public List<HandData> Hands;
        public List<GestureData> Gestures;
        public int Phase;
    }

    /// <summary>
    /// 阶段控制器 - 管理 启阵→布阵→御剑→破阵 状态流转
    /// activeInputHandler已设为0(旧版Input Manager)，直接用Input.GetKeyDown
    /// </summary>
    public class PhaseController : MonoBehaviour
    {
        public GamePhase CurrentPhase { get; private set; } = GamePhase.Idle;

        [Header("阶段设置")]
        [SerializeField] private float kaiZhenDuration = 3f;   // 启阵动画时长
        [SerializeField] private float poZhenDuration = 2f;    // 破阵动画时长

        private float _phaseTimer;

        private void OnEnable()
        {
            EventBus.Subscribe<GestureDetectedEvent>(OnGestureDetected);
        }

        private void OnDisable()
        {
            EventBus.Unsubscribe<GestureDetectedEvent>(OnGestureDetected);
        }

        private void Update()
        {
            // 键盘快捷键（调试/测试用）
            // 1=启阵  2=布阵  3=御剑  4=破阵  5=万剑归宗  0=回到待机
            if (Input.GetKeyDown(KeyCode.Alpha1) && CurrentPhase == GamePhase.Idle)
            {
                Debug.Log("[Phase] 键盘触发: 启阵");
                TransitionTo(GamePhase.KaiZhen);
            }
            else if (Input.GetKeyDown(KeyCode.Alpha2) && CurrentPhase == GamePhase.KaiZhen)
            {
                Debug.Log("[Phase] 键盘触发: 布阵");
                TransitionTo(GamePhase.BuZhen);
            }
            else if (Input.GetKeyDown(KeyCode.Alpha3))
            {
                Debug.Log("[Phase] 键盘触发: 御剑");
                TransitionTo(GamePhase.YuJian);
            }
            else if (Input.GetKeyDown(KeyCode.Alpha4))
            {
                Debug.Log("[Phase] 键盘触发: 破阵");
                TransitionTo(GamePhase.PoZhen);
            }
            else if (Input.GetKeyDown(KeyCode.Alpha5))
            {
                Debug.Log("[Phase] 键盘触发: 万剑归宗!");
                Vector3 center = Camera.main != null
                    ? Camera.main.transform.position + Camera.main.transform.forward * 8f
                      + Vector3.up * 3f
                    : new Vector3(0, 3f, 8f);
                EventBus.Publish(new WanJianGuiZongEvent { Center = center });
            }
            else if (Input.GetKeyDown(KeyCode.Alpha0))
            {
                Debug.Log("[Phase] 键盘触发: 回到待机");
                TransitionTo(GamePhase.Idle);
            }

            // 阶段计时器（用于自动过渡的阶段）
            if (CurrentPhase == GamePhase.KaiZhen)
            {
                _phaseTimer -= Time.deltaTime;
                if (_phaseTimer <= 0)
                    TransitionTo(GamePhase.BuZhen);
            }
            else if (CurrentPhase == GamePhase.PoZhen)
            {
                _phaseTimer -= Time.deltaTime;
                if (_phaseTimer <= 0)
                    TransitionTo(GamePhase.Idle);
            }
        }

        private void OnGestureDetected(GestureDetectedEvent evt)
        {
            switch (evt.Type)
            {
                case GestureType.OpenArray when CurrentPhase == GamePhase.Idle:
                    TransitionTo(GamePhase.KaiZhen);
                    break;

                case GestureType.FormCircle when CurrentPhase == GamePhase.BuZhen:
                    // 布阵画圆 - 由FormationManager处理
                    break;

                case GestureType.PointPlace when CurrentPhase == GamePhase.BuZhen:
                    // 布阵定位 - 由FormationManager处理
                    // 布阵完成后自动进入御剑
                    TransitionTo(GamePhase.YuJian);
                    break;

                case GestureType.CloseArray when CurrentPhase == GamePhase.YuJian:
                    TransitionTo(GamePhase.PoZhen);
                    break;

                case GestureType.WanJian:
                    Debug.Log("[Phase] 手势触发: 万剑归宗!");
                    Vector3 center = Camera.main != null
                        ? Camera.main.transform.position + Camera.main.transform.forward * 8f
                          + Vector3.up * 3f
                        : new Vector3(0, 3f, 8f);
                    EventBus.Publish(new WanJianGuiZongEvent { Center = center });
                    break;
            }
        }

        public void TransitionTo(GamePhase newPhase)
        {
            if (newPhase == CurrentPhase) return;

            var prev = CurrentPhase;
            CurrentPhase = newPhase;

            // 设置阶段计时器
            switch (newPhase)
            {
                case GamePhase.KaiZhen:
                    _phaseTimer = kaiZhenDuration;
                    break;
                case GamePhase.PoZhen:
                    _phaseTimer = poZhenDuration;
                    break;
            }

            Debug.Log($"[Phase] {prev} → {newPhase}");
            EventBus.Publish(new PhaseChangedEvent
            {
                PreviousPhase = prev,
                NewPhase = newPhase,
            });
        }
    }
}
