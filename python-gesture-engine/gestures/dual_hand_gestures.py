"""双手协同手势检测 - 启阵/破阵/布阵等需要双手配合的手势"""

import time
import math
import numpy as np
from collections import deque
from network.protocol import GestureType
from tracking.hand_state import HandState
from gestures.gesture_types import GestureEvent


class DualHandGestureDetector:
    """双手协同手势检测器

    双手手势:
    - OPEN_ARRAY (启阵): 双手合十 → 缓缓拉开
    - CLOSE_ARRAY (破阵): 双掌快速合拢
    - FORM_CIRCLE (布阵画圆): 左手画圆轨迹
    - POINT_PLACE (布阵定位): 右手食指点击
    - WAN_JIAN (万剑归宗): 双手握拳 → 同时向前推
    """

    def __init__(self, config: dict = None):
        cfg = config or {}
        dual_cfg = cfg.get("dual_hand", {})

        # 双掌靠近距离阈值
        self.palms_together_dist = dual_cfg.get("palms_together_distance", 0.15)
        # 拉开距离阈值
        self.open_dist = dual_cfg.get("open_distance", 0.18)
        # 合拢速度阈值(每帧距离变化)
        self.close_velocity = dual_cfg.get("close_velocity", 0.15)
        # 合拢触发距离
        self.close_distance = dual_cfg.get("close_distance", 0.20)
        # 画圆参数
        self.circle_min_radius = dual_cfg.get("circle_min_radius", 0.05)
        self.circle_min_points = dual_cfg.get("circle_min_points", 8)
        # 万剑归宗参数
        wan_jian_cfg = dual_cfg.get("wan_jian", {})
        self.wan_jian_push_vel = wan_jian_cfg.get("push_velocity", 0.06)
        self.wan_jian_fist_time = wan_jian_cfg.get("fist_hold_time", 0.3)

        # 状态追踪
        self._palms_were_together = False
        self._together_time = 0.0
        self._circle_points: deque = deque(maxlen=60)  # 左手轨迹点
        self._prev_palm_distance = 0.0
        # 万剑归宗状态
        self._both_fists_since = 0.0
        self._both_fists_active = False

    def detect(self, hand_states: list[HandState]) -> list[GestureEvent]:
        """
        检测双手协同手势

        Args:
            hand_states: 当前帧的手部状态列表

        Returns:
            检测到的双手手势事件列表
        """
        events = []
        now = time.monotonic()

        # 需要两只手
        left = None
        right = None
        for hs in hand_states:
            if hs.handedness == "Left":
                left = hs
            elif hs.handedness == "Right":
                right = hs

        if left and right:
            # 双手距离
            palm_dist = float(np.linalg.norm(
                left.palm_center - right.palm_center
            ))

            # 检测启阵 (合十→拉开)
            open_event = self._check_open_array(left, right, palm_dist, now)
            if open_event:
                events.append(open_event)

            # 检测破阵 (快速合拢)
            close_event = self._check_close_array(left, right, palm_dist, now)
            if close_event:
                events.append(close_event)

            # 检测万剑归宗 (双手握拳→前推)
            wan_jian_event = self._check_wan_jian(left, right, now)
            if wan_jian_event:
                events.append(wan_jian_event)

            self._prev_palm_distance = palm_dist

        # 单手手势（左手画圆、右手点指）
        if left:
            circle_event = self._check_form_circle(left, now)
            if circle_event:
                events.append(circle_event)

        if right:
            point_event = self._check_point_place(right, now)
            if point_event:
                events.append(point_event)

        return events

    def _check_open_array(self, left: HandState, right: HandState,
                          palm_dist: float, now: float):
        """检测启阵: 双手靠近后拉开

        放宽条件：不要求严格合十，双手靠近即可
        """
        # 检测双手靠近状态
        if palm_dist < self.palms_together_dist:
            if not self._palms_were_together:
                self._palms_were_together = True
                self._together_time = now
            return None

        # 靠近后拉开
        if self._palms_were_together and palm_dist > self.open_dist:
            self._palms_were_together = False
            duration = now - self._together_time
            if 0.15 < duration < 5.0:  # 放宽时间窗口
                return GestureEvent(
                    gesture_type=GestureType.OPEN_ARRAY,
                    hand_id="Both",
                    confidence=min(1.0, palm_dist / self.open_dist),
                    params={
                        "spread_distance": palm_dist,
                        "duration": duration,
                    },
                    timestamp=now,
                )

        # 超时重置
        if self._palms_were_together and (now - self._together_time) > 5.0:
            self._palms_were_together = False

        return None

    def _check_close_array(self, left: HandState, right: HandState,
                           palm_dist: float, now: float):
        """检测破阵: 双掌快速合拢

        使用距离差值检测合拢趋势，阈值已从config读取
        """
        if self._prev_palm_distance <= 0:
            return None

        # 计算合拢速度 (距离减小速率，每帧)
        closing_speed = self._prev_palm_distance - palm_dist
        if closing_speed <= 0:
            return None

        close_dist = getattr(self, 'close_distance', self.palms_together_dist * 2)

        # 需要从较远距离快速合拢到较近距离
        if (palm_dist < close_dist and
                closing_speed > self.close_velocity and
                self._prev_palm_distance > self.open_dist * 0.6):
            return GestureEvent(
                gesture_type=GestureType.CLOSE_ARRAY,
                hand_id="Both",
                confidence=min(1.0, closing_speed / self.close_velocity),
                params={"closing_speed": closing_speed},
                timestamp=now,
            )
        return None

    def _check_form_circle(self, left: HandState, now: float):
        """检测布阵画圆: 左手掌心轨迹形成圆形"""
        # 记录左手掌心轨迹
        self._circle_points.append({
            "pos": left.palm_center[:2].copy(),  # 只用x,y
            "time": now,
        })

        # 清理超时点 (3秒窗口，放宽)
        while self._circle_points and (now - self._circle_points[0]["time"]) > 3.0:
            self._circle_points.popleft()

        if len(self._circle_points) < self.circle_min_points:
            return None

        # 检测圆形轨迹
        points = np.array([p["pos"] for p in self._circle_points])
        center = np.mean(points, axis=0)
        radii = np.linalg.norm(points - center, axis=1)
        mean_radius = np.mean(radii)
        radius_std = np.std(radii)

        # 圆形判定: 半径标准差小于平均半径的40%(放宽)
        if (mean_radius > self.circle_min_radius and
                radius_std < mean_radius * 0.4):
            # 检查是否覆盖了足够的角度范围
            angles = np.arctan2(points[:, 1] - center[1],
                                points[:, 0] - center[0])
            angle_range = np.ptp(angles)
            if angle_range > math.pi * 1.2:  # 降低到216度(原270度)
                self._circle_points.clear()
                return GestureEvent(
                    gesture_type=GestureType.FORM_CIRCLE,
                    hand_id="Left",
                    confidence=min(1.0, 1.0 - radius_std / mean_radius),
                    params={
                        "center": center.tolist(),
                        "radius": float(mean_radius),
                    },
                    timestamp=now,
                )
        return None

    def _check_point_place(self, right: HandState, now: float):
        """检测布阵定位: 右手食指伸出并点击

        放宽条件：允许拇指+食指伸出，或仅食指伸出
        """
        # 食指必须伸出，中指/无名指/小指弯曲
        index_extended = right.finger_states[1] if len(right.finger_states) > 1 else False
        other_curled = not any(right.finger_states[2:5]) if len(right.finger_states) >= 5 else False

        if index_extended and other_curled:
            # 检测点击动作 (食指快速向前) - 降低速度阈值
            if right.palm_velocity[2] > 0.05:
                return GestureEvent(
                    gesture_type=GestureType.POINT_PLACE,
                    hand_id="Right",
                    confidence=0.8,
                    params={
                        "position": right.landmarks[8].tolist(),
                    },
                    timestamp=now,
                )
        return None

    def _check_wan_jian(self, left: HandState, right: HandState,
                        now: float):
        """检测万剑归宗: 双手握拳后同时向前推

        两阶段检测:
        1. 双手都握拳(0根手指伸出)，保持至少0.3秒
        2. 双手同时有正向z速度(向前推)
        """
        both_fists = left.is_fist and right.is_fist

        if both_fists:
            if not self._both_fists_active:
                self._both_fists_active = True
                self._both_fists_since = now
        else:
            self._both_fists_active = False
            return None

        # 握拳时间不够
        fist_duration = now - self._both_fists_since
        if fist_duration < self.wan_jian_fist_time:
            return None

        # 检测双手同时前推
        left_push = float(left.palm_velocity[2])
        right_push = float(right.palm_velocity[2])

        if (left_push > self.wan_jian_push_vel and
                right_push > self.wan_jian_push_vel):
            avg_vel = (left_push + right_push) / 2.0
            self._both_fists_active = False  # 触发后重置
            return GestureEvent(
                gesture_type=GestureType.WAN_JIAN,
                hand_id="Both",
                confidence=min(1.0, avg_vel / (self.wan_jian_push_vel * 3)),
                params={
                    "fist_duration": fist_duration,
                    "push_velocity": avg_vel,
                },
                timestamp=now,
            )

        return None

    def reset(self):
        """重置状态"""
        self._palms_were_together = False
        self._together_time = 0.0
        self._circle_points.clear()
        self._prev_palm_distance = 0.0
        self._both_fists_active = False
        self._both_fists_since = 0.0
