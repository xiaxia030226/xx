using UnityEngine;

/// <summary>
/// 敌人死亡后广播，供经验掉落、音效和统计等互不依赖的模块监听。
/// struct 只携带事件发生时的数据，不负责执行具体业务。
/// </summary>
public struct EnemyDiedEvent
{
    public string EnemyId;
    public Vector3 Position;
    public int ExpValue;
}

/// <summary>开始新波次时广播，Wave 是当前波次编号。</summary>
public struct WaveStartedEvent
{
    public int Wave;
}

/// <summary>本波敌人全部生成且全部死亡后广播。</summary>
public struct WaveClearedEvent
{
    public int Wave;
}

/// <summary>配置表中的全部波次完成后广播；空事件只表达“发生了什么”。</summary>
public struct AllWavesClearedEvent
{
}

/// <summary>玩家切换当前武器槽时广播，槽位下标从 0 开始。</summary>
public struct WeaponSwitchedEvent
{
    public int SlotIndex;
}

/// <summary>武器资源变化时广播，供 HUD 更新能量或弹药显示。</summary>
public struct WeaponResourceChangedEvent
{
    public int SlotIndex;
    public float Current;
    public float Max;
}

/// <summary>玩家升级后广播，供升级三选一面板和音效等模块监听。</summary>
public struct LevelUpEvent
{
    public int Level;
}

/// <summary>升级三选一面板关闭后广播，供 GameRoot 判断是否还有待处理的升级。</summary>
public struct LevelUpPanelClosedEvent
{
}
