using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public sealed class SpawnGroup
{
    [SerializeField] private string mEnemyId;
    [SerializeField, Min(0)] private int mCount;
    [SerializeField, Min(0f)] private float mInterval;
    [SerializeField, Range(0, 5)] private int mShieldLevel;

    public string EnemyId => mEnemyId;
    public int Count => mCount;
    public float Interval => mInterval;
    public int ShieldLevel => mShieldLevel;

    public SpawnGroup(string enemyId, int count, float interval, int shieldLevel = 0)
    {
        mEnemyId = enemyId;
        mCount = count;
        mInterval = interval;
        mShieldLevel = shieldLevel;
    }
}

[Serializable]
public sealed class WaveConfig
{
    [SerializeField, Min(1)] private int mWave;
    [SerializeField, Min(0f)] private float mIntervalFromPrev;
    [SerializeField] private SpawnGroup[] mGroups = Array.Empty<SpawnGroup>();
    [SerializeField] private bool mIsBoss;

    public int Wave => mWave;
    public float IntervalFromPrev => mIntervalFromPrev;
    public IReadOnlyList<SpawnGroup> Groups => mGroups;
    public bool IsBoss => mIsBoss;

    public WaveConfig(int wave, float interval, SpawnGroup[] groups, bool isBoss = false)
    {
        mWave = wave;
        mIntervalFromPrev = interval;
        mGroups = groups;
        mIsBoss = isBoss;
    }
}
