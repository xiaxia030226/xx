using QFramework;

public class AddBulletsCommand : AbstractCommand
{
    public Caliber Caliber { get; } // 入库弹药口径。
    public int Level { get; } // 入库弹药穿甲等级。
    public AmmoBatch Ammo { get; } // 入库批次，保留补给与战利品份额。

    // 作用：记录待入库的弹药及分类；返回：无返回值（构造函数）。
    public AddBulletsCommand(Caliber caliber, int level, AmmoBatch ammo)
    {
        // 保存完整批次，拾取和退弹均不在命令中重算来源。
        Caliber = caliber;
        Level = level;
        Ammo = ammo;
    }

    // 作用：将批次加入对应口径和等级的库存；返回：无返回值。
    protected override void OnExecute()
    {
        // 由库存模型合并数量及来源份额，并统一通知库存变化。
        this.GetModel<IBulletInventoryModel>().Add(Caliber, Level, Ammo);
    }
}
