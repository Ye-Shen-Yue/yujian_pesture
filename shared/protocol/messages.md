# 御剑·灵枢 通信协议规范 v1

## 概述

Python手势引擎通过WebSocket向Unity剑阵系统发送二进制消息。
- 传输协议: WebSocket (ws://localhost:8765)
- 序列化格式: msgpack (开发阶段可用JSON)
- 发送频率: 最高60Hz
- 消息方向: Python → Unity (主要), Unity → Python (配置/反馈)

## 消息类型

| 类型ID | 名称 | 方向 | 说明 |
|--------|------|------|------|
| 1 | HAND_FRAME | Python→Unity | 手部追踪帧数据（主消息） |
| 2 | GESTURE_EVENT | Python→Unity | 独立手势事件 |
| 3 | PHASE_CHANGE | 双向 | 阶段切换通知 |
| 10 | HEARTBEAT | 双向 | 心跳保活 |
| 20 | CONFIG_UPDATE | Unity→Python | 配置参数更新 |

## HAND_FRAME 消息结构

```json
{
    "v": 1,                          // 协议版本
    "seq": 12345,                    // 消息序列号
    "type": 1,                       // 消息类型 = HAND_FRAME
    "t": 1234567890.123,             // 时间戳 (monotonic seconds)
    "hands": [                       // 手部数据数组 (0~2个)
        {
            "id": "Right",           // 手标识: "Left" | "Right"
            "landmarks": [           // 21个3D关键点 (归一化坐标 0~1)
                [0.52, 0.48, 0.01],  // [0] WRIST
                [0.55, 0.45, 0.02],  // [1] THUMB_CMC
                // ... 共21个点
                [0.60, 0.30, 0.03]   // [20] PINKY_TIP
            ],
            "palm_pos": [0.50, 0.45, 0.02],   // 掌心中心位置
            "palm_vel": [0.12, -0.05, 0.30],   // 掌心速度 (单位/秒)
            "palm_normal": [0.0, 0.0, 0.95],   // 掌心法向量 (单位向量)
            "fingers": [1, 1, 1, 1, 1],        // 五指伸展状态 (0=弯曲, 1=伸展)
            "confidence": 0.95                  // 检测置信度
        }
    ],
    "gestures": [                    // 当前帧识别到的手势事件
        {
            "type": 2,               // 手势类型 (见手势类型表)
            "hand": "Right",         // 触发手
            "confidence": 0.92,      // 识别置信度
            "params": {              // 手势参数 (因手势类型而异)
                "force": 0.8,
                "direction": [0, 0, 1]
            }
        }
    ],
    "phase": 3                       // 当前交互阶段
}
```

## 手势类型枚举

| ID | 名称 | 说明 | params字段 |
|----|------|------|-----------|
| 0 | NONE | 无手势 | - |
| 1 | GRAB | 抓取 | duration |
| 2 | PUSH | 推 | force, direction |
| 3 | PULL | 拉 | force, direction |
| 4 | ROTATE | 旋转 | angular_velocity |
| 5 | PINCH | 捏合 | position |
| 10 | LAUNCH | 发射(组合) | combo_from |
| 20 | OPEN_ARRAY | 启阵(双手) | spread_distance, duration |
| 21 | CLOSE_ARRAY | 破阵(双手) | closing_speed |
| 22 | FORM_CIRCLE | 布阵画圆 | center, radius |
| 23 | POINT_PLACE | 布阵定位 | position |

## 交互阶段枚举

| ID | 名称 | 说明 |
|----|------|------|
| 0 | IDLE | 待机 |
| 1 | KAI_ZHEN | 启阵 |
| 2 | BU_ZHEN | 布阵 |
| 3 | YU_JIAN | 御剑 |
| 4 | PO_ZHEN | 破阵 |

## 关键点索引

MediaPipe 21点手部关键点:
```
0  = WRIST
1  = THUMB_CMC      5  = INDEX_MCP     9  = MIDDLE_MCP    13 = RING_MCP     17 = PINKY_MCP
2  = THUMB_MCP      6  = INDEX_PIP     10 = MIDDLE_PIP    14 = RING_PIP     18 = PINKY_PIP
3  = THUMB_IP       7  = INDEX_DIP     11 = MIDDLE_DIP    15 = RING_DIP     19 = PINKY_DIP
4  = THUMB_TIP      8  = INDEX_TIP     12 = MIDDLE_TIP    16 = RING_TIP     20 = PINKY_TIP
```
