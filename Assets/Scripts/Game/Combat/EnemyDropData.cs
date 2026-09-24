using System.Collections.Generic;
using UnityEngine;

public readonly struct EnemyDropData
{
    public int Gold { get; } // 掷点后的金币数。
    public bool DropAmmo { get; } // true 表示掷中弹药掉落；false 表示不掉弹药。
    public Caliber AmmoCaliber { get; } // 掉落弹药口径，仅在掉弹药时使用。
    public int AmmoLevel { get; } // 掉落弹药穿甲等级。
    public int AmmoCount { get; } // 掉落弹药数量，不随倍率放大。
    public int ShieldLevel { get; } // 掉落护盾等级，零表示无护盾掉落。
    public string WeaponId { get; } // 掉落武器配置标识，null 表示无武器掉落。

    // 作用：保存一次敌人掉落的掷点结果；返回：无返回值（构造函数）。
    public EnemyDropData(int gold, bool dropAmmo, Caliber ammoCaliber, int ammoLevel,
        int ammoCount, int shieldLevel, string weaponId)
    {
        // 将已确定的结果作为快照传递，构造时不再随机或生成拾取物。
        Gold = gold;
        DropAmmo = dropAmmo;
        AmmoCaliber = ammoCaliber;
        AmmoLevel = ammoLevel;
        AmmoCount = ammoCount;
        ShieldLevel = shieldLevel;
        WeaponId = weaponId;
    }

    // 作用：按敌人类别、倍率及武器池掷出掉落；返回：掉落快照，子体或空配置返回默认空结果。
    public static EnemyDropData Roll(EnemyConfig config, float multiplier, StageConfig stage,
        IReadOnlyList<WeaponBase> weapons, bool child = false)
    {
        // 子体不产出掉落，避免分裂或召唤子体重复发放收益。
        if (child || config == null) return default;

        // 倍率只放大金币数和普通掉落概率；精英、首领的三类物品免概率判定。
        multiplier = Mathf.Max(0f, multiplier);
        var guaranteed = config.Category == EnemyCategory.Elite || config.Category == EnemyCategory.Boss;
        var gold = Mathf.RoundToInt(Random.Range(config.GoldMin, config.GoldMax + 1) * multiplier);
        var ammo = guaranteed || Random.value < Mathf.Clamp01(config.AmmoChance * multiplier);
        var shield = guaranteed || Random.value < Mathf.Clamp01(config.ShieldChance * multiplier);
        var weapon = guaranteed || Random.value < Mathf.Clamp01(config.WeaponChance * multiplier);

        var caliber = Caliber.S;
        var ammoLevel = 0;
        var ammoCount = 0;
        if (ammo)
        {
            // 仅命中弹药掉落时选口径、等级和数量，弹量仍在原配置范围内抽取。
            caliber = RollCaliber(stage, weapons);
            ammoLevel = RollLevel(config.AmmoLevelMin, config.AmmoLevelMax);
            ammoCount = Random.Range(config.AmmoCountMin, config.AmmoCountMax + 1);
        }

        // 护盾等级在钳制后的闭区间内均匀抽取；没有掉落时用零或 null 表示。
        var shieldLevel = shield
            ? Random.Range(Mathf.Clamp(config.ShieldLevelMin, 1, 5),
                Mathf.Clamp(Mathf.Max(config.ShieldLevelMin, config.ShieldLevelMax), 1, 5) + 1)
            : 0;
        var weaponId = weapon ? RollWeapon(stage) : null;
        return new EnemyDropData(gold, ammo, caliber, ammoLevel, ammoCount, shieldLevel, weaponId);
    }

    // 作用：从弹药等级上下限中选择掉落等级；返回：钳制后的下限或上限，不抽取中间等级。
    private static int RollLevel(int min, int max)
    {
        // 先修正边界，再以 70% 选下限、30% 选上限；两端相同时直接返回。
        min = Mathf.Clamp(min, 0, 5);
        max = Mathf.Clamp(max, min, 5);
        return min == max || Random.value < 0.7f ? min : max;
    }

    // 作用：结合持枪口径与关卡武器池选择掉弹口径；返回：选中的口径，无可用池时返回 S 或 AR。
    private static Caliber RollCaliber(StageConfig stage, IReadOnlyList<WeaponBase> weapons)
    {
        var held = new List<Caliber>(3);
        if (weapons != null)
        {
            // 持有枪械按口径去重，避免同口径多把枪增加该口径在池内的权重。
            for (var i = 0; i < weapons.Count; i++)
                if (weapons[i] is GunWeapon gun && !held.Contains(gun.Caliber)) held.Add(gun.Caliber);
        }

        // 有持枪口径时，70% 走持有池；其余分支仍可能从关卡池抽到同口径。
        if (held.Count > 0 && Random.value < 0.7f) return held[Random.Range(0, held.Count)];

        var stageCalibers = new List<Caliber>(3);
        if (stage?.WeaponIds != null)
        {
            // 关卡武器配置决定候选口径，每种口径在池内仅保留一次。
            for (var i = 0; i < stage.WeaponIds.Count; i++)
            {
                var caliber = WeaponConfigTable.Get(stage.WeaponIds[i]).Caliber;
                if (!stageCalibers.Contains(caliber)) stageCalibers.Add(caliber);
            }
        }

        if (stageCalibers.Count > 0) return stageCalibers[Random.Range(0, stageCalibers.Count)];
        // 缺少关卡候选口径时，固定在 S 与 AR 之间等概率兜底。
        return Random.value < 0.5f ? Caliber.S : Caliber.AR;
    }

    // 作用：从关卡武器池选择掉落武器；返回：武器标识，无候选时等概率返回手枪或机枪标识。
    private static string RollWeapon(StageConfig stage)
    {
        var ids = new List<string>(2);
        if (stage?.WeaponIds != null)
        {
            // 去除空标识和重复项，使有效武器各占一个候选位置。
            for (var i = 0; i < stage.WeaponIds.Count; i++)
            {
                var id = stage.WeaponIds[i];
                if (!string.IsNullOrEmpty(id) && !ids.Contains(id)) ids.Add(id);
            }
        }
        if (ids.Count > 0) return ids[Random.Range(0, ids.Count)];
        return Random.value < 0.5f ? WeaponConfigTable.PistolId : WeaponConfigTable.MachineGunId;
    }
}
