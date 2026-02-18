using UnityEngine;
using System.Collections.Generic;

namespace YuJian.Sword
{
    /// <summary>
    /// 剑体对象池 - 预实例化并复用飞剑对象
    /// </summary>
    public class SwordPool : MonoBehaviour
    {
        [Header("对象池设置")]
        [SerializeField] private GameObject swordPrefab;
        [SerializeField] private int poolSize = 60;
        [SerializeField] private Transform poolParent;

        private readonly Queue<SwordEntity> _available = new();
        private readonly List<SwordEntity> _active = new();
        private readonly List<SwordEntity> _all = new();

        /// <summary>当前活跃的剑数量</summary>
        public int ActiveCount => _active.Count;

        /// <summary>所有活跃的剑</summary>
        public IReadOnlyList<SwordEntity> ActiveSwords => _active;

        /// <summary>所有剑（含休眠）</summary>
        public IReadOnlyList<SwordEntity> AllSwords => _all;

        private void Awake()
        {
            if (poolParent == null)
                poolParent = transform;

            // 预实例化
            for (int i = 0; i < poolSize; i++)
            {
                var go = Instantiate(swordPrefab, poolParent);
                var entity = go.GetComponent<SwordEntity>();
                if (entity == null)
                    entity = go.AddComponent<SwordEntity>();

                entity.SwordId = i;
                entity.SetState(Core.SwordState.Dormant);
                _available.Enqueue(entity);
                _all.Add(entity);
            }
        }

        /// <summary>从池中获取一把剑</summary>
        public SwordEntity Get(Vector3 spawnPosition)
        {
            if (_available.Count == 0)
            {
                Debug.LogWarning("[SwordPool] 对象池已空");
                return null;
            }

            var entity = _available.Dequeue();
            entity.transform.position = spawnPosition;
            entity.SetState(Core.SwordState.Summoning);
            _active.Add(entity);
            return entity;
        }

        /// <summary>归还一把剑到池中</summary>
        public void Release(SwordEntity entity)
        {
            entity.SetState(Core.SwordState.Dormant);
            _active.Remove(entity);
            _available.Enqueue(entity);
        }

        /// <summary>归还所有活跃的剑</summary>
        public void ReleaseAll()
        {
            foreach (var entity in _active.ToArray())
            {
                entity.SetState(Core.SwordState.Dormant);
                _available.Enqueue(entity);
            }
            _active.Clear();
        }

        /// <summary>批量获取指定数量的剑</summary>
        public List<SwordEntity> GetMultiple(int count, Vector3 spawnPosition)
        {
            var swords = new List<SwordEntity>();
            for (int i = 0; i < count && _available.Count > 0; i++)
            {
                var sword = Get(spawnPosition);
                if (sword != null)
                    swords.Add(sword);
            }
            return swords;
        }
    }
}
