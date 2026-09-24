using System;

public enum ItemOrigin
{
    Supply, // 补给来源；枪械来源与弹药来源分别记录。
    Loot // 战利品来源；不由所持枪械来源推断弹药来源。
}

// 用总量和补给份额记录弹药来源，可在库存、待装填批次和弹夹之间完整转移。
public readonly struct AmmoBatch
{
    public int Count { get; } // 批次总弹数，包含补给弹与战利品弹。
    public int SupplyCount { get; } // 批次内补给弹数量。
    public int LootCount => Count - SupplyCount; // 战利品弹数量，由总量减去补给份额得到。

    // 作用：创建来源份额合法的弹药批次；返回：无返回值（构造函数）。
    public AmmoBatch(int count, int supplyCount)
    {
        // 总量不能为负，补给份额必须落在总量内，确保剩余战利品份额也非负。
        if (count < 0) throw new ArgumentOutOfRangeException(nameof(count));
        if (supplyCount < 0 || supplyCount > count)
            throw new ArgumentOutOfRangeException(nameof(supplyCount));
        Count = count;
        SupplyCount = supplyCount;
    }

    // 作用：创建纯补给弹批次；返回：总量与补给量均为 count 的批次。
    public static AmmoBatch Supply(int count) => new AmmoBatch(count, count); // 将全部数量计入补给份额。
    // 作用：创建纯战利品弹批次；返回：总量为 count、补给量为零的批次。
    public static AmmoBatch Loot(int count) => new AmmoBatch(count, 0); // 未计入补给的数量全部属于战利品。
}
