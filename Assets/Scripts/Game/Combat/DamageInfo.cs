public enum CombatFaction
{
    Player, // 玩家阵营的攻击。
    Enemy // 敌人阵营的攻击。
}

public readonly struct DamageInfo
{
    public float Amount { get; } // 护盾结算前的伤害量。
    public int PenetrationLevel { get; } // 攻击穿甲等级，用于与护盾等级比较。
    public CombatFaction Faction { get; } // 发起攻击的阵营。
    public bool IsBullet { get; } // true 表示子弹命中；false 表示非子弹伤害。

    // 作用：封装一次攻击的伤害及来源信息；返回：无返回值（构造函数）。
    public DamageInfo(float amount, int penetrationLevel, CombatFaction faction, bool isBullet = false)
    {
        // 只保存输入，伤害下限和护盾分摊交由结算器处理。
        Amount = amount;
        PenetrationLevel = penetrationLevel;
        Faction = faction;
        IsBullet = isBullet;
    }
}

public readonly struct DamageResult
{
    public float HealthDamage { get; } // 本次应扣除的生命值。
    public float ShieldDamage { get; } // 本次应扣除的护盾容量。
    public bool BrokeShield { get; } // true 表示本次耗尽有效护盾；false 表示未破盾。

    // 作用：封装生命与护盾分摊后的结算结果；返回：无返回值（构造函数）。
    public DamageResult(float healthDamage, float shieldDamage, bool brokeShield)
    {
        // 保存扣除量和破盾标志，由调用方实际更新目标状态。
        HealthDamage = healthDamage;
        ShieldDamage = shieldDamage;
        BrokeShield = brokeShield;
    }
}
