"""手势状态机 - 管理手势生命周期和事件发射"""

import time
from network.protocol import GestureType
from tracking.hand_state import HandState
from gestures.gesture_types import GestureEvent, GestureState
from gestures.atomic_gestures import AtomicGestureClassifier


class GestureStateMachine:
    """主手势状态机

    状态流转: Idle → Tracking → GestureActive → Cooldown → Idle

    每只手独立维护状态，每帧更新后输出手势事件列表。
    """

    def __init__(self, config: dict = None):
        cfg = config or {}
        self._classifier = AtomicGestureClassifier(cfg)
        self._cooldown_time = cfg.get("combo", {}).get("cooldown", 0.3)

        # 从配置读取状态机参数
        sm_cfg = cfg.get("state_machine", {})
        self.ACTIVATION_THRESHOLD = sm_cfg.get("activation_threshold", 0.4)
        self.SUSTAIN_THRESHOLD = sm_cfg.get("sustain_threshold", 0.25)
        self.MIN_DURATION = sm_cfg.get("min_duration", 0.05)

        self._hand_states: dict[str, GestureState] = {}

    def update(self, hand_states: list[HandState],
               dt: float) -> list[GestureEvent]:
        """
        更新状态机，返回本帧产生的手势事件

        Args:
            hand_states: 当前帧检测到的手部状态
            dt: 帧间隔(秒)

        Returns:
            手势事件列表
        """
        events = []
        now = time.monotonic()
        active_hands = set()

        for hs in hand_states:
            hand_id = hs.handedness
            active_hands.add(hand_id)

            # 初始化手势状态
            if hand_id not in self._hand_states:
                self._hand_states[hand_id] = GestureState()

            gs = self._hand_states[hand_id]

            # 冷却中则跳过
            if now < gs.cooldown_until:
                continue

            # 分类当前帧手势
            gesture_type, confidence = self._classifier.classify(hs, dt)

            if gs.is_active:
                # 当前有活跃手势
                if gesture_type == gs.current_gesture and confidence >= self.SUSTAIN_THRESHOLD:
                    # 同一手势持续
                    gs.duration = now - gs.start_time
                    gs.confidence = confidence
                else:
                    # 手势结束
                    if gs.duration >= self.MIN_DURATION:
                        events.append(GestureEvent(
                            gesture_type=gs.current_gesture,
                            hand_id=hand_id,
                            confidence=gs.confidence,
                            params={"duration": gs.duration},
                            timestamp=now,
                        ))
                    # 进入冷却
                    gs.cooldown_until = now + self._cooldown_time
                    gs.current_gesture = GestureType.NONE
                    gs.confidence = 0.0
                    gs.duration = 0.0
            else:
                # 当前无活跃手势，检测新手势
                if gesture_type != GestureType.NONE and confidence >= self.ACTIVATION_THRESHOLD:
                    gs.current_gesture = gesture_type
                    gs.confidence = confidence
                    gs.start_time = now
                    gs.duration = 0.0

        # 清理消失的手
        for hand_id in list(self._hand_states.keys()):
            if hand_id not in active_hands:
                gs = self._hand_states[hand_id]
                if gs.is_active and gs.duration >= self.MIN_DURATION:
                    events.append(GestureEvent(
                        gesture_type=gs.current_gesture,
                        hand_id=hand_id,
                        confidence=gs.confidence,
                        params={"duration": gs.duration},
                        timestamp=time.monotonic(),
                    ))
                del self._hand_states[hand_id]

        return events

    def get_active_gestures(self) -> dict[str, GestureState]:
        """获取当前所有活跃手势状态（用于实时UI显示）"""
        return {
            hand_id: gs
            for hand_id, gs in self._hand_states.items()
            if gs.is_active
        }

    def reset(self):
        """重置所有状态"""
        self._hand_states.clear()
