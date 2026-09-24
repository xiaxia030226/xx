using QFramework;

/// <summary>同步预扣换弹所需库存；发送后通过 Taken 读取实际批次，尚未直接装入弹夹。</summary>
public class TakeBulletsCommand : AbstractCommand
{
    public Caliber Caliber { get; } // 要取出的子弹口径。
    public int Level { get; } // 要取出的穿甲等级。
    public int Want { get; } // 期望预扣数量，补满时为弹夹缺口而非总容量。

    public AmmoBatch Taken { get; private set; } // 实际出库批次，包含补给与战利品份额。

    // 作用：记录子弹出库分类及期望数量；返回：无返回值（构造函数）。
    public TakeBulletsCommand(Caliber caliber, int level, int want)
    {
        // 只保存请求，实际数量由执行时的库存决定。
        Caliber = caliber;
        Level = level;
        Want = want;
    }

    // 作用：按库存余量取出弹药并记录批次；返回：无返回值，结果由 Taken 提供。
    protected override void OnExecute()
    {
        // 库存模型先扣补给份额，调用方随后将批次移入 pending，避免重复计数。
        Taken = this.GetModel<IBulletInventoryModel>().Take(Caliber, Level, Want);
    }
}
