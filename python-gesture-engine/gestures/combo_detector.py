"""组合手势检测器 - 检测时序组合手势（如抓取→推=发射）"""

import time
from collections import deque
from network.protocol import GestureType
from gestures.gesture_types import GestureEvent


class ComboDetector:
    """时序组合手势检测

    监听原子手势事件序列，匹配预定义的组合模式。

    组合手势定义:
    - LAUNCH (发射): GRAB → PUSH (同一只手)
    """

    def __init__(self, max_interval: float = 0.8):
        self.max_interval = max_interval  # 组合手势最大间隔(秒)

        # 每只手的最近手势历史
        self._history: dict[str, deque] = {}

        # 组合手势模式定义: (手势序列, 结果手势类型)
        self._patterns = [
            ([GestureType.GRAB, GestureType.PUSH], GestureType.LAUNCH),
        ]

    def process(self, events: list[GestureEvent]) -> list[GestureEvent]:
        """
        处理原子手势事件，检测组合手势

        Args:
            events: 本帧的原子手势事件

        Returns:
            检测到的组合手势事件列表
        """
        combo_events = []
        now = time.monotonic()

        for event in events:
            hand_id = event.hand_id

            if hand_id not in self._history:
                self._history[hand_id] = deque(maxlen=5)

            self._history[hand_id].append(event)

            # 清理过期事件
            self._cleanup(hand_id, now)

            # 匹配组合模式
            for pattern, combo_type in self._patterns:
                if self._match_pattern(hand_id, pattern):
                    combo_events.append(GestureEvent(
                        gesture_type=combo_type,
                        hand_id=hand_id,
                        confidence=min(
                            e.confidence
                            for e in list(self._history[hand_id])[-len(pattern):]
                        ),
                        params={"combo_from": [int(g) for g in pattern]},
                        timestamp=now,
                    ))
                    # 消耗已匹配的事件
                    self._history[hand_id].clear()
                    break

        return combo_events

    def _cleanup(self, hand_id: str, now: float):
        """清理超时的历史事件"""
        history = self._history.get(hand_id)
        if not history:
            return
        while history and (now - history[0].timestamp) > self.max_interval:
            history.popleft()

    def _match_pattern(self, hand_id: str,
                       pattern: list[GestureType]) -> bool:
        """检查历史事件是否匹配指定模式"""
        history = self._history.get(hand_id)
        if not history or len(history) < len(pattern):
            return False

        recent = list(history)[-len(pattern):]

        # 检查手势类型序列匹配
        for event, expected_type in zip(recent, pattern):
            if event.gesture_type != expected_type:
                return False

        # 检查时间间隔
        if len(recent) >= 2:
            time_span = recent[-1].timestamp - recent[0].timestamp
            if time_span > self.max_interval:
                return False

        return True

    def reset(self):
        """重置历史"""
        self._history.clear()
