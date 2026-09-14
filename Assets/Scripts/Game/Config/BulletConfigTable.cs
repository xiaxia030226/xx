using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 子弹配置查询表。配置数据以 ScriptableObject 资产的形式存放在
/// Resources/Configs/Weapons/ 文件夹下（与武器配置同目录），首次访问时一次性加载填充。
/// 新增子弹：在该文件夹右键 Create → Game → 子弹配置，填好字段即可，代码零改动。
/// </summary>
public static class BulletConfigTable
{
    // ConfigsFolder：子弹配置资产在 Resources 下的相对文件夹路径（与武器配置同目录）。
    private const string ConfigsFolder = "Configs/Weapons";

    // sConfigs：懒加载的配置字典（子弹 id → 配置资产），首次访问时从 Resources 加载填充。
    private static Dictionary<string, BulletConfig> sConfigs;

    // Configs：配置字典访问入口；sConfigs 为 null 时触发一次性加载。
    private static Dictionary<string, BulletConfig> Configs => sConfigs ??= LoadAll();

    // AllIds：配置表中全部子弹 id 的只读集合，供武器系统遍历注册子弹对象池。
    public static IReadOnlyCollection<string> AllIds => Configs.Keys;

    /// <summary>
    /// 安全查询配置：找不到时返回 false，适合调用方自行处理缺失情况。
    /// </summary>
    public static bool TryGet(string id, out BulletConfig config)
    {
        return Configs.TryGetValue(id, out config);
    }

    /// <summary>
    /// 必须取得配置：id 错误时直接抛出异常，便于尽早发现配置问题。
    /// </summary>
    public static BulletConfig Get(string id)
    {
        if (!Configs.TryGetValue(id, out var config))
        {
            throw new KeyNotFoundException($"未找到子弹配置：{id}");
        }

        return config;
    }

    /// <summary>
    /// 从 Resources/Configs/Weapons 加载全部子弹配置资产并填入字典。
    /// LoadAll 按类型过滤，同文件夹下的武器配置资产不会被误读进来。
    /// </summary>
    private static Dictionary<string, BulletConfig> LoadAll()
    {
        var configs = new Dictionary<string, BulletConfig>();

        foreach (var config in Resources.LoadAll<BulletConfig>(ConfigsFolder))
        {
            // id 是字典的 key，重复时后加载的资产会覆盖先加载的，数据就乱了，因此直接报错提示。
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
