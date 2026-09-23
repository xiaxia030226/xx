using QFramework;

public class PickupWeaponCommand : AbstractCommand
{
    private readonly string mWeaponId;
    private readonly ItemOrigin mOrigin;
    public bool Succeeded { get; private set; }

    public PickupWeaponCommand(string weaponId, ItemOrigin origin)
    {
        mWeaponId = weaponId;
        mOrigin = origin;
    }

    protected override void OnExecute()
    {
        var state = this.GetModel<IGameStateModel>().State.Value;
        if (state != GameState.Playing && state != GameState.SafeLoot) return;
        Succeeded = this.GetSystem<IWeaponSystem>().TryPickupWeapon(mWeaponId, mOrigin);
    }
}
