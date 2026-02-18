"""帧预处理 - 色彩转换、翻转、ROI裁剪"""

import cv2
import numpy as np


class FramePreprocessor:
    """对摄像头原始帧进行预处理，输出MediaPipe所需的RGB帧"""

    def __init__(self, flip_horizontal: bool = True):
        self.flip_horizontal = flip_horizontal

    def process(self, frame: np.ndarray) -> np.ndarray:
        """
        预处理流程：
        1. 水平翻转（镜像模式，手势方向更直觉）
        2. BGR -> RGB（MediaPipe要求RGB输入）
        """
        if self.flip_horizontal:
            frame = cv2.flip(frame, 1)
        rgb_frame = cv2.cvtColor(frame, cv2.COLOR_BGR2RGB)
        return rgb_frame

    def process_for_display(self, frame: np.ndarray) -> np.ndarray:
        """仅翻转，保持BGR用于OpenCV显示"""
        if self.flip_horizontal:
            frame = cv2.flip(frame, 1)
        return frame
