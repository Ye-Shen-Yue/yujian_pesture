using System.Collections.Generic;
using YuJian.Core;

namespace YuJian.Network
{
    /// <summary>
    /// 消息反序列化器 - 将JSON数据转为C#结构
    /// Python端发送JSON格式，Unity端使用MiniJSON解析
    /// </summary>
    public static class MessageDeserializer
    {
        /// <summary>
        /// 从字节数组反序列化为GestureFrame
        /// </summary>
        public static GestureFrame Deserialize(byte[] data)
        {
            try
            {
                string json = System.Text.Encoding.UTF8.GetString(data);

                // 调试：输出收到的原始数据前200字符
                if (json.Length < 10)
                {
                    UnityEngine.Debug.LogWarning($"[Deserializer] 数据过短({json.Length}字符): {json}");
                    return null;
                }

                var dict = MiniJSON.Json.Deserialize(json) as Dictionary<string, object>;
                if (dict == null)
                {
                    // 截取前200字符用于调试
                    string preview = json.Length > 200 ? json.Substring(0, 200) + "..." : json;
                    UnityEngine.Debug.LogWarning($"[Deserializer] JSON解析返回null, 数据({json.Length}字符): {preview}");
                    return null;
                }

                return ParseFrame(dict);
            }
            catch (System.Exception e)
            {
                UnityEngine.Debug.LogWarning($"[Deserializer] 解析失败: {e.Message}");
                return null;
            }
        }

        private static GestureFrame ParseFrame(Dictionary<string, object> dict)
        {
            var frame = new GestureFrame
            {
                Version = GetInt(dict, "v"),
                Sequence = GetInt(dict, "seq"),
                Timestamp = GetFloat(dict, "t"),
                Phase = GetInt(dict, "phase"),
                Hands = new List<HandData>(),
                Gestures = new List<GestureData>(),
            };

            // 解析手部数据
            if (dict.ContainsKey("hands") && dict["hands"] is List<object> hands)
            {
                foreach (var h in hands)
                {
                    if (h is Dictionary<string, object> hd)
                        frame.Hands.Add(ParseHand(hd));
                }
            }

            // 解析手势数据
            if (dict.ContainsKey("gestures") && dict["gestures"] is List<object> gestures)
            {
                foreach (var g in gestures)
                {
                    if (g is Dictionary<string, object> gd)
                        frame.Gestures.Add(ParseGesture(gd));
                }
            }

            return frame;
        }

        private static HandData ParseHand(Dictionary<string, object> dict)
        {
            var hand = new HandData
            {
                Id = GetString(dict, "id"),
                Confidence = GetFloat(dict, "confidence"),
            };

            // 解析掌心位置
            if (dict.ContainsKey("palm_pos") && dict["palm_pos"] is List<object> pos)
                hand.PalmPosition = ParseVector3(pos);

            // 解析掌心速度
            if (dict.ContainsKey("palm_vel") && dict["palm_vel"] is List<object> vel)
                hand.PalmVelocity = ParseVector3(vel);

            // 解析掌心法向量
            if (dict.ContainsKey("palm_normal") && dict["palm_normal"] is List<object> normal)
                hand.PalmNormal = ParseVector3(normal);

            // 解析手指状态
            if (dict.ContainsKey("fingers") && dict["fingers"] is List<object> fingers)
            {
                hand.FingerStates = new int[fingers.Count];
                for (int i = 0; i < fingers.Count; i++)
                    hand.FingerStates[i] = System.Convert.ToInt32(fingers[i]);
            }

            // 解析21个关键点
            if (dict.ContainsKey("landmarks") && dict["landmarks"] is List<object> lms)
            {
                hand.Landmarks = new UnityEngine.Vector3[lms.Count];
                for (int i = 0; i < lms.Count; i++)
                {
                    if (lms[i] is List<object> pt)
                        hand.Landmarks[i] = ParseVector3(pt);
                }
            }

            return hand;
        }

        private static GestureData ParseGesture(Dictionary<string, object> dict)
        {
            return new GestureData
            {
                Type = (GestureType)GetInt(dict, "type"),
                HandId = GetString(dict, "hand"),
                Confidence = GetFloat(dict, "confidence"),
                Params = dict.ContainsKey("params") ?
                    dict["params"] as Dictionary<string, object> : null,
            };
        }

        private static UnityEngine.Vector3 ParseVector3(List<object> list)
        {
            if (list == null || list.Count < 3)
                return UnityEngine.Vector3.zero;
            return new UnityEngine.Vector3(
                System.Convert.ToSingle(list[0]),
                System.Convert.ToSingle(list[1]),
                System.Convert.ToSingle(list[2])
            );
        }

        private static int GetInt(Dictionary<string, object> d, string key)
        {
            return d.ContainsKey(key) ? System.Convert.ToInt32(d[key]) : 0;
        }

        private static float GetFloat(Dictionary<string, object> d, string key)
        {
            return d.ContainsKey(key) ? System.Convert.ToSingle(d[key]) : 0f;
        }

        private static string GetString(Dictionary<string, object> d, string key)
        {
            return d.ContainsKey(key) ? d[key]?.ToString() ?? "" : "";
        }
    }

    /// <summary>
    /// JSON解析工具 - 将WebSocket接收的JSON字节转为字典
    /// </summary>
    public static class MessagePackLite
    {
        public static Dictionary<string, object> Unpack(byte[] data)
        {
            string json = System.Text.Encoding.UTF8.GetString(data);
            return MiniJSON.Json.Deserialize(json) as Dictionary<string, object>;
        }
    }
}
