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
    private const string ResourceMenu = "Game/阶段三/校验资源（只读）"; // 只读资源检查菜单路径。
    private const string LogicMenu = "Game/阶段三/运行逻辑断言"; // 隔离逻辑断言菜单路径。
    private static readonly float[] ShieldCapacities = { 0f, 50f, 100f, 150f, 220f, 300f }; // 按护盾等级索引的预期容量，零级表示无盾。

    // 作用：只读检查资源合同与静态装配，不替代完整 Play 验收；返回：无返回值。
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

    // 作用：汇总伤害纯计算与隔离枪械换弹断言，并列出未执行范围；返回：无返回值。
    [MenuItem(LogicMenu)]
    private static void RunLogicAssertions()
    {
        // 分区隔离伤害与换弹断言的失败，再列出未执行范围以限定汇总结论。
        var report = new Report("逻辑断言");
        report.Section("DamageResolver", () => ValidateDamage(report));
        report.Section("真实 GunWeapon 换弹", () => ValidateReloads(report));
        report.Skip("未执行：实际开火/弹丸碰撞、伤害 Command 状态门控、死亡/破盾事件及硬直、AI/毒区/导航。");
        report.Skip("未执行：连续 F、启动倍率快照、暂停、延迟分裂/召唤、清场判定、掉落预算、拾取/槽位及 SafeLoot/结算。");
        report.Note("换弹只手动 Tick 真实 GunWeapon；不代表 WeaponSystem 后台调度或场景生命周期已通过。");
        report.Finish();
    }

    // 作用：直接计算两个校验菜单的可用状态；返回：true 表示未播放、未切换到播放且未编译，false 表示禁止校验。
    [MenuItem(ResourceMenu, true)]
    [MenuItem(LogicMenu, true)]
    private static bool CanValidate() => !EditorApplication.isPlayingOrWillChangePlaymode && !EditorApplication.isCompiling; // 同时排除播放切换与编译阶段，只开放稳定的编辑状态。

    // 作用：核对子弹数量、唯一 ID、口径等级、弹速及根组件；返回：无返回值。
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

    // 作用：读取枪械目录并逐一核对三种枪的资源合同；返回：无返回值。
    private static void ValidateWeapons(Report report)
    {
        // 共用一次目录读取结果，分别传入三种枪的独立预期值进行核对。
        var configs = ReadCatalog<WeaponConfig>(report, "Configs/Weapons", c => c.Id);
        ValidateWeapon(report, configs, "pistol", Caliber.S, 100f, 12, 10f, 1.5f, false, 0f, 0.25f);
        ValidateWeapon(report, configs, "machinegun", Caliber.AR, 240f, 50, 6f, 2.5f, true, 600f, 0.25f);
        ValidateWeapon(report, configs, "smg", Caliber.S, 160f, 30, 8f, 1.8f, true, 720f, 0.25f);
    }

    // 作用：核对单种枪械的数值、射速模式与磁盘迁移字段；返回：无返回值。
    private static void ValidateWeapon(Report report, WeaponConfig[] configs, string id, Caliber caliber,
        float durability, int magazine, float damage, float reload, bool automatic, float rpm, float semiAutoInterval)
    {
        // 加载后的默认值与磁盘显式字段分开检查，避免误判旧配置已经迁移。
        report.Check($"枪械 {id}：{caliber} / 耐久{durability} / 容量{magazine} / 伤害{damage}", () =>
        {
            var config = Unique(configs, c => c.Id == id, id);
            Require(config.Caliber == caliber, $"mCaliber 实际 {config.Caliber}，预期 {caliber}。");
            Near(config.DurabilityMax, durability, "mDurabilityMax（旧资产缺省为 0）");
            Require(config.Magazine == magazine, $"mMagazine 实际 {config.Magazine}，预期 {magazine}。");
            Near(config.Damage, damage, "mDamage");
            Near(config.ReloadTime, reload, "mReloadTime");
            Require(config.IsAutomatic == automatic, "mIsAutomatic 不匹配。");
            if (automatic) Near(config.RoundsPerMinute, rpm, "mRoundsPerMinute");
            else Near(config.SemiAutoInterval, semiAutoInterval, "mSemiAutoInterval");
        });
        InspectSerializedFields(report, id + " 磁盘迁移", () => Unique(configs, c => c.Id == id, id),
            new[] { "mCaliber", "mDurabilityMax" }, new[] { "mBulletId" });
    }

    private sealed class EnemyExpected
    {
        public readonly string Code, Id; // Code：设计编号；Id：预期的唯一配置标识。
        public readonly int Health, Damage, AttackLevel; // Health：预期生命上限；Damage：接触伤害；AttackLevel：攻击等级。
        public readonly EnemyAIType AI; // 预期的敌人行为类型。
        public readonly EnemyCategory Category; // 预期的敌人分类。
        // 作用：记录独立于被测资源的敌人期望值；返回：无返回值（构造函数）。
        public EnemyExpected(string code, string id, int health, int damage, int attackLevel,
            EnemyAIType ai, EnemyCategory category = EnemyCategory.Normal)
        {
            // 将设计标识与战斗、行为预期绑定在一起，供逐敌人比对时统一取值。
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
    }; // 十二类敌人的独立预期规格，用于对照正式配置。

    // 作用：核对敌人属性、预制体静态接线和旧 E01 的掉落字段；返回：无返回值。
    private static void ValidateEnemies(Report report)
    {
        // 逐敌人分别报告属性与装配，引用检查不触发 AI 或技能表现。
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

    // 作用：检查护盾配置恰有五级且每级容量符合预期；返回：无返回值。
    private static void ValidateShields(Report report)
    {
        // 先核对配置总数，再逐级检查唯一项及容量，避免数量正确却等级缺漏。
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

    // 作用：构造正式资源合同的六波预期值，不模拟 EnemySpawnSystem 调度；返回：新建的波次合同数组。
    private static WaveConfig[] ExpectedWaves() => new[]
    {
        new WaveConfig(1, 0f, new[] { new SpawnGroup("slime_green", 18, 0f) }),
        new WaveConfig(2, 45f, new[] { new SpawnGroup("slime_green", 16, 0f), new SpawnGroup("slime_red", 4, 0f), new SpawnGroup("ram", 1, 0f, 1) }),
        new WaveConfig(3, 45f, new[] { new SpawnGroup("goblin", 14, 0f), new SpawnGroup("drummer", 2, 0f, 1) }),
        new WaveConfig(4, 50f, new[] { new SpawnGroup("splitter", 8, 0f), new SpawnGroup("slime_green", 18, 0f, 1) }),
        new WaveConfig(5, 50f, new[] { new SpawnGroup("archer", 8, 0f), new SpawnGroup("ram", 2, 0f, 1), new SpawnGroup("drummer", 2, 0f, 1), new SpawnGroup("slime_green", 20, 0f) }),
        new WaveConfig(6, 60f, new[] { new SpawnGroup("slime_king", 1, 0f, 2) }, true)
    }; // 按波序固定分组、数量与盾级，作为独立于正式配置的比对基准。

    // 作用：检查第一关波次合同、人数血盾预算、枪池奖励及环境引用；返回：无返回值。
    private static void ValidateStage(Report report)
    {
        // 逐波逐组比对静态数据，汇总预算只做算术，不运行出生、战斗或导航流程。
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
        report.Check("Stage1：枪池恰好 pistol/machinegun/smg，结算奖励 500", () =>
        {
            var config = stage();
            Require(config.WeaponIds != null && config.WeaponIds.Count == 3
                && config.WeaponIds.OrderBy(id => id, StringComparer.Ordinal).SequenceEqual(new[] { "machinegun", "pistol", "smg" }),
                "本阶段枪池必须恰好为 pistol、machinegun 与 smg。");
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

    // 作用：断言目标波次存在并读取它；返回：指定索引的非空 WaveConfig。
    private static WaveConfig WaveAt(StageConfig stage, int index)
    {
        // 先确认波次列表及目标项存在，再返回目标波，避免缺项变成后续空引用错误。
        Require(stage.Waves != null && stage.Waves.Count > index && stage.Waves[index] != null, "缺少波 " + (index + 1));
        return stage.Waves[index];
    }

    // 作用：验证六波的生成组合法性并展平以便统计；返回：所有有效生成组的数组。
    private static SpawnGroup[] StageGroups(StageConfig stage)
    {
        // 先验证波、组与盾级范围，避免缺项或非法值掩盖汇总错误。
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

    // 作用：检查拾取物、敌弹与毒区预制体路径及必要组件引用；返回：无返回值。
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

    // 作用：只读检查 HUD 护盾、结算节点及预告区域高度，将装配缺项标为待处理；返回：无返回值。
    private static void ValidateHud(Report report)
    {
        // 分项检查节点接线和预告高度，装配不足记为待处理，不冒充运行时交互验收。
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

    // 作用：分别检查 HUD 指定节点组件、Bind 及 Designer 序列化引用；返回：无返回值。
    private static void ValidateHudNode<T>(Report report, Func<GameObject> hud, string name) where T : Component
    {
        // 只检存在性和引用对应关系，不调用会写字段的 getter，也不补装节点。
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

    // 作用：直接委托 Unique 查找包含未激活节点的同名 HUD 节点；返回：唯一匹配的 Transform，缺失或重名时抛出异常。
    private static Transform NamedNode(GameObject prefab, string name) =>
        Unique(prefab.GetComponentsInChildren<Transform>(true), t => t.name == name, "HUD 节点 " + name); // 纳入隐藏节点并强制名称唯一，避免接线检查误取重名项。

    // 作用：遍历阵营、攻盾等级与边界案例验证伤害分配和连续破盾；返回：无返回值。
    private static void ValidateDamage(Report report)
    {
        // 独立计算预期分配再调用解析器，仅验证纯计算，不触发伤害命令或破盾事件。
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

    // 作用：直接委托报告器执行一条指定预期的伤害案例；返回：无返回值。
    private static void DamageExample(Report report, string name, DamageInfo hit, int level, float capacity,
        float health, float shield, bool broke) =>
        report.Check(name, () => AssertDamage(hit, level, capacity, health, shield, broke)); // 将单例参数封装为断言回调，由报告器捕获并记录结果。

    // 作用：核对真实伤害计算的血伤、盾伤、守恒关系和破盾标记；返回：无返回值。
    private static void AssertDamage(DamageInfo hit, int level, float capacity, float health, float shield, bool broke)
    {
        // 只计算一次实际结果，再分别核对血盾分配、总量守恒、容量边界与破盾标记。
        var actual = DamageResolver.Calculate(hit, level, capacity);
        var label = $"伤害{hit.Amount}/攻{hit.PenetrationLevel}/盾{level}/余{capacity}";
        Near(actual.HealthDamage, health, label + " 血伤");
        Near(actual.ShieldDamage, shield, label + " 盾伤");
        Near(actual.HealthDamage + actual.ShieldDamage, health + shield, label + " 血盾守恒");
        Require(actual.HealthDamage >= 0f && actual.ShieldDamage >= 0f && actual.ShieldDamage <= Mathf.Max(0f, capacity), label + " 超过容量或负伤害。");
        Require(actual.BrokeShield == broke, label + " 破盾标记错误。");
    }

    // 作用：逐例验证真实枪械同级补弹、换级预扣、缺弹、取消及报废边界；返回：无返回值。
    private static void ValidateReloads(Report report)
    {
        // 每例重新创建隔离夹具，按准备库存、发起请求、手动 Tick、核对事件与守恒的顺序验证。
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

    // 作用：在自动释放的独立枪械夹具中执行并报告单条换弹案例；返回：无返回值。
    private static void GunCase(Report report, string name, Action<GunFixture> test)
    {
        report.Check("GunWeapon：" + name, () =>
        {
            // 即使断言抛出异常，using 仍释放订阅、测试架构和临时配置。
            using (var fixture = new GunFixture()) test(fixture);
        });
    }

    // QFramework.cs 的真实入口是 Interface / RegisterModel / RegisterSystem / Deinit。
    // 此泛型闭包只属于测试；不注册正式架构、不注册玩家/经济/存档模型，不注册任何池工厂。
    private sealed class ReloadTestArchitecture : Architecture<ReloadTestArchitecture>
    {
        // 作用：提供独立换弹架构的空构造入口，注册由 Init 完成；返回：无返回值（构造函数）。
        public ReloadTestArchitecture()
        {
            // 依赖注册交给 Init，保持构造阶段不访问尚未就绪的模型和系统。
        }
        // 作用：仅注册换弹需要的库存模型与禁止生成对象的池替身；返回：无返回值。
        protected override void Init()
        {
            // 仅提供独立库存和禁止生成对象的池替身，将测试范围限制在换弹逻辑。
            RegisterModel<IBulletInventoryModel>(new BulletInventoryModel());
            RegisterSystem<IGameObjectPoolSystem>(new ReloadTestPool());
        }
    }

    private sealed class ReloadTestPool : AbstractSystem, IGameObjectPoolSystem
    {
        // 作用：保留空初始化入口，不创建池或预热对象；返回：无返回值。
        protected override void OnInit()
        {
            // 换弹断言不需要场景对象，初始化时不注册工厂或预热。
        }
        // 作用：直接抛出异常，阻止换弹测试注册工厂或执行预热；返回：无返回值。
        public void Register(string key, Func<GameObject> factory, int initialCount = 0) =>
            throw new InvalidOperationException("阶段三换弹断言不应注册池工厂。"); // 以异常暴露越界的池注册调用，不允许隐式预热。
        // 作用：直接拒绝换弹测试生成对象；返回：始终抛出异常，不返回 GameObject。
        public GameObject Spawn(string key, Vector3 position, Quaternion rotation, Transform parent = null) =>
            throw new InvalidOperationException("阶段三换弹断言不应生成对象。"); // 发现生成请求立即失败，防止换弹断言夹带场景对象副作用。
        // 作用：满足池接口的回收入口，本替身没有对象可回收；返回：无返回值。
        public void Recycle(string key, GameObject instance)
        {
            // 替身没有创建过池对象，回收入口不接管或修改传入实例。
        }
        // 作用：直接报告替身没有缓存对象；返回：固定为 0。
        public int GetCachedCount(string key) => 0; // 替身不创建或持有对象，缓存计数始终为零。
        // 作用：满足池接口的空清理入口，本替身不持有资源；返回：无返回值。
        public void ClearAll()
        {
            // 没有工厂、缓存或租出对象，不需要执行资源释放。
        }
    }

    private sealed class GunFixture : IDisposable
    {
        private WeaponConfig mConfig; // 夹具独占的内存武器配置，释放时销毁。
        private IArchitecture mArchitecture; // 仅供本条换弹案例使用的独立架构。
        private IUnRegister mInventorySubscription, mShortageSubscription; // mInventorySubscription：库存事件注销句柄；mShortageSubscription：缺弹事件注销句柄。
        public GunWeapon Gun { get; private set; } // 本夹具测试的真实枪械实例。
        public IBulletInventoryModel Inventory { get; private set; } // 独立架构中的测试弹药库存。
        public readonly List<BulletInventoryChangedEvent> Changes = new List<BulletInventoryChangedEvent>(); // 按发送顺序捕获的库存变化事件。
        public readonly List<AmmoShortageEvent> Shortages = new List<AmmoShortageEvent>(); // 捕获的缺弹提示事件。

        // 作用：创建临时配置、清空隔离库存并构造真实枪械与事件监听；返回：无返回值（构造函数）。
        public GunFixture()
        {
            try
            {
                // HideAndDontSave 使测试配置不进入资源保存；仍须在 Dispose 中显式销毁。
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
                Gun = new GunWeapon(mConfig, mArchitecture, null, ItemOrigin.Loot) { SlotIndex = 4 };
                State(0f, false);
                Require(Gun.LoadedLevel == 0 && Gun.NextLoadLevel == 0, "初始装填等级不为 0。");
            }
            catch
            {
                Dispose();
                throw;
            }
        }

        // 作用：直接发送命令，为 S 口径指定等级加入 Loot 来源弹药；返回：无返回值。
        public void Seed(int level, int count) => mArchitecture.SendCommand(new AddBulletsCommand(Caliber.S, level, AmmoBatch.Loot(count))); // 用真实入库命令准备全 Loot 初态，保留正常库存更新流程。
        // 作用：直接断言 S 口径指定等级库存等于预期；返回：无返回值。
        public void Count(int level, int expected) =>
            Require(Inventory.GetCount(Caliber.S, level) == expected, $"S·{level} 库存实际 {Inventory.GetCount(Caliber.S, level)}，预期 {expected}。"); // 定位指定等级的 S 弹药桶，直接比较操作后的库存数量。
        // 作用：清空已捕获事件，以单独核对下一步操作的副作用；返回：无返回值。
        public void ClearEvents()
        {
            // 同时清掉库存变化与缺弹记录，避免准备阶段的事件干扰下一步断言。
            Changes.Clear(); Shortages.Clear();
        }
        // 作用：检查弹夹总量、Loot 来源守恒及两种换弹状态表示；返回：无返回值。
        public void State(float resource, bool reloading)
        {
            // 先核对弹夹数量与来源守恒，再交叉检查布尔和枚举两种换弹状态。
            Near(Gun.Resource, resource, "弹夹余弹");
            var ammo = Gun.LoadedAmmo;
            Near(ammo.Count, resource, "LoadedAmmo.Count 与 Resource 一致");
            Require(ammo.SupplyCount >= 0 && ammo.LootCount >= 0 && ammo.SupplyCount + ammo.LootCount == ammo.Count,
                "弹夹来源份额非法。");
            Require(ammo.SupplyCount == 0, "阶段三测试初态应全部为 Loot。");
            Require(Gun.IsReloading == reloading, "IsReloading 不匹配。");
            Require(Gun.State == (reloading ? WeaponState.Reloading : WeaponState.Ready), "WeaponState 不匹配。");
        }
        // 作用：通过反射设置已知的全 Loot 弹夹余弹测试初态；返回：无返回值。
        public void SetResource(float value)
        {
            // 只设置已知测试初态，模拟已有余弹；绝不调用 TryAttack/DoAttack 或创建 owner。
            var property = typeof(WeaponBase).GetProperty(nameof(WeaponBase.Resource), BindingFlags.Public | BindingFlags.Instance);
            var setter = property?.GetSetMethod(true);
            Require(setter != null, "WeaponBase.Resource 非公开 setter 不可用。");
            setter.Invoke(Gun, new object[] { value });
            var supply = typeof(GunWeapon).GetField("mLoadedSupplyCount", BindingFlags.NonPublic | BindingFlags.Instance);
            Require(supply != null, "GunWeapon.mLoadedSupplyCount 不存在。");
            supply.SetValue(Gun, 0);
        }
        // 作用：准备一级弹夹余四发、一级库存八发的换级测试初态；返回：无返回值。
        public void PrepareLevelOneWithFourRounds()
        {
            // 先走真实换弹流程，再用反射模拟已消耗的余弹，不触发真实开火。
            Seed(1, 20); Gun.CycleNextLoadLevel(); Gun.RequestReload(); Gun.Tick(1.1f);
            Require(Gun.LoadedLevel == 1, "换级测试准备装填失败。");
            SetResource(4); Count(1, 8);
        }
        // 作用：按发送顺序核对库存事件的口径、等级与新数量；返回：无返回值。
        public void InventoryEvent(int index, int level, int count)
        {
            // 先确认指定顺序的事件已捕获，再核对其弹药桶与更新后数量。
            Require(Changes.Count > index, "缺少库存变化事件 " + index);
            var e = Changes[index];
            Require(e.Caliber == Caliber.S && e.Level == level && e.Count == count,
                $"库存事件实际 {e.Caliber}·{e.Level}={e.Count}，预期 S·{level}={count}。");
        }
        // 作用：核对唯一缺弹事件中的槽位、实际预扣量和请求量；返回：无返回值。
        public void Shortage(int loaded, int wanted)
        {
            // 先限制缺弹提示恰好一次，再核对固定测试槽位及实际预扣与请求数量。
            Require(Shortages.Count == 1, "应恰好发送一次 AmmoShortageEvent。");
            var e = Shortages[0];
            Require(e.SlotIndex == 4 && e.Loaded == loaded && e.Wanted == wanted,
                $"缺弹事件实际 slot={e.SlotIndex}, loaded={e.Loaded}, wanted={e.Wanted}。");
        }
        // 作用：注销监听、反初始化测试架构并销毁临时配置；返回：无返回值。
        public void Dispose()
        {
            // 嵌套 finally 使前序清理抛错时仍尝试释放后续资源，构造失败也复用此路径。
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

    // 作用：读取指定 Resources 目录并检查全项目同类配置的空 ID 与重复 ID；返回：目录配置数组，读取未成功时保留空数组。
    private static T[] ReadCatalog<T>(Report report, string folder, Func<T, string> id) where T : ScriptableObject
    {
        // 运行时目录与全项目资产各自检查；直接读取，不访问或刷新 ConfigTable 静态缓存。
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

    // 作用：检查 Resources 路径确为预制体且根节点带有所需组件；返回：只读加载的预制体资产，不创建实例。
    private static GameObject RequirePrefab<T>(string path) where T : Component
    {
        // 从非空路径逐步验证加载结果、资产后缀与根组件，全程只读而不实例化。
        Require(!string.IsNullOrWhiteSpace(path), "Resources prefab 路径为空。");
        var prefab = Resources.Load<GameObject>(path);
        Require(prefab != null, "缺少 Resources/" + path + " prefab。");
        Require(AssetDatabase.GetAssetPath(prefab).EndsWith(".prefab", StringComparison.OrdinalIgnoreCase), path + " 不是 prefab 资产。");
        Require(prefab.GetComponent<T>() != null, path + " 根节点缺少 " + typeof(T).Name);
        return prefab;
    }

    // 作用：筛选并断言结果恰好唯一；返回：唯一匹配项，零项或多项时抛出异常。
    private static T Unique<T>(IEnumerable<T> values, Func<T, bool> predicate, string label)
    {
        // 先收集全部匹配项并核对唯一性，再返回结果，防止静默选中重复配置。
        var matches = values.Where(predicate).ToArray();
        Require(matches.Length == 1, label + " 预期恰好 1 个，实际 " + matches.Length + "。");
        return matches[0];
    }

    // 作用：按精确名称查找序列化字段并拒绝缺失字段；返回：已找到的 SerializedProperty。
    private static SerializedProperty Property(SerializedObject serialized, string name)
    {
        // 按当前源码字段名精确查找，确认存在后才允许调用方读取或设置值。
        var property = serialized.FindProperty(name);
        Require(property != null, serialized.targetObject.GetType().Name + "." + name + " 字段不存在；请复核当前源码，未猜测替代字段。");
        return property;
    }

    // 作用：只读检查磁盘 YAML 是否显式保存必需字段，并提示遗留字段；返回：无返回值。
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

    // 作用：断言实际浮点值有限且与预期误差不超过 0.001；返回：无返回值。
    private static void Near(float actual, float expected, string label)
    {
        // 先排除非有限值，再按绝对误差核对预期，避免浮点舍入造成误报。
        Require(!float.IsNaN(actual) && !float.IsInfinity(actual) && Mathf.Abs(actual - expected) <= 0.001f,
            $"{label} 实际 {actual}，预期 {expected}。");
    }

    // 作用：条件不成立时抛出带说明的断言异常；返回：无返回值。
    private static void Require(bool condition, string message)
    {
        // 将失败条件转换为带业务说明的异常，供外层报告器归类记录。
        if (!condition) throw new InvalidOperationException(message);
    }

    private sealed class Report
    {
        private readonly string mName; // 本轮报告的检查类别名称。
        private int mPassed, mFailed, mPending, mSkipped; // mPassed：通过数；mFailed：失败数；mPending：待装配或迁移数；mSkipped：未执行数。
        // 作用：记录本轮报告名称，各项计数保持初始零值；返回：无返回值（构造函数）。
        public Report(string name)
        {
            // 保存本轮检查类别供最终汇总标识，计数使用新实例的零初值。
            mName = name;
        }
        // 作用：执行单项检查并按结果累计通过、失败或待处理数量；返回：无返回值。
        public void Check(string name, Action check, bool pendingOnFailure = false)
        {
            // 装配或迁移缺项可记为待处理；异常在本项内吸收，不阻断其他检查。
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
        // 作用：隔离整个检查分区的异常，分区中断记为失败并继续后续分区；返回：无返回值。
        public void Section(string name, Action section)
        {
            // 将分区级异常转为一次失败记录，避免单个分区中断整轮检查。
            try { section(); }
            catch (Exception exception)
            {
                mFailed++;
                Debug.LogError("[阶段三][FAIL] " + name + " 检查中断：" + exception.GetBaseException().Message);
            }
        }
        // 作用：直接输出不影响计数的信息说明；返回：无返回值。
        public void Note(string message) => Debug.Log("[阶段三][INFO] " + message); // 仅补充检查背景，不将说明文本计入任何结果类别。
        // 作用：记录并提示一项未执行的检查范围；返回：无返回值。
        public void Skip(string message)
        {
            // 先累计未执行项再发出警告，将验收空白与通过、失败结果明确区分。
            mSkipped++;
            Debug.LogWarning("[阶段三][SKIP/未执行] " + message);
        }
        // 作用：直接输出本轮分类计数与验收边界，不把逐项结果等同端到端通过；返回：无返回值。
        public void Finish() => Debug.Log($"[阶段三] {mName}：PASS {mPassed} / FAIL {mFailed} / 待装配或迁移 {mPending} / 未执行 {mSkipped}。仅本次逐项结果，不代表阶段三或端到端验收完成。"); // 按四类计数汇总本轮结果，同时保留逐项检查的结论边界。
    }
}
#endif
