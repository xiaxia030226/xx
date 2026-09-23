using System;

public enum ItemOrigin
{
    Supply,
    Loot
}

public readonly struct AmmoBatch
{
    public int Count { get; }
    public int SupplyCount { get; }
    public int LootCount => Count - SupplyCount;

    public AmmoBatch(int count, int supplyCount)
    {
        if (count < 0) throw new ArgumentOutOfRangeException(nameof(count));
        if (supplyCount < 0 || supplyCount > count)
            throw new ArgumentOutOfRangeException(nameof(supplyCount));
        Count = count;
        SupplyCount = supplyCount;
    }

    public static AmmoBatch Supply(int count) => new AmmoBatch(count, count);
    public static AmmoBatch Loot(int count) => new AmmoBatch(count, 0);
}
