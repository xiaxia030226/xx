using QFramework;

public class AddBulletsCommand : AbstractCommand
{
    public Caliber Caliber { get; }
    public int Level { get; }
    public AmmoBatch Ammo { get; }

    public AddBulletsCommand(Caliber caliber, int level, AmmoBatch ammo)
    {
        Caliber = caliber;
        Level = level;
        Ammo = ammo;
    }

    protected override void OnExecute()
    {
        this.GetModel<IBulletInventoryModel>().Add(Caliber, Level, Ammo);
    }
}
