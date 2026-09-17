using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

public static class StageThreeAssetGenerator
{
    private const string ResourcesRoot = "Assets/Resources/";
    private const string Prefabs = "Prefabs/StageThree/";
    private const string MapPath = Prefabs + "Stage1Environment";
    private const string GoldNotice = "当前 GameDesign 仅规定金币必掉，未给出数量区间；暂设所有敌人金币 1–1，须确认后手调，不代表正式经济数值。";
    private static int sCreated;

    private sealed class EnemySpec
    {
        public string Code, Id, Name;
        public int HP, Damage, Level;
        public float Speed, Interval, Range = 1f, Windup, Recovery = 0.35f, ProjectileSpeed = 12f;
        public float ChargeDistance, ChargeSpeed, AreaRadius, TeleportDistance, Radius = 0.5f;
        public EnemyAIType AI;
        public EnemyCategory Category;
        public PrimitiveType Shape = PrimitiveType.Sphere;
        public Vector3 Size = new Vector3(1f, 0.8f, 1f);

        public EnemySpec(string code, string id, string name, EnemyAIType ai, int hp, float speed,
            int damage, float interval, int level)
        {
            Code = code; Id = id; Name = name; AI = ai; HP = hp; Speed = speed;
            Damage = damage; Interval = interval; Level = level;
        }
    }

    private static EnemySpec[] EnemySpecs() => new[]
    {
        new EnemySpec("E01", EnemyConfigTable.SlimeGreenId, "绿史莱姆", EnemyAIType.ChaseMelee, 30, 2f, 10, 1f, 0),
        new EnemySpec("E02", EnemyConfigTable.SlimeRedId, "红史莱姆", EnemyAIType.SlimeCharge, 40, 2.5f, 12, 4f, 1)
            { Windup = 0.7f, ChargeDistance = 4f, ChargeSpeed = 8f },
        new EnemySpec("E03", EnemyConfigTable.GoblinId, "哥布林小兵", EnemyAIType.ThrowStone, 45, 2.6f, 10, 2f, 1)
            { Shape = PrimitiveType.Capsule, Size = new Vector3(0.8f, 0.65f, 0.8f), Range = 8f, Windup = 0.4f, ProjectileSpeed = 10f },
        new EnemySpec("E04", EnemyConfigTable.ArcherId, "哥布林弓手", EnemyAIType.Archer, 35, 2.2f, 12, 2.5f, 1)
            { Shape = PrimitiveType.Cylinder, Size = new Vector3(0.8f, 0.75f, 0.8f), Range = 10f, Windup = 0.8f },
        new EnemySpec("E05", EnemyConfigTable.WolfId, "森林狼", EnemyAIType.Flank, 50, 3.8f, 12, 1.2f, 1)
            { Shape = PrimitiveType.Cube, Size = new Vector3(0.7f, 0.7f, 1f) },
        new EnemySpec("E06", EnemyConfigTable.SpiderId, "毒蜘蛛", EnemyAIType.Poison, 40, 2f, 8, 3f, 1)
            { Size = new Vector3(1f, 0.45f, 1f), Range = 8f, Windup = 0.4f, AreaRadius = 2f },
        new EnemySpec("E07", EnemyConfigTable.TreantId, "树精", EnemyAIType.Slam, 140, 1.5f, 18, 2.5f, 2)
            { Shape = PrimitiveType.Cube, Size = new Vector3(1.7f, 2.4f, 1.7f), Radius = 1f, Range = 3f, Windup = 1f, AreaRadius = 3f },
        new EnemySpec("E08", EnemyConfigTable.MageId, "暗影法师", EnemyAIType.Teleport, 65, 2.6f, 14, 3f, 2)
            { Shape = PrimitiveType.Capsule, Size = new Vector3(0.9f, 0.8f, 0.9f), Range = 8f, Windup = 0.6f, TeleportDistance = 6f },
        new EnemySpec("N01", EnemyConfigTable.RamId, "撞角兽", EnemyAIType.Ram, 100, 2.5f, 18, 5f, 1)
            { Category = EnemyCategory.Elite, Shape = PrimitiveType.Cube, Size = Vector3.one, Windup = 1f, Recovery = 1f, ChargeDistance = 10f, ChargeSpeed = 10f },
        new EnemySpec("N02", EnemyConfigTable.DrummerId, "护盾鼓手", EnemyAIType.ShieldDrummer, 60, 2f, 6, 1.5f, 0)
            { Category = EnemyCategory.Mechanism, Shape = PrimitiveType.Cylinder, Size = new Vector3(1f, 0.6f, 1f) },
        new EnemySpec("N03", EnemyConfigTable.SplitterId, "分裂囊虫", EnemyAIType.Split, 40, 2.2f, 8, 1f, 0)
            { Windup = 0.8f, Size = new Vector3(0.9f, 1.1f, 0.9f) },
        new EnemySpec("B01", EnemyConfigTable.SlimeKingId, "史莱姆王", EnemyAIType.SlimeKing, 650, 2f, 20, 5f, 1)
            { Category = EnemyCategory.Boss, Size = new Vector3(3f, 1.2f, 3f), Radius = 1.5f, Range = 10f, Windup = 1.2f,
                Recovery = 1.5f, ChargeDistance = 12f, ChargeSpeed = 8f, AreaRadius = 3f }
    };

    [MenuItem("Game/阶段三/生成缺失资源")]
    public static void Generate()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling)
        {
            Debug.LogWarning("[阶段三] 请在停止播放且编译结束后生成。");
            return;
        }
        if (!EditorUtility.DisplayDialog("阶段三：仅生成缺失资源",
            "创建盾/敌人/关卡配置、地图、预告、拾取和毒区占位资源。已有路径和同 ID 配置一律跳过，不修改旧玩家、枪、子弹或 UI。\n\n" + GoldNotice,
            "确认生成", "取消")) return;
        GenerateAssets();
    }

    /// <summary>实际生成入口：无确认框，供菜单与 MCP 自动化共用；重复运行跳过已存在项。</summary>
    public static void GenerateAssets()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling)
        {
            Debug.LogWarning("[阶段三] 请在停止播放且编译结束后生成。");
            return;
        }
        sCreated = 0;
        var specs = EnemySpecs();
        try
        {
            var enemyIds = ScanIds<EnemyConfig>("mId");
            var shieldIds = ScanIds<ShieldConfig>("mLevel");
            var stageIds = ScanIds<StageConfig>("mLevel");
            var lineMaterial = MaterialAsset("Telegraph", Color.white, true);
            var font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(ResourcesRoot + "Font/yixindanhuangsu/yixindanhuangsu SDF.asset")
                ?? TMP_Settings.defaultFontAsset;
            if (font == null) throw new InvalidOperationException("缺少 TMP 字体；请先由用户装配字体后重试。");
            var telegraph = PrefabAsset(Prefabs + "EnemyTelegraph", root => BuildTelegraph(root, lineMaterial, font));
            var poison = MaterialAsset("Poison", new Color(0.3f, 0.75f, 0.12f, 0.45f), true);
            PrefabAsset(PoisonArea.PrefabPath, root =>
            {
                var visual = Primitive(root.transform, "Visual", PrimitiveType.Cylinder, Vector3.zero,
                    new Vector3(1f, 0.01f, 1f), poison);
                Assign(root.AddComponent<PoisonArea>(), "mRenderer", visual.GetComponent<Renderer>());
            });
            BuildPickups(font);
            var ground = MaterialAsset("Ground", new Color(0.2f, 0.33f, 0.17f));
            var clearing = MaterialAsset("Clearing", new Color(0.3f, 0.43f, 0.22f));
            var rock = MaterialAsset("Rock", new Color(0.35f, 0.38f, 0.4f));
            var trail = MaterialAsset("Trail", new Color(0.6f, 0.53f, 0.33f));
            PrefabAsset(MapPath, root => BuildMap(root, ground, clearing, rock, trail, font));
            for (var i = 0; i < specs.Length; i++)
            {
                var spec = specs[i];
                var tint = spec.Code == "E01" ? new Color(0.3f, 0.8f, 0.2f)
                    : spec.Code == "E02" ? new Color(0.9f, 0.2f, 0.15f) : Color.HSVToRGB(i / 12f, 0.65f, 0.85f);
                var material = MaterialAsset(spec.Code, tint);
                PrefabAsset(Prefabs + spec.Code, root => BuildEnemy(root, spec, material, telegraph));
                ConfigAsset<EnemyConfig>("Enemies/" + spec.Code, spec.Id, enemyIds, so => ConfigureEnemy(so, spec));
            }
            var capacities = new[] { 50f, 100f, 150f, 220f, 300f };
            for (var i = 0; i < capacities.Length; i++)
            {
                var level = i + 1;
                var capacity = capacities[i];
                ConfigAsset<ShieldConfig>("Shields/Shield" + level, level.ToString(), shieldIds, so =>
                {
                    Set(so, "mLevel", level); Set(so, "mCapacity", capacity);
                });
            }
            ConfigAsset<StageConfig>("Stages/Stage1", "1", stageIds, ConfigureStage);
        }
        catch (Exception exception) { Debug.LogException(exception); }
        finally
        {
            PrintManualChanges(specs[0]);
            Debug.LogWarning("[阶段三] " + GoldNotice);
            Debug.Log($"[阶段三] 已新建 {sCreated} 项；每项创建/跳过路径见日志。未保存任何旧资产。请由用户执行 Unity 编译与 Play 验收。\n"
                + "敌弹仍为 Prefabs/Bullet；毒区=" + PoisonArea.PrefabPath + "；当前分裂预告复用敌人 EnemyTelegraph，无独立加载路径。\n"
                + "组内生成间隔 0.35s、未定射程/恢复/瞬移距离及 B01 间隔字段 5s 为显式试玩默认；Boss 实际按技能循环推进。");
        }
    }

    private static Dictionary<string, string> ScanIds<T>(string field) where T : ScriptableObject
    {
        var result = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var guid in AssetDatabase.FindAssets("t:" + typeof(T).Name))
        {
            var path = AssetDatabase.GUIDToAssetPath(guid);
            foreach (var asset in AssetDatabase.LoadAllAssetsAtPath(path))
            {
                if (!(asset is T)) continue;
                var property = new SerializedObject(asset).FindProperty(field);
                var id = property.propertyType == SerializedPropertyType.String ? property.stringValue : property.intValue.ToString();
                if (string.IsNullOrEmpty(id)) continue;
                if (result.TryGetValue(id, out var previous)) Debug.LogWarning($"[阶段三] 已有重复 ID {typeof(T).Name}/{id}：{previous} 与 {path}，不修改。");
                else result.Add(id, path);
                if (!path.Contains("/Resources/Configs/")) Debug.LogWarning($"[阶段三] {id} 位于运行时配置目录以外：{path}；仍按已有 ID 跳过，请人工处理寻址。");
            }
        }
        return result;
    }

    private static bool SkipPath(string path)
    {
        if (AssetDatabase.LoadMainAssetAtPath(path) == null && !File.Exists(path) && !Directory.Exists(path)
            && !File.Exists(path + ".meta")) return false;
        Debug.Log("[阶段三] 跳过已有路径（含未导入文件/孤立 meta）：" + path);
        return true;
    }

    private static void EnsureFolder(string path)
    {
        if (AssetDatabase.IsValidFolder(path)) return;
        var parent = Path.GetDirectoryName(path).Replace('\\', '/');
        EnsureFolder(parent);
        if (string.IsNullOrEmpty(AssetDatabase.CreateFolder(parent, Path.GetFileName(path))))
            throw new IOException("无法创建资源目录：" + path);
    }

    private static void ConfigAsset<T>(string relative, string id, Dictionary<string, string> ids,
        Action<SerializedObject> configure) where T : ScriptableObject
    {
        if (ids.TryGetValue(id, out var existing))
        {
            Debug.Log($"[阶段三] 跳过已有 ID {typeof(T).Name}/{id}：{existing}");
            return;
        }
        var path = ResourcesRoot + "Configs/" + relative + ".asset";
        if (SkipPath(path)) return;
        var config = ScriptableObject.CreateInstance<T>();
        try
        {
            var so = new SerializedObject(config);
            configure(so);
            so.ApplyModifiedPropertiesWithoutUndo();
            EnsureFolder(Path.GetDirectoryName(path).Replace('\\', '/'));
            AssetDatabase.CreateAsset(config, path);
            ids.Add(id, path);
            Created(path);
        }
        finally { if (!EditorUtility.IsPersistent(config)) Object.DestroyImmediate(config); }
    }

    private static Material MaterialAsset(string name, Color color, bool vertexColor = false)
    {
        var path = ResourcesRoot + "Materials/StageThree/" + name + ".mat";
        if (SkipPath(path)) return AssetDatabase.LoadAssetAtPath<Material>(path)
            ?? throw new InvalidOperationException("已有路径不是可用材质，保留原文件：" + path);
        var shader = Shader.Find(vertexColor ? "Sprites/Default"
            : GraphicsSettings.currentRenderPipeline != null ? "Universal Render Pipeline/Lit" : "Standard");
        if (shader == null) throw new InvalidOperationException("缺少占位材质 Shader：" + name);
        var material = new Material(shader) { name = name };
        if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", color);
        if (material.HasProperty("_Color")) material.SetColor("_Color", color);
        EnsureFolder(Path.GetDirectoryName(path).Replace('\\', '/'));
        AssetDatabase.CreateAsset(material, path);
        Created(path);
        return material;
    }

    private static GameObject PrefabAsset(string resourcePath, Action<GameObject> build)
    {
        var path = ResourcesRoot + resourcePath + ".prefab";
        if (SkipPath(path)) return AssetDatabase.LoadAssetAtPath<GameObject>(path)
            ?? throw new InvalidOperationException("已有路径不是可用 prefab，保留原文件：" + path);
        var scene = EditorSceneManager.NewPreviewScene();
        var root = new GameObject(Path.GetFileName(resourcePath));
        SceneManager.MoveGameObjectToScene(root, scene);
        try
        {
            build(root);
            EnsureFolder(Path.GetDirectoryName(path).Replace('\\', '/'));
            var prefab = PrefabUtility.SaveAsPrefabAsset(root, path);
            if (prefab == null) throw new IOException("保存 prefab 失败：" + path);
            Created(path);
            return prefab;
        }
        finally { Object.DestroyImmediate(root); EditorSceneManager.ClosePreviewScene(scene); }
    }

    private static void Created(string path) { sCreated++; Debug.Log("[阶段三] 新建：" + path); }

    private static void Set(SerializedObject so, string name, object value)
    {
        var property = so.FindProperty(name) ?? throw new MissingFieldException(so.targetObject.GetType().Name, name);
        if (value is string text) property.stringValue = text;
        else if (value is int integer) property.intValue = integer;
        else if (value is float number) property.floatValue = number;
        else if (value is bool flag) property.boolValue = flag;
        else if (value is Object reference) property.objectReferenceValue = reference;
        else throw new ArgumentException("不支持的序列化值：" + name);
    }

    private static void Assign(Object target, string field, object value)
    {
        var so = new SerializedObject(target);
        Set(so, field, value);
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void ConfigureEnemy(SerializedObject so, EnemySpec e)
    {
        var boss = e.Category == EnemyCategory.Boss;
        var guaranteed = boss || e.Category == EnemyCategory.Elite;
        Set(so, "mId", e.Id); Set(so, "mName", e.Name); Set(so, "mMaxHP", e.HP); Set(so, "mMoveSpeed", e.Speed);
        Set(so, "mContactDamage", e.Damage); Set(so, "mAttackInterval", e.Interval); Set(so, "mAttackRange", e.Range);
        Set(so, "mAIType", (int)e.AI); Set(so, "mCategory", (int)e.Category); Set(so, "mAttackLevel", e.Level);
        Set(so, "mWindup", e.Windup); Set(so, "mRecovery", e.Recovery); Set(so, "mProjectileSpeed", e.ProjectileSpeed);
        Set(so, "mChargeDistance", e.ChargeDistance); Set(so, "mChargeSpeed", e.ChargeSpeed);
        Set(so, "mAreaRadius", e.AreaRadius); Set(so, "mTeleportDistance", e.TeleportDistance);
        Set(so, "mGoldMin", 1); Set(so, "mGoldMax", 1);
        Set(so, "mAmmoChance", guaranteed ? 1f : 0.55f); Set(so, "mShieldChance", guaranteed ? 1f : 0.03f);
        Set(so, "mWeaponChance", guaranteed ? 1f : 0.01f);
        Set(so, "mAmmoLevelMin", boss ? 2 : guaranteed ? 1 : 0); Set(so, "mAmmoLevelMax", guaranteed ? 2 : 1);
        Set(so, "mAmmoCountMin", 10); Set(so, "mAmmoCountMax", 20);
        Set(so, "mShieldLevelMin", boss ? 2 : 1); Set(so, "mShieldLevelMax", guaranteed ? 2 : 1);
        Set(so, "mPrefabPath", Prefabs + e.Code);
    }

    private static void ConfigureStage(SerializedObject so)
    {
        Set(so, "mLevel", 1); Set(so, "mEnvironmentPath", MapPath); Set(so, "mClearBonus", 500);
        var weapons = so.FindProperty("mWeaponIds");
        weapons.arraySize = 2;
        weapons.GetArrayElementAtIndex(0).stringValue = "pistol";
        weapons.GetArrayElementAtIndex(1).stringValue = "machinegun";
        var ids = new[] { EnemyConfigTable.SlimeGreenId, EnemyConfigTable.SlimeRedId, EnemyConfigTable.GoblinId,
            EnemyConfigTable.ArcherId, EnemyConfigTable.RamId, EnemyConfigTable.DrummerId, EnemyConfigTable.SplitterId, EnemyConfigTable.SlimeKingId };
        // 每组三元组为敌人索引、数量、盾级；组内 0.35s 是显式试玩默认。
        var data = new[] { new[] { 0,18,0 }, new[] { 0,16,0, 1,4,0, 4,1,1 }, new[] { 2,14,0, 5,2,1 },
            new[] { 6,8,0, 0,18,1 }, new[] { 3,8,0, 4,2,1, 5,2,1, 0,20,0 }, new[] { 7,1,2 } };
        var intervals = new[] { 0f, 45f, 45f, 50f, 50f, 60f };
        var waves = so.FindProperty("mWaves");
        waves.arraySize = data.Length;
        for (var i = 0; i < data.Length; i++)
        {
            var wave = waves.GetArrayElementAtIndex(i);
            wave.FindPropertyRelative("mWave").intValue = i + 1;
            wave.FindPropertyRelative("mIntervalFromPrev").floatValue = intervals[i];
            wave.FindPropertyRelative("mIsBoss").boolValue = i == 5;
            var groups = wave.FindPropertyRelative("mGroups");
            groups.arraySize = data[i].Length / 3;
            for (var j = 0; j < groups.arraySize; j++)
            {
                var group = groups.GetArrayElementAtIndex(j);
                group.FindPropertyRelative("mEnemyId").stringValue = ids[data[i][j * 3]];
                group.FindPropertyRelative("mCount").intValue = data[i][j * 3 + 1];
                group.FindPropertyRelative("mShieldLevel").intValue = data[i][j * 3 + 2];
                group.FindPropertyRelative("mInterval").floatValue = 0.35f;
            }
        }
    }

    private static GameObject Primitive(Transform parent, string name, PrimitiveType shape, Vector3 position,
        Vector3 size, Material material, bool solid = false)
    {
        var go = GameObject.CreatePrimitive(shape);
        go.name = name;
        go.transform.SetParent(parent, false);
        go.transform.localPosition = position;
        go.transform.localScale = size;
        go.GetComponent<Renderer>().sharedMaterial = material;
        if (!solid) Object.DestroyImmediate(go.GetComponent<Collider>());
        return go;
    }

    private static TMP_Text Label(Transform parent, TMP_FontAsset font, string text, float height)
    {
        var go = new GameObject("Label", typeof(RectTransform));
        go.transform.SetParent(parent, false);
        go.transform.localPosition = Vector3.up * height;
        go.transform.localRotation = Quaternion.Euler(53.13f, 0f, 0f);
        var label = go.AddComponent<TextMeshPro>();
        label.font = font; label.text = text; label.fontSize = 3.2f;
        label.alignment = TextAlignmentOptions.Center;
        label.rectTransform.sizeDelta = new Vector2(6f, 1.2f);
        label.overflowMode = TextOverflowModes.Overflow;
        label.raycastTarget = false;
        return label;
    }

    private static void BuildTelegraph(GameObject root, Material material, TMP_FontAsset font)
    {
        var so = new SerializedObject(root.AddComponent<EnemyTelegraph>());
        var lines = so.FindProperty("mLines");
        lines.arraySize = 5;
        for (var i = 0; i < 5; i++)
        {
            var go = new GameObject(i == 0 ? "Attack" : i == 4 ? "ShieldRing" : "Support" + i);
            go.transform.SetParent(root.transform, false);
            var line = go.AddComponent<LineRenderer>();
            line.sharedMaterial = material; line.useWorldSpace = true;
            line.positionCount = 0; line.enabled = false;
            line.shadowCastingMode = ShadowCastingMode.Off; line.receiveShadows = false;
            lines.GetArrayElementAtIndex(i).objectReferenceValue = line;
        }
        Set(so, "mShieldLabel", Label(root.transform, font, string.Empty, 1.6f));
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void BuildEnemy(GameObject root, EnemySpec e, Material material, GameObject telegraph)
    {
        var body = root.AddComponent<SphereCollider>();
        body.radius = e.Radius; body.isTrigger = true;
        var rigidbody = root.AddComponent<Rigidbody>();
        rigidbody.isKinematic = true; rigidbody.useGravity = false;
        var visual = Primitive(root.transform, "Visual", e.Shape, Vector3.zero, e.Size, material);
        var height = visual.GetComponent<Renderer>().bounds.size.y;
        visual.transform.localPosition = Vector3.up * (height * 0.5f - 0.6f);
        var feedback = Object.Instantiate(telegraph, root.transform).GetComponent<EnemyTelegraph>();
        if (feedback == null) throw new InvalidOperationException("已有 EnemyTelegraph prefab 缺组件；未覆盖，请人工修复。");
        feedback.transform.localPosition = Vector3.zero;
        var label = feedback.GetComponentInChildren<TMP_Text>();
        if (label != null) label.transform.localPosition = Vector3.up * (height + 0.1f);
        var enemy = root.AddComponent<Enemy>();
        Assign(enemy, "mVisual", visual.transform); Assign(enemy, "mTelegraph", feedback);
        if (e.AI == EnemyAIType.Slam) Assign(root.AddComponent<BattleObstacle>(), "mDynamic", true);
    }

    private static void BuildPickups(TMP_FontAsset font)
    {
        var names = new[] { "GoldPickup", "AmmoPackPickup", "ShieldPickup", "WeaponPickup" };
        var types = new[] { typeof(GoldPickup), typeof(AmmoPackPickup), typeof(ShieldPickup), typeof(WeaponPickup) };
        var colors = new[] { Color.yellow, new Color(1f, 0.55f, 0.15f), Color.cyan, new Color(0.8f, 0.4f, 1f) };
        for (var i = 0; i < names.Length; i++)
        {
            var index = i;
            var material = MaterialAsset(names[i], colors[i]);
            PrefabAsset(Prefabs + names[i], root =>
            {
                var size = index == 0 ? new Vector3(0.4f, 0.08f, 0.4f) : new Vector3(0.6f, 0.35f, 0.45f);
                Primitive(root.transform, "Visual", index == 0 ? PrimitiveType.Cylinder : PrimitiveType.Cube, Vector3.zero, size, material);
                var component = root.AddComponent(types[index]);
                if (index >= 2) Assign(component, "mLabel", Label(root.transform, font, index == 2 ? "S1 50" : "G1 / G2", 0.7f));
            });
        }
    }

    private static void BuildMap(GameObject root, Material ground, Material clearing, Material rock, Material trail, TMP_FontAsset font)
    {
        var environment = root.AddComponent<StageEnvironment>();
        root.AddComponent<BattleNavigation>();
        Assign(environment, "mHalfSize", 45f);
        Primitive(root.transform, "Ground90x90", PrimitiveType.Cube, new Vector3(0f, -0.25f, 0f), new Vector3(90f, 0.5f, 90f), ground);
        Primitive(root.transform, "CentralClearing", PrimitiveType.Cylinder, new Vector3(0f, 0.012f, 0f), new Vector3(26f, 0.01f, 26f), clearing);
        for (var side = -1; side <= 1; side += 2)
        {
            Primitive(root.transform, "OuterRingX", PrimitiveType.Cube, new Vector3(side * 38f, 0.012f, 0f), new Vector3(12f, 0.02f, 90f), trail);
            Primitive(root.transform, "OuterRingZ", PrimitiveType.Cube, new Vector3(0f, 0.012f, side * 38f), new Vector3(90f, 0.02f, 12f), trail);
            for (var i = 0; i <= 10; i++)
            {
                if (i == 5) continue;
                var angle = (-75f + i * 15f) * Mathf.Deg2Rad;
                var position = new Vector3(side * (17f + 6f * Mathf.Cos(angle)), 1.2f, 18f * Mathf.Sin(angle));
                var go = Primitive(root.transform, $"Rock_{side}_{i}", PrimitiveType.Cube, position, new Vector3(3.2f, 2.4f, 3.2f), rock, true);
                go.transform.localRotation = Quaternion.Euler(0f, -side * angle * Mathf.Rad2Deg, 0f);
                Assign(go.AddComponent<BattleObstacle>(), "mDynamic", false);
                Assign(go.AddComponent<DestructibleRock>(), "mMaxHP", 30f);
            }
            for (var half = -1; half <= 1; half += 2)
            {
                var xWall = Primitive(root.transform, "BoundaryX", PrimitiveType.Cube, new Vector3(side * 45.5f, 1.5f, half * 25f), new Vector3(1f, 3f, 40f), rock, true);
                var zWall = Primitive(root.transform, "BoundaryZ", PrimitiveType.Cube, new Vector3(half * 25f, 1.5f, side * 45.5f), new Vector3(40f, 3f, 1f), rock, true);
                xWall.AddComponent<BattleObstacle>(); zWall.AddComponent<BattleObstacle>();
            }
        }
        var so = new SerializedObject(environment);
        var entrances = so.FindProperty("mEntrances");
        entrances.arraySize = 4;
        for (var i = 0; i < 4; i++)
        {
            var entrance = new GameObject("Entrance" + (i + 1));
            entrance.transform.SetParent(root.transform, false);
            var direction = Quaternion.Euler(0f, i * 90f, 0f) * Vector3.forward;
            entrance.transform.localPosition = direction * 41f + Vector3.up * 0.6f;
            Primitive(entrance.transform, "EntryMarker", PrimitiveType.Cube, new Vector3(0f, -0.55f, 0f), new Vector3(5f, 0.04f, 5f), clearing);
            Label(entrance.transform, font, "入口 " + (i + 1), 1f);
            entrances.GetArrayElementAtIndex(i).objectReferenceValue = entrance.transform;
        }
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void PrintManualChanges(EnemySpec e01)
    {
        var temp = ScriptableObject.CreateInstance<EnemyConfig>();
        try
        {
            var so = new SerializedObject(temp);
            ConfigureEnemy(so, e01);
            so.ApplyModifiedPropertiesWithoutUndo();
            var log = new StringBuilder("[阶段三] 旧 E01 Inspector 完整目标字段（同 ID 资产均跳过，请人工核对；金币为待确认占位）：\n");
            var field = so.GetIterator();
            for (var enter = true; field.NextVisible(enter); enter = false)
            {
                if (field.name == "m_Script") continue;
                var value = field.propertyType == SerializedPropertyType.String ? field.stringValue
                    : field.propertyType == SerializedPropertyType.Float ? field.floatValue.ToString(CultureInfo.InvariantCulture)
                    : field.propertyType == SerializedPropertyType.Enum ? field.enumDisplayNames[field.enumValueIndex] + " (" + field.intValue + ")"
                    : field.intValue.ToString();
                log.Append(field.name).Append(" = ").Append(value).AppendLine();
            }
            Debug.Log(log.ToString());
        }
        finally { Object.DestroyImmediate(temp); }
        var weapons = ScanIds<WeaponConfig>("mId");
        foreach (var id in new[] { "pistol", "machinegun" })
            Debug.Log($"[阶段三] 手改 G{(id == "pistol" ? 1 : 2)}：mId={id}，mCaliber={(id == "pistol" ? "S" : "AR")}，mDurabilityMax={(id == "pistol" ? 100 : 240)}；路径={(weapons.TryGetValue(id, out var path) ? path : "缺失，需用户创建")}。本菜单不生成/修改枪械。");
        Debug.Log("[阶段三] 18 种子弹请使用已有 Game/生成子弹配置资产（18个）菜单；本菜单不修改旧玩家/子弹/UI，也不调用全局 SaveAssets。");
    }
}
