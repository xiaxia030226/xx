using System;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "NewStage", menuName = "Game/关卡配置", order = 0)]
public class StageConfig : ScriptableObject
{
    [SerializeField, Min(1)] private int mLevel; // 关卡编号，作为关卡配置查询键。
    [SerializeField] private string mEnvironmentPath; // 关卡环境预制体的资源路径。
    [SerializeField] private WaveConfig[] mWaves = Array.Empty<WaveConfig>(); // 按推进顺序保存的波次配置。
    [SerializeField] private string[] mWeaponIds = Array.Empty<string>(); // 本关掉落候选武器标识，也用于推导掉弹口径池。
    [SerializeField, Min(0)] private int mClearBonus = 500; // 胜利结算额外奖励的金币数。
    [SerializeField] private string mEmergencyWeaponId = "machinegun"; // 应急补给所用武器标识。
    [SerializeField, Min(1)] private int mEmergencyAmmoLevel0 = 40; // 应急补给提供的零级弹药数量。
    [SerializeField, Min(1)] private int mEmergencyAmmoLevel1 = 20; // 应急补给提供的一级弹药数量。

    public int Level => mLevel; // 关卡编号。
    public string EnvironmentPath => mEnvironmentPath; // 关卡环境资源路径。
    public IReadOnlyList<WaveConfig> Waves => mWaves; // 只读形式暴露的波次列表。
    public IReadOnlyList<string> WeaponIds => mWeaponIds; // 本关候选武器标识列表。
    public int ClearBonus => mClearBonus; // 通关奖励金币数。
    public string EmergencyWeaponId => mEmergencyWeaponId; // 应急补给武器标识。
    public int EmergencyAmmoLevel0 => mEmergencyAmmoLevel0; // 应急补给零级弹数。
    public int EmergencyAmmoLevel1 => mEmergencyAmmoLevel1; // 应急补给一级弹数。
}
