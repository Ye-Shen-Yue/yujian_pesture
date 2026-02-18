"""御剑·灵枢 - Python手势引擎主入口

启动流程:
1. 加载配置
2. 初始化摄像头采集
3. 初始化手部追踪
4. 初始化手势识别
5. 启动WebSocket服务端
6. 进入主循环: 采集→追踪→识别→发送
"""

import sys
import time
import cv2
import yaml
import logging

from capture.camera_source import CameraSource
from capture.frame_preprocessor import FramePreprocessor
from tracking.hand_tracker import HandTracker
from gestures.gesture_state_machine import GestureStateMachine
from gestures.combo_detector import ComboDetector
from gestures.dual_hand_gestures import DualHandGestureDetector
from network.ws_server import WebSocketServer
from network.message_builder import MessageBuilder
from debug.visualizer import Visualizer
from debug.logger import setup_logger, FPSCounter, LatencyTracker


def load_config(path: str = "config.yaml") -> dict:
    """加载YAML配置文件"""
    with open(path, "r", encoding="utf-8") as f:
        return yaml.safe_load(f)


def main():
    # 加载配置
    config = load_config()
    cam_cfg = config["camera"]
    track_cfg = config["tracking"]
    gesture_cfg = config["gestures"]
    net_cfg = config["network"]
    debug_cfg = config["debug"]

    # 初始化日志
    logger = setup_logger(
        level=debug_cfg.get("log_level", "INFO"),
        log_file=debug_cfg.get("log_file"),
    )
    logger.info("=== 御剑·灵枢 手势引擎启动 ===")

    # 初始化摄像头
    camera = CameraSource(
        device_index=cam_cfg["device_index"],
        width=cam_cfg["width"],
        height=cam_cfg["height"],
        fps=cam_cfg["fps"],
    )
    if not camera.open():
        logger.error("无法打开摄像头，请检查设备连接")
        sys.exit(1)
    logger.info(f"摄像头已打开: {camera.actual_resolution}")

    # 初始化预处理器
    preprocessor = FramePreprocessor(
        flip_horizontal=cam_cfg.get("flip_horizontal", True)
    )

    # 初始化手部追踪
    smoother_cfg = track_cfg.get("smoother", {})
    tracker = HandTracker(
        max_hands=track_cfg["max_hands"],
        detection_confidence=track_cfg["detection_confidence"],
        tracking_confidence=track_cfg["tracking_confidence"],
        smoother_min_cutoff=smoother_cfg.get("min_cutoff", 1.0),
        smoother_beta=smoother_cfg.get("beta", 0.007),
    )
    logger.info("手部追踪器已初始化")

    # 初始化手势识别
    gesture_fsm = GestureStateMachine(gesture_cfg)
    combo_detector = ComboDetector(
        max_interval=gesture_cfg.get("combo", {}).get("max_interval", 0.8)
    )
    dual_hand_detector = DualHandGestureDetector(gesture_cfg)
    logger.info("手势识别系统已初始化")

    # 初始化WebSocket服务端
    ws_server = WebSocketServer(
        host=net_cfg["host"],
        port=net_cfg["port"],
    )
    ws_server.start_in_thread()
    msg_builder = MessageBuilder()
    logger.info(f"WebSocket服务端已启动: ws://{net_cfg['host']}:{net_cfg['port']}")

    # 初始化调试工具
    visualizer = Visualizer(
        show_landmarks=debug_cfg.get("show_landmarks", True),
        show_gesture_labels=debug_cfg.get("show_gesture_labels", True),
        show_fps=debug_cfg.get("show_fps", True),
    )
    fps_counter = FPSCounter()
    latency_tracker = LatencyTracker()

    logger.info("主循环开始运行，按 'q' 退出")

    prev_time = time.monotonic()

    try:
        while True:
            loop_start = time.monotonic()

            # 1. 采集帧
            success, frame, timestamp = camera.read()
            if not success:
                logger.warning("帧采集失败")
                continue

            # 2. 预处理
            rgb_frame = preprocessor.process(frame)
            display_frame = preprocessor.process_for_display(frame)

            # 3. 手部追踪
            track_start = time.monotonic()
            hand_states = tracker.process(rgb_frame, timestamp)
            track_time = (time.monotonic() - track_start) * 1000
            latency_tracker.record("tracking", track_time)

            # 4. 手势识别
            dt = timestamp - prev_time
            prev_time = timestamp

            gesture_start = time.monotonic()

            # 原子手势
            atomic_events = gesture_fsm.update(hand_states, dt)

            # 组合手势
            combo_events = combo_detector.process(atomic_events)

            # 双手手势
            dual_events = dual_hand_detector.detect(hand_states)

            # 合并所有手势事件
            all_events = atomic_events + combo_events + dual_events
            all_event_dicts = [e.to_dict() for e in all_events]

            gesture_time = (time.monotonic() - gesture_start) * 1000
            latency_tracker.record("gesture", gesture_time)

            # 5. 发送数据到Unity
            if ws_server.client_count > 0:
                msg = msg_builder.build_hand_frame(
                    hand_states=hand_states,
                    gesture_events=all_event_dicts,
                )
                ws_server.send(msg)

            # 6. 调试可视化
            if debug_cfg.get("show_visualization", True):
                fps_counter.tick()
                display = visualizer.draw(
                    display_frame, hand_states,
                    all_event_dicts, fps_counter.fps,
                )
                cv2.imshow("YuJian Gesture Engine", display)

                key = cv2.waitKey(1) & 0xFF
                if key == ord('q'):
                    break
                elif key == ord('d'):
                    # 打印延迟统计
                    stats = latency_tracker.get_all_stats()
                    for stage, s in stats.items():
                        logger.info(
                            f"[{stage}] avg={s['avg']:.1f}ms "
                            f"min={s['min']:.1f}ms max={s['max']:.1f}ms"
                        )

            # 帧率统计
            loop_time = (time.monotonic() - loop_start) * 1000
            latency_tracker.record("loop", loop_time)

    except KeyboardInterrupt:
        logger.info("收到中断信号")
    finally:
        logger.info("正在清理资源...")
        camera.release()
        tracker.release()
        ws_server.stop()
        cv2.destroyAllWindows()
        logger.info("=== 御剑·灵枢 手势引擎已停止 ===")


if __name__ == "__main__":
    main()
