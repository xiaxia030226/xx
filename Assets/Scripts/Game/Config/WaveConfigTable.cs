using System.Collections.Generic;

/// <summary>
/// 一组连续生成规则：生成哪种敌人、数量以及相邻两只的间隔。
/// </summary>
public sealed class SpawnGroup
{
    public string EnemyId { get; }
    public int Count { get; }
    public float Interval { get; }

    public SpawnGroup(string enemyId, int count, float interval)
    {
        EnemyId = enemyId;
        Count = count;
        Interval = interval;
    }
}

/// <summary>
/// 一整波的配置。一波可以按顺序包含多组不同类型的敌人。
/// </summary>
public sealed class WaveConfig
{
    public int Wave { get; }
    public IReadOnlyList<SpawnGroup> Groups { get; }

    public WaveConfig(int wave, params SpawnGroup[] groups)
    {
        Wave = wave;
        Groups = groups;
    }
}

/// <summary>
/// 波次静态配置。结构支持一波包含多组不同敌人。
/// </summary>
public static class WaveConfigTable
{
    public const float WaveInterval = 3f;

    private static readonly Dictionary<int, WaveConfig> Configs = new Dictionary<int, WaveConfig>
    {
        // 第一波：每隔 1 秒生成一只绿史莱姆，共生成 8 只。
        { 1, new WaveConfig(1, new SpawnGroup(EnemyConfigTable.SlimeGreenId, 8, 1f)) }
    };

    public static int WaveCount => Configs.Count;

    public static bool TryGet(int wave, out WaveConfig config)
    {
        return Configs.TryGetValue(wave, out config);
    }
}
