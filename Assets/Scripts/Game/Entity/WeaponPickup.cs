using QFramework;
using TMPro;
using UnityEngine;

public class WeaponPickup : MonoBehaviour, IController
{
    public const string PoolKey = "weapon_pickup";
    [SerializeField] private TMP_Text mLabel;
    private string mWeaponId;
    private ItemOrigin mOrigin;
    private Transform mTarget;

    public IArchitecture GetArchitecture() => GameArchitecture.Interface;

    public void OnSpawn(string weaponId, ItemOrigin origin, Transform target)
    {
        mWeaponId = weaponId;
        mOrigin = origin;
        mTarget = target;
        if (mLabel != null) mLabel.text = WeaponConfigTable.Get(weaponId).Name;
    }

    private void Update()
    {
        if (mTarget == null) return;
        var state = this.GetModel<IGameStateModel>().State.Value;
        if (state != GameState.Playing && state != GameState.SafeLoot) return;
        var offset = mTarget.position - transform.position;
        offset.y = 0f;
        if (offset.sqrMagnitude > 0.8f * 0.8f) return;
        var command = new PickupWeaponCommand(mWeaponId, mOrigin);
        this.SendCommand(command);
        if (command.Succeeded) this.GetSystem<IGameObjectPoolSystem>().Recycle(PoolKey, gameObject);
    }
}
