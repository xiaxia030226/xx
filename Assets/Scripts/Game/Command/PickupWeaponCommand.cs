using QFramework;

public class PickupWeaponCommand : AbstractCommand
{
    private readonly string mWeaponId; // 待拾取武器的配置标识。
    private readonly ItemOrigin mOrigin; // 武器本体来源，不代表枪内弹药来源。
    public bool Succeeded { get; private set; } // true 表示拾取成功；false 表示尚未成功或拾取被拒绝。

    // 作用：保存待拾取武器及其来源；返回：无返回值（构造函数）。
    public PickupWeaponCommand(string weaponId, ItemOrigin origin)
    {
        // 来源随武器请求传递，不根据库存或弹药批次推断。
        mWeaponId = weaponId;
        mOrigin = origin;
    }

    // 作用：在允许拾取的阶段请求武器入槽；返回：无返回值，成功与否写入 Succeeded。
    protected override void OnExecute()
    {
        // 安全拾取仍可拿枪；槽位及武器有效性由武器系统判断。
        var state = this.GetModel<IGameStateModel>().State.Value;
        if (state != GameState.Playing && state != GameState.SafeLoot) return;
        Succeeded = this.GetSystem<IWeaponSystem>().TryPickupWeapon(mWeaponId, mOrigin);
    }
}
