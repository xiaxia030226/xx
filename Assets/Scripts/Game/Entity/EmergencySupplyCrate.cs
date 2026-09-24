using System;
using QFramework;
using UnityEngine;

public class EmergencySupplyCrate : MonoBehaviour
{
    [SerializeField, Min(0.01f)] private float mOpenRadius = 1.5f; // 玩家触发开箱的 XZ 平面距离半径。
    [SerializeField] private GameObject mClosedVisual; // 箱根下的未开启外观，可不指定。
    [SerializeField] private GameObject mOpenedVisual; // 箱根下的已开启外观，可不指定。

    private IGameStateModel mState; // 用于开箱状态门控的游戏状态模型。
    private IGameObjectPoolSystem mPool; // 生成武器和弹药拾取物的对象池。
    private WeaponConfig mWeapon; // 本关应急武器配置，亦决定弹药口径。
    private Transform mTarget; // 开箱距离判定及拾取物追踪的玩家目标。
    private Transform mPickupRoot; // 新生成拾取物的场景父节点。
    private AmmoBatch mAmmoLevel0; // 标记为 Supply 来源的零级弹药批次。
    private AmmoBatch mAmmoLevel1; // 标记为 Supply 来源的一级弹药批次。
    private bool mInitialized; // 所有配置及依赖检查完成后才为真。

    public float OpenRadius => mOpenRadius; // 当前开箱判定半径。
    public bool IsOpened { get; private set; } // 是否已触发开箱；初始化时重置。

    // 作用：校验箱子装配并准备本关补给数据；返回：无返回值。
    public void Initialize(StageConfig stage, Transform target, IArchitecture architecture, Transform pickupRoot)
    {
        // 先关闭可用标记，任何校验抛错都不会让半初始化的箱子触发。
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
        // 查询会校验池键已注册，避免开箱时才暴露缺失的拾取物池。
        mPool.GetCachedCount(WeaponPickup.PoolKey);
        mPool.GetCachedCount(AmmoPackPickup.PoolKey);

        mWeapon = weapon;
        mTarget = target;
        mPickupRoot = pickupRoot;
        // 这里只准备补给来源批次，尚不生成掉落或直接加入背包。
        mAmmoLevel0 = AmmoBatch.Supply(stage.EmergencyAmmoLevel0);
        mAmmoLevel1 = AmmoBatch.Supply(stage.EmergencyAmmoLevel1);
        IsOpened = false;
        UpdateVisuals();
        mInitialized = true;
    }

    // 作用：每帧尝试根据玩家距离自动开箱；返回：无返回值。
    private void Update()
    {
        // 每帧只转交开箱尝试，由 TryOpen 统一拦截重复开启及状态、距离不符的情况。
        TryOpen();
    }

    // 作用：满足状态与距离条件时生成补给掉落；返回：本次成功开箱为真，条件不满足或已开启为假。
    public bool TryOpen()
    {
        // 只允许 Playing/SafeLoot，且每轮初始化最多开启一次。
        if (!mInitialized || IsOpened || mTarget == null || mPickupRoot == null) return false;
        var state = mState.State.Value;
        if (state != GameState.Playing && state != GameState.SafeLoot) return false;
        // 忽略高度，仅比较箱子与玩家的 XZ 平面距离。
        var offset = mTarget.position - transform.position;
        offset.y = 0f;
        if (offset.sqrMagnitude > mOpenRadius * mOpenRadius) return false;

        // 先标记开启，再生成三个 Supply 掉落：一把武器和两级弹药；不直接入栏。
        IsOpened = true;
        var position = new Vector3(transform.position.x, 0.4f, transform.position.z);
        var weapon = mPool.Spawn(WeaponPickup.PoolKey, position + Vector3.forward * 0.5f, Quaternion.identity, mPickupRoot);
        weapon.GetComponent<WeaponPickup>().OnSpawn(mWeapon.Id, ItemOrigin.Supply, mTarget);
        SpawnAmmo(position + Vector3.left * 0.5f, 0, mAmmoLevel0);
        SpawnAmmo(position + Vector3.right * 0.5f, 1, mAmmoLevel1);
        UpdateVisuals();
        return true;
    }

    // 作用：生成指定等级弹药掉落并覆盖池对象本轮数据；返回：无返回值。
    private void SpawnAmmo(Vector3 position, int level, AmmoBatch ammo)
    {
        // 先从池中取得掉落物，再用应急武器的口径和传入批次初始化，保持补给弹药与武器匹配。
        var pickup = mPool.Spawn(AmmoPackPickup.PoolKey, position, Quaternion.identity, mPickupRoot);
        pickup.GetComponent<AmmoPackPickup>().OnSpawn(mWeapon.Caliber, level, ammo, mTarget);
    }

    // 作用：校验非空外观确属箱根的子对象，否则抛错；返回：无返回值。
    private void ValidateVisual(GameObject visual)
    {
        // 不允许切换箱根自身，避免连带禁用开箱逻辑。
        if (visual != null && (visual.transform == transform || !visual.transform.IsChildOf(transform)))
            throw new InvalidOperationException("阶段四装配异常：应急箱外观必须引用箱根下的子对象。");
    }

    // 作用：根据开启标记切换两种可选外观；返回：无返回值。
    private void UpdateVisuals()
    {
        // 两种外观使用相反的开启条件，各自判空以兼容只配置一种外观的箱子。
        if (mClosedVisual != null) mClosedVisual.SetActive(!IsOpened);
        if (mOpenedVisual != null) mOpenedVisual.SetActive(IsOpened);
    }
}
