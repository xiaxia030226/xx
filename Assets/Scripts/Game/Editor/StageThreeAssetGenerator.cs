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
    private const string ResourcesRoot = "Assets/Resources/"; // Unity Resources 资产根路径。
    private const string Prefabs = "Prefabs/StageThree/"; // 阶段三预制体的 Resources 相对目录。
    private const string MapPath = Prefabs + "Stage1Environment"; // 第一关环境预制体的 Resources 相对路径。
    private const string GoldNotice = "当前 GameDesign 仅规定金币必掉，未给出数量区间；暂设所有敌人金币 1–1，须确认后手调，不代表正式经济数值。"; // 金币占位数值需人工确认的提示。
    private static int sCreated; // 本轮生成中新建资产的累计数量。

    private sealed class EnemySpec
    {
        public string Code, Id, Name; // Code：资源编号；Id：配置唯一标识；Name：显示名称。
        public int HP, Damage, Level; // HP：生命上限；Damage：接触伤害；Level：攻击等级。
        public float Speed, Interval, Range = 1f, Windup, Recovery = 0.35f, ProjectileSpeed = 12f; // Speed：移速；Interval：攻击间隔；Range：攻击范围；Windup：前摇；Recovery：恢复时间；ProjectileSpeed：弹速。
        public float ChargeDistance, ChargeSpeed, AreaRadius, TeleportDistance, Radius = 0.5f; // ChargeDistance：冲锋距离；ChargeSpeed：冲锋速度；AreaRadius：范围技能半径；TeleportDistance：瞬移距离；Radius：碰撞半径。
        public EnemyAIType AI; // 敌人的行为类型。
        public EnemyCategory Category; // 普通、精英、机制或 Boss 分类。
        public PrimitiveType Shape = PrimitiveType.Sphere; // 占位外观使用的基础几何体类型。
        public Vector3 Size = new Vector3(1f, 0.8f, 1f); // 占位外观的局部缩放。

        // 作用：记录敌人基础规格，其余参数使用字段默认值或对象初始化器补齐；返回：无返回值（构造函数）。
        public EnemySpec(string code, string id, string name, EnemyAIType ai, int hp, float speed,
            int damage, float interval, int level)
        {
            // 将标识、行为与基础战斗参数配成一条规格，特殊技能参数留给初始化器补齐。
            Code = code; Id = id; Name = name; AI = ai; HP = hp; Speed = speed;
            Damage = damage; Interval = interval; Level = level;
        }
    }

    // 作用：构造阶段三敌人占位资源的规格清单；返回：包含十二类敌人参数的新数组。
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
    }; // 逐类组合基础规格与技能、外观差异，供生成流程统一遍历。

    // 作用：检查编辑器状态并确认用户意图后执行缺失资源生成；返回：无返回值。
    [MenuItem("Game/阶段三/生成缺失资源")]
    public static void Generate()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling)
        {
            Debug.LogWarning("[阶段三] 请在停止播放且编译结束后生成。");
            return;
        }
        // 菜单入口先确认；实际生成会写入新资产，但跳过已存在路径或 ID。
        if (!EditorUtility.DisplayDialog("阶段三：仅生成缺失资源",
            "创建盾/敌人/关卡配置、地图、预告、拾取和毒区占位资源。已有路径和同 ID 配置一律跳过，不修改旧玩家、枪、子弹或 UI。\n\n" + GoldNotice,
            "确认生成", "取消")) return;
        GenerateAssets();
    }

    // 作用：无确认框地生成并保存缺失资源，供菜单与自动化共用；返回：无返回值。
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
            // 先扫描全项目 ID，再按依赖顺序创建材质、预制体和配置；不以新值覆盖旧资产。
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
            // 已创建的资产不回滚；始终列出旧资产需人工调整的字段，不调用全局 SaveAssets。
            PrintManualChanges(specs[0]);
            Debug.LogWarning("[阶段三] " + GoldNotice);
            Debug.Log($"[阶段三] 已新建 {sCreated} 项；每项创建/跳过路径见日志。未保存任何旧资产。请由用户执行 Unity 编译与 Play 验收。\n"
                + "敌弹仍为 Prefabs/Bullet；毒区=" + PoisonArea.PrefabPath + "；当前分裂预告复用敌人 EnemyTelegraph，无独立加载路径。\n"
                + "组内生成间隔 0.35s、未定射程/恢复/瞬移距离及 B01 间隔字段 5s 为显式试玩默认；Boss 实际按技能循环推进。");
        }
    }

    // 作用：扫描指定类型配置的序列化标识并报告重复或目录异常；返回：首次出现的 ID 到资产路径映射。
    private static Dictionary<string, string> ScanIds<T>(string field) where T : ScriptableObject
    {
        // 按序列化字段读 ID，包含子资产；目录外同 ID 也阻止生成，避免静默制造重复。
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

    // 作用：判断目标路径是否已占用以保护原有内容；返回：true 表示应跳过，false 表示没有资产、文件、目录或 meta 占用。
    private static bool SkipPath(string path)
    {
        // 同时检查资源库和磁盘，未导入文件与孤立 meta 也不覆盖。
        if (AssetDatabase.LoadMainAssetAtPath(path) == null && !File.Exists(path) && !Directory.Exists(path)
            && !File.Exists(path + ".meta")) return false;
        Debug.Log("[阶段三] 跳过已有路径（含未导入文件/孤立 meta）：" + path);
        return true;
    }

    // 作用：递归补齐 Unity 资产目录，创建失败则抛出异常；返回：无返回值。
    private static void EnsureFolder(string path)
    {
        // 已有目录立即复用，缺失时递归补齐父目录再创建当前层。
        if (AssetDatabase.IsValidFolder(path)) return;
        var parent = Path.GetDirectoryName(path).Replace('\\', '/');
        EnsureFolder(parent);
        if (string.IsNullOrEmpty(AssetDatabase.CreateFolder(parent, Path.GetFileName(path))))
            throw new IOException("无法创建资源目录：" + path);
    }

    // 作用：跳过已有 ID 或路径后配置并持久化新的 ScriptableObject；返回：无返回值。
    private static void ConfigAsset<T>(string relative, string id, Dictionary<string, string> ids,
        Action<SerializedObject> configure) where T : ScriptableObject
    {
        // ID 与路径双重防覆盖；配置完成才保存，未持久化的临时对象由 finally 销毁。
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

    // 作用：复用已有材质或按渲染管线创建并保存占位材质；返回：有效的材质资产，已有路径不可用时抛出异常。
    private static Material MaterialAsset(string name, Color color, bool vertexColor = false)
    {
        // 已占用路径只读取不覆盖；新建时区分顶点色与当前渲染管线的着色器。
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

    // 作用：复用已有预制体或在预览场景中构建并保存新预制体；返回：预制体资产，已有路径不可用时抛出异常。
    private static GameObject PrefabAsset(string resourcePath, Action<GameObject> build)
    {
        // 隔离临时根节点，保存后或异常时都销毁节点并关闭预览场景，不改当前场景。
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

    // 作用：累计本轮新建资产数并输出路径；返回：无返回值。
    private static void Created(string path)
    {
        // 先累计已创建数量，再记录路径，让最终总数能对应逐项日志。
        sCreated++; Debug.Log("[阶段三] 新建：" + path);
    }

    // 作用：按值类型写入指定序列化字段，字段缺失或类型不支持则报错；返回：无返回值。
    private static void Set(SerializedObject so, string name, object value)
    {
        // 明确按字段名定位，不猜测替代字段；这里只赋值，统一由调用方应用修改。
        var property = so.FindProperty(name) ?? throw new MissingFieldException(so.targetObject.GetType().Name, name);
        if (value is string text) property.stringValue = text;
        else if (value is int integer) property.intValue = integer;
        else if (value is float number) property.floatValue = number;
        else if (value is bool flag) property.boolValue = flag;
        else if (value is Object reference) property.objectReferenceValue = reference;
        else throw new ArgumentException("不支持的序列化值：" + name);
    }

    // 作用：写入对象的单个序列化字段并立即应用，不记录 Undo；返回：无返回值。
    private static void Assign(Object target, string field, object value)
    {
        // 通过统一的类型分派写入序列化字段，再立即应用到当前装配对象。
        var so = new SerializedObject(target);
        Set(so, field, value);
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    // 作用：将敌人规格与分级掉落默认值写入待应用的序列化配置；返回：无返回值。
    private static void ConfigureEnemy(SerializedObject so, EnemySpec e)
    {
        // Boss 与精英使用保底掉落，其余按普通概率；金币区间仍为待确认占位。
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

    // 作用：写入第一关环境、初始两种枪池、结算奖励及六波生成组；返回：无返回值。
    private static void ConfigureStage(SerializedObject so)
    {
        // 此生成器保留初始两枪配置，不自动迁移后续阶段需要的三枪资源合同。
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

    // 作用：创建并挂载指定位置、缩放与材质的占位几何体，按需保留碰撞体；返回：新建的 GameObject。
    private static GameObject Primitive(Transform parent, string name, PrimitiveType shape, Vector3 position,
        Vector3 size, Material material, bool solid = false)
    {
        // 先统一设置层级、外观与尺寸，再按实体需求移除纯视觉几何体的碰撞体。
        var go = GameObject.CreatePrimitive(shape);
        go.name = name;
        go.transform.SetParent(parent, false);
        go.transform.localPosition = position;
        go.transform.localScale = size;
        go.GetComponent<Renderer>().sharedMaterial = material;
        if (!solid) Object.DestroyImmediate(go.GetComponent<Collider>());
        return go;
    }

    // 作用：创建朝向固定俯视视角、使用指定字体的世界空间标签；返回：新建的 TMP 文本组件。
    private static TMP_Text Label(Transform parent, TMP_FontAsset font, string text, float height)
    {
        // 按给定高度抬升标签并对齐俯视角，再配置居中文字且关闭射线拦截。
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

    // 作用：装配敌人技能预告的线条数组与护盾文字引用；返回：无返回值。
    private static void BuildTelegraph(GameObject root, Material material, TMP_FontAsset font)
    {
        // 五条线依次供攻击、三条支援连线及盾环使用，初始关闭，交由运行时绘制。
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

    // 作用：装配敌人的触发体、运动学刚体、占位外观和预告引用；返回：无返回值。
    private static void BuildEnemy(GameObject root, EnemySpec e, Material material, GameObject telegraph)
    {
        // 外观高度用于抬升标签；复用预告必须有组件，树精额外登记为动态障碍。
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

    // 作用：生成缺失的金币、弹包、护盾与武器拾取占位材质和预制体；返回：无返回值。
    private static void BuildPickups(TMP_FontAsset font)
    {
        // 同一索引对应名称、组件与颜色，护盾和武器额外挂载标签；保存由资产辅助方法完成。
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

    // 作用：装配第一关地面、环道、可破坏岩石、边界和四个入口引用；返回：无返回值。
    private static void BuildMap(GameObject root, Material ground, Material clearing, Material rock, Material trail, TMP_FontAsset font)
    {
        // 用对称几何布局构造占位地图，视觉地面不保留碰撞体，岩石与边界登记障碍。
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

    // 作用：用临时配置打印旧 E01 的目标字段及枪械、子弹人工装配提示；返回：无返回值。
    private static void PrintManualChanges(EnemySpec e01)
    {
        // 只配置新建内存对象来枚举目标值，finally 销毁；不会保存或改写已有 E01 资产。
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
