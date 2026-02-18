"""消息构建器 - 将手部状态和手势事件序列化为JSON消息"""

import json
import time
from network.protocol import MessageType, PROTOCOL_VERSION


class MessageBuilder:
    """构建发送给Unity的JSON消息

    使用JSON格式以兼容Unity端的MiniJSON解析器。
    WebSocket以text模式发送。
    """

    def __init__(self):
        self._seq = 0  # 消息序列号

    def build_hand_frame(self, hand_states: list,
                         gesture_events: list = None,
                         phase: int = 0) -> str:
        """
        构建完整的手部帧消息

        Args:
            hand_states: HandState对象列表
            gesture_events: 手势事件字典列表
            phase: 当前交互阶段

        Returns:
            JSON字符串
        """
        self._seq += 1

        msg = {
            "v": PROTOCOL_VERSION,
            "seq": self._seq,
            "type": int(MessageType.HAND_FRAME),
            "t": time.monotonic(),
            "hands": [hs.to_dict() for hs in hand_states],
            "gestures": gesture_events or [],
            "phase": phase,
        }

        return json.dumps(msg, ensure_ascii=False, separators=(',', ':'))

    def build_gesture_event(self, gesture_type: int, hand_id: str,
                            confidence: float,
                            params: dict = None) -> dict:
        """
        构建单个手势事件字典（嵌入到hand_frame中）

        Args:
            gesture_type: GestureType枚举值
            hand_id: "Left" or "Right"
            confidence: 置信度 0~1
            params: 手势参数（如力度、方向等）

        Returns:
            手势事件字典
        """
        return {
            "type": int(gesture_type),
            "hand": hand_id,
            "confidence": round(confidence, 3),
            "params": params or {},
        }

    def build_heartbeat(self) -> str:
        """构建心跳消息"""
        msg = {
            "v": PROTOCOL_VERSION,
            "type": int(MessageType.HEARTBEAT),
            "t": time.monotonic(),
        }
        return json.dumps(msg, ensure_ascii=False, separators=(',', ':'))
