"""通信协议定义 - 消息类型和数据结构"""

from enum import IntEnum


class MessageType(IntEnum):
    """消息类型枚举"""
    HAND_FRAME = 1       # 手部追踪帧数据
    GESTURE_EVENT = 2    # 手势事件
    PHASE_CHANGE = 3     # 阶段切换
    HEARTBEAT = 10       # 心跳
    CONFIG_UPDATE = 20   # 配置更新


class GestureType(IntEnum):
    """手势类型枚举"""
    NONE = 0
    GRAB = 1         # 抓取
    PUSH = 2         # 推
    PULL = 3         # 拉
    ROTATE = 4       # 旋转
    PINCH = 5        # 捏合
    # 组合手势
    LAUNCH = 10      # 抓取→推 = 发射
    # 双手手势
    OPEN_ARRAY = 20  # 双手合十→拉开 = 启阵
    CLOSE_ARRAY = 21 # 双掌快速合拢 = 破阵
    FORM_CIRCLE = 22 # 左手画圆 = 布阵
    POINT_PLACE = 23 # 右手点指 = 定位
    # 终极手势
    WAN_JIAN = 30    # 双手握拳→同时前推 = 万剑归宗


class PhaseType(IntEnum):
    """交互阶段枚举"""
    IDLE = 0
    KAI_ZHEN = 1     # 启阵
    BU_ZHEN = 2      # 布阵
    YU_JIAN = 3      # 御剑
    PO_ZHEN = 4      # 破阵


# 协议版本号
PROTOCOL_VERSION = 1
