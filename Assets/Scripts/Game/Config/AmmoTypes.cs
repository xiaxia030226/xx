/// <summary>
/// 子弹口径。枪械按口径装填子弹，同口径不同穿甲等级共享同一子弹预制体与对象池。
/// </summary>
public enum Caliber
{
    // S：小型弹，手枪等轻型武器使用。
    S,

    // AR：步枪弹，机枪等自动武器使用。
    AR,

    // L：重型弹，狙击/榴弹等重型武器使用（阶段四引入）。
    L
}

/// <summary>
/// 子弹口径与穿甲等级的静态规则表：等级倍率、磨损系数、子弹 id 约定。
/// 穿甲等级共 0~5 六级；0 级肉弹仅在命中前无有效护盾时获得额外 1.5 倍加成。
/// </summary>
public static class AmmoTypes
{
    // MaxPenetrationLevel：最高穿甲等级；合法等级范围为 0~MaxPenetrationLevel。
    public const int MaxPenetrationLevel = 5;

    // LevelCount：穿甲等级总数（0~5 共 6 级）。
    public const int LevelCount = MaxPenetrationLevel + 1;

    // DamageMultipliers：各穿甲等级的伤害倍率（下标即等级）。
    // 0 级肉弹基础倍率 1.0（对无护盾目标另有 ×1.5），高等级穿甲弹逐级增伤。
    private static readonly float[] DamageMultipliers = { 1.0f, 1.0f, 1.1f, 1.2f, 1.3f, 1.4f };

    // WearFactors：各穿甲等级的耐久磨损系数（下标即等级）。
    // 高等级弹对枪械磨损更小，每发磨损 = 1 × 系数。
    private static readonly float[] WearFactors = { 1.00f, 0.95f, 0.90f, 0.85f, 0.80f, 0.75f };

    // NoShieldBonus：0 级肉弹命中无护盾目标时的伤害加成倍率。
    public const float NoShieldBonus = 1.5f;

    /// <summary>
    /// 取指定穿甲等级的伤害倍率；越界等级钳制到合法范围。
    /// </summary>
    public static float DamageMultiplier(int level)
    {
        return DamageMultipliers[ClampLevel(level)];
    }

    /// <summary>
    /// 取指定穿甲等级的耐久磨损系数；越界等级钳制到合法范围。
    /// </summary>
    public static float WearFactor(int level)
    {
        return WearFactors[ClampLevel(level)];
    }

    /// <summary>
    /// 生成子弹配置 id（兼作配置资产文件名）：bullet_s_0、bullet_ar_3 等。
    /// BulletConfigTable 与 Editor 资产生成器都按此约定命名。
    /// </summary>
    public static string BulletId(Caliber caliber, int level)
    {
        return $"bullet_{caliber.ToString().ToLower()}_{ClampLevel(level)}";
    }

    /// <summary>
    /// 同口径子弹共用的对象池 key（不含等级）：bullet_s、bullet_ar、bullet_l。
    /// </summary>
    public static string PoolKey(Caliber caliber)
    {
        return $"bullet_{caliber.ToString().ToLower()}";
    }

    /// <summary>
    /// 把穿甲等级钳制到 0~MaxPenetrationLevel。
    /// </summary>
    private static int ClampLevel(int level)
    {
        return level < 0 ? 0 : (level > MaxPenetrationLevel ? MaxPenetrationLevel : level);
    }
}
