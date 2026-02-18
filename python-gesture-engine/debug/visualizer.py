"""调试可视化 - OpenCV叠加层显示关键点、手势标签和FPS"""

import cv2
import numpy as np
from tracking.hand_state import HandState
from network.protocol import GestureType


# 手势类型中文名映射
GESTURE_NAMES = {
    GestureType.NONE: "",
    GestureType.GRAB: "抓取",
    GestureType.PUSH: "推",
    GestureType.PULL: "拉",
    GestureType.ROTATE: "旋转",
    GestureType.PINCH: "捏合",
    GestureType.LAUNCH: "发射",
    GestureType.OPEN_ARRAY: "启阵",
    GestureType.CLOSE_ARRAY: "破阵",
    GestureType.FORM_CIRCLE: "布阵",
    GestureType.POINT_PLACE: "定位",
}

# 关键点连接关系 (用于绘制骨架)
HAND_CONNECTIONS = [
    (0, 1), (1, 2), (2, 3), (3, 4),       # 拇指
    (0, 5), (5, 6), (6, 7), (7, 8),       # 食指
    (0, 9), (9, 10), (10, 11), (11, 12),  # 中指
    (0, 13), (13, 14), (14, 15), (15, 16),# 无名指
    (0, 17), (17, 18), (18, 19), (19, 20),# 小指
    (5, 9), (9, 13), (13, 17),            # 掌面横连
]

# 颜色定义 (BGR)
COLOR_LEFT = (255, 128, 0)    # 橙色 - 左手
COLOR_RIGHT = (0, 200, 255)   # 青色 - 右手
COLOR_GESTURE = (0, 255, 128) # 绿色 - 手势标签
COLOR_FPS = (255, 255, 255)   # 白色 - FPS


class Visualizer:
    """调试可视化叠加层"""

    def __init__(self, show_landmarks: bool = True,
                 show_gesture_labels: bool = True,
                 show_fps: bool = True):
        self.show_landmarks = show_landmarks
        self.show_gesture_labels = show_gesture_labels
        self.show_fps = show_fps

    def draw(self, frame: np.ndarray, hand_states: list[HandState],
             gesture_events: list[dict] = None,
             fps: float = 0.0) -> np.ndarray:
        """
        在帧上绘制调试信息

        Args:
            frame: BGR格式图像
            hand_states: 手部状态列表
            gesture_events: 手势事件列表
            fps: 当前帧率

        Returns:
            绘制后的帧
        """
        overlay = frame.copy()
        h, w = frame.shape[:2]

        # 绘制手部关键点和骨架
        if self.show_landmarks:
            for hs in hand_states:
                self._draw_hand(overlay, hs, w, h)

        # 绘制手势标签
        if self.show_gesture_labels and gesture_events:
            self._draw_gestures(overlay, gesture_events, hand_states, w, h)

        # 绘制FPS
        if self.show_fps:
            fps_text = f"FPS: {fps:.1f}"
            cv2.putText(overlay, fps_text, (10, 30),
                        cv2.FONT_HERSHEY_SIMPLEX, 0.8, COLOR_FPS, 2)

        # 绘制连接状态
        client_text = f"Hands: {len(hand_states)}"
        cv2.putText(overlay, client_text, (10, 60),
                    cv2.FONT_HERSHEY_SIMPLEX, 0.6, COLOR_FPS, 1)

        return overlay

    def _draw_hand(self, frame: np.ndarray, hs: HandState,
                   w: int, h: int):
        """绘制单只手的关键点和骨架"""
        color = COLOR_LEFT if hs.handedness == "Left" else COLOR_RIGHT

        # 将归一化坐标转为像素坐标
        pts = []
        for i in range(21):
            px = int(hs.landmarks[i, 0] * w)
            py = int(hs.landmarks[i, 1] * h)
            pts.append((px, py))

        # 绘制骨架连线
        for start, end in HAND_CONNECTIONS:
            cv2.line(frame, pts[start], pts[end], color, 2)

        # 绘制关键点
        for i, (px, py) in enumerate(pts):
            radius = 5 if i in HandState.FINGERTIP_INDICES else 3
            cv2.circle(frame, (px, py), radius, color, -1)

        # 绘制掌心位置
        cx = int(hs.palm_center[0] * w)
        cy = int(hs.palm_center[1] * h)
        cv2.circle(frame, (cx, cy), 8, (0, 0, 255), 2)

        # 绘制手标签
        label = f"{hs.handedness} ({hs.extended_finger_count})"
        cv2.putText(frame, label, (pts[0][0] - 20, pts[0][1] - 15),
                    cv2.FONT_HERSHEY_SIMPLEX, 0.5, color, 1)

    def _draw_gestures(self, frame: np.ndarray, gesture_events: list[dict],
                       hand_states: list[HandState], w: int, h: int):
        """绘制手势标签"""
        for event in gesture_events:
            gesture_type = event.get("type", 0)
            hand_id = event.get("hand", "")
            confidence = event.get("confidence", 0)

            name = GESTURE_NAMES.get(gesture_type, f"G{gesture_type}")
            if not name:
                continue

            # 找到对应手的位置
            for hs in hand_states:
                if hs.handedness == hand_id:
                    x = int(hs.palm_center[0] * w)
                    y = int(hs.palm_center[1] * h) - 40
                    text = f"{name} ({confidence:.0%})"
                    cv2.putText(frame, text, (x - 30, y),
                                cv2.FONT_HERSHEY_SIMPLEX, 0.7,
                                COLOR_GESTURE, 2)
                    break
