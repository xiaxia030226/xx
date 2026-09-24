using UnityEngine;

/// <summary>敌人死亡时广播已掷出的掉落结果，由监听方生成拾取物。</summary>
public struct EnemyDiedEvent
{
    public string EnemyId; // 死亡敌人的配置标识。
    public Vector3 Position; // 敌人死亡时的世界坐标，供掉落定位。

    public int Gold; // 掉落金币数，零表示无金币掉落。

    public bool DropAmmo; // true 表示有弹药掉落；false 时弹药分类及数量不用于生成。

    public Caliber AmmoCaliber; // 掉落弹药的口径。

    public int AmmoLevel; // 掉落弹药的穿甲等级。

    public int AmmoCount; // 掉落弹药总数，生成拾取物时记为 Loot 来源。
    public int ShieldLevel; // 掉落护盾等级，零表示无护盾掉落。
    public string WeaponId; // 掉落武器标识，空值表示无武器掉落。
}

// 武器成功加入槽位后通知界面刷新。
public struct WeaponAddedEvent
{
    public int SlotIndex; // 新增武器所在槽位，从零开始。
}

// 玩家本次受击破盾但仍存活时发送，不携带额外数据。
public struct PlayerShieldBrokenEvent
{
}

/// <summary>开始新波次时广播，Wave 是当前波次编号。</summary>
public struct WaveStartedEvent
{
    public int Wave; // 本次启动的波次编号。
}

/// <summary>全部波次生成任务结束且场上敌人清零后广播，由流程进入安全拾取阶段。</summary>
public struct AllWavesClearedEvent
{
}

/// <summary>当前武器槽同步时广播，包括切换及初始化同步，槽位下标从零开始。</summary>
public struct WeaponSwitchedEvent
{
    public int SlotIndex; // 当前选中的槽位，允许该槽为空。
}

/// <summary>武器资源（弹夹弹量）变化或当前槽同步时广播，供 HUD 更新弹药显示。</summary>
public struct WeaponResourceChangedEvent
{
    public int SlotIndex; // 资源数值所属武器槽位。
    public float Current; // 当前弹夹弹量，不含换弹预扣批次。
    public float Max; // 弹夹容量上限，空槽同步时为零。
}

/// <summary>武器耐久变化或当前槽同步时广播，供 HUD 更新耐久显示。</summary>
public struct WeaponDurabilityChangedEvent
{
    public int SlotIndex; // 耐久数值所属武器槽位。
    public float Current; // 当前剩余耐久。
    public float Max; // 武器耐久上限，空槽同步时为零。
}

/// <summary>武器耐久归零报废时广播，该槽位已被置空；HUD 用于清槽，不做自动切枪。</summary>
public struct WeaponBrokenEvent
{
    public int SlotIndex; // 已报废并置空的武器槽位。
}

// 报废枪的已装余弹转为地面拾取物；pending 另行退库，不混入本事件。
public struct WeaponAmmoDroppedEvent
{
    public Vector3 Position; // 余弹掉落的世界坐标。
    public Caliber Caliber; // 余弹口径。
    public int Level; // 余弹原装填穿甲等级。
    public AmmoBatch Ammo; // 卸出的完整弹药批次，保留 Supply/Loot 份额，不随枪械来源改写。
}

/// <summary>换弹预扣数量小于本次需求时广播，包含完全无弹的情况。</summary>
public struct AmmoShortageEvent
{
    public int SlotIndex; // 发起换弹且库存不足的武器槽位。

    public int Loaded; // 本次实际预扣的弹数，尚未装夹；零表示未能取出弹药。

    public int Wanted; // 本次期望补入的弹数，即弹夹剩余缺口。
}

/// <summary>子弹库存变化时广播，供 HUD 刷新库存显示。</summary>
public struct BulletInventoryChangedEvent
{
    public Caliber Caliber; // 发生变化的库存口径。
    public int Level; // 发生变化的库存穿甲等级。
    public int Count; // 该分类最新库存总数，包含两种来源但不含 pending 或弹夹。
}

/// <summary>玩家生命归零时广播，由 GameRoot 接管进入失败结算。只在战斗进行中发送。</summary>
public struct PlayerDiedEvent
{
}
