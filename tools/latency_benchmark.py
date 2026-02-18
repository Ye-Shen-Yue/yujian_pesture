"""延迟基准测试工具 - 测量端到端管线延迟"""

import sys
import os
import time
import statistics

sys.path.insert(0, os.path.join(os.path.dirname(__file__), "..", "python-gesture-engine"))

from capture.camera_source import CameraSource
from capture.frame_preprocessor import FramePreprocessor
from tracking.hand_tracker import HandTracker
from gestures.gesture_state_machine import GestureStateMachine
from network.message_builder import MessageBuilder


def main():
    print("=== 御剑·灵枢 延迟基准测试 ===")
    print("测量各阶段耗时，请将手放在摄像头前")
    print("按 Ctrl+C 结束测试\n")

    camera = CameraSource(device_index=0, width=640, height=480)
    if not camera.open():
        print("无法打开摄像头")
        return

    preprocessor = FramePreprocessor(flip_horizontal=True)
    tracker = HandTracker(max_hands=2)
    gesture_fsm = GestureStateMachine()
    msg_builder = MessageBuilder()

    # 延迟采样
    samples = {
        "capture": [],
        "preprocess": [],
        "tracking": [],
        "gesture": [],
        "serialize": [],
        "total": [],
    }

    num_frames = 300
    prev_time = time.monotonic()

    try:
        for i in range(num_frames):
            total_start = time.monotonic()

            # 采集
            t0 = time.monotonic()
            success, frame, timestamp = camera.read()
            samples["capture"].append((time.monotonic() - t0) * 1000)
            if not success:
                continue

            # 预处理
            t0 = time.monotonic()
            rgb_frame = preprocessor.process(frame)
            samples["preprocess"].append((time.monotonic() - t0) * 1000)

            # 追踪
            t0 = time.monotonic()
            hand_states = tracker.process(rgb_frame, timestamp)
            samples["tracking"].append((time.monotonic() - t0) * 1000)

            # 手势识别
            dt = timestamp - prev_time
            prev_time = timestamp
            t0 = time.monotonic()
            events = gesture_fsm.update(hand_states, dt)
            samples["gesture"].append((time.monotonic() - t0) * 1000)

            # 序列化
            t0 = time.monotonic()
            msg = msg_builder.build_hand_frame(hand_states, [e.to_dict() for e in events])
            samples["serialize"].append((time.monotonic() - t0) * 1000)

            # 总耗时
            samples["total"].append((time.monotonic() - total_start) * 1000)

            # 进度
            if (i + 1) % 50 == 0:
                print(f"  已采集 {i + 1}/{num_frames} 帧...")

    except KeyboardInterrupt:
        pass
    finally:
        camera.release()
        tracker.release()

    # 输出统计
    print("\n" + "=" * 60)
    print(f"{'阶段':<15} {'平均(ms)':<12} {'中位(ms)':<12} {'P95(ms)':<12} {'最大(ms)':<12}")
    print("-" * 60)

    for stage, data in samples.items():
        if not data:
            continue
        data_sorted = sorted(data)
        avg = statistics.mean(data)
        median = statistics.median(data)
        p95 = data_sorted[int(len(data_sorted) * 0.95)]
        max_val = max(data)
        print(f"{stage:<15} {avg:<12.2f} {median:<12.2f} {p95:<12.2f} {max_val:<12.2f}")

    print("=" * 60)

    total_avg = statistics.mean(samples["total"]) if samples["total"] else 0
    fps = 1000 / total_avg if total_avg > 0 else 0
    print(f"\n平均总延迟: {total_avg:.2f}ms")
    print(f"等效帧率: {fps:.1f} FPS")
    print(f"目标: <30ms (33 FPS)")
    print(f"状态: {'PASS' if total_avg < 30 else 'NEEDS OPTIMIZATION'}")


if __name__ == "__main__":
    main()
