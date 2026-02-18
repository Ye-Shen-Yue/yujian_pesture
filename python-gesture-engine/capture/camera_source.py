"""摄像头采集源 - 封装OpenCV VideoCapture"""

import cv2
import time


class CameraSource:
    """摄像头采集，支持配置分辨率、帧率和设备索引"""

    def __init__(self, device_index: int = 0, width: int = 640,
                 height: int = 480, fps: int = 30):
        self.device_index = device_index
        self.width = width
        self.height = height
        self.fps = fps
        self.cap = None
        self._last_frame_time = 0.0

    def open(self) -> bool:
        """打开摄像头，返回是否成功"""
        self.cap = cv2.VideoCapture(self.device_index)
        if not self.cap.isOpened():
            return False
        self.cap.set(cv2.CAP_PROP_FRAME_WIDTH, self.width)
        self.cap.set(cv2.CAP_PROP_FRAME_HEIGHT, self.height)
        self.cap.set(cv2.CAP_PROP_FPS, self.fps)
        return True

    def read(self):
        """读取一帧，返回 (success, frame, timestamp)"""
        if self.cap is None or not self.cap.isOpened():
            return False, None, 0.0
        success, frame = self.cap.read()
        timestamp = time.monotonic()
        self._last_frame_time = timestamp
        return success, frame, timestamp

    @property
    def actual_resolution(self) -> tuple:
        """返回实际分辨率 (width, height)"""
        if self.cap is None:
            return (0, 0)
        return (int(self.cap.get(cv2.CAP_PROP_FRAME_WIDTH)),
                int(self.cap.get(cv2.CAP_PROP_FRAME_HEIGHT)))

    def release(self):
        """释放摄像头资源"""
        if self.cap is not None:
            self.cap.release()
            self.cap = None

    def __enter__(self):
        self.open()
        return self

    def __exit__(self, exc_type, exc_val, exc_tb):
        self.release()
        return False
