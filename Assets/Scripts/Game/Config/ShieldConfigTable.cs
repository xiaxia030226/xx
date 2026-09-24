using System.Collections.Generic;
using UnityEngine;

public static class ShieldConfigTable
{
    private const string ConfigsFolder = "Configs/Shields"; // 护盾配置在 Resources 下的目录。
    private static Dictionary<int, ShieldConfig> sConfigs; // 按护盾等级索引的配置缓存。
    private static Dictionary<int, ShieldConfig> Configs => sConfigs ??= LoadAll(); // 首次访问加载全部配置，后续复用缓存。

    // 作用：按等级取得护盾配置；返回：对应配置，缺失时抛出 KeyNotFoundException。
    public static ShieldConfig Get(int level)
    {
        // 缺失配置不静默回退，以免用错误等级或容量参与战斗。
        if (!Configs.TryGetValue(level, out var config))
        {
            throw new KeyNotFoundException($"未找到护盾配置：{level}");
        }

        return config;
    }

    // 作用：加载护盾资产并建立等级索引；返回：保留每个等级首个资产的配置字典。
    private static Dictionary<int, ShieldConfig> LoadAll()
    {
        var configs = new Dictionary<int, ShieldConfig>();
        // 重复等级记录错误并跳过，避免覆盖先前载入的配置。
        foreach (var config in Resources.LoadAll<ShieldConfig>(ConfigsFolder))
        {
            if (configs.ContainsKey(config.Level))
            {
                Debug.LogError($"[ShieldConfig] 护盾等级重复：{config.Level}（资产 {config.name}）");
                continue;
            }

            configs[config.Level] = config;
        }

        return configs;
    }
}
