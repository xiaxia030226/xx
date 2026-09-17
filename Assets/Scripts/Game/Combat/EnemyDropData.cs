using System.Collections.Generic;
using UnityEngine;

public readonly struct EnemyDropData
{
    public int Gold { get; }
    public bool DropAmmo { get; }
    public Caliber AmmoCaliber { get; }
    public int AmmoLevel { get; }
    public int AmmoCount { get; }
    public int ShieldLevel { get; }
    public string WeaponId { get; }

    public EnemyDropData(int gold, bool dropAmmo, Caliber ammoCaliber, int ammoLevel,
        int ammoCount, int shieldLevel, string weaponId)
    {
        Gold = gold;
        DropAmmo = dropAmmo;
        AmmoCaliber = ammoCaliber;
        AmmoLevel = ammoLevel;
        AmmoCount = ammoCount;
        ShieldLevel = shieldLevel;
        WeaponId = weaponId;
    }

    public static EnemyDropData Roll(EnemyConfig config, float multiplier, StageConfig stage,
        IReadOnlyList<WeaponBase> weapons, bool child = false)
    {
        if (child || config == null) return default;

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
            caliber = RollCaliber(stage, weapons);
            ammoLevel = RollLevel(config.AmmoLevelMin, config.AmmoLevelMax);
            ammoCount = Random.Range(config.AmmoCountMin, config.AmmoCountMax + 1);
        }

        var shieldLevel = shield
            ? Random.Range(Mathf.Clamp(config.ShieldLevelMin, 1, 5),
                Mathf.Clamp(Mathf.Max(config.ShieldLevelMin, config.ShieldLevelMax), 1, 5) + 1)
            : 0;
        var weaponId = weapon ? RollWeapon(stage) : null;
        return new EnemyDropData(gold, ammo, caliber, ammoLevel, ammoCount, shieldLevel, weaponId);
    }

    private static int RollLevel(int min, int max)
    {
        min = Mathf.Clamp(min, 0, 5);
        max = Mathf.Clamp(max, min, 5);
        return min == max || Random.value < 0.7f ? min : max;
    }

    private static Caliber RollCaliber(StageConfig stage, IReadOnlyList<WeaponBase> weapons)
    {
        var held = new List<Caliber>(3);
        if (weapons != null)
        {
            for (var i = 0; i < weapons.Count; i++)
                if (weapons[i] is GunWeapon gun && !held.Contains(gun.Caliber)) held.Add(gun.Caliber);
        }

        if (held.Count > 0 && Random.value < 0.7f) return held[Random.Range(0, held.Count)];

        var stageCalibers = new List<Caliber>(3);
        if (stage?.WeaponIds != null)
        {
            for (var i = 0; i < stage.WeaponIds.Count; i++)
            {
                var caliber = WeaponConfigTable.Get(stage.WeaponIds[i]).Caliber;
                if (!stageCalibers.Contains(caliber)) stageCalibers.Add(caliber);
            }
        }

        if (stageCalibers.Count > 0) return stageCalibers[Random.Range(0, stageCalibers.Count)];
        // 独立行为测试没有关卡资产时仍只产生本阶段可用口径。
        return Random.value < 0.5f ? Caliber.S : Caliber.AR;
    }

    private static string RollWeapon(StageConfig stage)
    {
        var ids = new List<string>(2);
        if (stage?.WeaponIds != null)
        {
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
