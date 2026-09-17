using System.Collections.Generic;
using QFramework;
using UnityEngine;

/// <summary>
/// 武器系统对外接口，向 Player 隐藏具体武器列表和切换规则。
/// 槽位可空：武器报废后槽位置 null 腾格，下标即 HUD 槽位号。
/// </summary>
public interface IWeaponSystem : ISystem
{
    int CurrentIndex { get; }

    // Weapons：武器槽位列表（可空元素）。下标即槽位号；报废的槽位为 null，调用方需判空。
    IReadOnlyList<WeaponBase> Weapons { get; }

    // CurrentWeapon：当前槽位的武器；槽位为空或武器栏为空时返回 null，调用方需判空。
    WeaponBase CurrentWeapon { get; }
    void Setup(Transform owner, Transform bulletParent);
    void Tick(float deltaTime);
    bool TryAttackCurrent();
    void SwitchTo(int index);
    void SwitchBy(int offset);

    /// <summary>R：当前武器主动换弹（余弹退库存，按下次装填等级预扣）。</summary>
    void RequestReloadCurrent();

    /// <summary>B：循环切换当前武器下次装填的穿甲等级，各枪独立记忆。</summary>
    void CycleNextLoadLevelCurrent();
    bool TryPickupWeapon(string weaponId);
    void CancelReloads();
}

/// <summary>
/// 管理玩家武器槽、攻击、换弹与耐久报废。
/// 新设计不自动切枪：打空自动换弹、报废腾格，都要玩家手动切枪。
/// </summary>
public class WeaponSystem : AbstractSystem, IWeaponSystem
{
    // ResourceChangeThreshold：弹量/耐久变化的广播阈值，低于该值的变化不触发事件。
    private const float ResourceChangeThreshold = 0.01f;

    // mWeapons：武器槽位列表（可空）。新武器追加到末尾，报废置 null 保持下标稳定。
    private readonly List<WeaponBase> mWeapons = new List<WeaponBase>(9);

    // mOwner：武器持有者（玩家）。
    private Transform mOwner;
    private Transform mBulletParent;

    // mRegisteredPools：本局已注册的子弹池 key（按口径），Setup 时清空重建。
    private readonly HashSet<string> mRegisteredPools = new HashSet<string>();

    // mLastSentResources / mLastSentDurabilities：各槽位上次广播的弹量/耐久，值变化超阈值才发事件。
    private float[] mLastSentResources = new float[0];
    private float[] mLastSentDurabilities = new float[0];

    public int CurrentIndex { get; private set; }
    public IReadOnlyList<WeaponBase> Weapons => mWeapons;

    public WeaponBase CurrentWeapon =>
        CurrentIndex >= 0 && CurrentIndex < mWeapons.Count ? mWeapons[CurrentIndex] : null;

    /// <summary>
    /// 绑定武器持有者并建立初始武器栏：第一格手枪（S 口径）、第二格机枪（AR 口径）。
    /// 子弹对象池按口径注册（同口径 6 级共享预制体），用到哪个口径才注册哪个（懒注册）。
    /// 注意：这里不广播切枪事件——本方法执行时 HUD 尚未打开，
    /// 事件会因无人订阅而丢失；HUD 在 OnInit 中主动拉取当前状态完成初始显示。
    /// </summary>
    /// <param name="owner">武器持有者（玩家）</param>
    /// <param name="bulletParent">子弹实例的挂载点（BulletRoot）</param>
    public void Setup(Transform owner, Transform bulletParent)
    {
        mOwner = owner;
        mBulletParent = bulletParent;

        // 跨场景重入时直接丢弃上局武器：换弹预扣子弹随 BulletInventoryModel.Reset 一并清零，
        // 无需逐枪退回（退回了也会被 Reset 覆盖）。
        mWeapons.Clear();
        mRegisteredPools.Clear();

        // 装配开局武器（机枪为阶段二临时测试配置，用于验证手动切枪与各枪独立装填；
        // 阶段四整枪掉落上线后移除开局机枪）。
        AddGun(WeaponConfigTable.PistolId, bulletParent);
        AddGun(WeaponConfigTable.MachineGunId, bulletParent);

        CurrentIndex = 0;

        // 初始化广播缓存：初值 -1 与任何合法值都不同，保证首帧必发一次事件。
        mLastSentResources = new float[mWeapons.Count];
        mLastSentDurabilities = new float[mWeapons.Count];
        for (var i = 0; i < mWeapons.Count; i++)
        {
            mLastSentResources[i] = -1f;
            mLastSentDurabilities[i] = -1f;
        }

        // 首次装填：从库存预扣（初始 S·0×60 已含手枪首装份额；机枪 AR 弹靠掉落或调试键补充）。
        foreach (var weapon in mWeapons)
        {
            (weapon as GunWeapon)?.RequestReload();
        }
    }

    /// <summary>
    /// 每帧推进所有武器（含后台武器，换弹在后台照常计时），并按需广播弹量/耐久变化。
    /// </summary>
    public void Tick(float deltaTime)
    {
        if (this.GetModel<IGameStateModel>().State.Value != GameState.Playing) return;
        for (var i = 0; i < mWeapons.Count; i++)
        {
            var weapon = mWeapons[i];
            if (weapon == null) continue;

            weapon.Tick(deltaTime);

            // 耐久归零报废：退回换弹预扣子弹、槽位置空、广播事件；不自动切枪。
            if (weapon.IsBroken)
            {
                BreakWeapon(i);
                continue;
            }

            // 弹量变化超阈值才广播，避免静止时 HUD 重复刷新相同显示。
            if (Mathf.Abs(weapon.Resource - mLastSentResources[i]) > ResourceChangeThreshold)
            {
                mLastSentResources[i] = weapon.Resource;
                this.SendEvent(new WeaponResourceChangedEvent
                {
                    SlotIndex = i,
                    Current = weapon.Resource,
                    Max = weapon.ResourceMax
                });
            }

            // 耐久变化同理广播，供 HUD 更新耐久数字与警示色。
            if (Mathf.Abs(weapon.Durability - mLastSentDurabilities[i]) > ResourceChangeThreshold)
            {
                mLastSentDurabilities[i] = weapon.Durability;
                this.SendEvent(new WeaponDurabilityChangedEvent
                {
                    SlotIndex = i,
                    Current = weapon.Durability,
                    Max = weapon.DurabilityMax
                });
            }
        }
    }

    /// <summary>
    /// 尝试用当前武器攻击；失败（换弹中/冷却中/空夹/已报废/空槽）仅返回 false，不做任何切换。
    /// </summary>
    public bool TryAttackCurrent()
    {
        if (this.GetModel<IGameStateModel>().State.Value != GameState.Playing) return false;
        var weapon = CurrentWeapon;
        if (mOwner == null || weapon == null) return false;
        var fired = weapon.TryAttack(mOwner);
        if (weapon.IsBroken) BreakWeapon(CurrentIndex);
        return fired;
    }

    /// <summary>
    /// 数字键直切到指定槽位（允许切到空槽，由 HUD 显示空格）。
    /// </summary>
    public void SwitchTo(int index)
    {
        if (index < 0 || index >= mWeapons.Count || index == CurrentIndex) return;
        CurrentIndex = index;
        SendCurrentWeaponEvents();
    }

    /// <summary>
    /// 滚轮循环切枪：跳过空槽，在实装武器间循环。
    /// </summary>
    public void SwitchBy(int offset)
    {
        if (mWeapons.Count <= 1 || offset == 0) return;

        // 逐格探测下一个非空槽位；全部为空（极端情况）时保持当前槽不动。
        for (var step = 1; step <= mWeapons.Count; step++)
        {
            // 取模实现首尾循环；C# 负数取模仍为负，所以反向切换时要再加一次数量。
            var index = (CurrentIndex + offset * step) % mWeapons.Count;
            if (index < 0) index += mWeapons.Count;
            if (mWeapons[index] == null) continue;

            SwitchTo(index);
            return;
        }
    }

    public void RequestReloadCurrent()
    {
        if (this.GetModel<IGameStateModel>().State.Value != GameState.Playing) return;
        (CurrentWeapon as GunWeapon)?.RequestReload();
    }

    public void CycleNextLoadLevelCurrent()
    {
        if (this.GetModel<IGameStateModel>().State.Value != GameState.Playing) return;
        (CurrentWeapon as GunWeapon)?.CycleNextLoadLevel();
    }

    public bool TryPickupWeapon(string weaponId)
    {
        var state = this.GetModel<IGameStateModel>().State.Value;
        if (state != GameState.Playing && state != GameState.SafeLoot) return false;
        if (weaponId != WeaponConfigTable.PistolId && weaponId != WeaponConfigTable.MachineGunId) return false;
        var index = mWeapons.FindIndex(weapon => weapon == null);
        if (index < 0 && mWeapons.Count >= 9) return false;
        var config = WeaponConfigTable.Get(weaponId);
        if (config.DurabilityMax <= 0f || config.Magazine <= 0) return false;
        EnsureBulletPool(config.Caliber, mBulletParent);
        if (index < 0)
        {
            index = mWeapons.Count;
            mWeapons.Add(null);
            System.Array.Resize(ref mLastSentResources, mWeapons.Count);
            System.Array.Resize(ref mLastSentDurabilities, mWeapons.Count);
        }
        mWeapons[index] = new GunWeapon(config, GameArchitecture.Interface, mBulletParent) { SlotIndex = index };
        mLastSentResources[index] = -1f;
        mLastSentDurabilities[index] = -1f;
        this.SendEvent(new WeaponAddedEvent { SlotIndex = index });
        return true;
    }

    public void CancelReloads()
    {
        foreach (var weapon in mWeapons) (weapon as GunWeapon)?.RefundPendingLoad();
    }

    protected override void OnInit()
    {
    }

    /// <summary>
    /// 装配一把枪到下一个槽位：确保其口径的子弹池已注册，创建 GunWeapon 并写入槽位号。
    /// </summary>
    private void AddGun(string weaponId, Transform bulletParent)
    {
        var config = WeaponConfigTable.Get(weaponId);
        if (config.DurabilityMax <= 0f || config.Magazine <= 0)
            throw new System.InvalidOperationException($"武器配置未迁移：{weaponId}，请执行阶段三资源校验。");
        EnsureBulletPool(config.Caliber, bulletParent);

        // AbstractSystem 的 GetArchitecture 是显式接口实现无法直接调用，这里与实体脚本一致用架构单例。
        var gun = new GunWeapon(config, GameArchitecture.Interface, bulletParent)
        {
            SlotIndex = mWeapons.Count
        };
        mWeapons.Add(gun);
    }

    /// <summary>
    /// 按口径懒注册子弹对象池：同口径 6 个穿甲等级共享同一预制体与池。
    /// 子弹配置资产由 Editor 菜单一键生成；缺失时报错提示先生成。
    /// </summary>
    private void EnsureBulletPool(Caliber caliber, Transform bulletParent)
    {
        var poolKey = AmmoTypes.PoolKey(caliber);
        if (mRegisteredPools.Contains(poolKey)) return;
        for (var level = 0; level < AmmoTypes.LevelCount; level++)
            BulletConfigTable.Get(AmmoTypes.BulletId(caliber, level));
        var bulletConfig = BulletConfigTable.Get(AmmoTypes.BulletId(caliber, 0));
        var prefab = Resources.Load<GameObject>(bulletConfig.PrefabPath);
        if (prefab == null || prefab.GetComponent<Bullet>() == null)
            throw new System.InvalidOperationException($"缺少有效子弹预制体：{bulletConfig.PrefabPath}");
        this.GetSystem<IGameObjectPoolSystem>().Register(poolKey, () =>
        {
            var bulletObject = Object.Instantiate(prefab, bulletParent);
            bulletObject.name = bulletConfig.Name;
            return bulletObject;
        }, 16);
        mRegisteredPools.Add(poolKey);
    }

    /// <summary>
    /// 武器报废处理：退回换弹预扣子弹、槽位置空、广播报废事件。不自动切枪。
    /// </summary>
    private void BreakWeapon(int slotIndex)
    {
        var weapon = mWeapons[slotIndex];

        (weapon as GunWeapon)?.RefundPendingLoad();
        mWeapons[slotIndex] = null;

        // 重置广播缓存，将来该槽位装新枪时首帧必发事件。
        mLastSentResources[slotIndex] = -1f;
        mLastSentDurabilities[slotIndex] = -1f;

        this.SendEvent(new WeaponBrokenEvent { SlotIndex = slotIndex });
        Debug.Log($"[Weapon] 槽位 {slotIndex + 1} 的 {weapon.Name} 耐久归零报废");
    }

    /// <summary>
    /// 切枪后广播当前槽位状态；空槽广播零值事件让 HUD 清空显示。
    /// </summary>
    private void SendCurrentWeaponEvents()
    {
        this.SendEvent(new WeaponSwitchedEvent { SlotIndex = CurrentIndex });

        var weapon = CurrentWeapon;

        // 同步广播缓存为当前值，避免切枪后下一帧 Tick 因差值超过阈值再重复发一次相同数值。
        mLastSentResources[CurrentIndex] = weapon?.Resource ?? 0f;
        mLastSentDurabilities[CurrentIndex] = weapon?.Durability ?? 0f;

        this.SendEvent(new WeaponResourceChangedEvent
        {
            SlotIndex = CurrentIndex,
            Current = weapon?.Resource ?? 0f,
            Max = weapon?.ResourceMax ?? 0f
        });
        this.SendEvent(new WeaponDurabilityChangedEvent
        {
            SlotIndex = CurrentIndex,
            Current = weapon?.Durability ?? 0f,
            Max = weapon?.DurabilityMax ?? 0f
        });
    }
}
