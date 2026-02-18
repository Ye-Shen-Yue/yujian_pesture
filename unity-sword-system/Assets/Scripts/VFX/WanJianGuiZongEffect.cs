using UnityEngine;
using System.Collections.Generic;
using YuJian.Core;
using YuJian.Sword;
using YuJian.Formation;

namespace YuJian.VFX
{
    /// <summary>
    /// 万剑归宗特效控制器
    /// 效果流程：
    /// 1. 当前剑收束到中心点（聚剑）
    /// 2. 从中心爆发出56把剑（分裂）
    /// 3. 剑散开后螺旋飞入万剑天罡阵阵位（布阵）
    /// 4. 阵法旋转发光（持续）
    /// </summary>
    public class WanJianGuiZongEffect : MonoBehaviour
    {
        [Header("引用")]
        [SerializeField] private SwordPool swordPool;

        [Header("效果参数")]
        [SerializeField] private float convergeTime = 0.8f;
        [SerializeField] private float burstForce = 25f;
        [SerializeField] private float burstDuration = 0.6f;
        [SerializeField] private float formationDelay = 0.8f;
        [SerializeField] private int totalSwords = 56;

        private FormationDefinition _wanJianFormation;
        private Vector3 _center;
        private float _timer;
        private EffectPhase _phase = EffectPhase.Idle;
        private List<SwordEntity> _allSwords = new List<SwordEntity>();
        private float _formationRotation;

        private enum EffectPhase
        {
            Idle,
            Converging,   // 聚剑到中心
            Bursting,     // 爆发散开
            Forming,      // 飞入阵位
            Holding,      // 阵法持续旋转
        }

        private void OnEnable()
        {
            EventBus.Subscribe<WanJianGuiZongEvent>(OnWanJianGuiZong);
        }

        private void OnDisable()
        {
            EventBus.Unsubscribe<WanJianGuiZongEvent>(OnWanJianGuiZong);
        }

        private void Start()
        {
            _wanJianFormation = FormationDefinition.CreateWanJianTianGang();
        }

        private void OnWanJianGuiZong(WanJianGuiZongEvent evt)
        {
            if (_phase != EffectPhase.Idle) return;
            _center = evt.Center;
            StartConverge();
        }

        private void StartConverge()
        {
            _phase = EffectPhase.Converging;
            _timer = convergeTime;

            // 所有活跃剑飞向中心
            foreach (var sword in swordPool.ActiveSwords)
            {
                sword.TargetPosition = _center;
                sword.SetState(SwordState.Returning);
            }

            Debug.Log("[万剑归宗] 聚剑开始");
        }

        private void Update()
        {
            if (_phase == EffectPhase.Idle) return;

            _timer -= Time.deltaTime;

            switch (_phase)
            {
                case EffectPhase.Converging:
                    if (_timer <= 0f)
                        StartBurst();
                    break;

                case EffectPhase.Bursting:
                    if (_timer <= 0f)
                        StartForming();
                    break;

                case EffectPhase.Forming:
                    UpdateForming();
                    break;

                case EffectPhase.Holding:
                    UpdateHolding();
                    break;
            }
        }

        private void StartBurst()
        {
            _phase = EffectPhase.Bursting;
            _timer = burstDuration;

            // 先归还所有现有剑
            swordPool.ReleaseAll();

            // 从池中取出56把剑，全部生成在中心点
            _allSwords.Clear();
            var swords = swordPool.GetMultiple(totalSwords, _center);
            _allSwords.AddRange(swords);

            // 给每把剑一个随机爆发方向
            for (int i = 0; i < _allSwords.Count; i++)
            {
                var sword = _allSwords[i];
                sword.FormationSlotIndex = i;

                // 球形随机方向爆发
                Vector3 dir = Random.onUnitSphere;
                dir.y = Mathf.Abs(dir.y) * 0.5f + 0.2f; // 偏向上方
                sword.Rb.velocity = Vector3.zero;
                sword.Rb.AddForce(dir * burstForce, ForceMode.VelocityChange);
                sword.SetState(SwordState.FreeControl);
            }

            Debug.Log($"[万剑归宗] 分裂爆发! {_allSwords.Count}把剑");
        }

        private void StartForming()
        {
            _phase = EffectPhase.Forming;
            _timer = _wanJianFormation.TransitionDuration;
            _formationRotation = 0f;

            // 设置每把剑的目标阵位
            for (int i = 0; i < _allSwords.Count; i++)
            {
                var sword = _allSwords[i];
                if (i < _wanJianFormation.SwordCount)
                {
                    Vector3 slotPos = _wanJianFormation.GetSlotWorldPosition(
                        i, _center, 0f);
                    sword.TargetPosition = slotPos;
                    sword.SetState(SwordState.Summoning);
                }
            }

            Debug.Log("[万剑归宗] 布阵 - 万剑天罡阵");
        }

        private void UpdateForming()
        {
            // 检查是否所有剑都到位
            bool allInPlace = true;
            foreach (var sword in _allSwords)
            {
                if (sword.CurrentState == SwordState.Summoning)
                {
                    float dist = Vector3.Distance(
                        sword.transform.position, sword.TargetPosition);
                    if (dist > 0.3f)
                        allInPlace = false;
                }
            }

            if (allInPlace || _timer <= -3f)
            {
                _phase = EffectPhase.Holding;
                foreach (var sword in _allSwords)
                    sword.SetState(SwordState.InFormation);
                Debug.Log("[万剑归宗] 阵法成型!");
            }
        }

        private void UpdateHolding()
        {
            // 阵法持续旋转
            _formationRotation += _wanJianFormation.RotationSpeed * Time.deltaTime;

            for (int i = 0; i < _allSwords.Count; i++)
            {
                if (i < _wanJianFormation.SwordCount)
                {
                    Vector3 slotPos = _wanJianFormation.GetSlotWorldPosition(
                        i, _center, _formationRotation);
                    _allSwords[i].TargetPosition = slotPos;
                }
            }
        }

        /// <summary>结束万剑归宗，归还所有剑</summary>
        public void Cancel()
        {
            if (_phase == EffectPhase.Idle) return;
            _phase = EffectPhase.Idle;
            _allSwords.Clear();
            Debug.Log("[万剑归宗] 结束");
        }

        public bool IsActive => _phase != EffectPhase.Idle;
    }
}
