using System;
using System.Collections.Generic;
using QFramework;

public interface IBulletInventoryModel : IModel
{
    // 作用：查询指定口径和等级的库存总量；返回：现有弹数，无记录时为零。
    int GetCount(Caliber caliber, int level);
    // 作用：查询库存的数量及来源份额；返回：现有弹药批次，无记录时为默认空批次。
    AmmoBatch GetAmmo(Caliber caliber, int level);
    // 作用：将批次按原来源份额并入库存；返回：无返回值。
    void Add(Caliber caliber, int level, AmmoBatch ammo);
    // 作用：优先扣补给弹并取出不超过需求的库存；返回：实际取出的批次，无需求或无库存时为空。
    AmmoBatch Take(Caliber caliber, int level, int want);
    // 作用：清空库存并恢复初始补给弹；返回：无返回值。
    void Reset();
}

public class BulletInventoryModel : AbstractModel, IBulletInventoryModel
{
    private const int InitialReserve = 60; // 初始发放的 S 口径零级补给弹数量。
    private readonly Dictionary<(Caliber, int), AmmoBatch> mCounts =
        new Dictionary<(Caliber, int), AmmoBatch>(); // 按口径和等级保存库存批次，不含已移入 pending 或弹夹的弹药。

    // 作用：读取指定分类的库存总弹数；返回：补给弹与战利品弹的数量之和。
    public int GetCount(Caliber caliber, int level) => GetAmmo(caliber, level).Count; // 总量直接取自保留来源信息的批次。

    // 作用：读取指定分类的库存批次；返回：已保存批次，分类不存在时为默认空批次。
    public AmmoBatch GetAmmo(Caliber caliber, int level) =>
        mCounts.TryGetValue((caliber, level), out var ammo) ? ammo : default; // 未建档分类按零库存处理，不额外插入字典。

    // 作用：合并入库弹药并广播库存变化；返回：无返回值。
    public void Add(Caliber caliber, int level, AmmoBatch ammo)
    {
        // 空批次不改变库存，也不发送无意义的变化事件。
        if (ammo.Count == 0) return;
        var current = GetAmmo(caliber, level);
        // 总量和补给量分别相加，战利品份额由差值保留，退弹不会改变来源。
        mCounts[(caliber, level)] = new AmmoBatch(current.Count + ammo.Count,
            current.SupplyCount + ammo.SupplyCount);
        NotifyChanged(caliber, level);
    }

    // 作用：按需求优先取出补给弹并扣减库存；返回：实际出库批次，无正需求或无弹时为空批次。
    public AmmoBatch Take(Caliber caliber, int level, int want)
    {
        // 需求和库存共同限制取出量，绝不透支。
        if (want <= 0) return default;
        var current = GetAmmo(caliber, level);
        var count = Math.Min(want, current.Count);
        if (count == 0) return default;
        // 先从补给份额取，缺口才由战利品补足；原库存按相同批次扣除以保持守恒。
        var taken = new AmmoBatch(count, Math.Min(count, current.SupplyCount));
        mCounts[(caliber, level)] = new AmmoBatch(current.Count - taken.Count,
            current.SupplyCount - taken.SupplyCount);
        NotifyChanged(caliber, level);
        return taken;
    }

    // 作用：重置为初始 S 口径零级补给库存；返回：无返回值。
    public void Reset()
    {
        // 清除全部分类后仅补回初始批次；这里只广播 S 零级的变化。
        mCounts.Clear();
        mCounts[(Caliber.S, 0)] = AmmoBatch.Supply(InitialReserve);
        NotifyChanged(Caliber.S, 0);
    }

    // 作用：模型初始化时准备初始弹药库存；返回：无返回值。
    protected override void OnInit() => Reset(); // 复用重置流程，确保初始化与新局库存规则一致。

    // 作用：广播指定口径和等级的最新库存总量；返回：无返回值。
    private void NotifyChanged(Caliber caliber, int level)
    {
        // 变更完成后重新取总量，事件不携带或改写来源份额。
        this.SendEvent(new BulletInventoryChangedEvent
        {
            Caliber = caliber,
            Level = level,
            Count = GetCount(caliber, level)
        });
    }
}
