/*
 * 御剑·灵枢 - 一键场景搭建工具
 * 使用方法: Unity菜单栏 → YuJian → 一键搭建场景
 * 自动创建: 剑体Prefab、材质、场景对象、组件挂载、引用连接
 */

using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;
using System.IO;

public class YuJianSceneBuilder : EditorWindow
{
    [MenuItem("YuJian/一键搭建场景 (Build Scene)")]
    public static void BuildScene()
    {
        if (!EditorUtility.DisplayDialog("御剑·灵枢",
            "将自动创建:\n" +
            "- 剑体Prefab (含Rigidbody+Trail)\n" +
            "- SwordGlow材质\n" +
            "- 阵法连线材质\n" +
            "- 场景中所有管理器对象\n" +
            "- 组件引用自动连接\n\n" +
            "继续?", "开始搭建", "取消"))
            return;

        // 确保目录存在
        EnsureDirectory("Assets/Materials");
        EnsureDirectory("Assets/Prefabs/Sword");
        EnsureDirectory("Assets/Scenes");
        EnsureDirectory("Assets/ScriptableObjects/Formations");

        // 1. 创建材质
        var swordMat = CreateSwordMaterial();
        var trailMat = CreateTrailMaterial();
        var glowLineMat = CreateGlowLineMaterial();

        // 2. 创建剑体Prefab
        var swordPrefab = CreateSwordPrefab(swordMat, trailMat);

        // 3. 创建阵法ScriptableObject
        CreateFormationAssets();

        // 4. 搭建场景
        BuildMainScene(swordPrefab, glowLineMat);

        Debug.Log("=== 御剑·灵枢 场景搭建完成! ===");
        Debug.Log("步骤: 1) 先运行Python手势引擎  2) 再点击Unity Play");
    }

    // ========== 材质创建 ==========

    static Material CreateSwordMaterial()
    {
        string path = "Assets/Materials/SwordBlade.mat";
        var existing = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (existing != null) return existing;

        // 优先使用自定义shader，否则用Standard
        var shader = Shader.Find("YuJian/SwordGlow");
        if (shader == null) shader = Shader.Find("Standard");

        var mat = new Material(shader);
        mat.color = new Color(0.75f, 0.8f, 0.9f, 1f);
        mat.EnableKeyword("_EMISSION");
        mat.SetColor("_EmissionColor", new Color(0.2f, 0.5f, 1f) * 1.5f);

        AssetDatabase.CreateAsset(mat, path);
        Debug.Log($"[材质] 创建: {path}");
        return mat;
    }

    static Material CreateTrailMaterial()
    {
        string path = "Assets/Materials/TrailMaterial.mat";
        var existing = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (existing != null) return existing;

        // Trail用Particles/Additive shader
        var shader = Shader.Find("Particles/Standard Unlit");
        if (shader == null) shader = Shader.Find("Sprites/Default");

        var mat = new Material(shader);
        mat.SetFloat("_Mode", 1); // Additive
        mat.color = new Color(0.3f, 0.6f, 1f, 0.7f);

        AssetDatabase.CreateAsset(mat, path);
        Debug.Log($"[材质] 创建: {path}");
        return mat;
    }

    static Material CreateGlowLineMaterial()
    {
        string path = "Assets/Materials/FormationLine.mat";
        var existing = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (existing != null) return existing;

        var shader = Shader.Find("Sprites/Default");
        if (shader == null) shader = Shader.Find("Unlit/Color");

        var mat = new Material(shader);
        mat.color = new Color(0.2f, 0.8f, 1f, 0.5f);

        AssetDatabase.CreateAsset(mat, path);
        Debug.Log($"[材质] 创建: {path}");
        return mat;
    }

    // ========== Prefab创建 ==========

    static GameObject CreateSwordPrefab(Material swordMat, Material trailMat)
    {
        string path = "Assets/Prefabs/Sword/SwordPrefab.prefab";
        var existing = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        if (existing != null)
        {
            // 删除旧的，重新生成
            AssetDatabase.DeleteAsset(path);
        }

        // 根对象
        var sword = new GameObject("Sword");

        // === 程序化剑形Mesh ===
        var swordMesh = YuJian.Sword.SwordMeshGenerator.Generate();
        // 保存Mesh资源
        string meshPath = "Assets/Prefabs/Sword/SwordMesh.asset";
        var existingMesh = AssetDatabase.LoadAssetAtPath<Mesh>(meshPath);
        if (existingMesh != null) AssetDatabase.DeleteAsset(meshPath);
        AssetDatabase.CreateAsset(Object.Instantiate(swordMesh), meshPath);
        var savedMesh = AssetDatabase.LoadAssetAtPath<Mesh>(meshPath);

        // 剑身MeshFilter + MeshRenderer
        var blade = new GameObject("Blade");
        blade.transform.SetParent(sword.transform);
        blade.transform.localPosition = Vector3.zero;
        // 剑尖朝+Z，旋转使剑尖朝+X（与原来的朝向一致）
        blade.transform.localRotation = Quaternion.Euler(0, -90, 0);
        blade.transform.localScale = Vector3.one;
        var mf = blade.AddComponent<MeshFilter>();
        mf.sharedMesh = savedMesh;
        var mr = blade.AddComponent<MeshRenderer>();
        if (swordMat != null)
            mr.sharedMaterial = swordMat;

        // 剑尖发光点
        var tip = new GameObject("SwordTip");
        tip.transform.SetParent(sword.transform);
        tip.transform.localPosition = new Vector3(0.85f, 0, 0);
        var tipLight = tip.AddComponent<Light>();
        tipLight.type = LightType.Point;
        tipLight.color = new Color(0.3f, 0.6f, 1f);
        tipLight.intensity = 2f;
        tipLight.range = 1.5f;

        // Rigidbody
        var rb = sword.AddComponent<Rigidbody>();
        rb.useGravity = false;
        rb.mass = 0.8f;
        rb.drag = 2f;
        rb.interpolation = RigidbodyInterpolation.Interpolate;
        rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;

        // BoxCollider
        var col = sword.AddComponent<BoxCollider>();
        col.size = new Vector3(1.2f, 0.06f, 0.06f);
        col.isTrigger = true;

        // TrailRenderer
        var trail = new GameObject("Trail");
        trail.transform.SetParent(sword.transform);
        trail.transform.localPosition = new Vector3(0.3f, 0, 0);
        var tr = trail.AddComponent<TrailRenderer>();
        tr.time = 0.3f;
        tr.startWidth = 0.05f;
        tr.endWidth = 0.0f;
        tr.startColor = new Color(0.3f, 0.6f, 1f, 0.8f);
        tr.endColor = new Color(0.3f, 0.6f, 1f, 0f);
        tr.minVertexDistance = 0.05f;
        if (trailMat != null)
            tr.sharedMaterial = trailMat;

        // SwordTrailController (VFX)
        trail.AddComponent<YuJian.VFX.SwordTrailController>();

        // SwordEntity脚本
        sword.AddComponent<YuJian.Sword.SwordEntity>();

        // 保存Prefab
        var prefab = PrefabUtility.SaveAsPrefabAsset(sword, path);
        Object.DestroyImmediate(sword);
        Debug.Log($"[Prefab] 创建: {path} (程序化剑形Mesh)");
        return prefab;
    }

    // ========== 阵法资源 ==========

    static void CreateFormationAssets()
    {
        CreateFormationAsset("LiangYi", YuJian.Formation.FormationDefinition.CreateLiangYi());
        CreateFormationAsset("QiXing", YuJian.Formation.FormationDefinition.CreateQiXing());
        CreateFormationAsset("ZhuXian", YuJian.Formation.FormationDefinition.CreateZhuXian());
        CreateFormationAsset("TianGang", YuJian.Formation.FormationDefinition.CreateTianGang());
        CreateFormationAsset("WanJianTianGang", YuJian.Formation.FormationDefinition.CreateWanJianTianGang());
    }

    static void CreateFormationAsset(string name,
        YuJian.Formation.FormationDefinition def)
    {
        string path = $"Assets/ScriptableObjects/Formations/{name}Formation.asset";
        if (AssetDatabase.LoadAssetAtPath<Object>(path) != null) return;

        AssetDatabase.CreateAsset(def, path);
        Debug.Log($"[阵法] 创建: {path}");
    }

    // ========== 场景搭建 ==========

    static void BuildMainScene(GameObject swordPrefab, Material glowLineMat)
    {
        // 创建新场景
        var scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects,
            NewSceneMode.Single);

        // --- 调整默认相机 ---
        var cam = Camera.main;
        if (cam != null)
        {
            cam.transform.position = new Vector3(0, 5, -10);
            cam.transform.rotation = Quaternion.Euler(25, 0, 0);
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.02f, 0.02f, 0.06f);
            cam.farClipPlane = 200f;
        }

        // --- 调整默认灯光 ---
        var lights = Object.FindObjectsOfType<Light>();
        foreach (var l in lights)
        {
            if (l.type == LightType.Directional)
            {
                l.color = new Color(0.4f, 0.45f, 0.6f);
                l.intensity = 0.5f;
                l.transform.rotation = Quaternion.Euler(50, -30, 0);
            }
        }

        // --- 环境光 ---
        RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
        RenderSettings.ambientLight = new Color(0.05f, 0.08f, 0.15f);

        // --- 地面参考平面 ---
        var ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
        ground.name = "GroundPlane";
        ground.transform.position = Vector3.zero;
        ground.transform.localScale = new Vector3(5, 1, 5);
        var groundMat = new Material(Shader.Find("Standard"));
        groundMat.color = new Color(0.05f, 0.05f, 0.1f);
        ground.GetComponent<MeshRenderer>().sharedMaterial = groundMat;

        // ============================
        // 1. GameManager (核心管理器)
        // ============================
        var gmObj = new GameObject("GameManager");
        var gm = gmObj.AddComponent<YuJian.Core.GameManager>();
        var pc = gmObj.AddComponent<YuJian.Core.PhaseController>();
        // 通过SerializedObject连接引用
        WireSerializedField(gm, "phaseController", pc);

        // ============================
        // 2. SwordSystem (剑体系统)
        // ============================
        var swordSysObj = new GameObject("SwordSystem");

        // SwordPool
        var pool = swordSysObj.AddComponent<YuJian.Sword.SwordPool>();
        WireSerializedField(pool, "swordPrefab", swordPrefab);
        WireSerializedFieldInt(pool, "poolSize", 60);

        // SwordController
        var swordCtrl = swordSysObj.AddComponent<YuJian.Sword.SwordController>();
        WireSerializedField(swordCtrl, "swordPool", pool);

        // ============================
        // 3. FormationSystem (阵法系统)
        // ============================
        var formObj = new GameObject("FormationSystem");
        var formMgr = formObj.AddComponent<YuJian.Formation.FormationManager>();
        WireSerializedField(formMgr, "swordPool", pool);

        // 加载阵法ScriptableObject资源
        var formationList = new System.Collections.Generic.List<YuJian.Formation.FormationDefinition>();
        string[] formNames = { "LiangYi", "QiXing", "ZhuXian", "TianGang", "WanJianTianGang" };
        foreach (var fn in formNames)
        {
            string fPath = $"Assets/ScriptableObjects/Formations/{fn}Formation.asset";
            var def = AssetDatabase.LoadAssetAtPath<YuJian.Formation.FormationDefinition>(fPath);
            if (def != null) formationList.Add(def);
        }
        WireSerializedFieldList(formMgr, "formations", formationList);

        // ============================
        // 4. VFX (视觉特效)
        // ============================
        var vfxObj = new GameObject("VFXSystem");
        vfxObj.AddComponent<YuJian.VFX.VFXManager>();

        // 阵法连线效果
        var glowObj = new GameObject("FormationGlow");
        glowObj.transform.SetParent(vfxObj.transform);
        var glowEffect = glowObj.AddComponent<YuJian.VFX.FormationGlowEffect>();
        var lr = glowObj.AddComponent<LineRenderer>();
        lr.startWidth = 0.05f;
        lr.endWidth = 0.05f;
        lr.startColor = new Color(0.2f, 0.8f, 1f, 0.5f);
        lr.endColor = new Color(0.2f, 0.8f, 1f, 0.5f);
        lr.positionCount = 0;
        if (glowLineMat != null)
            lr.sharedMaterial = glowLineMat;
        WireSerializedField(glowEffect, "lineRenderer", lr);

        // 剑气波发射器
        var waveObj = new GameObject("EnergyWaveEmitter");
        waveObj.transform.SetParent(vfxObj.transform);
        waveObj.AddComponent<YuJian.VFX.EnergyWaveEmitter>();

        // 万剑归宗特效控制器
        var wanJianObj = new GameObject("WanJianGuiZong");
        wanJianObj.transform.SetParent(vfxObj.transform);
        var wanJianEffect = wanJianObj.AddComponent<YuJian.VFX.WanJianGuiZongEffect>();
        WireSerializedField(wanJianEffect, "swordPool", pool);

        // ============================
        // 5. Audio (音频系统)
        // ============================
        var audioObj = new GameObject("AudioSystem");
        audioObj.AddComponent<YuJian.Audio.AudioManager>();

        // PLACEHOLDER_UI_CANVAS
        // ============================
        // 6. UI Canvas (界面)
        // ============================
        CreateUICanvas();

        // --- 保存场景 ---
        EditorSceneManager.MarkSceneDirty(scene);
        string scenePath = "Assets/Scenes/YuJianMain.unity";
        EditorSceneManager.SaveScene(scene, scenePath);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log($"[场景] 创建并保存: {scenePath}");
        Debug.Log("[场景] 对象层级:");
        Debug.Log("  GameManager (GameManager + PhaseController + WebSocketClient + InputBridge)");
        Debug.Log("  SwordSystem (SwordPool + SwordController)");
        Debug.Log("  FormationSystem (FormationManager)");
        Debug.Log("  VFXSystem (VFXManager + FormationGlow + EnergyWave)");
        Debug.Log("  AudioSystem (AudioManager)");
        Debug.Log("  UICanvas (HUD + Tutorial + DebugPanel)");
    }

    static void CreateUICanvas()
    {
        // Canvas
        var canvasObj = new GameObject("UICanvas");
        var canvas = canvasObj.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 100;
        canvasObj.AddComponent<UnityEngine.UI.CanvasScaler>();
        canvasObj.AddComponent<UnityEngine.UI.GraphicRaycaster>();

        // --- HUD ---
        var hudObj = new GameObject("HUD");
        hudObj.transform.SetParent(canvasObj.transform, false);
        var hud = hudObj.AddComponent<YuJian.UI.HUDController>();

        // 阶段文字 (左上)
        var phaseText = CreateUIText("PhaseText", hudObj.transform,
            new Vector2(0, 1), new Vector2(0, 1),
            new Vector2(20, -20), new Vector2(300, 50),
            "待机", 28, TextAnchor.UpperLeft, Color.cyan);

        // 手势文字 (右上)
        var gestureText = CreateUIText("GestureText", hudObj.transform,
            new Vector2(1, 1), new Vector2(1, 1),
            new Vector2(-20, -20), new Vector2(300, 50),
            "", 24, TextAnchor.UpperRight, Color.yellow);

        // 连接状态 (底部中央)
        var connText = CreateUIText("ConnectionText", hudObj.transform,
            new Vector2(0.5f, 0), new Vector2(0.5f, 0),
            new Vector2(0, 20), new Vector2(400, 40),
            "未连接 - 等待Python引擎...", 18, TextAnchor.LowerCenter, Color.red);

        WireSerializedField(hud, "phaseText", phaseText);
        WireSerializedField(hud, "gestureText", gestureText);
        WireSerializedField(hud, "connectionText", connText);

        // --- Tutorial ---
        var tutObj = new GameObject("Tutorial");
        tutObj.transform.SetParent(canvasObj.transform, false);
        var tut = tutObj.AddComponent<YuJian.UI.TutorialOverlay>();

        var instrText = CreateUIText("InstructionText", tutObj.transform,
            new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
            new Vector2(0, -100), new Vector2(600, 60),
            "双手合十，然后缓缓拉开 → 启阵", 22, TextAnchor.MiddleCenter,
            new Color(0.8f, 0.9f, 1f));
        WireSerializedField(tut, "instructionText", instrText);
        tutObj.SetActive(false);

        // --- Debug Panel ---
        var dbgObj = new GameObject("DebugPanel");
        dbgObj.transform.SetParent(canvasObj.transform, false);
        var dbg = dbgObj.AddComponent<YuJian.UI.DebugPanel>();

        var dbgText = CreateUIText("DebugText", dbgObj.transform,
            new Vector2(0, 0), new Vector2(0, 0),
            new Vector2(20, 60), new Vector2(250, 120),
            "FPS: --\nNo data", 14, TextAnchor.LowerLeft,
            new Color(0.5f, 1f, 0.5f, 0.7f));
        WireSerializedField(dbg, "debugText", dbgText);
    }

    static UnityEngine.UI.Text CreateUIText(string name, Transform parent,
        Vector2 anchorMin, Vector2 anchorMax,
        Vector2 anchoredPos, Vector2 sizeDelta,
        string text, int fontSize, TextAnchor alignment, Color color)
    {
        var obj = new GameObject(name);
        obj.transform.SetParent(parent, false);

        var rt = obj.AddComponent<RectTransform>();
        rt.anchorMin = anchorMin;
        rt.anchorMax = anchorMax;
        rt.pivot = anchorMin;
        rt.anchoredPosition = anchoredPos;
        rt.sizeDelta = sizeDelta;

        var t = obj.AddComponent<UnityEngine.UI.Text>();
        t.text = text;
        t.fontSize = fontSize;
        t.alignment = alignment;
        t.color = color;
        t.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        if (t.font == null)
            t.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
        t.horizontalOverflow = HorizontalWrapMode.Overflow;
        t.verticalOverflow = VerticalWrapMode.Overflow;

        return t;
    }

    // ========== 序列化引用连接工具 ==========

    static void WireSerializedField(Component comp, string fieldName, Object value)
    {
        var so = new SerializedObject(comp);
        var prop = so.FindProperty(fieldName);
        if (prop != null)
        {
            prop.objectReferenceValue = value;
            so.ApplyModifiedPropertiesWithoutUndo();
        }
        else
        {
            Debug.LogWarning($"[Wire] 找不到字段: {comp.GetType().Name}.{fieldName}");
        }
    }

    static void WireSerializedFieldInt(Component comp, string fieldName, int value)
    {
        var so = new SerializedObject(comp);
        var prop = so.FindProperty(fieldName);
        if (prop != null)
        {
            prop.intValue = value;
            so.ApplyModifiedPropertiesWithoutUndo();
        }
    }

    static void WireSerializedFieldList(Component comp, string fieldName,
        System.Collections.Generic.List<YuJian.Formation.FormationDefinition> list)
    {
        var so = new SerializedObject(comp);
        var prop = so.FindProperty(fieldName);
        if (prop != null && prop.isArray)
        {
            prop.ClearArray();
            for (int i = 0; i < list.Count; i++)
            {
                prop.InsertArrayElementAtIndex(i);
                prop.GetArrayElementAtIndex(i).objectReferenceValue = list[i];
            }
            so.ApplyModifiedPropertiesWithoutUndo();
        }
    }

    // ========== 工具方法 ==========

    static void EnsureDirectory(string path)
    {
        if (!AssetDatabase.IsValidFolder(path))
        {
            string parent = Path.GetDirectoryName(path).Replace("\\", "/");
            string folder = Path.GetFileName(path);
            if (!AssetDatabase.IsValidFolder(parent))
                EnsureDirectory(parent);
            AssetDatabase.CreateFolder(parent, folder);
        }
    }
}
