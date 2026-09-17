#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using QFramework;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

/// <summary>
/// 只读资源预检与隔离的 EditMode 逻辑断言。不会生成/保存资源、实例化 prefab 或访问正式游戏架构。
/// PASS 仅代表该条实际检查；资源装配、纯计算与 Play 验收不能互相替代。
/// </summary>
public static class StageThreeValidation
{
    private const string ResourceMenu = "Game/阶段三/校验资源（只读）";
    private const string LogicMenu = "Game/阶段三/运行逻辑断言";
    private static readonly float[] ShieldCapacities = { 0f, 50f, 100f, 150f, 220f, 300f };

    [MenuItem(ResourceMenu)]
    private static void ValidateResources()
    {
        var report = new Report("资源（只读）");
        report.Note("直接读取 Resources/AssetDatabase，不访问任何 ConfigTable 缓存，不保存或修复资产。");
        // 各部分、各条检查分别捕获，缺一个资源不会阻断其余检查。
        report.Section("子弹", () => ValidateBullets(report));
        report.Section("枪械", () => ValidateWeapons(report));
        report.Section("敌人", () => ValidateEnemies(report));
        report.Section("护盾", () => ValidateShields(report));
        report.Section("Stage1", () => ValidateStage(report));
        report.Section("掉落与攻击资源", () => ValidateCombatPrefabs(report));
        report.Section("HUD", () => ValidateHud(report));
        report.Skip("未执行：场景实例化、Physics、技能行为、波次调度、UI 交互和 Play 验收。");
        report.Finish();
    }

    [MenuItem(LogicMenu)]
    private static void RunLogicAssertions()
    {
        var report = new Report("逻辑断言");
        report.Section("DamageResolver", () => ValidateDamage(report));
        report.Section("真实 GunWeapon 换弹", () => ValidateReloads(report));
        report.Skip("未执行：实际开火/弹丸碰撞、伤害 Command 状态门控、死亡/破盾事件及硬直、AI/毒区/导航。");
        report.Skip("未执行：连续 F、启动倍率快照、暂停、延迟分裂/召唤、清场判定、掉落预算、拾取/槽位及 SafeLoot/结算。");
        report.Note("换弹只手动 Tick 真实 GunWeapon；不代表 WeaponSystem 后台调度或场景生命周期已通过。");
        report.Finish();
    }

    [MenuItem(ResourceMenu, true)]
    [MenuItem(LogicMenu, true)]
    private static bool CanValidate() => !EditorApplication.isPlayingOrWillChangePlaymode && !EditorApplication.isCompiling;

    private static void ValidateBullets(Report report)
    {
        var configs = ReadCatalog<BulletConfig>(report, "Configs/Bullets", c => c.Id);
        report.Check("子弹：恰好 18 个配置及唯一 ID", () =>
        {
            Require(configs.Length == 18, $"实际 {configs.Length} 个，预期 18；检查旧 NormalBullet/错误目录。");
            Require(configs.Select(c => c.Id).Distinct(StringComparer.Ordinal).Count() == 18, "存在重复 ID。");
        });
        foreach (var caliber in new[] { Caliber.S, Caliber.AR, Caliber.L })
        for (var level = 0; level <= 5; level++)
        {
            var expectedCaliber = caliber;
            var expectedLevel = level;
            // 独立拼出合同 ID，避免待测规则同时充当预期值。
            var id = "bullet_" + caliber.ToString().ToLowerInvariant() + "_" + level;
            report.Check("子弹 " + id + "：ID/口径/等级", () =>
            {
                var config = Unique(configs, c => c.Id == id, id);
                Require(config.Caliber == expectedCaliber, $"{id} 口径实际 {config.Caliber}，预期 {expectedCaliber}。");
                Require(config.PenetrationLevel == expectedLevel, $"{id} 穿甲实际 {config.PenetrationLevel}，预期 {expectedLevel}。");
                Require(config.Speed > 0f && !float.IsInfinity(config.Speed), "弹速必须为正的有限值。");
            });
            report.Check("子弹 " + id + "：根节点 Bullet prefab", () =>
                RequirePrefab<Bullet>(Unique(configs, c => c.Id == id, id).PrefabPath));
        }
    }

    private static void ValidateWeapons(Report report)
    {
        var configs = ReadCatalog<WeaponConfig>(report, "Configs/Weapons", c => c.Id);
        ValidateWeapon(report, configs, "pistol", Caliber.S, 100f, 12, 10f, 1.5f, false);
        ValidateWeapon(report, configs, "machinegun", Caliber.AR, 240f, 50, 6f, 2.5f, true);
    }

    private static void ValidateWeapon(Report report, WeaponConfig[] configs, string id, Caliber caliber,
        float durability, int magazine, float damage, float reload, bool automatic)
    {
        report.Check($"枪械 {id}：{caliber} / 耐久{durability} / 容量{magazine} / 伤害{damage}", () =>
        {
            var config = Unique(configs, c => c.Id == id, id);
            Require(config.Caliber == caliber, $"mCaliber 实际 {config.Caliber}，预期 {caliber}。");
            Near(config.DurabilityMax, durability, "mDurabilityMax（旧资产缺省为 0）");
            Require(config.Magazine == magazine, $"mMagazine 实际 {config.Magazine}，预期 {magazine}。");
            Near(config.Damage, damage, "mDamage");
            Near(config.ReloadTime, reload, "mReloadTime");
            Require(config.IsAutomatic == automatic, "mIsAutomatic 不匹配。");
            if (automatic) Near(config.RoundsPerMinute, 600f, "mRoundsPerMinute");
        });
        InspectSerializedFields(report, id + " 磁盘迁移", () => Unique(configs, c => c.Id == id, id),
            new[] { "mCaliber", "mDurabilityMax" }, new[] { "mBulletId" });
    }

    private sealed class EnemyExpected
    {
        public readonly string Code, Id;
        public readonly int Health, Damage, AttackLevel;
        public readonly EnemyAIType AI;
        public readonly EnemyCategory Category;
        public EnemyExpected(string code, string id, int health, int damage, int attackLevel,
            EnemyAIType ai, EnemyCategory category = EnemyCategory.Normal)
        {
            Code = code; Id = id; Health = health; Damage = damage; AttackLevel = attackLevel;
            AI = ai; Category = category;
        }
    }

    private static readonly EnemyExpected[] ExpectedEnemies =
    {
        new EnemyExpected("E01", "slime_green", 30, 10, 0, EnemyAIType.ChaseMelee),
        new EnemyExpected("E02", "slime_red", 40, 12, 1, EnemyAIType.SlimeCharge),
        new EnemyExpected("E03", "goblin", 45, 10, 1, EnemyAIType.ThrowStone),
        new EnemyExpected("E04", "archer", 35, 12, 1, EnemyAIType.Archer),
        new EnemyExpected("E05", "wolf", 50, 12, 1, EnemyAIType.Flank),
        new EnemyExpected("E06", "spider", 40, 8, 1, EnemyAIType.Poison),
        new EnemyExpected("E07", "treant", 140, 18, 2, EnemyAIType.Slam),
        new EnemyExpected("E08", "mage", 65, 14, 2, EnemyAIType.Teleport),
        new EnemyExpected("N01", "ram", 100, 18, 1, EnemyAIType.Ram, EnemyCategory.Elite),
        new EnemyExpected("N02", "drummer", 60, 6, 0, EnemyAIType.ShieldDrummer, EnemyCategory.Mechanism),
        new EnemyExpected("N03", "splitter", 40, 8, 0, EnemyAIType.Split),
        new EnemyExpected("B01", "slime_king", 650, 20, 1, EnemyAIType.SlimeKing, EnemyCategory.Boss)
    };

    private static void ValidateEnemies(Report report)
    {
        var configs = ReadCatalog<EnemyConfig>(report, "Configs/Enemies", c => c.Id);
        foreach (var expected in ExpectedEnemies)
        {
            var e = expected;
            var label = e.Code + " / " + e.Id;
            report.Check(label + "：唯一 ID/属性/行为类型", () =>
            {
                var config = Unique(configs, c => c.Id == e.Id, label);
                Require(config.MaxHP == e.Health, $"HP 实际 {config.MaxHP}，预期 {e.Health}。");
                Require(config.ContactDamage == e.Damage, $"伤害实际 {config.ContactDamage}，预期 {e.Damage}。");
                Require(config.AttackLevel == e.AttackLevel, $"攻击等级实际 {config.AttackLevel}，预期 {e.AttackLevel}。");
                Require(config.AIType == e.AI, $"AI 实际 {config.AIType}，预期 {e.AI}。");
                Require(config.Category == e.Category, $"类别实际 {config.Category}，预期 {e.Category}。");
            });
            report.Check(label + "：配置路径对应根节点 Enemy prefab", () =>
                RequirePrefab<Enemy>(Unique(configs, c => c.Id == e.Id, label).PrefabPath));
            report.Check(label + "：EnemyTelegraph 静态装配（非表现验收）", () =>
            {
                var prefab = RequirePrefab<Enemy>(Unique(configs, c => c.Id == e.Id, label).PrefabPath);
                var telegraphs = prefab.GetComponentsInChildren<EnemyTelegraph>(true);
                var visual = Unique(telegraphs, t => t != null, label + " EnemyTelegraph");
                // 当前代码字段：mLines[0] 技能、[1..3] 连线、[4] 盾环；mShieldLabel 盾信息。
                var serialized = new SerializedObject(visual);
                var lines = Property(serialized, "mLines");
                Require(lines.isArray && lines.arraySize >= 5, "EnemyTelegraph.mLines 至少需要 5 条线。");
                for (var i = 0; i < 5; i++)
                    Require(lines.GetArrayElementAtIndex(i).objectReferenceValue is LineRenderer,
                        "EnemyTelegraph.mLines[" + i + "] 未绑定 LineRenderer。");
                Require(Property(serialized, "mShieldLabel").objectReferenceValue is TMP_Text,
                    "EnemyTelegraph.mShieldLabel 未绑定 TMP_Text。");
            });
        }
        InspectSerializedFields(report, "E01 旧资产缺省字段", () => Unique(configs, c => c.Id == "slime_green", "E01"),
            new[] { "mCategory", "mAttackLevel", "mGoldMin", "mGoldMax", "mAmmoChance", "mAmmoLevelMin",
                "mAmmoLevelMax", "mAmmoCountMin", "mAmmoCountMax", "mShieldChance", "mWeaponChance",
                "mShieldLevelMin", "mShieldLevelMax" }, new[] { "mExpValue" });
        report.Check("E01：掉落字段不可沿用旧资产的零缺省", () =>
        {
            var config = Unique(configs, c => c.Id == "slime_green", "E01");
            Require(config.GoldMin > 0 && config.GoldMax >= config.GoldMin, "mGoldMin/mGoldMax 必须提供有效金币区间。");
            Near(config.AmmoChance, 0.55f, "mAmmoChance");
            Require(config.AmmoLevelMin == 0 && config.AmmoLevelMax == 1, "子弹掉落等级必须为 0～1。");
            Require(config.AmmoCountMin == 10 && config.AmmoCountMax == 20, "子弹掉落数量必须为 10～20。");
            Near(config.ShieldChance, 0.03f, "mShieldChance");
            Near(config.WeaponChance, 0.01f, "mWeaponChance");
            Require(config.ShieldLevelMin == 1 && config.ShieldLevelMax == 1, "普通掉落盾级必须为 1。");
        });
    }

    private static void ValidateShields(Report report)
    {
        var configs = ReadCatalog<ShieldConfig>(report, "Configs/Shields", c => c.Level.ToString());
        report.Check("护盾：恰好 5 个等级", () => Require(configs.Length == 5, $"实际 {configs.Length} 个，预期 5。"));
        for (var level = 1; level <= 5; level++)
        {
            var expected = level;
            report.Check($"护盾 {level}：容量 {ShieldCapacities[level]}", () =>
                Near(Unique(configs, c => c.Level == expected, "盾" + expected).Capacity,
                    ShieldCapacities[expected], "mCapacity"));
        }
    }

    // 这里只描述正式资源合同，不复制/模拟 EnemySpawnSystem 的调度实现。
    private static WaveConfig[] ExpectedWaves() => new[]
    {
        new WaveConfig(1, 0f, new[] { new SpawnGroup("slime_green", 18, 0f) }),
        new WaveConfig(2, 45f, new[] { new SpawnGroup("slime_green", 16, 0f), new SpawnGroup("slime_red", 4, 0f), new SpawnGroup("ram", 1, 0f, 1) }),
        new WaveConfig(3, 45f, new[] { new SpawnGroup("goblin", 14, 0f), new SpawnGroup("drummer", 2, 0f, 1) }),
        new WaveConfig(4, 50f, new[] { new SpawnGroup("splitter", 8, 0f), new SpawnGroup("slime_green", 18, 0f, 1) }),
        new WaveConfig(5, 50f, new[] { new SpawnGroup("archer", 8, 0f), new SpawnGroup("ram", 2, 0f, 1), new SpawnGroup("drummer", 2, 0f, 1), new SpawnGroup("slime_green", 20, 0f) }),
        new WaveConfig(6, 60f, new[] { new SpawnGroup("slime_king", 1, 0f, 2) }, true)
    };

    private static void ValidateStage(Report report)
    {
        var stages = ReadCatalog<StageConfig>(report, "Configs/Stages", c => c.Level.ToString());
        Func<StageConfig> stage = () => Unique(stages, c => c.Level == 1, "Stage1");
        var expected = ExpectedWaves();
        report.Check("Stage1：5 个普通波 + Boss", () =>
            Require(stage().Waves != null && stage().Waves.Count == 6, "mWaves 必须恰好 6 波。"));
        for (var i = 0; i < expected.Length; i++)
        {
            var index = i;
            var contract = expected[i];
            report.Check($"Stage1 波{contract.Wave}：序号/间隔{contract.IntervalFromPrev}s/Boss 标记/组数", () =>
            {
                var wave = WaveAt(stage(), index);
                Require(wave.Wave == contract.Wave, "波序号与列表顺序不匹配。");
                Near(wave.IntervalFromPrev, contract.IntervalFromPrev, "距上一波实际启动间隔");
                Require(wave.IsBoss == contract.IsBoss, "Boss 标记不匹配。");
                Require(wave.Groups != null && wave.Groups.Count == contract.Groups.Count, "生成组数不匹配。");
            });
            for (var g = 0; g < contract.Groups.Count; g++)
            {
                var groupIndex = g;
                var wanted = contract.Groups[g];
                report.Check($"Stage1 波{contract.Wave}/组{g + 1}：{wanted.EnemyId}×{wanted.Count}/盾{wanted.ShieldLevel}", () =>
                {
                    var wave = WaveAt(stage(), index);
                    Require(wave.Groups != null && wave.Groups.Count > groupIndex, "缺少该生成组。");
                    var actual = wave.Groups[groupIndex];
                    Require(actual != null, "生成组为空。");
                    Require(actual.EnemyId == wanted.EnemyId && actual.Count == wanted.Count && actual.ShieldLevel == wanted.ShieldLevel,
                        $"实际 {actual.EnemyId}×{actual.Count}/盾{actual.ShieldLevel}。");
                    Require(actual.Interval >= 0f && !float.IsInfinity(actual.Interval), "组内生成间隔必须为非负有限值。");
                });
            }
        }
        report.Check("Stage1：资源初始人头 114", () =>
            Require(StageGroups(stage()).Sum(g => g.Count) == 114, "正式组初始人数不是 114。"));
        report.Check("Stage1：初始 HP 4740（非生成/战斗断言）", () =>
        {
            var enemies = Resources.LoadAll<EnemyConfig>("Configs/Enemies");
            var health = StageGroups(stage()).Sum(g => g.Count * (float)Unique(enemies, e => e.Id == g.EnemyId, g.EnemyId).MaxHP);
            Near(health, 4740f, "正式组初始总 HP");
        });
        report.Check("Stage1：初始护盾总容量 1350", () =>
        {
            var shields = Resources.LoadAll<ShieldConfig>("Configs/Shields");
            var capacity = StageGroups(stage()).Sum(g => g.ShieldLevel == 0 ? 0f
                : g.Count * Unique(shields, s => s.Level == g.ShieldLevel, "盾" + g.ShieldLevel).Capacity);
            Near(capacity, 1350f, "正式组总盾容量");
        });
        report.Check("Stage1：派生体上限预算 36，总出生预算 150（仅算术）", () =>
        {
            var groups = StageGroups(stage());
            var splitters = groups.Where(g => g.EnemyId == "splitter").Sum(g => g.Count);
            var bosses = groups.Where(g => g.EnemyId == "slime_king").Sum(g => g.Count);
            Require(splitters == 8 && bosses == 1, "必须为 8 个 N03 和 1 个 B01。");
            var children = splitters * 3;
            var summons = bosses * 2 * 6;
            Require(children == 24 && summons == 12 && groups.Sum(g => g.Count) + children + summons == 150,
                "N03×3、Boss 两批×6 的合同预算不匹配。");
            Near(children * 10f + summons * 30f, 600f, "派生体额外 HP 预算");
        });
        report.Note("150 是资源人数结合设计上限得到的预算；未执行阈值、延迟出生或清场调度，不能据此认定运行时最多出生 150。");
        report.Check("Stage1：枪池仅 pistol/machinegun，结算奖励 500", () =>
        {
            var config = stage();
            Require(config.WeaponIds != null && config.WeaponIds.Count == 2
                && config.WeaponIds.OrderBy(id => id, StringComparer.Ordinal).SequenceEqual(new[] { "machinegun", "pistol" }),
                "本阶段枪池必须恰好为 pistol 与 machinegun。");
            Require(config.ClearBonus == 500, "mClearBonus 应为 500。");
        });
        report.Check("Stage1：从 EnvironmentPath 读取场地 prefab（非导航验收）", () =>
        {
            var prefab = RequirePrefab<StageEnvironment>(stage().EnvironmentPath);
            var environment = prefab.GetComponent<StageEnvironment>();
            Near(environment.HalfSize, 45f, "场地 HalfSize（90×90）");
            Require(prefab.GetComponent<BattleNavigation>() != null, "场地根节点缺少 BattleNavigation。");
            Require(environment.Entrances.Count >= 2 && environment.Entrances.All(t => t != null), "需要至少两个非空入口引用。");
        });
    }

    private static WaveConfig WaveAt(StageConfig stage, int index)
    {
        Require(stage.Waves != null && stage.Waves.Count > index && stage.Waves[index] != null, "缺少波 " + (index + 1));
        return stage.Waves[index];
    }

    private static SpawnGroup[] StageGroups(StageConfig stage)
    {
        Require(stage.Waves != null && stage.Waves.Count == 6, "汇总依赖完整六波。");
        var groups = new List<SpawnGroup>();
        for (var i = 0; i < stage.Waves.Count; i++)
        {
            var wave = WaveAt(stage, i);
            Require(wave.Groups != null && wave.Groups.Count > 0, "波内没有生成组。");
            foreach (var group in wave.Groups)
            {
                Require(group != null && group.Count > 0 && group.ShieldLevel >= 0 && group.ShieldLevel <= 5, "存在无效生成组。");
                groups.Add(group);
            }
        }
        return groups.ToArray();
    }

    private static void ValidateCombatPrefabs(Report report)
    {
        report.Check("GoldPickup：GameRoot 的 StageThree 路径", () => RequirePrefab<GoldPickup>("Prefabs/StageThree/GoldPickup"));
        report.Check("AmmoPackPickup：GameRoot 的 StageThree 路径", () => RequirePrefab<AmmoPackPickup>("Prefabs/StageThree/AmmoPackPickup"));
        report.Check("ShieldPickup：GameRoot 的 StageThree 路径", () => RequirePrefab<ShieldPickup>("Prefabs/StageThree/ShieldPickup"));
        report.Check("WeaponPickup：GameRoot 的 StageThree 路径", () => RequirePrefab<WeaponPickup>("Prefabs/StageThree/WeaponPickup"));
        report.Check("敌人投射物：当前系统资源路径及根节点 Bullet", () => RequirePrefab<Bullet>(EnemySpawnSystem.ProjectilePrefabPath));
        report.Check("PoisonArea：当前常量路径及 Renderer 装配", () =>
        {
            var prefab = RequirePrefab<PoisonArea>(PoisonArea.PrefabPath);
            var serialized = new SerializedObject(prefab.GetComponent<PoisonArea>());
            // Awake 有 GetComponentInChildren<Renderer> 兜底；只检查可用引用，不调用 Awake。
            Require(Property(serialized, "mRenderer").objectReferenceValue is Renderer
                || prefab.GetComponentInChildren<Renderer>() != null, "mRenderer 未绑定且没有可用子 Renderer。");
        });
        report.Note("EnemyTelegraph/PoisonArea 只查已核实的序列化引用，不声明视觉效果、毒区减速或池复用通过。");
    }

    private static void ValidateHud(Report report)
    {
        Func<GameObject> hud = () => RequirePrefab<Game.UI.GameHUD>("UI/GameHUD");
        ValidateHudNode<Image>(report, hud, "ShieldFill");
        ValidateHudNode<TextMeshProUGUI>(report, hud, "ShieldText");
        ValidateHudNode<Button>(report, hud, "SafeLootButton");
        report.Check("HUD：WavePreviewText 预告区域高度至少 96", () =>
        {
            var node = NamedNode(hud(), "WavePreviewText");
            var rect = node.GetComponent<RectTransform>();
            Require(rect != null, "WavePreviewText 缺少 RectTransform。");
            Require(rect.rect.height >= 96f, $"当前 prefab 预告区域高度 {rect.rect.height:0.##}，至少需要 96；未运行布局重建。");
        }, true);
        report.Skip("HUD 新字段的运行时订阅/按钮点击接线未执行；有节点、Bind 或序列化引用都不等于业务已接通。");
    }

    private static void ValidateHudNode<T>(Report report, Func<GameObject> hud, string name) where T : Component
    {
        report.Check("HUD 待装配检查：" + name + " 节点/组件", () =>
        {
            var component = NamedNode(hud(), name).GetComponent<T>();
            Require(component != null, name + " 缺少 " + typeof(T).Name);
            if (component is Image image) Require(image.type == Image.Type.Filled, name + " 应使用 Filled Image。");
        }, true);
        report.Check("HUD 待装配检查：" + name + " Bind", () =>
        {
            // AbstractBind.TypeName getter 会写 mComponentName；只读检查禁止调用该 getter。
            Require(NamedNode(hud(), name).GetComponent<Bind>() != null, name + " 缺少 QFramework.Bind。");
        }, true);
        report.Check("HUD 待生成/装配检查：" + name + " 序列化引用", () =>
        {
            var prefab = hud();
            var field = new SerializedObject(prefab.GetComponent<Game.UI.GameHUD>()).FindProperty(name);
            Require(field != null, "Designer 尚无 " + name + " 字段；请用户装配 Bind 后生成，不由校验器补字段。");
            var component = NamedNode(prefab, name).GetComponent<T>();
            Require(component != null && field.objectReferenceValue == component, name + " 字段未指向对应节点组件。");
        }, true);
    }

    private static Transform NamedNode(GameObject prefab, string name) =>
        Unique(prefab.GetComponentsInChildren<Transform>(true), t => t.name == name, "HUD 节点 " + name);

    private static void ValidateDamage(Report report)
    {
        foreach (var faction in new[] { CombatFaction.Player, CombatFaction.Enemy })
        for (var attack = 0; attack <= 5; attack++)
        for (var shield = 0; shield <= 5; shield++)
        {
            var attacker = faction;
            var attackLevel = attack;
            var shieldLevel = shield;
            report.Check($"伤害矩阵 {attacker} 攻{attackLevel}/盾{shieldLevel}：分配/守恒/破盾边界", () =>
            {
                var capacities = shieldLevel == 0 ? new[] { 0f }
                    : new[] { 0f, 0.125f, 10f, ShieldCapacities[shieldLevel], 300f };
                foreach (var capacity in capacities)
                foreach (var amount in new[] { 0f, 0.1f, 37.25f, 100f })
                {
                    var hasShield = shieldLevel > 0 && capacity > 0f;
                    var effective = !hasShield && attacker == CombatFaction.Player && attackLevel == 0 ? amount * 1.5f : amount;
                    var ratio = attackLevel < shieldLevel ? 1f : attackLevel == shieldLevel ? 0.7f : 0.3f;
                    var expectedShield = hasShield ? Mathf.Min(capacity, amount * ratio) : 0f;
                    AssertDamage(new DamageInfo(amount, attackLevel, attacker, true), shieldLevel, capacity,
                        effective - expectedShield, expectedShield, hasShield && expectedShield >= capacity);
                }
            });
        }
        DamageExample(report, "100伤害/盾1容量50/低级", new DamageInfo(100, 0, CombatFaction.Player, true), 1, 50, 50, 50, true);
        DamageExample(report, "100伤害/盾1容量50/同级", new DamageInfo(100, 1, CombatFaction.Player, true), 1, 50, 50, 50, true);
        DamageExample(report, "100伤害/盾1容量50/高级", new DamageInfo(100, 2, CombatFaction.Player, true), 1, 50, 70, 30, false);
        DamageExample(report, "100伤害/盾1余10/同级溢出", new DamageInfo(100, 1, CombatFaction.Player, true), 1, 10, 90, 10, true);
        DamageExample(report, "100伤害/无盾玩家肉弹=150", new DamageInfo(100, 0, CombatFaction.Player, true), 0, 0, 150, 0, false);
        DamageExample(report, "100伤害/敌人0级非弹丸不加成", new DamageInfo(100, 0, CombatFaction.Enemy), 0, 0, 100, 0, false);
        DamageExample(report, "100伤害/敌人0级弹丸不加成", new DamageInfo(100, 0, CombatFaction.Enemy, true), 0, 0, 100, 0, false);
        DamageExample(report, "玩家0级非子弹不加成", new DamageInfo(100, 0, CombatFaction.Player), 0, 0, 100, 0, false);
        DamageExample(report, "等级0但残留容量不作为有效盾", new DamageInfo(100, 0, CombatFaction.Player, true), 0, 50, 150, 0, false);
        DamageExample(report, "负伤害归零且不破盾", new DamageInfo(-10, 5, CombatFaction.Player, true), 1, 10, 0, 0, false);
        for (var level = 1; level <= 5; level++)
        {
            var shieldLevel = level;
            report.Check($"连续命中盾{level}：正盾→0仅一次，破盾当发不补肉弹倍率", () =>
            {
                var shield = 25f;
                var health = 1000f;
                var breaks = 0;
                var expectedHealth = new[] { 0f, 0f, 5f, 15f };
                for (var i = 0; i < 4; i++)
                {
                    var result = DamageResolver.Calculate(new DamageInfo(10f, 0, CombatFaction.Player, true), shieldLevel, shield);
                    Near(result.HealthDamage, expectedHealth[i], "连续命中第" + (i + 1) + "发血伤");
                    Near(result.ShieldDamage, i < 2 ? 10f : i == 2 ? 5f : 0f, "连续命中盾伤");
                    Require(result.BrokeShield == (i == 2), "只允许第三发给出破盾标记。");
                    shield -= result.ShieldDamage;
                    health -= result.HealthDamage;
                    if (result.BrokeShield) breaks++;
                }
                Require(breaks == 1, "破盾次数必须为 1。");
                Near(shield, 0f, "最终盾量");
                Near(health, 980f, "最终血量");
            });
        }
    }

    private static void DamageExample(Report report, string name, DamageInfo hit, int level, float capacity,
        float health, float shield, bool broke) =>
        report.Check(name, () => AssertDamage(hit, level, capacity, health, shield, broke));

    private static void AssertDamage(DamageInfo hit, int level, float capacity, float health, float shield, bool broke)
    {
        var actual = DamageResolver.Calculate(hit, level, capacity);
        var label = $"伤害{hit.Amount}/攻{hit.PenetrationLevel}/盾{level}/余{capacity}";
        Near(actual.HealthDamage, health, label + " 血伤");
        Near(actual.ShieldDamage, shield, label + " 盾伤");
        Near(actual.HealthDamage + actual.ShieldDamage, health + shield, label + " 血盾守恒");
        Require(actual.HealthDamage >= 0f && actual.ShieldDamage >= 0f && actual.ShieldDamage <= Mathf.Max(0f, capacity), label + " 超过容量或负伤害。");
        Require(actual.BrokeShield == broke, label + " 破盾标记错误。");
    }

    private static void ValidateReloads(Report report)
    {
        GunCase(report, "同级保留余弹、仅预扣缺口、重复请求不重扣、满弹不再装填", f =>
        {
            f.Seed(0, 20); f.SetResource(5); f.ClearEvents();
            f.Gun.RequestReload();
            f.State(5, true); f.Count(0, 13); f.InventoryEvent(0, 0, 13);
            Require(f.Shortages.Count == 0, "足量补弹不应发送缺弹提示。");
            f.Gun.RequestReload();
            Require(f.Changes.Count == 1, "重复请求产生额外库存事件。");
            f.Gun.Tick(0.5f); f.State(5, true);
            f.Gun.Tick(0.6f); f.State(12, false); f.Count(0, 13);
            Require(f.Gun.LoadedLevel == 0, "同级补弹变更了等级。");
            Near(f.Gun.Resource + f.Inventory.GetCount(Caliber.S, 0), 25, "余弹+库存守恒");
            f.Gun.RequestReload(); f.Gun.Tick(10f); f.State(12, false);
            Require(f.Changes.Count == 1, "满弹请求或完成后的 Tick 不得再次扣库存。");
        });
        GunCase(report, "异级退回旧弹，再预扣新级整夹", f =>
        {
            f.PrepareLevelOneWithFourRounds(); f.Seed(2, 20); f.ClearEvents();
            f.Gun.CycleNextLoadLevel(); f.Gun.RequestReload();
            f.State(0, true); f.Count(1, 12); f.Count(2, 8);
            Require(f.Changes.Count == 2, "应依次发生退旧弹和预扣新弹两个事件。");
            f.InventoryEvent(0, 1, 12); f.InventoryEvent(1, 2, 8);
            f.Gun.Tick(1.1f); f.State(12, false);
            Require(f.Gun.LoadedLevel == 2, "未装入选定的新等级。");
            Near(f.Gun.Resource + f.Inventory.GetCount(Caliber.S, 1) + f.Inventory.GetCount(Caliber.S, 2), 32f, "换级库存守恒");
        });
        GunCase(report, "部分装填发送实际预扣数/缺口/槽位事件", f =>
        {
            f.Seed(0, 3); f.SetResource(5); f.ClearEvents();
            f.Gun.RequestReload(); f.State(5, true); f.Count(0, 0);
            f.Shortage(3, 7); f.InventoryEvent(0, 0, 0);
            f.Gun.Tick(1.1f); f.State(8, false); f.Count(0, 0);
            Require(f.Shortages.Count == 1 && f.Changes.Count == 1, "完成装填不应重复发送库存或缺弹事件。");
        });
        GunCase(report, "完全缺弹提示但不进入装填", f =>
        {
            f.SetResource(5); f.ClearEvents();
            f.Gun.RequestReload(); f.State(5, false); f.Shortage(0, 7);
            f.Gun.Tick(10f); f.State(5, false);
            Require(f.Changes.Count == 0, "完全缺弹不应改变库存。");
        });
        GunCase(report, "B 只改下次等级，不改 pending 预扣等级", f =>
        {
            f.Seed(1, 20); f.Seed(2, 20); f.ClearEvents();
            f.Gun.CycleNextLoadLevel(); f.Gun.RequestReload(); f.Count(1, 8);
            f.Gun.CycleNextLoadLevel();
            Require(f.Gun.NextLoadLevel == 2, "B 未切换下一次等级。");
            f.Gun.Tick(1.1f); f.State(12, false); f.Count(1, 8); f.Count(2, 20);
            Require(f.Gun.LoadedLevel == 1 && f.Gun.NextLoadLevel == 2, "B 修改了本次 pending 等级。");
            f.Gun.RequestReload(); f.Gun.Tick(1.1f);
            Require(f.Gun.LoadedLevel == 2, "下一次没有使用新选择等级。");
            f.State(12, false); f.Count(1, 20); f.Count(2, 8);
        });
        GunCase(report, "同级取消仅退 pending，保留原余弹且取消幂等", f =>
        {
            f.Seed(0, 20); f.SetResource(5); f.ClearEvents();
            f.Gun.RequestReload(); f.Count(0, 13);
            f.Gun.RefundPendingLoad(); f.State(5, false); f.Count(0, 20);
            f.InventoryEvent(1, 0, 20);
            f.Gun.RefundPendingLoad(); f.Gun.Tick(10f);
            f.State(5, false); f.Count(0, 20);
            Require(f.Changes.Count == 2, "重复取消重复退还了子弹。");
        });
        GunCase(report, "换级后再按 B 并取消：退原 pending 桶，不二次退旧余弹", f =>
        {
            f.PrepareLevelOneWithFourRounds(); f.Seed(2, 20); f.ClearEvents();
            f.Gun.CycleNextLoadLevel(); f.Gun.RequestReload();
            f.Gun.CycleNextLoadLevel();
            f.Gun.RefundPendingLoad(); f.Gun.RefundPendingLoad(); f.Gun.Tick(10f);
            f.State(0, false); f.Count(1, 12); f.Count(2, 20); f.Count(3, 0);
            Require(f.Gun.NextLoadLevel == 3, "下次选择被取消操作回写。");
            Require(f.Changes.Count == 3, "退旧、预扣、取消应各产生一次库存事件。");
            f.InventoryEvent(2, 2, 20);
        });
        GunCase(report, "报废枪不再开始装填", f =>
        {
            f.Seed(0, 20); f.ClearEvents(); f.Gun.Wear(100f);
            Require(f.Gun.IsBroken, "测试初态未报废。");
            f.Gun.RequestReload(); f.Gun.Tick(10f); f.State(0, false); f.Count(0, 20);
            Require(f.Changes.Count == 0 && f.Shortages.Count == 0, "报废后产生装填副作用。");
        });
        GunCase(report, "装填中报废会退 pending，不完成装填、不再扣弹", f =>
        {
            f.Seed(0, 20); f.SetResource(5); f.ClearEvents();
            f.Gun.RequestReload(); f.Count(0, 13); f.Gun.Wear(100f);
            f.Gun.Tick(10f); f.State(5, false); f.Count(0, 20);
            f.Gun.RequestReload(); f.Gun.Tick(10f);
            f.State(5, false); f.Count(0, 20);
            Require(f.Changes.Count == 2 && f.Shortages.Count == 0, "报废处理重复扣/退弹。");
        });
        GunCase(report, "完成后取消不退已经装入的子弹", f =>
        {
            f.Seed(0, 20); f.ClearEvents(); f.Gun.RequestReload(); f.Gun.Tick(1.1f);
            f.Gun.RefundPendingLoad(); f.Gun.Tick(10f);
            f.State(12, false); f.Count(0, 8);
            Require(f.Changes.Count == 1, "取消退回了已装入弹夹的子弹。");
        });
    }

    private static void GunCase(Report report, string name, Action<GunFixture> test)
    {
        report.Check("GunWeapon：" + name, () =>
        {
            using (var fixture = new GunFixture()) test(fixture);
        });
    }

    // QFramework.cs 的真实入口是 Interface / RegisterModel / RegisterSystem / Deinit。
    // 此泛型闭包只属于测试；不注册正式架构、不注册玩家/经济/存档模型，不注册任何池工厂。
    private sealed class ReloadTestArchitecture : Architecture<ReloadTestArchitecture>
    {
        public ReloadTestArchitecture() { }
        protected override void Init()
        {
            RegisterModel<IBulletInventoryModel>(new BulletInventoryModel());
            RegisterSystem<IGameObjectPoolSystem>(new GameObjectPoolSystem());
        }
    }

    private sealed class GunFixture : IDisposable
    {
        private WeaponConfig mConfig;
        private IArchitecture mArchitecture;
        private IUnRegister mInventorySubscription, mShortageSubscription;
        public GunWeapon Gun { get; private set; }
        public IBulletInventoryModel Inventory { get; private set; }
        public readonly List<BulletInventoryChangedEvent> Changes = new List<BulletInventoryChangedEvent>();
        public readonly List<AmmoShortageEvent> Shortages = new List<AmmoShortageEvent>();

        public GunFixture()
        {
            try
            {
                mConfig = ScriptableObject.CreateInstance<WeaponConfig>();
                mConfig.hideFlags = HideFlags.HideAndDontSave;
                var serialized = new SerializedObject(mConfig);
                Property(serialized, "mId").stringValue = "stage_three_validation_memory_only";
                Property(serialized, "mName").stringValue = "内存换弹测试";
                Property(serialized, "mCaliber").intValue = (int)Caliber.S;
                Property(serialized, "mMagazine").intValue = 12;
                Property(serialized, "mReloadTime").floatValue = 1f;
                Property(serialized, "mDurabilityMax").floatValue = 100f;
                Property(serialized, "mDamage").floatValue = 10f;
                Property(serialized, "mRoundsPerMinute").floatValue = 0f;
                Property(serialized, "mIsAutomatic").boolValue = false;
                // 仅应用到自己创建的非持久化 SO，不进入 Undo、不保存任何资产。
                serialized.ApplyModifiedPropertiesWithoutUndo();
                mArchitecture = ReloadTestArchitecture.Interface;
                Inventory = mArchitecture.GetModel<IBulletInventoryModel>();
                Require(Inventory != null && mArchitecture.GetSystem<IGameObjectPoolSystem>() != null, "测试架构注册失败。");
                foreach (var caliber in new[] { Caliber.S, Caliber.AR, Caliber.L })
                for (var level = 0; level <= 5; level++)
                    mArchitecture.SendCommand(new TakeBulletsCommand(caliber, level, Inventory.GetCount(caliber, level)));
                mInventorySubscription = mArchitecture.RegisterEvent<BulletInventoryChangedEvent>(e => Changes.Add(e));
                mShortageSubscription = mArchitecture.RegisterEvent<AmmoShortageEvent>(e => Shortages.Add(e));
                Gun = new GunWeapon(mConfig, mArchitecture, null) { SlotIndex = 4 };
                State(0f, false);
                Require(Gun.LoadedLevel == 0 && Gun.NextLoadLevel == 0, "初始装填等级不为 0。");
            }
            catch
            {
                Dispose();
                throw;
            }
        }

        public void Seed(int level, int count) => mArchitecture.SendCommand(new AddBulletsCommand(Caliber.S, level, count));
        public void Count(int level, int expected) =>
            Require(Inventory.GetCount(Caliber.S, level) == expected, $"S·{level} 库存实际 {Inventory.GetCount(Caliber.S, level)}，预期 {expected}。");
        public void ClearEvents() { Changes.Clear(); Shortages.Clear(); }
        public void State(float resource, bool reloading)
        {
            Near(Gun.Resource, resource, "弹夹余弹");
            Require(Gun.IsReloading == reloading, "IsReloading 不匹配。");
            Require(Gun.State == (reloading ? WeaponState.Reloading : WeaponState.Ready), "WeaponState 不匹配。");
        }
        public void SetResource(float value)
        {
            // 只设置已知测试初态，模拟已有余弹；绝不调用 TryAttack/DoAttack 或创建 owner。
            var property = typeof(WeaponBase).GetProperty(nameof(WeaponBase.Resource), BindingFlags.Public | BindingFlags.Instance);
            var setter = property?.GetSetMethod(true);
            Require(setter != null, "WeaponBase.Resource 非公开 setter 不可用。");
            setter.Invoke(Gun, new object[] { value });
        }
        public void PrepareLevelOneWithFourRounds()
        {
            Seed(1, 20); Gun.CycleNextLoadLevel(); Gun.RequestReload(); Gun.Tick(1.1f);
            Require(Gun.LoadedLevel == 1, "换级测试准备装填失败。");
            SetResource(4); Count(1, 8);
        }
        public void InventoryEvent(int index, int level, int count)
        {
            Require(Changes.Count > index, "缺少库存变化事件 " + index);
            var e = Changes[index];
            Require(e.Caliber == Caliber.S && e.Level == level && e.Count == count,
                $"库存事件实际 {e.Caliber}·{e.Level}={e.Count}，预期 S·{level}={count}。");
        }
        public void Shortage(int loaded, int wanted)
        {
            Require(Shortages.Count == 1, "应恰好发送一次 AmmoShortageEvent。");
            var e = Shortages[0];
            Require(e.SlotIndex == 4 && e.Loaded == loaded && e.Wanted == wanted,
                $"缺弹事件实际 slot={e.SlotIndex}, loaded={e.Loaded}, wanted={e.Wanted}。");
        }
        public void Dispose()
        {
            try
            {
                mInventorySubscription?.UnRegister();
                mShortageSubscription?.UnRegister();
            }
            finally
            {
                try
                {
                    // IArchitecture 没有 Dispose；Deinit 释放本测试的模型/系统并重置该泛型的静态实例。
                    mArchitecture?.Deinit();
                }
                finally
                {
                    mArchitecture = null;
                    mInventorySubscription = null;
                    mShortageSubscription = null;
                    if (mConfig != null) Object.DestroyImmediate(mConfig);
                    mConfig = null;
                }
            }
        }
    }

    private static T[] ReadCatalog<T>(Report report, string folder, Func<T, string> id) where T : ScriptableObject
    {
        var configs = Array.Empty<T>();
        report.Check(typeof(T).Name + "：直接读取 Resources/" + folder, () =>
        {
            configs = Resources.LoadAll<T>(folder);
            Require(configs.Length > 0, "Resources/" + folder + " 没有配置。");
        });
        report.Check(typeof(T).Name + "：全项目空/重复 ID（包括旧资产）", () =>
        {
            var all = AssetDatabase.FindAssets("t:" + typeof(T).Name)
                .Select(AssetDatabase.GUIDToAssetPath).Distinct()
                .SelectMany(AssetDatabase.LoadAllAssetsAtPath).OfType<T>().ToArray();
            Require(all.Length > 0, "项目内没有 " + typeof(T).Name);
            var invalid = all.Where(c => string.IsNullOrWhiteSpace(id(c))).Select(AssetDatabase.GetAssetPath).ToArray();
            Require(invalid.Length == 0, "空 ID：" + string.Join(", ", invalid));
            var duplicates = all.GroupBy(id, StringComparer.Ordinal).Where(g => g.Count() > 1)
                .Select(g => g.Key + " => " + string.Join(", ", g.Select(AssetDatabase.GetAssetPath))).ToArray();
            Require(duplicates.Length == 0, "重复 ID：" + string.Join("; ", duplicates));
        });
        return configs;
    }

    private static GameObject RequirePrefab<T>(string path) where T : Component
    {
        Require(!string.IsNullOrWhiteSpace(path), "Resources prefab 路径为空。");
        var prefab = Resources.Load<GameObject>(path);
        Require(prefab != null, "缺少 Resources/" + path + " prefab。");
        Require(AssetDatabase.GetAssetPath(prefab).EndsWith(".prefab", StringComparison.OrdinalIgnoreCase), path + " 不是 prefab 资产。");
        Require(prefab.GetComponent<T>() != null, path + " 根节点缺少 " + typeof(T).Name);
        return prefab;
    }

    private static T Unique<T>(IEnumerable<T> values, Func<T, bool> predicate, string label)
    {
        var matches = values.Where(predicate).ToArray();
        Require(matches.Length == 1, label + " 预期恰好 1 个，实际 " + matches.Length + "。");
        return matches[0];
    }

    private static SerializedProperty Property(SerializedObject serialized, string name)
    {
        var property = serialized.FindProperty(name);
        Require(property != null, serialized.targetObject.GetType().Name + "." + name + " 字段不存在；请复核当前源码，未猜测替代字段。");
        return property;
    }

    private static void InspectSerializedFields(Report report, string label, Func<Object> asset,
        string[] required, string[] obsolete)
    {
        // SerializedObject 会呈现 C# 缺省字段，不能用 FindProperty 成功冒充旧磁盘资产已迁移。
        // YAML 只读；非文本序列化明确 SKIP，不为了检查而强制重新序列化。
        report.Section(label, () =>
        {
            var target = asset();
            var relativePath = AssetDatabase.GetAssetPath(target);
            var projectRoot = Path.GetDirectoryName(Application.dataPath);
            var text = File.ReadAllText(Path.Combine(projectRoot, relativePath));
            if (!text.StartsWith("%YAML", StringComparison.Ordinal))
            {
                report.Skip(label + "：非 YAML，未核实磁盘字段是否显式保存；只检查已加载数值。");
                return;
            }
            Func<string, bool> present = name => Regex.IsMatch(text, @"^\s*" + Regex.Escape(name) + @"\s*:", RegexOptions.Multiline);
            var missing = required.Where(name => !present(name)).ToArray();
            var legacy = obsolete.Where(present).ToArray();
            if (legacy.Length > 0) report.Note(label + "：遗留字段 " + string.Join(", ", legacy) + "；只报告，不清除。");
            report.Check(label + "：显式保存字段", () => Require(missing.Length == 0,
                relativePath + " 缺少 " + string.Join(", ", missing) + "；当前读值来自 C# 缺省，不能视为已迁移。"), true);
        });
    }

    private static void Near(float actual, float expected, string label)
    {
        Require(!float.IsNaN(actual) && !float.IsInfinity(actual) && Mathf.Abs(actual - expected) <= 0.001f,
            $"{label} 实际 {actual}，预期 {expected}。");
    }

    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }

    private sealed class Report
    {
        private readonly string mName;
        private int mPassed, mFailed, mPending, mSkipped;
        public Report(string name) { mName = name; }
        public void Check(string name, Action check, bool pendingOnFailure = false)
        {
            try
            {
                check();
                mPassed++;
                Debug.Log("[阶段三][PASS] " + name);
            }
            catch (Exception exception)
            {
                if (pendingOnFailure)
                {
                    mPending++;
                    Debug.LogWarning("[阶段三][PENDING/待装配或迁移] " + name + "：" + exception.GetBaseException().Message);
                }
                else
                {
                    mFailed++;
                    Debug.LogError("[阶段三][FAIL] " + name + "：" + exception.GetBaseException().Message);
                }
            }
        }
        public void Section(string name, Action section)
        {
            try { section(); }
            catch (Exception exception)
            {
                mFailed++;
                Debug.LogError("[阶段三][FAIL] " + name + " 检查中断：" + exception.GetBaseException().Message);
            }
        }
        public void Note(string message) => Debug.Log("[阶段三][INFO] " + message);
        public void Skip(string message)
        {
            mSkipped++;
            Debug.LogWarning("[阶段三][SKIP/未执行] " + message);
        }
        public void Finish() => Debug.Log($"[阶段三] {mName}：PASS {mPassed} / FAIL {mFailed} / 待装配或迁移 {mPending} / 未执行 {mSkipped}。仅本次逐项结果，不代表阶段三或端到端验收完成。");
    }
}
#endif
