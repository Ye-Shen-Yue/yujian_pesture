using UnityEngine;

namespace YuJian.Formation
{
    /// <summary>
    /// 阵法插值器 - Bezier曲线过渡动画
    /// 实现阵法切换时飞剑的弧形飞行轨迹
    /// </summary>
    public class FormationInterpolator
    {
        /// <summary>
        /// 计算Bezier曲线过渡位置
        /// 使用二次Bezier曲线产生弧形轨迹
        /// </summary>
        /// <param name="start">起始位置</param>
        /// <param name="end">目标位置</param>
        /// <param name="t">插值参数 0~1</param>
        /// <param name="arcHeight">弧形高度</param>
        /// <returns>当前位置</returns>
        public static Vector3 BezierInterpolate(Vector3 start, Vector3 end,
                                                  float t, float arcHeight = 2f)
        {
            // 控制点：起点和终点的中点上方
            Vector3 mid = (start + end) * 0.5f;
            Vector3 controlPoint = mid + Vector3.up * arcHeight;

            // 二次Bezier: B(t) = (1-t)²P0 + 2(1-t)tP1 + t²P2
            float u = 1f - t;
            return u * u * start + 2f * u * t * controlPoint + t * t * end;
        }

        /// <summary>
        /// 应用AnimationCurve的缓动插值
        /// </summary>
        public static Vector3 EasedBezierInterpolate(Vector3 start, Vector3 end,
                                                       float t,
                                                       AnimationCurve curve,
                                                       float arcHeight = 2f)
        {
            float easedT = curve.Evaluate(t);
            return BezierInterpolate(start, end, easedT, arcHeight);
        }

        /// <summary>
        /// 螺旋过渡（用于天罡北斗阵等旋转阵法）
        /// </summary>
        public static Vector3 SpiralInterpolate(Vector3 start, Vector3 end,
                                                  Vector3 center, float t,
                                                  float extraRotation = 180f)
        {
            // 线性插值位置
            Vector3 linearPos = Vector3.Lerp(start, end, t);

            // 添加围绕中心的额外旋转
            Vector3 offset = linearPos - center;
            float angle = extraRotation * t * (1f - t) * 4f;  // 中间旋转最多
            Quaternion rot = Quaternion.Euler(0, angle, 0);
            return center + rot * offset;
        }
    }
}
