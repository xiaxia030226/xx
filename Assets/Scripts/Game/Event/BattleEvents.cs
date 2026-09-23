using UnityEngine;

/// <summary>
/// 敌人死亡后广播，供掉落生成、音效和统计等互不依赖的模块监听。
/// 掉落掷点在 Enemy.Die 中完成，本事件只携带掷点结果。
/// </summary>
public struct EnemyDiedEvent
{
    public string EnemyId;
    public Vector3 Position;

    // Gold：掷出的金币掉落数量，0 表示不掉金币。
    public int Gold;

    // DropAmmo：是否掉落子弹包；false 时下面三个子弹字段无意义。
    public bool DropAmmo;

    // AmmoCaliber：子弹包的口径（S/AR/L）。
    public Caliber AmmoCaliber;

    // AmmoLevel：子弹包的穿甲等级（0~5）。
    public int AmmoLevel;

    // AmmoCount：子弹包含有的子弹数量。
    public int AmmoCount;
    public int ShieldLevel;
    public string WeaponId;
}

public struct WeaponAddedEvent
{
    public int SlotIndex;
}

public struct PlayerShieldBrokenEvent
{
}

/// <summary>开始新波次时广播，Wave 是当前波次编号。</summary>
public struct WaveStartedEvent
{
    public int Wave;
}

/// <summary>配置表中的最后一波全部生成且场上敌人清零后广播；空事件只表达“发生了什么”。</summary>
public struct AllWavesClearedEvent
{
}

/// <summary>玩家切换当前武器槽时广播，槽位下标从 0 开始。</summary>
public struct WeaponSwitchedEvent
{
    public int SlotIndex;
}

/// <summary>武器资源（弹夹弹量）变化时广播，供 HUD 更新弹药显示。</summary>
public struct WeaponResourceChangedEvent
{
    public int SlotIndex;
    public float Current;
    public float Max;
}

/// <summary>武器耐久变化时广播，供 HUD 更新耐久数字与警示色。</summary>
public struct WeaponDurabilityChangedEvent
{
    public int SlotIndex;
    public float Current;
    public float Max;
}

/// <summary>武器耐久归零报废时广播，该槽位已被置空；HUD 用于清槽，不做自动切枪。</summary>
public struct WeaponBrokenEvent
{
    public int SlotIndex;
}

public struct WeaponAmmoDroppedEvent
{
    public Vector3 Position;
    public Caliber Caliber;
    public int Level;
    public AmmoBatch Ammo;
}

/// <summary>换弹时库存子弹不足（含完全无弹）广播，供 HUD 提示缺弹。</summary>
public struct AmmoShortageEvent
{
    public int SlotIndex;

    // Loaded：本次实际装入的子弹数；0 表示一颗都没装上。
    public int Loaded;

    // Wanted：本次想要装填的子弹数（弹夹容量）。
    public int Wanted;
}

/// <summary>子弹库存变化时广播，供 HUD 刷新库存显示。</summary>
public struct BulletInventoryChangedEvent
{
    public Caliber Caliber;
    public int Level;
    public int Count;
}

/// <summary>玩家生命归零时广播，由 GameRoot 接管进入失败结算。只在战斗进行中发送。</summary>
public struct PlayerDiedEvent
{
}
