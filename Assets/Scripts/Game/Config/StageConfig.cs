using System;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "NewStage", menuName = "Game/关卡配置", order = 0)]
public class StageConfig : ScriptableObject
{
    [SerializeField, Min(1)] private int mLevel;
    [SerializeField] private string mEnvironmentPath;
    [SerializeField] private WaveConfig[] mWaves = Array.Empty<WaveConfig>();
    [SerializeField] private string[] mWeaponIds = Array.Empty<string>();
    [SerializeField, Min(0)] private int mClearBonus = 500;

    public int Level => mLevel;
    public string EnvironmentPath => mEnvironmentPath;
    public IReadOnlyList<WaveConfig> Waves => mWaves;
    public IReadOnlyList<string> WeaponIds => mWeaponIds;
    public int ClearBonus => mClearBonus;
}
