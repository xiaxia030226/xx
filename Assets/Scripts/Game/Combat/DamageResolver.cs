using System;

public static class DamageResolver
{
    public static DamageResult Calculate(DamageInfo hit, int shieldLevel, float shieldCurrent)
    {
        float amount = Math.Max(0f, hit.Amount);
        bool hasShield = shieldLevel > 0 && shieldCurrent > 0f;
        if (!hasShield)
        {
            if (hit.Faction == CombatFaction.Player && hit.IsBullet && hit.PenetrationLevel == 0)
            {
                amount *= AmmoTypes.NoShieldBonus;
            }

            return new DamageResult(amount, 0f, false);
        }

        float shieldRatio = hit.PenetrationLevel < shieldLevel ? 1f
            : hit.PenetrationLevel == shieldLevel ? 0.7f : 0.3f;
        float shieldDamage = Math.Min(shieldCurrent, amount * shieldRatio);
        return new DamageResult(amount - shieldDamage, shieldDamage, shieldDamage >= shieldCurrent);
    }
}
