using System.Collections.Generic;
using UnityEngine;

public static class StageConfigTable
{
    private const string ConfigsFolder = "Configs/Stages";
    private static Dictionary<int, StageConfig> sConfigs;
    private static Dictionary<int, StageConfig> Configs => sConfigs ??= LoadAll();

    public static StageConfig Get(int level)
    {
        if (!Configs.TryGetValue(level, out var config))
        {
            throw new KeyNotFoundException($"未找到关卡配置：{level}");
        }

        return config;
    }

    private static Dictionary<int, StageConfig> LoadAll()
    {
        var configs = new Dictionary<int, StageConfig>();
        foreach (var config in Resources.LoadAll<StageConfig>(ConfigsFolder))
        {
            if (configs.ContainsKey(config.Level))
            {
                Debug.LogError($"[StageConfig] 关卡等级重复：{config.Level}（资产 {config.name}）");
                continue;
            }

            configs[config.Level] = config;
        }

        return configs;
    }
}
