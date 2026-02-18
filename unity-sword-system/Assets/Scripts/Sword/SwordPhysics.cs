using UnityEngine;

namespace YuJian.Sword
{
    /// <summary>
    /// 剑体物理 - 力驱动的运动控制
    /// 使用临界阻尼弹簧模型实现平滑跟随
    /// </summary>
    public class SwordPhysics
    {
        private readonly Rigidbody _rb;
        private float _spiralAngle;

        public SwordPhysics(Rigidbody rb)
        {
            _rb = rb;
        }

        /// <summary>
        /// 临界阻尼弹簧驱动移向目标
        /// force = stiffness * (target - pos) - damping * velocity
        /// </summary>
        public void MoveToTarget(Vector3 target, float stiffness = 50f,
                                  float damping = 10f)
        {
            Vector3 displacement = target - _rb.position;
            Vector3 springForce = stiffness * displacement;
            Vector3 dampingForce = damping * _rb.velocity;
            Vector3 totalForce = springForce - dampingForce;

            _rb.AddForce(totalForce, ForceMode.Acceleration);
        }

        /// <summary>
        /// 螺旋收束运动（破阵效果）
        /// 剑体沿螺旋线向中心收束
        /// </summary>
        public void SpiralToTarget(Vector3 center, float dt)
        {
            _spiralAngle += dt * 8f;  // 旋转速度
            float distance = Vector3.Distance(_rb.position, center);
            float shrinkRate = Mathf.Max(0.1f, distance * 0.95f);  // 逐渐收缩

            // 螺旋轨迹
            Vector3 offset = new Vector3(
                Mathf.Cos(_spiralAngle) * shrinkRate,
                Mathf.Sin(_spiralAngle * 0.7f) * shrinkRate * 0.3f,
                Mathf.Sin(_spiralAngle) * shrinkRate
            );

            Vector3 targetPos = center + offset;
            MoveToTarget(targetPos, stiffness: 60f, damping: 8f);
        }

        /// <summary>
        /// 发射飞剑（推手势触发）
        /// </summary>
        public void Launch(Vector3 direction, float force = 20f)
        {
            _rb.velocity = Vector3.zero;
            _rb.AddForce(direction.normalized * force, ForceMode.VelocityChange);
        }

        /// <summary>
        /// 急停（拉手势触发）
        /// </summary>
        public void Brake(float brakeFactor = 0.1f)
        {
            _rb.velocity *= brakeFactor;
        }

        public void ResetSpiralAngle()
        {
            _spiralAngle = 0f;
        }
    }
}
