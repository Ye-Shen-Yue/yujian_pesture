"""原子手势分类器 - 5种基础手势的规则识别"""

from network.protocol import GestureType
from tracking.hand_state import HandState
from gestures.feature_extractor import FeatureExtractor


class AtomicGestureClassifier:
    """基于规则的原子手势分类器

    5种原子手势:
    - Grab (抓取): 五指从张开到握拢
    - Push (推): 掌心朝前+z轴正向速度
    - Pull (拉): 掌心朝后+z轴负向速度
    - Rotate (旋转): 掌心法向量持续旋转
    - Pinch (捏合): 拇指与食指捏合
    """

    def __init__(self, config: dict = None):
        cfg = config or {}
        grab_cfg = cfg.get("grab", {})
        push_cfg = cfg.get("push", {})
        pull_cfg = cfg.get("pull", {})
        rotate_cfg = cfg.get("rotate", {})
        pinch_cfg = cfg.get("pinch", {})

        # Grab阈值
        self.grab_fingertip_dist = grab_cfg.get("fingertip_palm_distance", 0.08)
        self.grab_min_curl = grab_cfg.get("min_curl_fingers", 4)

        # Push阈值
        self.push_min_vel_z = push_cfg.get("min_velocity_z", 0.15)
        self.push_facing = push_cfg.get("palm_facing_threshold", 0.5)

        # Pull阈值
        self.pull_min_vel_z = pull_cfg.get("min_velocity_z", -0.15)
        self.pull_facing = pull_cfg.get("palm_facing_threshold", -0.5)

        # Rotate阈值
        self.rotate_min_angular_vel = rotate_cfg.get("min_angular_velocity", 1.5)
        self.rotate_sustain = rotate_cfg.get("sustain_frames", 5)

        # Pinch阈值
        self.pinch_dist = pinch_cfg.get("thumb_index_distance", 0.04)

        self._feature_extractor = FeatureExtractor()
        self._rotate_frame_count: dict[str, int] = {}
        self._prev_normals: dict[str, list] = {}

    def classify(self, hand_state: HandState,
                 dt: float = 0.033) -> tuple[GestureType, float]:
        """
        分类单只手的原子手势

        Args:
            hand_state: 手部状态
            dt: 帧间隔(秒)

        Returns:
            (手势类型, 置信度) 元组
        """
        features = self._feature_extractor.extract(hand_state)
        hand_id = hand_state.handedness

        # 按优先级检测 (互斥手势)
        results = []

        # 1. 检测Pinch (最精确的手势，优先级最高)
        pinch_conf = self._check_pinch(features)
        if pinch_conf > 0:
            results.append((GestureType.PINCH, pinch_conf))

        # 2. 检测Grab
        grab_conf = self._check_grab(features, hand_state)
        if grab_conf > 0:
            results.append((GestureType.GRAB, grab_conf))

        # 3. 检测Push
        push_conf = self._check_push(features)
        if push_conf > 0:
            results.append((GestureType.PUSH, push_conf))

        # 4. 检测Pull
        pull_conf = self._check_pull(features)
        if pull_conf > 0:
            results.append((GestureType.PULL, pull_conf))

        # 5. 检测Rotate
        rotate_conf = self._check_rotate(features, hand_id, dt)
        if rotate_conf > 0:
            results.append((GestureType.ROTATE, rotate_conf))

        if not results:
            return (GestureType.NONE, 0.0)

        # 返回置信度最高的手势
        results.sort(key=lambda x: x[1], reverse=True)
        return results[0]

    def _check_pinch(self, features: dict) -> float:
        """检测捏合: 拇指尖与食指尖距离小于阈值"""
        dist = features["thumb_index_dist"]
        if dist < self.pinch_dist:
            # 置信度与距离成反比
            return min(1.0, 1.0 - dist / self.pinch_dist)
        return 0.0

    def _check_grab(self, features: dict, hand_state: HandState) -> float:
        """检测抓取: 多数手指弯曲，指尖靠近掌心"""
        curled = 5 - features["extended_count"]
        if curled < self.grab_min_curl:
            return 0.0

        # 计算平均指尖到掌心距离
        avg_dist = sum(
            features[f"{f}_palm_dist"]
            for f in ["index", "middle", "ring", "pinky"]
        ) / 4.0

        if avg_dist < self.grab_fingertip_dist:
            return min(1.0, curled / 5.0 * (1.0 - avg_dist / self.grab_fingertip_dist))
        return 0.0

    def _check_push(self, features: dict) -> float:
        """检测推: 掌心朝前 + z轴正向速度"""
        vel_z = features["palm_vel_z"]
        normal_z = features["palm_normal_z"]

        if vel_z > self.push_min_vel_z and normal_z > self.push_facing:
            speed_factor = min(1.0, vel_z / (self.push_min_vel_z * 3))
            facing_factor = min(1.0, normal_z)
            return speed_factor * 0.6 + facing_factor * 0.4
        return 0.0

    def _check_pull(self, features: dict) -> float:
        """检测拉: 掌心朝后 + z轴负向速度"""
        vel_z = features["palm_vel_z"]
        normal_z = features["palm_normal_z"]

        if vel_z < self.pull_min_vel_z and normal_z < self.pull_facing:
            speed_factor = min(1.0, abs(vel_z) / abs(self.pull_min_vel_z * 3))
            facing_factor = min(1.0, abs(normal_z))
            return speed_factor * 0.6 + facing_factor * 0.4
        return 0.0

    def _check_rotate(self, features: dict, hand_id: str,
                      dt: float) -> float:
        """检测旋转: 掌心法向量持续旋转"""
        import numpy as np

        curr_normal = np.array([
            features["palm_normal_x"],
            features["palm_normal_y"],
            features["palm_normal_z"],
        ])

        if hand_id not in self._prev_normals:
            self._prev_normals[hand_id] = curr_normal
            self._rotate_frame_count[hand_id] = 0
            return 0.0

        prev_normal = self._prev_normals[hand_id]
        angular_vel = self._feature_extractor.compute_angular_velocity(
            prev_normal, curr_normal, dt
        )
        self._prev_normals[hand_id] = curr_normal

        if angular_vel > self.rotate_min_angular_vel:
            self._rotate_frame_count[hand_id] = min(
                self._rotate_frame_count.get(hand_id, 0) + 1,
                self.rotate_sustain * 2,
            )
        else:
            self._rotate_frame_count[hand_id] = max(
                self._rotate_frame_count.get(hand_id, 0) - 1, 0
            )

        count = self._rotate_frame_count[hand_id]
        if count >= self.rotate_sustain:
            return min(1.0, count / (self.rotate_sustain * 1.5))
        return 0.0
