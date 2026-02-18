using System;
using System.Collections.Generic;
using UnityEngine;

namespace YuJian.Core
{
    /// <summary>
    /// 解耦事件总线 - 发布/订阅模式
    /// 用于各子系统间的松耦合通信
    /// </summary>
    public static class EventBus
    {
        private static readonly Dictionary<Type, List<Delegate>> _handlers = new();

        /// <summary>订阅事件</summary>
        public static void Subscribe<T>(Action<T> handler)
        {
            var type = typeof(T);
            if (!_handlers.ContainsKey(type))
                _handlers[type] = new List<Delegate>();
            _handlers[type].Add(handler);
        }

        /// <summary>取消订阅</summary>
        public static void Unsubscribe<T>(Action<T> handler)
        {
            var type = typeof(T);
            if (_handlers.ContainsKey(type))
                _handlers[type].Remove(handler);
        }

        /// <summary>发布事件</summary>
        public static void Publish<T>(T eventData)
        {
            var type = typeof(T);
            if (!_handlers.ContainsKey(type)) return;
            foreach (var handler in _handlers[type].ToArray())
            {
                (handler as Action<T>)?.Invoke(eventData);
            }
        }

        /// <summary>清除所有订阅</summary>
        public static void Clear()
        {
            _handlers.Clear();
        }
    }

    // ========== 事件定义 ==========

    /// <summary>手势数据帧接收事件</summary>
    public struct GestureFrameEvent
    {
        public GestureFrame Frame;
    }

    /// <summary>阶段切换事件</summary>
    public struct PhaseChangedEvent
    {
        public GamePhase PreviousPhase;
        public GamePhase NewPhase;
    }

    /// <summary>阵法激活事件</summary>
    public struct FormationActivatedEvent
    {
        public string FormationName;
        public int SwordCount;
    }

    /// <summary>剑体状态变化事件</summary>
    public struct SwordStateChangedEvent
    {
        public int SwordId;
        public SwordState NewState;
    }

    /// <summary>手势事件</summary>
    public struct GestureDetectedEvent
    {
        public GestureType Type;
        public string HandId;
        public float Confidence;
        public Dictionary<string, object> Params;
    }

    /// <summary>万剑归宗触发事件</summary>
    public struct WanJianGuiZongEvent
    {
        public Vector3 Center;  // 阵法中心点
    }
}
