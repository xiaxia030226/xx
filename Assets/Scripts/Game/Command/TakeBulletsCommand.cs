using QFramework;

/// <summary>
/// 子弹出库命令：换弹开始时按选定口径与等级预扣子弹。
/// Command 同步执行，发送后立即读取 Taken 获得实际取出的数量（库存不足时少于 Want）。
/// </summary>
public class TakeBulletsCommand : AbstractCommand
{
    // Caliber/Level/Want：要取出的子弹口径、穿甲等级与期望数量。
    public Caliber Caliber { get; }
    public int Level { get; }
    public int Want { get; }

    // Taken：实际取出的数量，执行后由 Model 写回；调用方发送命令后读取。
    public int Taken { get; private set; }

    public TakeBulletsCommand(Caliber caliber, int level, int want)
    {
        Caliber = caliber;
        Level = level;
        Want = want;
    }

    protected override void OnExecute()
    {
        Taken = this.GetModel<IBulletInventoryModel>().Take(Caliber, Level, Want);
    }
}
