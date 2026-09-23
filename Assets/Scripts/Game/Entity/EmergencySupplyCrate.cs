using System;
using QFramework;
using UnityEngine;

public class EmergencySupplyCrate : MonoBehaviour
{
    [SerializeField, Min(0.01f)] private float mOpenRadius = 1.5f;
    [SerializeField] private GameObject mClosedVisual;
    [SerializeField] private GameObject mOpenedVisual;

    private IGameStateModel mState;
    private IGameObjectPoolSystem mPool;
    private WeaponConfig mWeapon;
    private Transform mTarget;
    private Transform mPickupRoot;
    private AmmoBatch mAmmoLevel0;
    private AmmoBatch mAmmoLevel1;
    private bool mInitialized;

    public float OpenRadius => mOpenRadius;
    public bool IsOpened { get; private set; }

    public void Initialize(StageConfig stage, Transform target, IArchitecture architecture, Transform pickupRoot)
    {
        mInitialized = false;
        if (stage == null) throw new ArgumentNullException(nameof(stage));
        if (target == null) throw new ArgumentNullException(nameof(target));
        if (architecture == null) throw new ArgumentNullException(nameof(architecture));
        if (pickupRoot == null) throw new ArgumentNullException(nameof(pickupRoot));
        if (string.IsNullOrWhiteSpace(stage.EmergencyWeaponId) ||
            !WeaponConfigTable.TryGet(stage.EmergencyWeaponId, out var weapon) || weapon == null)
            throw new InvalidOperationException($"阶段四装配异常：应急箱武器配置无效：{stage.EmergencyWeaponId}");
        if (stage.EmergencyAmmoLevel0 <= 0 || stage.EmergencyAmmoLevel1 <= 0)
            throw new InvalidOperationException("阶段四装配异常：应急箱两个等级的弹药数量必须大于零。");
        if (mOpenRadius <= 0f || float.IsNaN(mOpenRadius) || float.IsInfinity(mOpenRadius))
            throw new InvalidOperationException("阶段四装配异常：应急箱开启半径必须为有限正数。");
        ValidateVisual(mClosedVisual);
        ValidateVisual(mOpenedVisual);
        if (mClosedVisual != null && mOpenedVisual != null &&
            (mClosedVisual.transform.IsChildOf(mOpenedVisual.transform) || mOpenedVisual.transform.IsChildOf(mClosedVisual.transform)))
            throw new InvalidOperationException("阶段四装配异常：开箱前后外观不能是同一对象或互为父子。");

        mPool = architecture.GetSystem<IGameObjectPoolSystem>() ??
            throw new InvalidOperationException("阶段四装配异常：应急箱缺少拾取物池系统。");
        mState = architecture.GetModel<IGameStateModel>() ??
            throw new InvalidOperationException("阶段四装配异常：应急箱缺少游戏状态模型。");
        mPool.GetCachedCount(WeaponPickup.PoolKey);
        mPool.GetCachedCount(AmmoPackPickup.PoolKey);

        mWeapon = weapon;
        mTarget = target;
        mPickupRoot = pickupRoot;
        mAmmoLevel0 = AmmoBatch.Supply(stage.EmergencyAmmoLevel0);
        mAmmoLevel1 = AmmoBatch.Supply(stage.EmergencyAmmoLevel1);
        IsOpened = false;
        UpdateVisuals();
        mInitialized = true;
    }

    private void Update()
    {
        TryOpen();
    }

    public bool TryOpen()
    {
        if (!mInitialized || IsOpened || mTarget == null || mPickupRoot == null) return false;
        var state = mState.State.Value;
        if (state != GameState.Playing && state != GameState.SafeLoot) return false;
        var offset = mTarget.position - transform.position;
        offset.y = 0f;
        if (offset.sqrMagnitude > mOpenRadius * mOpenRadius) return false;

        IsOpened = true;
        var position = new Vector3(transform.position.x, 0.4f, transform.position.z);
        var weapon = mPool.Spawn(WeaponPickup.PoolKey, position + Vector3.forward * 0.5f, Quaternion.identity, mPickupRoot);
        weapon.GetComponent<WeaponPickup>().OnSpawn(mWeapon.Id, ItemOrigin.Supply, mTarget);
        SpawnAmmo(position + Vector3.left * 0.5f, 0, mAmmoLevel0);
        SpawnAmmo(position + Vector3.right * 0.5f, 1, mAmmoLevel1);
        UpdateVisuals();
        return true;
    }

    private void SpawnAmmo(Vector3 position, int level, AmmoBatch ammo)
    {
        var pickup = mPool.Spawn(AmmoPackPickup.PoolKey, position, Quaternion.identity, mPickupRoot);
        pickup.GetComponent<AmmoPackPickup>().OnSpawn(mWeapon.Caliber, level, ammo, mTarget);
    }

    private void ValidateVisual(GameObject visual)
    {
        if (visual != null && (visual.transform == transform || !visual.transform.IsChildOf(transform)))
            throw new InvalidOperationException("阶段四装配异常：应急箱外观必须引用箱根下的子对象。");
    }

    private void UpdateVisuals()
    {
        if (mClosedVisual != null) mClosedVisual.SetActive(!IsOpened);
        if (mOpenedVisual != null) mOpenedVisual.SetActive(IsOpened);
    }
}
