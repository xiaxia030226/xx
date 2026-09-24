using System.Collections.Generic;
using UnityEngine;

/// <summary>按武器标识查询静态配置，首次访问时加载 Resources/Configs/Weapons 中的资产。</summary>
public static class WeaponConfigTable
{
    public const string PistolId = "pistol"; // 手枪配置标识，用于初始装备及掉落兜底。

    public const string MachineGunId = "machinegun"; // 机枪配置标识。
    public const string SmgId = "smg"; // 冲锋枪配置标识。

    private const string ConfigsFolder = "Configs/Weapons"; // 武器配置在 Resources 下的相对目录。

    private static Dictionary<string, WeaponConfig> sConfigs; // 武器标识到配置资产的缓存。

    private static Dictionary<string, WeaponConfig> Configs => sConfigs ??= LoadAll(); // 空缓存触发加载，后续查询不重复读资源。

    public static IReadOnlyCollection<string> AllIds => Configs.Keys; // 表内全部武器配置标识。

    // 作用：尝试按标识查询武器配置；返回：找到为 true，否则为 false；out config 为对应资产或 null。
    public static bool TryGet(string id, out WeaponConfig config)
    {
        // 让调用方根据布尔结果决定如何处理缺失配置。
        return Configs.TryGetValue(id, out config);
    }

    // 作用：取得必须存在的武器配置；返回：对应资产，标识缺失时抛出 KeyNotFoundException。
    public static WeaponConfig Get(string id)
    {
        // 无配置时直接报出标识，不静默改用另一把武器。
        if (!Configs.TryGetValue(id, out var config))
        {
            throw new KeyNotFoundException($"未找到武器配置：{id}");
        }

        return config;
    }

    // 作用：加载所有武器配置并建立标识索引；返回：按唯一标识整理的配置字典。
    private static Dictionary<string, WeaponConfig> LoadAll()
    {
        var configs = new Dictionary<string, WeaponConfig>();

        foreach (var config in Resources.LoadAll<WeaponConfig>(ConfigsFolder))
        {
            // 后遇到的重复标识只报错并跳过，保留第一次登记的资产。
            if (configs.ContainsKey(config.Id))
            {
                Debug.LogError($"[WeaponConfig] 武器 id 重复：{config.Id}（资产 {config.name}）");
                continue;
            }

            configs[config.Id] = config;
        }

        Debug.Log($"[WeaponConfig] 已加载 {configs.Count} 个武器配置");
        return configs;
    }
}
