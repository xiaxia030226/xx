using System;
using System.Collections.Generic;
using QFramework;

public interface IBulletInventoryModel : IModel
{
    int GetCount(Caliber caliber, int level);
    AmmoBatch GetAmmo(Caliber caliber, int level);
    void Add(Caliber caliber, int level, AmmoBatch ammo);
    AmmoBatch Take(Caliber caliber, int level, int want);
    void Reset();
}

public class BulletInventoryModel : AbstractModel, IBulletInventoryModel
{
    private const int InitialReserve = 60;
    private readonly Dictionary<(Caliber, int), AmmoBatch> mCounts =
        new Dictionary<(Caliber, int), AmmoBatch>();

    public int GetCount(Caliber caliber, int level) => GetAmmo(caliber, level).Count;

    public AmmoBatch GetAmmo(Caliber caliber, int level) =>
        mCounts.TryGetValue((caliber, level), out var ammo) ? ammo : default;

    public void Add(Caliber caliber, int level, AmmoBatch ammo)
    {
        if (ammo.Count == 0) return;
        var current = GetAmmo(caliber, level);
        mCounts[(caliber, level)] = new AmmoBatch(current.Count + ammo.Count,
            current.SupplyCount + ammo.SupplyCount);
        NotifyChanged(caliber, level);
    }

    public AmmoBatch Take(Caliber caliber, int level, int want)
    {
        if (want <= 0) return default;
        var current = GetAmmo(caliber, level);
        var count = Math.Min(want, current.Count);
        if (count == 0) return default;
        var taken = new AmmoBatch(count, Math.Min(count, current.SupplyCount));
        mCounts[(caliber, level)] = new AmmoBatch(current.Count - taken.Count,
            current.SupplyCount - taken.SupplyCount);
        NotifyChanged(caliber, level);
        return taken;
    }

    public void Reset()
    {
        mCounts.Clear();
        mCounts[(Caliber.S, 0)] = AmmoBatch.Supply(InitialReserve);
        NotifyChanged(Caliber.S, 0);
    }

    protected override void OnInit() => Reset();

    private void NotifyChanged(Caliber caliber, int level)
    {
        this.SendEvent(new BulletInventoryChangedEvent
        {
            Caliber = caliber,
            Level = level,
            Count = GetCount(caliber, level)
        });
    }
}
