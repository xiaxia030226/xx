using QFramework;
using UnityEngine;

/// <summary>
/// 子弹库存数据接口。库存是本局资源，按（口径, 穿甲等级）计数。
/// </summary>
public interface IBulletInventoryModel : IModel
{
    /// <summary>查询某口径某等级子弹的当前库存数。</summary>
    int GetCount(Caliber caliber, int level);

    /// <summary>入库：增加库存并广播变化事件。仅由 AddBulletsCommand 调用。</summary>
    void Add(Caliber caliber, int level, int count);

    /// <summary>
    /// 出库：尽量取出 want 发子弹，库存不足时只取出实际剩余。
    /// 返回实际取出数量，并广播变化事件。仅由 TakeBulletsCommand 调用。
    /// </summary>
    int Take(Caliber caliber, int level, int want);

    /// <summary>开始新一局时恢复初始库存（S 口径 0 级 ×60）。</summary>
    void Reset();
}

/// <summary>
/// 子弹库存：玩家持有的全部子弹按（口径, 穿甲等级）分桶计数。
/// 初始携带 S 口径 0 级肉弹 60 发；局内通过子弹包掉落补充，换弹时预扣。
/// </summary>
public class BulletInventoryModel : AbstractModel, IBulletInventoryModel
{
    // InitialReserve：开局自带的 S·0 级子弹数量，需覆盖手枪首次装填。
    private const int InitialReserve = 60;

    // mCounts：库存字典，键为 (口径, 等级)。只记录有存量的桶，取空桶视为 0。
    private readonly System.Collections.Generic.Dictionary<(Caliber, int), int> mCounts =
        new System.Collections.Generic.Dictionary<(Caliber, int), int>();

    public int GetCount(Caliber caliber, int level)
    {
        return mCounts.TryGetValue((caliber, level), out var count) ? count : 0;
    }

    public void Add(Caliber caliber, int level, int count)
    {
        if (count <= 0) return;

        var key = (caliber, level);
        mCounts[key] = GetCount(caliber, level) + count;
        NotifyChanged(caliber, level);
    }

    public int Take(Caliber caliber, int level, int want)
    {
        if (want <= 0) return 0;

        var key = (caliber, level);

        // taken：实际取出数——想要的多于库存时只能拿走剩余部分。
        var taken = Mathf.Min(want, GetCount(caliber, level));
        if (taken <= 0) return 0;

        mCounts[key] = GetCount(caliber, level) - taken;
        NotifyChanged(caliber, level);
        return taken;
    }

    public void Reset()
    {
        mCounts.Clear();

        // 开局配给：S 口径 0 级肉弹 60 发，保证手枪能完成首次装填并有余量。
        mCounts[(Caliber.S, 0)] = InitialReserve;
        NotifyChanged(Caliber.S, 0);
    }

    protected override void OnInit()
    {
        Reset();
    }

    /// <summary>
    /// 广播某桶库存的最新数量，供 HUD 刷新对应格子。
    /// </summary>
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
