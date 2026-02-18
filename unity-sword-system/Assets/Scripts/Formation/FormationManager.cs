using UnityEngine;
using System.Collections.Generic;
using YuJian.Core;
using YuJian.Sword;

namespace YuJian.Formation
{
    /// <summary>
    /// 阵法管理器 - 激活/切换阵法，分配剑到槽位
    /// 使用匈牙利算法最优分配，Bezier曲线过渡
    /// </summary>
    public class FormationManager : MonoBehaviour
    {
        [Header("引用")]
        [SerializeField] private SwordPool swordPool;

        [Header("阵法库")]
        [SerializeField] private List<FormationDefinition> formations;

        [Header("设置")]
        [SerializeField] private Vector3 formationCenter = Vector3.zero;

        /// <summary>当前激活的阵法</summary>
        public FormationDefinition ActiveFormation { get; private set; }

        private float _rotationAngle;
        private bool _isTransitioning;
        private float _transitionProgress;
        private Vector3[] _transitionStartPositions;
        private Vector3[] _transitionEndPositions;
        private AnimationCurve _transitionCurve;
        private float _transitionDuration;

        // 剑到槽位的分配映射
        private readonly Dictionary<int, SwordEntity> _slotAssignments = new();

        private void OnEnable()
        {
            EventBus.Subscribe<PhaseChangedEvent>(OnPhaseChanged);
            EventBus.Subscribe<GestureDetectedEvent>(OnGesture);
        }

        private void OnDisable()
        {
            EventBus.Unsubscribe<PhaseChangedEvent>(OnPhaseChanged);
            EventBus.Unsubscribe<GestureDetectedEvent>(OnGesture);
        }

        private void Update()
        {
            if (ActiveFormation == null) return;

            // 处理过渡动画
            if (_isTransitioning)
            {
                _transitionProgress += Time.deltaTime / _transitionDuration;
                if (_transitionProgress >= 1f)
                {
                    _transitionProgress = 1f;
                    _isTransitioning = false;
                }

                UpdateTransition();
                return;
            }

            // 阵法旋转
            if (ActiveFormation.RotationSpeed != 0)
            {
                _rotationAngle += ActiveFormation.RotationSpeed * Time.deltaTime;
            }

            // 更新剑的目标位置
            UpdateSlotPositions();
        }

        /// <summary>激活指定阵法</summary>
        public void ActivateFormation(FormationDefinition formation)
        {
            if (formation == null) return;

            var prevFormation = ActiveFormation;
            ActiveFormation = formation;
            _rotationAngle = 0f;

            // 分配剑到槽位
            AssignSwordsToSlots();

            // 如果有前一个阵法，启动过渡动画
            if (prevFormation != null)
            {
                StartTransition();
            }
            else
            {
                UpdateSlotPositions();
            }

            EventBus.Publish(new FormationActivatedEvent
            {
                FormationName = formation.FormationName,
                SwordCount = formation.SwordCount,
            });

            Debug.Log($"[Formation] 激活阵法: {formation.FormationName} ({formation.SwordCount}剑)");
        }

        /// <summary>激活指定索引的阵法</summary>
        public void ActivateFormation(int index)
        {
            if (index >= 0 && index < formations.Count)
                ActivateFormation(formations[index]);
        }

        private void AssignSwordsToSlots()
        {
            _slotAssignments.Clear();
            var activeSwords = swordPool.ActiveSwords;

            int assignCount = Mathf.Min(activeSwords.Count,
                                         ActiveFormation.SwordCount);

            // 简化的贪心分配（匈牙利算法的近似）
            // 每个槽位分配距离最近的未分配剑
            var assigned = new HashSet<int>();

            for (int slot = 0; slot < assignCount; slot++)
            {
                Vector3 slotPos = ActiveFormation.GetSlotWorldPosition(
                    slot, formationCenter, _rotationAngle
                );

                float minDist = float.MaxValue;
                int bestSwordIdx = -1;

                for (int i = 0; i < activeSwords.Count; i++)
                {
                    if (assigned.Contains(i)) continue;
                    float dist = Vector3.Distance(
                        activeSwords[i].transform.position, slotPos
                    );
                    if (dist < minDist)
                    {
                        minDist = dist;
                        bestSwordIdx = i;
                    }
                }

                if (bestSwordIdx >= 0)
                {
                    assigned.Add(bestSwordIdx);
                    var sword = activeSwords[bestSwordIdx];
                    sword.FormationSlotIndex = slot;
                    _slotAssignments[slot] = sword;
                }
            }
        }

        private void StartTransition()
        {
            _isTransitioning = true;
            _transitionProgress = 0f;
            _transitionDuration = ActiveFormation.TransitionDuration;
            _transitionCurve = ActiveFormation.TransitionCurve;

            // 记录起始位置
            int count = _slotAssignments.Count;
            _transitionStartPositions = new Vector3[count];
            _transitionEndPositions = new Vector3[count];

            int i = 0;
            foreach (var kvp in _slotAssignments)
            {
                _transitionStartPositions[i] = kvp.Value.transform.position;
                _transitionEndPositions[i] = ActiveFormation.GetSlotWorldPosition(
                    kvp.Key, formationCenter, _rotationAngle
                );
                i++;
            }
        }

        private void UpdateTransition()
        {
            int i = 0;
            foreach (var kvp in _slotAssignments)
            {
                if (i < _transitionStartPositions.Length)
                {
                    kvp.Value.TargetPosition =
                        FormationInterpolator.EasedBezierInterpolate(
                            _transitionStartPositions[i],
                            _transitionEndPositions[i],
                            _transitionProgress,
                            _transitionCurve
                        );
                }
                i++;
            }
        }

        private void UpdateSlotPositions()
        {
            foreach (var kvp in _slotAssignments)
            {
                int slot = kvp.Key;
                var sword = kvp.Value;
                if (sword.CurrentState == SwordState.InFormation ||
                    sword.CurrentState == SwordState.Summoning)
                {
                    sword.TargetPosition = ActiveFormation.GetSlotWorldPosition(
                        slot, formationCenter, _rotationAngle
                    );
                }
            }
        }

        private void OnPhaseChanged(PhaseChangedEvent evt)
        {
            switch (evt.NewPhase)
            {
                case GamePhase.BuZhen:
                    // 进入布阵阶段，激活默认阵法
                    if (formations.Count > 0)
                        ActivateFormation(0);
                    break;

                case GamePhase.Idle:
                    ActiveFormation = null;
                    _slotAssignments.Clear();
                    break;
            }
        }

        private void OnGesture(GestureDetectedEvent evt)
        {
            switch (evt.Type)
            {
                case GestureType.FormCircle:
                    // 画圆切换阵法
                    CycleFormation();
                    break;

                case GestureType.Rotate:
                    // 手动旋转阵法
                    _rotationAngle += 90f * Time.deltaTime;
                    break;
            }
        }

        private void CycleFormation()
        {
            if (formations.Count == 0) return;
            int currentIdx = formations.IndexOf(ActiveFormation);
            int nextIdx = (currentIdx + 1) % formations.Count;
            ActivateFormation(nextIdx);
        }
    }
}
