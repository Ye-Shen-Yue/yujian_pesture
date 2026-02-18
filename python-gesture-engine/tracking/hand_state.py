"""手部状态数据类 - 封装单只手的所有追踪数据"""

from dataclasses import dataclass, field
import numpy as np


@dataclass
class HandState:
    """单只手的完整状态"""

    # 21个3D关键点 (归一化坐标 0~1)
    landmarks: np.ndarray  # shape: (21, 3)

    # 左手/右手标识
    handedness: str  # "Left" or "Right"

    # 检测置信度
    confidence: float

    # 掌心中心位置 (由WRIST + 4个MCP关键点平均)
    palm_center: np.ndarray  # shape: (3,)

    # 掌心速度 (单位/秒)
    palm_velocity: np.ndarray  # shape: (3,)

    # 掌心法向量 (由掌面两向量叉积得出)
    palm_normal: np.ndarray  # shape: (3,)

    # 五指伸展状态 [拇指, 食指, 中指, 无名指, 小指]
    finger_states: list  # [bool, bool, bool, bool, bool]

    # 时间戳
    timestamp: float = 0.0

    # MediaPipe关键点索引常量
    WRIST = 0
    THUMB_CMC, THUMB_MCP, THUMB_IP, THUMB_TIP = 1, 2, 3, 4
    INDEX_MCP, INDEX_PIP, INDEX_DIP, INDEX_TIP = 5, 6, 7, 8
    MIDDLE_MCP, MIDDLE_PIP, MIDDLE_DIP, MIDDLE_TIP = 9, 10, 11, 12
    RING_MCP, RING_PIP, RING_DIP, RING_TIP = 13, 14, 15, 16
    PINKY_MCP, PINKY_PIP, PINKY_DIP, PINKY_TIP = 17, 18, 19, 20

    PALM_INDICES = [WRIST, INDEX_MCP, MIDDLE_MCP, RING_MCP, PINKY_MCP]
    FINGERTIP_INDICES = [THUMB_TIP, INDEX_TIP, MIDDLE_TIP, RING_TIP, PINKY_TIP]

    @property
    def palm_speed(self) -> float:
        """掌心速度标量"""
        return float(np.linalg.norm(self.palm_velocity))

    @property
    def extended_finger_count(self) -> int:
        """伸展手指数量"""
        return sum(self.finger_states)

    @property
    def is_fist(self) -> bool:
        """是否握拳（所有手指弯曲）"""
        return self.extended_finger_count == 0

    @property
    def is_open_palm(self) -> bool:
        """是否张开手掌（所有手指伸展）"""
        return self.extended_finger_count == 5

    def fingertip_to_palm_distance(self, finger_index: int) -> float:
        """指定指尖到掌心的距离"""
        tip_idx = self.FINGERTIP_INDICES[finger_index]
        return float(np.linalg.norm(
            self.landmarks[tip_idx] - self.palm_center
        ))

    def to_dict(self) -> dict:
        """序列化为字典（用于网络传输）"""
        return {
            "id": self.handedness,
            "landmarks": self.landmarks.tolist(),
            "palm_pos": self.palm_center.tolist(),
            "palm_vel": self.palm_velocity.tolist(),
            "palm_normal": self.palm_normal.tolist(),
            "fingers": [int(f) for f in self.finger_states],
            "confidence": round(self.confidence, 3),
        }
