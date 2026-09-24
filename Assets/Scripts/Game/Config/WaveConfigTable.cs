using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public sealed class SpawnGroup
{
    [SerializeField] private string mEnemyId; // 本生成组使用的敌人配置标识。
    [SerializeField, Min(0)] private int mCount; // 本组计划生成的敌人数量。
    [SerializeField, Min(0f)] private float mInterval; // 本组相邻敌人的生成间隔，单位为秒。
    [SerializeField, Range(0, 5)] private int mShieldLevel; // 本组敌人的初始护盾等级，零表示无盾。

    public string EnemyId => mEnemyId; // 本组敌人配置标识。
    public int Count => mCount; // 本组生成总数。
    public float Interval => mInterval; // 本组生成间隔秒数。
    public int ShieldLevel => mShieldLevel; // 本组敌人初始护盾等级。

    // 作用：建立同类敌人的生成组配置；返回：无返回值（构造函数）。
    public SpawnGroup(string enemyId, int count, float interval, int shieldLevel = 0)
    {
        // 保存输入，不在构造阶段生成敌人或修正数量和间隔。
        mEnemyId = enemyId;
        mCount = count;
        mInterval = interval;
        mShieldLevel = shieldLevel;
    }
}

[Serializable]
public sealed class WaveConfig
{
    [SerializeField, Min(1)] private int mWave; // 波次编号。
    [SerializeField, Min(0f)] private float mIntervalFromPrev; // 距前一波启动的自然启动间隔，单位为秒。
    [SerializeField] private SpawnGroup[] mGroups = Array.Empty<SpawnGroup>(); // 本波各类敌人的生成组。
    [SerializeField] private bool mIsBoss; // true 标识首领波；false 标识非首领波。

    public int Wave => mWave; // 波次编号。
    public float IntervalFromPrev => mIntervalFromPrev; // 从前一波启动起计算的间隔秒数。
    public IReadOnlyList<SpawnGroup> Groups => mGroups; // 本波生成组的只读视图。
    public bool IsBoss => mIsBoss; // 是否被配置为首领波。

    // 作用：建立波次编号、时序及生成组配置；返回：无返回值（构造函数）。
    public WaveConfig(int wave, float interval, SpawnGroup[] groups, bool isBoss = false)
    {
        // 直接保存生成组引用，实际调度由刷怪系统读取后执行。
        mWave = wave;
        mIntervalFromPrev = interval;
        mGroups = groups;
        mIsBoss = isBoss;
    }
}
