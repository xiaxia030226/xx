using System.Collections.Generic;
using UnityEngine;

/// <summary>按子弹标识查询配置，首次访问时从 Resources/Configs/Bullets 加载并缓存。</summary>
public static class BulletConfigTable
{
    private const string ConfigsFolder = "Configs/Bullets"; // 子弹配置在 Resources 下的相对目录。

    private static Dictionary<string, BulletConfig> sConfigs; // 子弹标识到配置资产的缓存。

    private static Dictionary<string, BulletConfig> Configs => sConfigs ??= LoadAll(); // 缓存为空时加载，后续查询复用同一字典。

    public static IReadOnlyCollection<string> AllIds => Configs.Keys; // 已加载子弹配置的全部标识。

    // 作用：尝试按标识查询子弹配置；返回：找到为 true，否则为 false；out config 为找到的配置或 null。
    public static bool TryGet(string id, out BulletConfig config)
    {
        // 直接使用字典查询，让调用方自行处理不存在的标识。
        return Configs.TryGetValue(id, out config);
    }

    // 作用：取得必须存在的子弹配置；返回：对应配置，标识缺失时抛出 KeyNotFoundException。
    public static BulletConfig Get(string id)
    {
        // 无配置时立即暴露错误，不替换为其他口径或等级。
        if (!Configs.TryGetValue(id, out var config))
        {
            throw new KeyNotFoundException($"未找到子弹配置：{id}");
        }

        return config;
    }

    // 作用：加载全部子弹配置并建立标识索引；返回：按唯一标识整理的配置字典。
    private static Dictionary<string, BulletConfig> LoadAll()
    {
        var configs = new Dictionary<string, BulletConfig>();

        foreach (var config in Resources.LoadAll<BulletConfig>(ConfigsFolder))
        {
            // 重复标识只记录错误并跳过，保留首个加载资产而非覆盖。
            if (configs.ContainsKey(config.Id))
            {
                Debug.LogError($"[BulletConfig] 子弹 id 重复：{config.Id}（资产 {config.name}）");
                continue;
            }

            configs[config.Id] = config;
        }

        Debug.Log($"[BulletConfig] 已加载 {configs.Count} 个子弹配置");
        return configs;
    }
}
