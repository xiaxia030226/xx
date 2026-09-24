using System.Collections.Generic;
using UnityEngine;

public static class StageConfigTable
{
    private const string ConfigsFolder = "Configs/Stages"; // 关卡配置在 Resources 下的目录。
    private static Dictionary<int, StageConfig> sConfigs; // 按关卡编号索引的配置缓存。
    private static Dictionary<int, StageConfig> Configs => sConfigs ??= LoadAll(); // 缓存为空时才触发全量加载。

    // 作用：按关卡编号查询配置；返回：对应关卡资产，缺失时抛出 KeyNotFoundException。
    public static StageConfig Get(int level)
    {
        // 必须明确找到所选关卡，不用其他关卡替代缺失配置。
        if (!Configs.TryGetValue(level, out var config))
        {
            throw new KeyNotFoundException($"未找到关卡配置：{level}");
        }

        return config;
    }

    // 作用：加载关卡资产并建立编号索引；返回：每个编号保留首个加载资产的字典。
    private static Dictionary<int, StageConfig> LoadAll()
    {
        var configs = new Dictionary<int, StageConfig>();
        // 同编号的后续资产只报错并跳过，不覆盖已登记的关卡。
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
