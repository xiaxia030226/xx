using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 武器配置查询表。配置数据以 ScriptableObject 资产的形式存放在
/// Resources/Configs/Weapons/ 文件夹下，首次访问时一次性加载填充。
/// 新增武器：在该文件夹右键 Create → Game → 武器配置，填好字段即可，代码零改动。
/// </summary>
public static class WeaponConfigTable
{
    // PistolId：手枪的 id 约定常量，WeaponSystem 用它装配第一格武器。
    public const string PistolId = "pistol";

    // MachineGunId：机枪的 id 约定常量，WeaponSystem 用它装配第二格武器。
    public const string MachineGunId = "machinegun";

    // ConfigsFolder：武器配置资产在 Resources 下的相对文件夹路径（子弹配置在 Configs/Bullets）。
    private const string ConfigsFolder = "Configs/Weapons";

    // sConfigs：懒加载的配置字典（武器 id → 配置资产），首次访问时从 Resources 加载填充。
    private static Dictionary<string, WeaponConfig> sConfigs;

    // Configs：配置字典访问入口；sConfigs 为 null 时触发一次性加载。
    private static Dictionary<string, WeaponConfig> Configs => sConfigs ??= LoadAll();

    // AllIds：配置表中全部武器 id 的只读集合。
    public static IReadOnlyCollection<string> AllIds => Configs.Keys;

    /// <summary>
    /// 安全查询配置：找不到时返回 false，适合调用方自行处理缺失情况。
    /// </summary>
    public static bool TryGet(string id, out WeaponConfig config)
    {
        return Configs.TryGetValue(id, out config);
    }

    /// <summary>
    /// 必须取得配置：id 错误时直接抛出异常，便于尽早发现配置问题。
    /// </summary>
    public static WeaponConfig Get(string id)
    {
        if (!Configs.TryGetValue(id, out var config))
        {
            throw new KeyNotFoundException($"未找到武器配置：{id}");
        }

        return config;
    }

    /// <summary>
    /// 从 Resources/Configs/Weapons 加载全部武器配置资产并填入字典。
    /// </summary>
    private static Dictionary<string, WeaponConfig> LoadAll()
    {
        var configs = new Dictionary<string, WeaponConfig>();

        foreach (var config in Resources.LoadAll<WeaponConfig>(ConfigsFolder))
        {
            // id 是字典的 key，重复时后加载的资产会覆盖先加载的，数据就乱了，因此直接报错提示。
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
