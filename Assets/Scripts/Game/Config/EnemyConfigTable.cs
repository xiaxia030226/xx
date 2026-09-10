using System.Collections.Generic;

/// <summary>
/// 敌人 AI 行为类型。配置只保存类型，Enemy 再根据类型执行对应行为。
/// </summary>
public enum EnemyAIType
{
    // 直线追向玩家，进入攻击范围后停下并进行近战接触伤害。
    ChaseMelee
}

/// <summary>
/// 单种敌人的静态参数。
/// </summary>
public sealed class EnemyConfig
{
    public string Id { get; }
    public string Name { get; }
    public int MaxHP { get; }
    public float MoveSpeed { get; }
    public int ContactDamage { get; }
    public float AttackInterval { get; }
    public int ExpValue { get; }
    public EnemyAIType AIType { get; }
    public float AttackRange { get; }

    public EnemyConfig(string id, string name, int maxHP, float moveSpeed, int contactDamage,
        float attackInterval, int expValue, EnemyAIType aiType, float attackRange)
    {
        Id = id;
        Name = name;
        MaxHP = maxHP;
        MoveSpeed = moveSpeed;
        ContactDamage = contactDamage;
        AttackInterval = attackInterval;
        ExpValue = expValue;
        AIType = aiType;
        AttackRange = attackRange;
    }
}

/// <summary>
/// 阶段二敌人配置表。后续增加敌人时只需继续向字典添加配置。
/// </summary>
public static class EnemyConfigTable
{
    public const string SlimeGreenId = "slime_green";

    private static readonly Dictionary<string, EnemyConfig> Configs = new Dictionary<string, EnemyConfig>
    {
        {
            SlimeGreenId,
            new EnemyConfig(SlimeGreenId, "绿史莱姆", 30, 2f, 10, 1f, 1,
                EnemyAIType.ChaseMelee, 1f)
        }
    };

    /// <summary>
    /// 安全查询配置：找不到时返回 false，适合调用方自行处理缺失情况。
    /// </summary>
    public static bool TryGet(string id, out EnemyConfig config)
    {
        return Configs.TryGetValue(id, out config);
    }

    /// <summary>
    /// 必须取得配置：id 错误时直接抛出异常，便于尽早发现配置问题。
    /// </summary>
    public static EnemyConfig Get(string id)
    {
        if (!Configs.TryGetValue(id, out var config))
        {
            throw new KeyNotFoundException($"未找到敌人配置：{id}");
        }

        return config;
    }
}
