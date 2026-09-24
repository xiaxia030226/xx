using System.Collections.Generic;
using QFramework;
using UnityEngine;

/// <summary>
/// 武器系统对外接口，向 Player 隐藏具体武器列表和切换规则。
/// 槽位可空：武器报废后槽位置 null 腾格，下标即 HUD 槽位号。
/// </summary>
public interface IWeaponSystem : ISystem
{
    int CurrentIndex { get; } // 当前选中的零基槽位下标，槽内可以没有武器。

    IReadOnlyList<WeaponBase> Weapons { get; } // 武器槽只读视图，最多九槽，空槽为 null 且下标保持稳定。

    WeaponBase CurrentWeapon { get; } // 当前槽武器，槽空或下标无效时为 null。
    // 作用：绑定玩家和子弹节点，重建仅含补给手枪的初始武器栏；返回：无返回值。
    void Setup(Transform owner, Transform bulletParent);
    // 作用：推进全部武器并处理报废和数值变化广播；返回：无返回值。
    void Tick(float deltaTime);
    // 作用：尝试让当前武器攻击且不自动切枪；返回：true 表示已攻击，false 表示状态、持有者或武器条件不满足。
    bool TryAttackCurrent();
    // 作用：切到有效的指定槽位，允许目标为空；返回：无返回值。
    void SwitchTo(int index);
    // 作用：按偏移循环探测非空槽并切换；返回：无返回值。
    void SwitchBy(int offset);

    // 作用：请求当前枪主动换弹，同级补缺口、异级先退余弹，再预扣装填；返回：无返回值。
    void RequestReloadCurrent();

    // 作用：循环选择当前枪下次装填等级，不改变已装或预扣等级；返回：无返回值。
    void CycleNextLoadLevelCurrent();
    // 作用：按来源尝试将武器装入空槽或新增槽；返回：true 表示已装入，false 表示状态、配置或槽位条件不满足。
    bool TryPickupWeapon(string weaponId, ItemOrigin origin);
    // 作用：取消所有枪械的在途装填并退还预扣库存；返回：无返回值。
    void CancelReloads();
}

/// <summary>
/// 管理玩家武器槽、攻击、换弹与耐久报废。
/// 新设计不自动切枪：打空自动换弹、报废腾格，都要玩家手动切枪。
/// </summary>
public class WeaponSystem : AbstractSystem, IWeaponSystem
{
    private const float ResourceChangeThreshold = 0.01f; // 弹量或耐久差值必须大于此阈值才广播。

    private readonly List<WeaponBase> mWeapons = new List<WeaponBase>(9); // 最多九个稳定下标槽，拾取优先复用报废留下的 null 槽。

    private Transform mOwner; // 当前玩家的变换，用作射击朝向和报废掉弹位置。
    private Transform mBulletParent; // 当前战斗场景中子弹实例的父节点。

    private readonly HashSet<string> mRegisteredPools = new HashSet<string>(); // 本次 Setup 后已注册的口径池标识，用于去重。

    private float[] mLastSentResources = new float[0]; // 各槽上次广播的弹量，-1 强制后续 Tick 首次刷新。
    private float[] mLastSentDurabilities = new float[0]; // 各槽上次广播的耐久，与槽位列表保持相同长度。

    public int CurrentIndex { get; private set; } // 当前选中的零基槽位下标，报废后仍停留此槽。
    public IReadOnlyList<WeaponBase> Weapons => mWeapons; // 包含 null 空槽的武器栏只读视图。

    public WeaponBase CurrentWeapon =>
        CurrentIndex >= 0 && CurrentIndex < mWeapons.Count ? mWeapons[CurrentIndex] : null; // 安全读取当前槽；无效或空槽返回 null。

    // 作用：绑定持有者和子弹节点，创建开局手枪并从初始库存启动首装；返回：无返回值。
    public void Setup(Transform owner, Transform bulletParent)
    {
        mOwner = owner;
        mBulletParent = bulletParent;

        // 库存已由 GameRoot 重置，旧枪的 pending 不能退入新一局库存。
        mWeapons.Clear();
        mRegisteredPools.Clear();
        AddGun(WeaponConfigTable.PistolId, bulletParent);

        // 此时 HUD 尚未打开，不发送切枪事件；HUD 初始化时主动拉取状态。
        CurrentIndex = 0;

        // 初始化广播缓存：初值 -1 与任何合法值都不同，保证首帧必发一次事件。
        mLastSentResources = new float[mWeapons.Count];
        mLastSentDurabilities = new float[mWeapons.Count];
        for (var i = 0; i < mWeapons.Count; i++)
        {
            mLastSentResources[i] = -1f;
            mLastSentDurabilities[i] = -1f;
        }

        // 开局只装配一把手枪；S·0 总量 60 已含首装，预扣只是库存到 pending 的转移。
        foreach (var weapon in mWeapons)
        {
            (weapon as GunWeapon)?.RequestReload();
        }
    }

    // 作用：由 GameRoot 每帧推进全部槽位的武器并广播变化、处理报废；返回：无返回值。
    public void Tick(float deltaTime)
    {
        if (this.GetModel<IGameStateModel>().State.Value != GameState.Playing) return;
        for (var i = 0; i < mWeapons.Count; i++)
        {
            var weapon = mWeapons[i];
            if (weapon == null) continue;
            // 报废必须先走系统卸夹、腾槽流程，再退款，不能先让枪的 Tick 触发同步库存事件。
            if (weapon.IsBroken)
            {
                BreakWeapon(i);
                continue;
            }

            // 非当前槽也推进冷却和换弹，切枪不会重置或暂停后台装填。
            weapon.Tick(deltaTime);

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

    // 作用：尝试当前武器攻击并立即清理报废武器，不自动切枪；返回：true 表示已攻击，false 表示非战斗状态、无持有者或武器不满足攻击条件。
    public bool TryAttackCurrent()
    {
        if (this.GetModel<IGameStateModel>().State.Value != GameState.Playing) return false;
        var weapon = CurrentWeapon;
        if (mOwner == null || weapon == null) return false;
        var fired = weapon.TryAttack(mOwner);
        // 发射先于耐久归零处理，清槽不撤回最后一发子弹，返回值仍保留此次攻击结果。
        if (weapon.IsBroken) BreakWeapon(CurrentIndex);
        return fired;
    }

    // 作用：切换到指定有效槽位并广播新状态，允许切到空槽；返回：无返回值。
    public void SwitchTo(int index)
    {
        // 忽略越界与重复选择，不因目标为空而跳到其他武器。
        if (index < 0 || index >= mWeapons.Count || index == CurrentIndex) return;
        CurrentIndex = index;
        SendCurrentWeaponEvents();
    }

    // 作用：按偏移循环探测非空武器槽并切换；返回：无返回值。
    public void SwitchBy(int offset)
    {
        if (mWeapons.Count <= 1 || offset == 0) return;

        // 每次按 offset 步长探测，最多检查槽位数次；未找到非空槽则保留当前选择。
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

    // 作用：仅在战斗中将 R 换弹请求转交当前枪械，空槽忽略；返回：无返回值。
    public void RequestReloadCurrent()
    {
        // 暂停等非战斗状态不处理输入，空槽通过空条件调用直接忽略。
        if (this.GetModel<IGameStateModel>().State.Value != GameState.Playing) return;
        (CurrentWeapon as GunWeapon)?.RequestReload();
    }

    // 作用：仅在战斗中循环当前枪下次装填等级，B 不修改已预扣等级；返回：无返回值。
    public void CycleNextLoadLevelCurrent()
    {
        // 只允许战斗中修改当前枪的下次选择，不影响其他槽或正在装填的等级。
        if (this.GetModel<IGameStateModel>().State.Value != GameState.Playing) return;
        (CurrentWeapon as GunWeapon)?.CycleNextLoadLevel();
    }

    // 作用：按来源拾取配置有效的空夹武器，优先复用空槽且不自动切枪或首装；返回：true 表示装入成功，false 表示状态、配置或九槽容量限制阻止拾取。
    public bool TryPickupWeapon(string weaponId, ItemOrigin origin)
    {
        var state = this.GetModel<IGameStateModel>().State.Value;
        if (state != GameState.Playing && state != GameState.SafeLoot) return false;
        if (string.IsNullOrWhiteSpace(weaponId) || !WeaponConfigTable.TryGet(weaponId, out var config)) return false;
        // 先复用报废腾出的槽；只有无空槽且不足九槽时才扩容，维持 HUD 槽位下标稳定。
        var index = mWeapons.FindIndex(weapon => weapon == null);
        if (index < 0 && mWeapons.Count >= 9) return false;
        if (config.DurabilityMax <= 0f || config.Magazine <= 0 || config.ReloadTime <= 0f
            || (config.IsAutomatic ? config.RoundsPerMinute <= 0f : config.SemiAutoInterval <= 0f)) return false;
        EnsureBulletPool(config.Caliber, mBulletParent);
        if (index < 0)
        {
            index = mWeapons.Count;
            mWeapons.Add(null);
            System.Array.Resize(ref mLastSentResources, mWeapons.Count);
            System.Array.Resize(ref mLastSentDurabilities, mWeapons.Count);
        }
        mWeapons[index] = new GunWeapon(config, ((IBelongToArchitecture)this).GetArchitecture(), mBulletParent, origin)
        {
            SlotIndex = index
        };
        // 先完成武器和缓存写入再广播，订阅者可以同步拉取新槽状态。
        mLastSentResources[index] = -1f;
        mLastSentDurabilities[index] = -1f;
        this.SendEvent(new WeaponAddedEvent { SlotIndex = index });
        return true;
    }

    // 作用：取消所有槽中枪械的装填并退回预扣批次，保留已装弹夹；返回：无返回值。
    public void CancelReloads()
    {
        // 遍历全部槽位而非只处理当前枪，让后台换弹也能退回预扣的原来源批次。
        foreach (var weapon in mWeapons) (weapon as GunWeapon)?.RefundPendingLoad();
    }

    // 作用：响应 QFramework 初始化，场景引用及武器留待 Setup 装配；返回：无返回值。
    protected override void OnInit()
    {
        // 开局库存和玩家引用尚未就绪，不能在架构初始化时创建并装填武器。
    }

    // 作用：验证配置并向末尾追加一把补给来源的空夹枪，用于开局装配；返回：无返回值。
    private void AddGun(string weaponId, Transform bulletParent)
    {
        var config = WeaponConfigTable.Get(weaponId);
        // 开局配置错误直接抛出，由 GameRoot 统一中止装配，不生成不可用武器。
        if (config.DurabilityMax <= 0f || config.Magazine <= 0 || config.ReloadTime <= 0f
            || (config.IsAutomatic ? config.RoundsPerMinute <= 0f : config.SemiAutoInterval <= 0f))
            throw new System.InvalidOperationException($"武器配置无效：{weaponId}，请执行阶段三/阶段四资源校验。");
        EnsureBulletPool(config.Caliber, bulletParent);

        var gun = new GunWeapon(config, ((IBelongToArchitecture)this).GetArchitecture(), bulletParent, ItemOrigin.Supply)
        {
            SlotIndex = mWeapons.Count
        };
        mWeapons.Add(gun);
    }

    // 作用：校验该口径各等级配置，按口径懒注册并预热共享子弹池；返回：无返回值。
    private void EnsureBulletPool(Caliber caliber, Transform bulletParent)
    {
        var poolKey = AmmoTypes.PoolKey(caliber);
        if (mRegisteredPools.Contains(poolKey)) return;
        // 先确保所有等级配置可用；实例统一取零级预制体，等级差异在每次射击时设置。
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

    // 作用：卸出报废枪余弹并腾槽，退款预扣后广播余弹掉落及报废事件；返回：无返回值。
    private void BreakWeapon(int slotIndex)
    {
        var weapon = mWeapons[slotIndex];
        if (weapon == null) return;
        var gun = weapon as GunWeapon;
        // 卸夹取走已装弹所有权，再置空槽和缓存；这两步都必须在任何退款事件之前完成。
        var ammo = gun?.UnloadMagazine() ?? default;
        mWeapons[slotIndex] = null;
        mLastSentResources[slotIndex] = -1f;
        mLastSentDurabilities[slotIndex] = -1f;

        // RefundPendingLoad 会同步触发库存通知，订阅者此时只能读到空槽，不能读到旧枪重复退款。
        gun?.RefundPendingLoad();
        if (ammo.Count > 0)
        {
            // 已装余弹交给掉落事件，预扣弹则退库存；两份资源不重复处理且保留来源比例。
            this.SendEvent(new WeaponAmmoDroppedEvent
            {
                Position = mOwner.position,
                Caliber = gun.Caliber,
                Level = gun.LoadedLevel,
                Ammo = ammo
            });
        }
        // 不修改 CurrentIndex，也不回收已发射子弹，报废后的空槽等待玩家手动切换。
        this.SendEvent(new WeaponBrokenEvent { SlotIndex = slotIndex });
        Debug.Log($"[Weapon] 槽位 {slotIndex + 1} 的 {weapon.Name} 耐久归零报废");
    }

    // 作用：广播当前槽位、弹量和耐久，空槽使用零值让 HUD 清空显示；返回：无返回值。
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
