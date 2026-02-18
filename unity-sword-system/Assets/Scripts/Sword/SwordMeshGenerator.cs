using UnityEngine;

namespace YuJian.Sword
{
    /// <summary>
    /// 程序化生成中国古剑(Jian)形状的Mesh
    /// 包含：剑刃(双刃尖头)、剑格(护手)、剑柄、剑首(圆球)
    /// </summary>
    public static class SwordMeshGenerator
    {
        /// <summary>
        /// 生成一把完整的剑Mesh
        /// 剑尖朝+Z方向，剑柄朝-Z方向
        /// </summary>
        public static Mesh Generate(
            float bladeLength = 0.8f,
            float bladeWidth = 0.06f,
            float bladeThickness = 0.012f,
            float handleLength = 0.25f,
            float handleWidth = 0.025f,
            float guardWidth = 0.12f,
            float guardThickness = 0.02f)
        {
            var mesh = new Mesh { name = "SwordMesh" };

            // 剑的各部分沿Z轴排列:
            // 剑首(-handleLength-guardThickness) → 剑柄(-guardThickness) → 剑格(0) → 剑刃(bladeLength)
            float tipZ = bladeLength;
            float guardZ = 0f;
            float handleEndZ = -handleLength;
            float pommelZ = handleEndZ - 0.03f;

            // === 顶点定义 ===
            // 使用简化的多边形截面，每个截面6个点(菱形截面)
            var vertices = new Vector3[]
            {
                // --- 剑刃 (0-9) ---
                // 剑尖 (单点)
                new Vector3(0, 0, tipZ),                                    // 0: 剑尖

                // 剑刃前段截面 (距尖1/4处) - 窄
                new Vector3(0, bladeThickness * 0.6f, tipZ - bladeLength * 0.25f),  // 1: 上
                new Vector3(bladeWidth * 0.5f, 0, tipZ - bladeLength * 0.25f),      // 2: 右刃
                new Vector3(0, -bladeThickness * 0.6f, tipZ - bladeLength * 0.25f), // 3: 下
                new Vector3(-bladeWidth * 0.5f, 0, tipZ - bladeLength * 0.25f),     // 4: 左刃

                // 剑刃中段截面 (距尖1/2处) - 最宽
                new Vector3(0, bladeThickness, tipZ - bladeLength * 0.5f),          // 5: 上
                new Vector3(bladeWidth, 0, tipZ - bladeLength * 0.5f),              // 6: 右刃
                new Vector3(0, -bladeThickness, tipZ - bladeLength * 0.5f),         // 7: 下
                new Vector3(-bladeWidth, 0, tipZ - bladeLength * 0.5f),             // 8: 左刃

                // 剑刃根部截面 (剑格处)
                new Vector3(0, bladeThickness, guardZ + 0.01f),                     // 9: 上
                new Vector3(bladeWidth * 0.9f, 0, guardZ + 0.01f),                 // 10: 右刃
                new Vector3(0, -bladeThickness, guardZ + 0.01f),                    // 11: 下
                new Vector3(-bladeWidth * 0.9f, 0, guardZ + 0.01f),                // 12: 左刃

                // --- 剑格/护手 (13-20) ---
                // 剑格上面
                new Vector3(-guardWidth, guardThickness, guardZ),                    // 13
                new Vector3(guardWidth, guardThickness, guardZ),                     // 14
                new Vector3(guardWidth, guardThickness, guardZ - guardThickness),    // 15
                new Vector3(-guardWidth, guardThickness, guardZ - guardThickness),   // 16
                // 剑格下面
                new Vector3(-guardWidth, -guardThickness, guardZ),                   // 17
                new Vector3(guardWidth, -guardThickness, guardZ),                    // 18
                new Vector3(guardWidth, -guardThickness, guardZ - guardThickness),   // 19
                new Vector3(-guardWidth, -guardThickness, guardZ - guardThickness),  // 20

                // --- 剑柄 (21-28) ---
                // 剑柄上端截面
                new Vector3(-handleWidth, handleWidth, guardZ - guardThickness),     // 21
                new Vector3(handleWidth, handleWidth, guardZ - guardThickness),      // 22
                new Vector3(handleWidth, -handleWidth, guardZ - guardThickness),     // 23
                new Vector3(-handleWidth, -handleWidth, guardZ - guardThickness),    // 24
                // 剑柄下端截面
                new Vector3(-handleWidth * 1.1f, handleWidth * 1.1f, handleEndZ),   // 25
                new Vector3(handleWidth * 1.1f, handleWidth * 1.1f, handleEndZ),    // 26
                new Vector3(handleWidth * 1.1f, -handleWidth * 1.1f, handleEndZ),   // 27
                new Vector3(-handleWidth * 1.1f, -handleWidth * 1.1f, handleEndZ),  // 28

                // --- 剑首/圆球 (29-33) ---
                new Vector3(0, 0.035f, pommelZ),                                     // 29: 上
                new Vector3(0.035f, 0, pommelZ),                                     // 30: 右
                new Vector3(0, -0.035f, pommelZ),                                    // 31: 下
                new Vector3(-0.035f, 0, pommelZ),                                    // 32: 左
                new Vector3(0, 0, pommelZ - 0.03f),                                  // 33: 尾端点
            };

            // === 三角形索引 ===
            var triangles = new int[]
            {
                // --- 剑尖到前段 ---
                0,2,1,  0,3,2,  0,4,3,  0,1,4,

                // --- 前段到中段 ---
                1,2,6,  1,6,5,   // 右上
                2,3,7,  2,7,6,   // 右下
                3,4,8,  3,8,7,   // 左下
                4,1,5,  4,5,8,   // 左上

                // --- 中段到根部 ---
                5,6,10,  5,10,9,
                6,7,11,  6,11,10,
                7,8,12,  7,12,11,
                8,5,9,   8,9,12,

                // --- 剑格 ---
                // 上面
                13,14,15,  13,15,16,
                // 下面
                17,19,18,  17,20,19,
                // 前面
                13,18,14,  13,17,18,
                // 后面
                16,15,19,  16,19,20,
                // 左面
                13,16,20,  13,20,17,
                // 右面
                14,18,19,  14,19,15,

                // --- 剑柄 ---
                // 前面(上端)
                21,22,23,  21,23,24,
                // 上面
                21,26,22,  21,25,26,
                // 右面
                22,26,27,  22,27,23,
                // 下面
                23,27,28,  23,28,24,
                // 左面
                24,28,25,  24,25,21,
                // 后面(下端)
                25,27,26,  25,28,27,

                // --- 剑首 ---
                // 上半
                29,26,25,  29,30,26,  30,27,26,  31,27,30,
                31,28,27,  32,28,31,  32,25,28,  29,25,32,
                // 尾端收束
                33,30,29,  33,31,30,  33,32,31,  33,29,32,
            };

            mesh.vertices = vertices;
            mesh.triangles = triangles;
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            mesh.RecalculateTangents();

            return mesh;
        }
    }
}
