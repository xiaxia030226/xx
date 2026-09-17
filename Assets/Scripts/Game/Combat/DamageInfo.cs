public enum CombatFaction
{
    Player,
    Enemy
}

public readonly struct DamageInfo
{
    public float Amount { get; }
    public int PenetrationLevel { get; }
    public CombatFaction Faction { get; }
    public bool IsBullet { get; }

    public DamageInfo(float amount, int penetrationLevel, CombatFaction faction, bool isBullet = false)
    {
        Amount = amount;
        PenetrationLevel = penetrationLevel;
        Faction = faction;
        IsBullet = isBullet;
    }
}

public readonly struct DamageResult
{
    public float HealthDamage { get; }
    public float ShieldDamage { get; }
    public bool BrokeShield { get; }

    public DamageResult(float healthDamage, float shieldDamage, bool brokeShield)
    {
        HealthDamage = healthDamage;
        ShieldDamage = shieldDamage;
        BrokeShield = brokeShield;
    }
}
