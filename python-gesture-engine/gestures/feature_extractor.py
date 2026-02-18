"""特征提取器 - 从手部关键点提取手势识别所需的特征"""

import numpy as np
from tracking.hand_state import HandState


class FeatureExtractor:
    """从HandState提取高级特征用于手势分类"""

    def extract(self, hand_state: HandState) -> dict:
        """
        提取完整特征集

        Returns:
            特征字典，包含距离、角度、速度等
        """
        lm = hand_state.landmarks
        features = {}

        # 1. 指尖到掌心距离 (归一化)
        for i, name in enumerate(["thumb", "index", "middle", "ring", "pinky"]):
            features[f"{name}_palm_dist"] = hand_state.fingertip_to_palm_distance(i)

        # 2. 手指间距离
        features["thumb_index_dist"] = float(np.linalg.norm(lm[4] - lm[8]))
        features["index_middle_dist"] = float(np.linalg.norm(lm[8] - lm[12]))

        # 3. 掌心速度分量
        features["palm_vel_x"] = float(hand_state.palm_velocity[0])
        features["palm_vel_y"] = float(hand_state.palm_velocity[1])
        features["palm_vel_z"] = float(hand_state.palm_velocity[2])
        features["palm_speed"] = hand_state.palm_speed

        # 4. 掌心法向量分量
        features["palm_normal_x"] = float(hand_state.palm_normal[0])
        features["palm_normal_y"] = float(hand_state.palm_normal[1])
        features["palm_normal_z"] = float(hand_state.palm_normal[2])

        # 5. 手指伸展状态
        features["finger_states"] = hand_state.finger_states
        features["extended_count"] = hand_state.extended_finger_count

        # 6. 手掌张开度 (食指MCP到小指MCP距离)
        features["palm_spread"] = float(np.linalg.norm(lm[5] - lm[17]))

        # 7. 手掌面积估算 (用掌面四边形面积)
        v1 = lm[5] - lm[0]   # WRIST -> INDEX_MCP
        v2 = lm[17] - lm[0]  # WRIST -> PINKY_MCP
        features["palm_area"] = float(np.linalg.norm(np.cross(v1, v2)))

        # 8. 手腕角度 (手腕到中指MCP方向与y轴的夹角)
        wrist_to_middle = lm[9] - lm[0]
        norm = np.linalg.norm(wrist_to_middle)
        if norm > 1e-6:
            wrist_to_middle /= norm
        features["wrist_angle_y"] = float(np.arccos(
            np.clip(abs(wrist_to_middle[1]), 0, 1)
        ))

        return features

    def compute_angular_velocity(self, prev_normal: np.ndarray,
                                  curr_normal: np.ndarray,
                                  dt: float) -> float:
        """计算掌心法向量的角速度 (rad/s)"""
        if dt <= 1e-6:
            return 0.0
        dot = np.clip(np.dot(prev_normal, curr_normal), -1.0, 1.0)
        angle = np.arccos(dot)
        return angle / dt
