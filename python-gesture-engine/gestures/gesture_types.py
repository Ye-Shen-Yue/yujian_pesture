"""手势类型定义 - 枚举和数据类"""

from dataclasses import dataclass, field
from network.protocol import GestureType


@dataclass
class GestureEvent:
    """手势事件"""
    gesture_type: GestureType
    hand_id: str           # "Left" or "Right"
    confidence: float      # 0~1
    params: dict = field(default_factory=dict)
    timestamp: float = 0.0

    def to_dict(self) -> dict:
        return {
            "type": int(self.gesture_type),
            "hand": self.hand_id,
            "confidence": round(self.confidence, 3),
            "params": self.params,
        }


@dataclass
class GestureState:
    """单只手的手势状态"""
    current_gesture: GestureType = GestureType.NONE
    confidence: float = 0.0
    start_time: float = 0.0
    duration: float = 0.0
    cooldown_until: float = 0.0  # 冷却结束时间

    @property
    def is_active(self) -> bool:
        return self.current_gesture != GestureType.NONE

    @property
    def is_cooling_down(self) -> bool:
        import time
        return time.monotonic() < self.cooldown_until
