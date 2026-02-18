using UnityEngine;

namespace YuJian.Formation
{
    /// <summary>
    /// 阵法定义 - ScriptableObject数据驱动
    /// 每种阵法定义槽位坐标、颜色、旋转速度等参数
    /// </summary>
    [CreateAssetMenu(fileName = "NewFormation", menuName = "YuJian/Formation Definition")]
    public class FormationDefinition : ScriptableObject
    {
        [Header("基本信息")]
        public string FormationName;          // 阵法名称
        public string Description;            // 描述

        [Header("阵法参数")]
        public Vector3[] SlotPositions;       // 槽位坐标（相对于阵法中心的偏移）
        public float RotationSpeed = 0f;      // 阵法整体旋转速度(度/秒)
        public Color FormationColor = Color.cyan;  // 阵法主色调
        public float FormationRadius = 3f;    // 阵法半径

        [Header("过渡动画")]
        public AnimationCurve TransitionCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);
        public float TransitionDuration = 1.2f;  // 过渡时长(秒)

        /// <summary>剑数量</summary>
        public int SwordCount => SlotPositions != null ? SlotPositions.Length : 0;

        /// <summary>获取指定槽位的世界坐标</summary>
        public Vector3 GetSlotWorldPosition(int index, Vector3 center,
                                             float rotation = 0f)
        {
            if (index < 0 || index >= SwordCount) return center;

            Vector3 local = SlotPositions[index];
            // 应用旋转
            Quaternion rot = Quaternion.Euler(0, rotation, 0);
            return center + rot * local;
        }

        /// <summary>创建两仪阵（2剑）</summary>
        public static FormationDefinition CreateLiangYi()
        {
            var def = CreateInstance<FormationDefinition>();
            def.FormationName = "两仪阵";
            def.Description = "阴阳对立，两剑180°对称";
            def.FormationColor = new Color(0.5f, 0.8f, 1f);
            def.RotationSpeed = 30f;
            def.FormationRadius = 2f;
            def.SlotPositions = new[]
            {
                new Vector3(2f, 0f, 0f),
                new Vector3(-2f, 0f, 0f),
            };
            return def;
        }

        /// <summary>创建七星阵（7剑）</summary>
        public static FormationDefinition CreateQiXing()
        {
            var def = CreateInstance<FormationDefinition>();
            def.FormationName = "七星阵";
            def.Description = "北斗七星方位布阵";
            def.FormationColor = new Color(1f, 0.8f, 0.2f);
            def.RotationSpeed = 15f;
            def.FormationRadius = 3f;
            // 北斗七星近似坐标
            def.SlotPositions = new[]
            {
                new Vector3(0f, 0f, 3f),       // 天枢
                new Vector3(1f, 0.2f, 2.2f),   // 天璇
                new Vector3(1.8f, 0.1f, 1.2f), // 天玑
                new Vector3(2f, 0f, 0f),        // 天权
                new Vector3(1.5f, 0.3f, -1.5f), // 玉衡
                new Vector3(0.5f, 0.2f, -2.5f), // 开阳
                new Vector3(-0.5f, 0.1f, -3.2f),// 摇光
            };
            return def;
        }

        /// <summary>创建诛仙阵（4剑）</summary>
        public static FormationDefinition CreateZhuXian()
        {
            var def = CreateInstance<FormationDefinition>();
            def.FormationName = "诛仙阵";
            def.Description = "四象方位，东南西北四剑镇守";
            def.FormationColor = new Color(1f, 0.2f, 0.2f);
            def.RotationSpeed = 0f;  // 不旋转，四方镇守
            def.FormationRadius = 3.5f;
            def.SlotPositions = new[]
            {
                new Vector3(0f, 0f, 3.5f),   // 北 - 诛仙剑
                new Vector3(3.5f, 0f, 0f),   // 东 - 戮仙剑
                new Vector3(0f, 0f, -3.5f),  // 南 - 陷仙剑
                new Vector3(-3.5f, 0f, 0f),  // 西 - 绝仙剑
            };
            return def;
        }

        /// <summary>创建天罡北斗阵（7剑）</summary>
        public static FormationDefinition CreateTianGang()
        {
            var def = CreateInstance<FormationDefinition>();
            def.FormationName = "天罡北斗阵";
            def.Description = "七剑圆形等分，持续旋转";
            def.FormationColor = new Color(0.2f, 1f, 0.5f);
            def.RotationSpeed = 45f;
            def.FormationRadius = 3f;
            // 圆形等分
            def.SlotPositions = new Vector3[7];
            for (int i = 0; i < 7; i++)
            {
                float angle = i * (360f / 7f) * Mathf.Deg2Rad;
                def.SlotPositions[i] = new Vector3(
                    Mathf.Cos(angle) * 3f,
                    0f,
                    Mathf.Sin(angle) * 3f
                );
            }
            return def;
        }

        /// <summary>
        /// 创建万剑天罡阵（56剑）- 万剑归宗专用
        /// 多层环形阵法：中心1 + 内环8 + 中环16 + 外环24 + 天冠7
        /// </summary>
        public static FormationDefinition CreateWanJianTianGang()
        {
            var def = CreateInstance<FormationDefinition>();
            def.FormationName = "万剑天罡阵";
            def.Description = "五十六剑多层环阵，万剑归宗终极阵法";
            def.FormationColor = new Color(1f, 0.85f, 0.2f);
            def.RotationSpeed = 25f;
            def.FormationRadius = 8f;
            def.TransitionDuration = 2f;

            var slots = new System.Collections.Generic.List<Vector3>();

            // 层0: 中心主剑（略微抬高）
            slots.Add(new Vector3(0f, 1.5f, 0f));

            // 层1: 内环 8剑，半径2.5，高度0.8
            for (int i = 0; i < 8; i++)
            {
                float a = i * (360f / 8f) * Mathf.Deg2Rad;
                slots.Add(new Vector3(
                    Mathf.Cos(a) * 2.5f, 0.8f, Mathf.Sin(a) * 2.5f));
            }

            // 层2: 中环 16剑，半径5，波浪起伏
            for (int i = 0; i < 16; i++)
            {
                float a = i * (360f / 16f) * Mathf.Deg2Rad;
                float yOff = Mathf.Sin(a * 2f) * 0.3f;
                slots.Add(new Vector3(
                    Mathf.Cos(a) * 5f, 0.3f + yOff, Mathf.Sin(a) * 5f));
            }

            // 层3: 外环 24剑，半径7.5
            for (int i = 0; i < 24; i++)
            {
                float a = i * (360f / 24f) * Mathf.Deg2Rad;
                slots.Add(new Vector3(
                    Mathf.Cos(a) * 7.5f, 0f, Mathf.Sin(a) * 7.5f));
            }

            // 层4: 天冠 7剑，半径3，悬浮在上方
            for (int i = 0; i < 7; i++)
            {
                float a = i * (360f / 7f) * Mathf.Deg2Rad;
                slots.Add(new Vector3(
                    Mathf.Cos(a) * 3f, 3.5f, Mathf.Sin(a) * 3f));
            }

            def.SlotPositions = slots.ToArray();
            return def;
        }
    }
}
