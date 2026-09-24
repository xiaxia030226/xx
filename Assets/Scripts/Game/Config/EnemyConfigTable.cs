using System.Collections.Generic;
using UnityEngine;

/// <summary>按敌人标识查询配置，首次访问时加载 Resources/Configs/Enemies 中的资产。</summary>
public static class EnemyConfigTable
{
    public const string SlimeGreenId = "slime_green"; // 绿史莱姆配置标识。
    public const string SlimeRedId = "slime_red"; // 红史莱姆配置标识。
    public const string GoblinId = "goblin"; // 哥布林小兵配置标识。
    public const string ArcherId = "archer"; // 哥布林弓手配置标识。
    public const string WolfId = "wolf"; // 森林狼配置标识。
    public const string SpiderId = "spider"; // 毒蜘蛛配置标识。
    public const string TreantId = "treant"; // 树精配置标识。
    public const string MageId = "mage"; // 暗影法师配置标识。
    public const string RamId = "ram"; // 撞角兽配置标识。
    public const string DrummerId = "drummer"; // 护盾鼓手配置标识。
    public const string SplitterId = "splitter"; // 分裂囊虫配置标识。
    public const string SlimeKingId = "slime_king"; // 史莱姆王配置标识。

    private const string ConfigsFolder = "Configs/Enemies"; // 敌人配置在 Resources 下的相对目录。

    private static Dictionary<string, EnemyConfig> sConfigs; // 敌人标识到配置资产的缓存字典。

    private static Dictionary<string, EnemyConfig> Configs => sConfigs ??= LoadAll(); // 首次访问加载全部配置，之后复用缓存。

    public static IReadOnlyCollection<string> AllIds => Configs.Keys; // 配置表包含的全部敌人标识。

    // 作用：尝试按标识查询敌人配置；返回：存在为 true，否则为 false；out config 为对应配置或 null。
    public static bool TryGet(string id, out EnemyConfig config)
    {
        // 缺失情况交给调用方处理，不使用替代敌人配置。
        return Configs.TryGetValue(id, out config);
    }

    // 作用：取得必须存在的敌人配置；返回：对应配置，标识缺失时抛出 KeyNotFoundException。
    public static EnemyConfig Get(string id)
    {
        // 明确抛出缺失标识，避免生成与波次配置不符的敌人。
        if (!Configs.TryGetValue(id, out var config))
        {
            throw new KeyNotFoundException($"未找到敌人配置：{id}");
        }

        return config;
    }

    // 作用：加载敌人资产并建立标识索引；返回：每个标识保留首个资产的配置字典。
    private static Dictionary<string, EnemyConfig> LoadAll()
    {
        var configs = new Dictionary<string, EnemyConfig>();

        foreach (var config in Resources.LoadAll<EnemyConfig>(ConfigsFolder))
        {
            // 重复标识记录错误并跳过，不覆盖已经登记的资产。
            if (configs.ContainsKey(config.Id))
            {
                Debug.LogError($"[EnemyConfig] 敌人 id 重复：{config.Id}（资产 {config.name}）");
                continue;
            }

            configs[config.Id] = config;
        }

        Debug.Log($"[EnemyConfig] 已加载 {configs.Count} 个敌人配置");
        return configs;
    }
}
