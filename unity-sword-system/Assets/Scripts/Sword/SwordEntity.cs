using UnityEngine;
using YuJian.Core;

namespace YuJian.Sword
{
    /// <summary>
    /// 剑体实体 - 单把飞剑的核心组件
    /// 持有物理、视觉、音频组件引用和状态机
    /// </summary>
    [RequireComponent(typeof(Rigidbody))]
    public class SwordEntity : MonoBehaviour
    {
        [Header("剑体属性")]
        public int SwordId;
        public Color SwordColor = Color.cyan;

        // 组件引用
        public Rigidbody Rb { get; private set; }
        public TrailRenderer Trail { get; private set; }
        public AudioSource Audio { get; private set; }

        // 状态
        public SwordState CurrentState { get; private set; } = SwordState.Dormant;

        // 阵法槽位（当前分配的阵位索引）
        public int FormationSlotIndex { get; set; } = -1;

        // 目标位置（阵位或手势控制目标）
        public Vector3 TargetPosition { get; set; }

        private SwordPhysics _physics;

        private void Awake()
        {
            Rb = GetComponent<Rigidbody>();
            Trail = GetComponentInChildren<TrailRenderer>();
            Audio = GetComponentInChildren<AudioSource>();

            _physics = new SwordPhysics(Rb);

            // 配置Rigidbody
            Rb.useGravity = false;
            Rb.interpolation = RigidbodyInterpolation.Interpolate;
            Rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            Rb.mass = 0.8f;  // 约800g，接近真实长剑
            Rb.drag = 2f;
        }

        private void FixedUpdate()
        {
            switch (CurrentState)
            {
                case SwordState.Summoning:
                    _physics.MoveToTarget(TargetPosition, stiffness: 30f, damping: 8f);
                    // 到达目标位置后切换状态
                    if (Vector3.Distance(transform.position, TargetPosition) < 0.1f)
                        SetState(SwordState.InFormation);
                    break;

                case SwordState.InFormation:
                    _physics.MoveToTarget(TargetPosition, stiffness: 50f, damping: 12f);
                    break;

                case SwordState.FreeControl:
                    _physics.MoveToTarget(TargetPosition, stiffness: 80f, damping: 10f);
                    break;

                case SwordState.Returning:
                    _physics.MoveToTarget(TargetPosition, stiffness: 40f, damping: 10f);
                    if (Vector3.Distance(transform.position, TargetPosition) < 0.15f)
                        SetState(SwordState.InFormation);
                    break;

                case SwordState.Dissolving:
                    // 螺旋收束运动
                    _physics.SpiralToTarget(TargetPosition, Time.fixedDeltaTime);
                    break;
            }

            // 剑体朝向运动方向
            if (Rb.velocity.sqrMagnitude > 0.1f)
            {
                Quaternion targetRot = Quaternion.LookRotation(Rb.velocity.normalized);
                transform.rotation = Quaternion.Slerp(
                    transform.rotation, targetRot, Time.fixedDeltaTime * 10f
                );
            }
        }

        /// <summary>设置剑体状态</summary>
        public void SetState(SwordState newState)
        {
            if (newState == CurrentState) return;
            CurrentState = newState;

            switch (newState)
            {
                case SwordState.Dormant:
                    gameObject.SetActive(false);
                    break;
                case SwordState.Summoning:
                    gameObject.SetActive(true);
                    if (Trail) Trail.Clear();
                    break;
                case SwordState.Dissolving:
                    _physics.ResetSpiralAngle();
                    break;
            }

            EventBus.Publish(new SwordStateChangedEvent
            {
                SwordId = SwordId,
                NewState = newState,
            });
        }

        /// <summary>获取当前速度标量</summary>
        public float Speed => Rb.velocity.magnitude;
    }
}
