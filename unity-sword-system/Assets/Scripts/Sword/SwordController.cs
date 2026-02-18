using UnityEngine;
using System.Collections.Generic;
using YuJian.Core;

namespace YuJian.Sword
{
    /// <summary>
    /// 剑体控制器 - 将手势输入映射为剑体命令
    /// 监听手势事件和阶段变化，控制剑的行为
    /// </summary>
    public class SwordController : MonoBehaviour
    {
        [Header("引用")]
        [SerializeField] private SwordPool swordPool;

        [Header("召唤设置")]
        [SerializeField] private int defaultSwordCount = 7;
        [SerializeField] private float summonHeight = 3f;
        [SerializeField] private float summonRadius = 2f;

        private InputBridge _input;
        private PhaseController _phase;
        private SwordEntity _selectedSword;
        private GamePhase _currentPhase = GamePhase.Idle;

        private void Start()
        {
            _input = FindObjectOfType<InputBridge>();
            _phase = FindObjectOfType<PhaseController>();
        }

        private void OnEnable()
        {
            EventBus.Subscribe<GestureDetectedEvent>(OnGesture);
            EventBus.Subscribe<PhaseChangedEvent>(OnPhaseChanged);
        }

        private void OnDisable()
        {
            EventBus.Unsubscribe<GestureDetectedEvent>(OnGesture);
            EventBus.Unsubscribe<PhaseChangedEvent>(OnPhaseChanged);
        }

        private void Update()
        {
            if (_currentPhase == GamePhase.YuJian)
                UpdateFreeControl();
        }

        private void UpdateFreeControl()
        {
            if (_input == null || !_input.HasData) return;

            if (_input.RightHand != null)
            {
                Vector3 worldPos = _input.PalmToWorldPosition(
                    _input.RightHand.PalmPosition
                );

                if (_selectedSword != null)
                {
                    _selectedSword.TargetPosition = worldPos;
                }
                else
                {
                    // 所有活跃剑跟随右手，带偏移形成小阵型
                    foreach (var sword in swordPool.ActiveSwords)
                    {
                        if (sword.CurrentState == SwordState.FreeControl ||
                            sword.CurrentState == SwordState.InFormation)
                        {
                            Vector3 offset = GetSwordOffset(
                                sword.FormationSlotIndex, swordPool.ActiveCount);
                            sword.TargetPosition = worldPos + offset;
                        }
                    }
                }
            }
        }

        private Vector3 GetSwordOffset(int slotIndex, int totalCount)
        {
            if (totalCount <= 1) return Vector3.zero;
            float angle = slotIndex * (360f / totalCount) * Mathf.Deg2Rad;
            return new Vector3(Mathf.Cos(angle) * 1.5f, 0, Mathf.Sin(angle) * 1.5f);
        }

        private void OnGesture(GestureDetectedEvent evt)
        {
            switch (evt.Type)
            {
                case GestureType.Grab:
                    SelectNearestSword(evt.HandId);
                    break;
                case GestureType.Push:
                case GestureType.Launch:
                    LaunchSwords(evt);
                    break;
                case GestureType.Pull:
                    RecallSwords();
                    break;
                case GestureType.Rotate:
                    // FormationManager直接处理
                    break;
            }
        }

        private void OnPhaseChanged(PhaseChangedEvent evt)
        {
            _currentPhase = evt.NewPhase;

            switch (evt.NewPhase)
            {
                case GamePhase.KaiZhen:
                    SummonSwords(defaultSwordCount);
                    break;

                case GamePhase.YuJian:
                    foreach (var sword in swordPool.ActiveSwords)
                    {
                        if (sword.CurrentState == SwordState.InFormation)
                            sword.SetState(SwordState.FreeControl);
                    }
                    break;

                case GamePhase.PoZhen:
                    foreach (var sword in swordPool.ActiveSwords)
                    {
                        sword.TargetPosition = Vector3.zero;
                        sword.SetState(SwordState.Dissolving);
                    }
                    break;

                case GamePhase.Idle:
                    swordPool.ReleaseAll();
                    _selectedSword = null;
                    break;
            }
        }

        private void SummonSwords(int count)
        {
            Vector3 center = Camera.main != null
                ? Camera.main.transform.position + Camera.main.transform.forward * 5f
                  + Vector3.up * summonHeight
                : new Vector3(0, summonHeight, 3f);

            var swords = swordPool.GetMultiple(count, center);

            for (int i = 0; i < swords.Count; i++)
            {
                float angle = i * (360f / swords.Count) * Mathf.Deg2Rad;
                Vector3 slotPos = center + new Vector3(
                    Mathf.Cos(angle) * summonRadius, 0,
                    Mathf.Sin(angle) * summonRadius);
                swords[i].TargetPosition = slotPos;
                swords[i].FormationSlotIndex = i;
            }

            Debug.Log($"[SwordCtrl] 召唤 {swords.Count} 把飞剑 at {center}");
        }

        private void SelectNearestSword(string handId)
        {
            if (_input == null) return;
            HandData hand = handId == "Left" ? _input.LeftHand : _input.RightHand;
            if (hand == null) return;

            Vector3 worldPos = _input.PalmToWorldPosition(hand.PalmPosition);
            float minDist = float.MaxValue;
            SwordEntity nearest = null;

            foreach (var sword in swordPool.ActiveSwords)
            {
                float dist = Vector3.Distance(sword.transform.position, worldPos);
                if (dist < minDist) { minDist = dist; nearest = sword; }
            }

            if (nearest != null && minDist < 5f)
            {
                _selectedSword = nearest;
                nearest.SetState(SwordState.FreeControl);
                Debug.Log($"[SwordCtrl] 选中剑 #{nearest.SwordId}");
            }
        }

        private void LaunchSwords(GestureDetectedEvent evt)
        {
            Vector3 direction = Vector3.forward;
            if (_input != null && _input.RightHand != null)
            {
                direction = _input.RightHand.PalmNormal;
                if (direction.sqrMagnitude < 0.01f)
                    direction = Vector3.forward;
            }

            foreach (var sword in swordPool.ActiveSwords)
            {
                if (sword.CurrentState == SwordState.FreeControl ||
                    sword.CurrentState == SwordState.InFormation)
                {
                    sword.Rb.velocity = Vector3.zero;
                    sword.Rb.AddForce(direction.normalized * 15f, ForceMode.VelocityChange);
                }
            }
            Debug.Log($"[SwordCtrl] 发射飞剑 → {direction}");
        }

        private void RecallSwords()
        {
            foreach (var sword in swordPool.ActiveSwords)
            {
                sword.SetState(SwordState.Returning);
                if (Camera.main != null)
                    sword.TargetPosition = Camera.main.transform.position
                        + Camera.main.transform.forward * 3f;
            }
            Debug.Log("[SwordCtrl] 召回所有飞剑");
        }
    }
}
