"""手势数据录制工具 - 录制关键点序列用于测试和回放"""

import sys
import os
import json
import time
import cv2
import yaml

# 添加父目录到路径
sys.path.insert(0, os.path.join(os.path.dirname(__file__), "..", "python-gesture-engine"))

from capture.camera_source import CameraSource
from capture.frame_preprocessor import FramePreprocessor
from tracking.hand_tracker import HandTracker
from debug.visualizer import Visualizer


def main():
    print("=== 御剑·灵枢 手势录制工具 ===")
    print("按 'r' 开始/停止录制")
    print("按 's' 保存录制数据")
    print("按 'q' 退出")

    # 加载配置
    config_path = os.path.join(
        os.path.dirname(__file__), "..", "python-gesture-engine", "config.yaml"
    )
    with open(config_path, "r", encoding="utf-8") as f:
        config = yaml.safe_load(f)

    cam_cfg = config["camera"]
    track_cfg = config["tracking"]

    # 初始化
    camera = CameraSource(
        device_index=cam_cfg["device_index"],
        width=cam_cfg["width"],
        height=cam_cfg["height"],
    )
    if not camera.open():
        print("无法打开摄像头")
        return

    preprocessor = FramePreprocessor(flip_horizontal=cam_cfg.get("flip_horizontal", True))
    tracker = HandTracker(
        max_hands=track_cfg["max_hands"],
        detection_confidence=track_cfg["detection_confidence"],
        tracking_confidence=track_cfg["tracking_confidence"],
    )
    visualizer = Visualizer()

    recording = False
    recorded_frames = []
    frame_count = 0

    try:
        while True:
            success, frame, timestamp = camera.read()
            if not success:
                continue

            rgb_frame = preprocessor.process(frame)
            display_frame = preprocessor.process_for_display(frame)
            hand_states = tracker.process(rgb_frame, timestamp)

            # 录制中
            if recording and hand_states:
                frame_data = {
                    "timestamp": timestamp,
                    "frame_index": frame_count,
                    "hands": [hs.to_dict() for hs in hand_states],
                }
                recorded_frames.append(frame_data)
                frame_count += 1

            # 显示
            display = visualizer.draw(display_frame, hand_states, fps=0)

            # 录制状态指示
            status = "REC" if recording else "IDLE"
            color = (0, 0, 255) if recording else (200, 200, 200)
            cv2.putText(display, f"[{status}] Frames: {frame_count}",
                        (10, display.shape[0] - 20),
                        cv2.FONT_HERSHEY_SIMPLEX, 0.6, color, 2)

            cv2.imshow("Gesture Recorder", display)

            key = cv2.waitKey(1) & 0xFF
            if key == ord('q'):
                break
            elif key == ord('r'):
                recording = not recording
                if recording:
                    recorded_frames = []
                    frame_count = 0
                    print("开始录制...")
                else:
                    print(f"停止录制，共 {frame_count} 帧")
            elif key == ord('s') and recorded_frames:
                filename = f"gesture_recording_{int(time.time())}.json"
                filepath = os.path.join(os.path.dirname(__file__), filename)
                with open(filepath, "w", encoding="utf-8") as f:
                    json.dump(recorded_frames, f, indent=2)
                print(f"已保存: {filepath} ({len(recorded_frames)} 帧)")

    finally:
        camera.release()
        tracker.release()
        cv2.destroyAllWindows()


if __name__ == "__main__":
    main()
