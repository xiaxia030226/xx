using System.Collections.Generic;
using UnityEngine;

public static class ShieldConfigTable
{
    private const string ConfigsFolder = "Configs/Shields";
    private static Dictionary<int, ShieldConfig> sConfigs;
    private static Dictionary<int, ShieldConfig> Configs => sConfigs ??= LoadAll();

    public static ShieldConfig Get(int level)
    {
        if (!Configs.TryGetValue(level, out var config))
        {
            throw new KeyNotFoundException($"未找到护盾配置：{level}");
        }

        return config;
    }

    private static Dictionary<int, ShieldConfig> LoadAll()
    {
        var configs = new Dictionary<int, ShieldConfig>();
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
