"""结构化日志 - 手势事件和性能指标记录"""

import logging
import time


def setup_logger(name: str = "yujian", level: str = "INFO",
                 log_file: str = None) -> logging.Logger:
    """配置结构化日志

    Args:
        name: 日志器名称
        level: 日志级别 (DEBUG/INFO/WARNING/ERROR)
        log_file: 日志文件路径，None则仅控制台输出
    """
    logger = logging.getLogger(name)
    logger.setLevel(getattr(logging, level.upper(), logging.INFO))

    formatter = logging.Formatter(
        "[%(asctime)s] %(levelname)s %(name)s: %(message)s",
        datefmt="%H:%M:%S",
    )

    # 控制台输出
    console_handler = logging.StreamHandler()
    console_handler.setFormatter(formatter)
    logger.addHandler(console_handler)

    # 文件输出
    if log_file:
        file_handler = logging.FileHandler(log_file, encoding="utf-8")
        file_handler.setFormatter(formatter)
        logger.addHandler(file_handler)

    return logger


class LatencyTracker:
    """延迟追踪器 - 统计管线各阶段耗时"""

    def __init__(self, window_size: int = 60):
        self.window_size = window_size
        self._samples: dict[str, list[float]] = {}

    def record(self, stage: str, duration_ms: float):
        """记录一次耗时"""
        if stage not in self._samples:
            self._samples[stage] = []
        samples = self._samples[stage]
        samples.append(duration_ms)
        if len(samples) > self.window_size:
            samples.pop(0)

    def get_stats(self, stage: str) -> dict:
        """获取指定阶段的统计信息"""
        samples = self._samples.get(stage, [])
        if not samples:
            return {"avg": 0, "min": 0, "max": 0, "count": 0}
        return {
            "avg": sum(samples) / len(samples),
            "min": min(samples),
            "max": max(samples),
            "count": len(samples),
        }

    def get_all_stats(self) -> dict:
        """获取所有阶段的统计信息"""
        return {stage: self.get_stats(stage) for stage in self._samples}


class FPSCounter:
    """帧率计数器"""

    def __init__(self, window_size: int = 30):
        self.window_size = window_size
        self._timestamps: list[float] = []

    def tick(self):
        """记录一帧"""
        now = time.monotonic()
        self._timestamps.append(now)
        if len(self._timestamps) > self.window_size:
            self._timestamps.pop(0)

    @property
    def fps(self) -> float:
        """当前帧率"""
        if len(self._timestamps) < 2:
            return 0.0
        duration = self._timestamps[-1] - self._timestamps[0]
        if duration <= 0:
            return 0.0
        return (len(self._timestamps) - 1) / duration
