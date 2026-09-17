using QFramework;

/// <summary>
/// 子弹入库命令：拾取子弹包等场景向库存补充子弹。
/// </summary>
public class AddBulletsCommand : AbstractCommand
{
    // Caliber/Level/Count：要入库的子弹口径、穿甲等级与数量。
    public Caliber Caliber { get; }
    public int Level { get; }
    public int Count { get; }

    public AddBulletsCommand(Caliber caliber, int level, int count)
    {
        Caliber = caliber;
        Level = level;
        Count = count;
    }

    protected override void OnExecute()
    {
        this.GetModel<IBulletInventoryModel>().Add(Caliber, Level, Count);
    }
}
