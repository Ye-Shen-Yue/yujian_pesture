"""手部追踪器 - 封装MediaPipe HandLandmarker (Tasks API)，输出HandState"""

import os
import mediapipe as mp
import numpy as np
from tracking.landmark_smoother import LandmarkSmoother
from tracking.hand_state import HandState


class HandTracker:
    """MediaPipe手部追踪封装 (Tasks API)

    处理流程: RGB帧 → MediaPipe检测 → 关键点平滑 → HandState构建
    """

    PALM_INDICES = [0, 5, 9, 13, 17]  # WRIST + 4个MCP
    FINGERTIP_INDICES = [4, 8, 12, 16, 20]

    def __init__(self, max_hands: int = 2,
                 detection_confidence: float = 0.7,
                 tracking_confidence: float = 0.5,
                 smoother_min_cutoff: float = 1.0,
                 smoother_beta: float = 0.007,
                 model_path: str = None):
        # 定位模型文件
        if model_path is None:
            model_path = os.path.join(
                os.path.dirname(os.path.dirname(os.path.abspath(__file__))),
                "hand_landmarker.task"
            )

        if not os.path.exists(model_path):
            raise FileNotFoundError(
                f"手部追踪模型未找到: {model_path}\n"
                "请下载: https://storage.googleapis.com/mediapipe-models/"
                "hand_landmarker/hand_landmarker/float16/latest/hand_landmarker.task"
            )

        # 使用 Tasks API 创建 HandLandmarker
        options = mp.tasks.vision.HandLandmarkerOptions(
            base_options=mp.tasks.BaseOptions(model_asset_path=model_path),
            running_mode=mp.tasks.vision.RunningMode.VIDEO,
            num_hands=max_hands,
            min_hand_detection_confidence=detection_confidence,
            min_hand_presence_confidence=detection_confidence,
            min_tracking_confidence=tracking_confidence,
        )
        self._landmarker = mp.tasks.vision.HandLandmarker.create_from_options(options)
        self._frame_count = 0

        self.smoother_min_cutoff = smoother_min_cutoff
        self.smoother_beta = smoother_beta

        # 每只手独立的平滑器和历史状态
        self._smoothers: dict[str, LandmarkSmoother] = {}
        self._prev_palm_centers: dict[str, np.ndarray] = {}
        self._prev_timestamps: dict[str, float] = {}

    def process(self, frame_rgb: np.ndarray,
                timestamp: float) -> list[HandState]:
        """
        处理一帧RGB图像，返回检测到的手部状态列表

        Args:
            frame_rgb: RGB格式图像 (H, W, 3)
            timestamp: 当前时间戳(秒, monotonic)

        Returns:
            HandState列表，最多max_hands个
        """
        # 转换为MediaPipe Image
        mp_image = mp.Image(image_format=mp.ImageFormat.SRGB, data=frame_rgb)

        # 使用VIDEO模式需要递增的毫秒时间戳
        self._frame_count += 1
        timestamp_ms = int(self._frame_count * (1000 / 30))  # 假设30fps

        results = self._landmarker.detect_for_video(mp_image, timestamp_ms)
        hand_states = []

        if not results.hand_landmarks:
            return hand_states

        for i, hand_landmarks in enumerate(results.hand_landmarks):
            # 获取左右手标识
            if results.handedness and i < len(results.handedness):
                hand_label = results.handedness[i][0].category_name  # "Left"/"Right"
                confidence = results.handedness[i][0].score
            else:
                hand_label = "Right"
                confidence = 0.5

            # 提取21x3原始关键点 (归一化坐标)
            raw = np.array([
                [lm.x, lm.y, lm.z]
                for lm in hand_landmarks
            ])

            # 平滑处理
            if hand_label not in self._smoothers:
                self._smoothers[hand_label] = LandmarkSmoother(
                    num_points=21,
                    min_cutoff=self.smoother_min_cutoff,
                    beta=self.smoother_beta,
                )
            smoothed = self._smoothers[hand_label].smooth(raw, timestamp)

            # 构建HandState
            state = self._build_hand_state(
                smoothed, hand_label, confidence, timestamp
            )
            hand_states.append(state)

        return hand_states

    def _build_hand_state(self, landmarks: np.ndarray, handedness: str,
                          confidence: float, timestamp: float) -> HandState:
        """从平滑后的关键点构建完整HandState"""

        # 掌心中心
        palm_center = np.mean(landmarks[self.PALM_INDICES], axis=0)

        # 掌心速度
        velocity = np.zeros(3)
        if handedness in self._prev_palm_centers:
            dt = timestamp - self._prev_timestamps.get(handedness, timestamp)
            if dt > 1e-6:
                velocity = (palm_center - self._prev_palm_centers[handedness]) / dt
        self._prev_palm_centers[handedness] = palm_center.copy()
        self._prev_timestamps[handedness] = timestamp

        # 掌心法向量 (WRIST→INDEX_MCP × WRIST→PINKY_MCP)
        v1 = landmarks[5] - landmarks[0]
        v2 = landmarks[17] - landmarks[0]
        normal = np.cross(v1, v2)
        norm = np.linalg.norm(normal)
        if norm > 1e-6:
            normal = normal / norm
        else:
            normal = np.array([0.0, 0.0, 1.0])

        # 五指伸展检测
        finger_states = self._detect_finger_states(landmarks)

        return HandState(
            landmarks=landmarks,
            handedness=handedness,
            confidence=confidence,
            palm_center=palm_center,
            palm_velocity=velocity,
            palm_normal=normal,
            finger_states=finger_states,
            timestamp=timestamp,
        )

    def _detect_finger_states(self, landmarks: np.ndarray) -> list[bool]:
        """检测五指伸展状态

        逻辑: 指尖到MCP距离 > PIP到MCP距离 * 1.1 则判定为伸展

        Returns:
            [拇指, 食指, 中指, 无名指, 小指] 的伸展状态
        """
        states = []

        # 拇指: TIP到CMC距离 vs IP到CMC距离
        thumb_tip_dist = np.linalg.norm(landmarks[4] - landmarks[2])
        thumb_ip_dist = np.linalg.norm(landmarks[3] - landmarks[2])
        states.append(bool(thumb_tip_dist > thumb_ip_dist * 0.8))

        # 食指~小指: TIP到MCP距离 vs PIP到MCP距离
        finger_joints = [
            (8, 6, 5),    # 食指: TIP, PIP, MCP
            (12, 10, 9),  # 中指
            (16, 14, 13), # 无名指
            (20, 18, 17), # 小指
        ]
        for tip, pip, mcp in finger_joints:
            tip_dist = np.linalg.norm(landmarks[tip] - landmarks[mcp])
            pip_dist = np.linalg.norm(landmarks[pip] - landmarks[mcp])
            states.append(bool(tip_dist > pip_dist * 1.1))

        return states

    def release(self):
        """释放MediaPipe资源"""
        self._landmarker.close()
