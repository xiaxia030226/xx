using System;

public static class DamageResolver
{
    // 作用：按穿甲等级与有效护盾分摊伤害；返回：生命扣除量、护盾扣除量及是否破盾。
    public static DamageResult Calculate(DamageInfo hit, int shieldLevel, float shieldCurrent)
    {
        // 负伤害归零；等级和剩余容量同时为正才视为有盾。
        float amount = Math.Max(0f, hit.Amount);
        bool hasShield = shieldLevel > 0 && shieldCurrent > 0f;
        if (!hasShield)
        {
            // 肉弹加成仅适用于玩家的零级子弹，且要求命中前已无有效护盾。
            if (hit.Faction == CombatFaction.Player && hit.IsBullet && hit.PenetrationLevel == 0)
            {
                amount *= AmmoTypes.NoShieldBonus;
            }

            return new DamageResult(amount, 0f, false);
        }

        // 穿甲低于、等于、高于盾级时，护盾分别承担 100%、70%、30% 的伤害。
        float shieldRatio = hit.PenetrationLevel < shieldLevel ? 1f
            : hit.PenetrationLevel == shieldLevel ? 0.7f : 0.3f;
        // 实际吸收不超过剩余盾量；未吸收部分全部伤及生命，不追加无盾加成。
        float shieldDamage = Math.Min(shieldCurrent, amount * shieldRatio);
        return new DamageResult(amount - shieldDamage, shieldDamage, shieldDamage >= shieldCurrent);
    }
}
