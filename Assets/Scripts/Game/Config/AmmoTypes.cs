/// <summary>枪械按口径装填，同口径不同穿甲等级共享子弹预制体与对象池。</summary>
public enum Caliber
{
    S, // 小型弹口径，如手枪使用的弹药。

    AR, // 步枪弹口径，如机枪使用的弹药。

    L // 重型弹口径。
}

/// <summary>定义子弹等级倍率、磨损系数及配置和对象池的命名规则。</summary>
public static class AmmoTypes
{
    public const int MaxPenetrationLevel = 5; // 最高穿甲等级，最低为零。

    public const int LevelCount = MaxPenetrationLevel + 1; // 包含零级在内的穿甲等级总数。

    private static readonly float[] DamageMultipliers = { 1.0f, 1.0f, 1.1f, 1.2f, 1.3f, 1.4f }; // 按等级索引的基础伤害倍率，不含无盾加成。

    private static readonly float[] WearFactors = { 1.00f, 0.95f, 0.90f, 0.85f, 0.80f, 0.75f }; // 按等级索引的每发耐久磨损系数。

    public const float NoShieldBonus = 1.5f; // 玩家零级子弹命中前目标无有效护盾时的额外伤害倍率。

    // 作用：查询指定穿甲等级的伤害倍率；返回：钳制等级后对应的倍率。
    public static float DamageMultiplier(int level)
    {
        // 先限制索引范围，避免越界访问倍率表。
        return DamageMultipliers[ClampLevel(level)];
    }

    // 作用：查询指定穿甲等级的磨损系数；返回：钳制等级后对应的系数。
    public static float WearFactor(int level)
    {
        // 等级越高表中系数越低，枪械每发按此数值磨损。
        return WearFactors[ClampLevel(level)];
    }

    // 作用：生成口径与等级对应的子弹配置标识；返回：bullet_小写口径_合法等级 格式的字符串。
    public static string BulletId(Caliber caliber, int level)
    {
        // 配置细分到等级，统一小写口径并钳制等级后拼接。
        return $"bullet_{caliber.ToString().ToLower()}_{ClampLevel(level)}";
    }

    // 作用：生成同口径共用的子弹对象池键；返回：不含穿甲等级的 bullet_小写口径 字符串。
    public static string PoolKey(Caliber caliber)
    {
        // 不包含等级，使同口径不同伤害等级复用同一对象池。
        return $"bullet_{caliber.ToString().ToLower()}";
    }

    // 作用：限制穿甲等级的取值范围；返回：0 到 MaxPenetrationLevel 之间的等级。
    private static int ClampLevel(int level)
    {
        // 越过两端时取边界值，范围内保持原值。
        return level < 0 ? 0 : (level > MaxPenetrationLevel ? MaxPenetrationLevel : level);
    }
}
