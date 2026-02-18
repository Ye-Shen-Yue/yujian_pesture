"""One-Euro滤波器 - 对手部关键点进行平滑去抖"""

import numpy as np
import math


class OneEuroFilter:
    """单标量One-Euro滤波器

    参考论文: 1€ Filter (Casiez et al., 2012)
    特点: 低速时强平滑(去抖), 高速时弱平滑(保响应)
    """

    def __init__(self, min_cutoff: float = 1.0, beta: float = 0.007,
                 d_cutoff: float = 1.0):
        self.min_cutoff = min_cutoff
        self.beta = beta
        self.d_cutoff = d_cutoff
        self.x_prev = None
        self.dx_prev = 0.0
        self.t_prev = None

    def _smoothing_factor(self, cutoff: float, dt: float) -> float:
        tau = 1.0 / (2.0 * math.pi * cutoff)
        return 1.0 / (1.0 + tau / max(dt, 1e-6))

    def filter(self, x: float, t: float) -> float:
        if self.t_prev is None:
            self.x_prev = x
            self.t_prev = t
            return x

        dt = max(t - self.t_prev, 1e-6)

        # 估计导数
        dx = (x - self.x_prev) / dt
        a_d = self._smoothing_factor(self.d_cutoff, dt)
        dx_hat = a_d * dx + (1.0 - a_d) * self.dx_prev

        # 自适应截止频率
        cutoff = self.min_cutoff + self.beta * abs(dx_hat)
        a = self._smoothing_factor(cutoff, dt)

        # 滤波
        x_hat = a * x + (1.0 - a) * self.x_prev

        self.x_prev = x_hat
        self.dx_prev = dx_hat
        self.t_prev = t

        return x_hat

    def reset(self):
        self.x_prev = None
        self.dx_prev = 0.0
        self.t_prev = None


class LandmarkSmoother:
    """对21个3D关键点应用One-Euro滤波"""

    def __init__(self, num_points: int = 21, min_cutoff: float = 1.0,
                 beta: float = 0.007, d_cutoff: float = 1.0):
        self.num_points = num_points
        # 为每个坐标分量创建独立滤波器: 21点 x 3维 = 63个滤波器
        self.filters = [
            [OneEuroFilter(min_cutoff, beta, d_cutoff) for _ in range(3)]
            for _ in range(num_points)
        ]

    def smooth(self, landmarks: np.ndarray, timestamp: float) -> np.ndarray:
        """
        对关键点数组进行平滑

        Args:
            landmarks: shape (21, 3) 的关键点数组
            timestamp: 当前时间戳(秒)

        Returns:
            平滑后的关键点数组 shape (21, 3)
        """
        smoothed = np.zeros_like(landmarks)
        for i in range(self.num_points):
            for j in range(3):
                smoothed[i, j] = self.filters[i][j].filter(
                    landmarks[i, j], timestamp
                )
        return smoothed

    def reset(self):
        """重置所有滤波器状态"""
        for point_filters in self.filters:
            for f in point_filters:
                f.reset()
