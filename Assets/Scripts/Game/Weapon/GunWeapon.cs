using QFramework;
using UnityEngine;

public class GunWeapon : WeaponBase
{
    private readonly IArchitecture mArchitecture; // 用于发送库存命令和武器事件的架构引用。
    private readonly IGameObjectPoolSystem mPool; // 发射子弹时租用实例的对象池系统。
    private readonly Transform mBulletParent; // 子弹实例在战斗场景中的挂载节点。
    private bool mReloading; // 是否持有预扣批次并处于装填计时中。
    private float mReloadTimer; // 本次装填尚需等待的秒数。
    private int mPendingLevel; // 本次预扣批次锁定的穿甲等级，不随 B 键变化。
    private AmmoBatch mPendingAmmo; // 已从库存取走、尚未转入弹夹的子弹批次及来源份额。
    private int mLoadedSupplyCount; // 已装弹夹中剩余的补给子弹数量，射击优先消耗此份额。

    public Caliber Caliber { get; } // 本枪使用的子弹口径。
    public ItemOrigin Origin { get; } // 武器本体的补给或战利品来源，不等同于弹夹来源。
    public float ReloadTime { get; } // 每次装填需要等待的秒数。
    public int LoadedLevel { get; private set; } // 当前弹夹的穿甲等级，装填完成时更新。
    public int NextLoadLevel { get; private set; } // 下一次开始装填时使用的等级，每把枪独立记忆。
    public bool IsReloading => mReloading; // true 表示装填计时中，直接读取内部状态。
    public AmmoBatch LoadedAmmo => new AmmoBatch(Mathf.RoundToInt(Resource), mLoadedSupplyCount); // 当前弹夹总量和补给份额组成的批次快照。
    public AmmoBatch PendingAmmo => mPendingAmmo; // 本次装填预扣但尚未入夹的批次快照。

    // 作用：按配置创建空夹枪械并绑定架构、对象池与来源；返回：无返回值（构造函数）。
    public GunWeapon(WeaponConfig config, IArchitecture architecture, Transform bulletParent, ItemOrigin origin)
        : base(config.Id, config.Name, config.Magazine,
            config.IsAutomatic ? 60f / config.RoundsPerMinute : config.SemiAutoInterval,
            config.Damage, config.DurabilityMax, config.IsAutomatic)
    {
        // 连发枪在基类参数中将每分钟射速换算为秒间隔，半自动枪直接使用配置间隔。
        Caliber = config.Caliber;
        Origin = origin;
        ReloadTime = config.ReloadTime;
        mArchitecture = architecture;
        mPool = architecture.GetSystem<IGameObjectPoolSystem>();
        mBulletParent = bulletParent;
    }

    // 作用：循环选择下次装填等级，不改变现有弹夹或已经预扣的批次；返回：无返回值。
    public void CycleNextLoadLevel()
    {
        // 递增后取模回到零级，只更新下次选择，不触碰本次预扣批次。
        NextLoadLevel = (NextLoadLevel + 1) % AmmoTypes.LevelCount;
    }

    // 作用：处理主动换弹请求，满足条件时启动预扣及装填；返回：无返回值。
    public void RequestReload()
    {
        // 重复换弹、报废及同等级满夹都无需重新装填；满夹换等级仍允许继续。
        if (mReloading || IsBroken) return;
        if (Resource >= ResourceMax && LoadedLevel == NextLoadLevel) return;
        BeginReload();
    }

    // 作用：取消本次装填并按原等级与来源退还预扣批次；返回：无返回值。
    public void RefundPendingLoad()
    {
        if (!mReloading) return;
        var ammo = mPendingAmmo;
        var level = mPendingLevel;
        // 先清除本枪的预扣所有权，再同步发库存命令，防止事件回调重复退款。
        mPendingAmmo = default;
        mReloadTimer = 0f;
        mReloading = false;
        State = WeaponState.Ready;
        mArchitecture.SendCommand(new AddBulletsCommand(Caliber, level, ammo));
    }

    // 作用：清空已装弹夹并转交子弹所有权，不直接入库或生成掉落；返回：卸出的子弹总量及补给份额。
    public AmmoBatch UnloadMagazine()
    {
        // 先快照数量与来源，再同时清除总量和补给量，重复卸夹就只能得到空批次。
        var ammo = LoadedAmmo;
        Resource = 0f;
        mLoadedSupplyCount = 0;
        return ammo;
    }

    // 作用：推进基础冷却及本枪装填计时，到期将预扣批次转入弹夹；返回：无返回值。
    public override void Tick(float deltaTime)
    {
        base.Tick(deltaTime);
        if (!mReloading) return;
        // 正常由 WeaponSystem 在调用此方法前处理报废，此分支防御单独推进已报废枪械。
        if (IsBroken)
        {
            RefundPendingLoad();
            return;
        }
        mReloadTimer -= deltaTime;
        if (mReloadTimer > 0f) return;
        // 计时结束才入夹；库存已在开始时扣除，此处仅转移所有权并清空预扣记录。
        LoadedLevel = mPendingLevel;
        Resource += mPendingAmmo.Count;
        mLoadedSupplyCount += mPendingAmmo.SupplyCount;
        mPendingAmmo = default;
        mReloading = false;
        State = WeaponState.Ready;
    }

    // 作用：按下次等级补夹，异级余弹先退库，再锁定实际预扣批次并开始计时；返回：无返回值。
    private void BeginReload()
    {
        if (IsBroken || mReloading) return;
        // 同等级保留余弹只补缺口；换等级则先卸旧弹，避免一个弹夹混用等级。
        if (LoadedLevel != NextLoadLevel && Resource > 0f)
        {
            var returned = UnloadMagazine();
            mArchitecture.SendCommand(new AddBulletsCommand(Caliber, LoadedLevel, returned));
        }
        var want = Mathf.RoundToInt(ResourceMax - Resource);
        if (want <= 0) return;
        var level = NextLoadLevel;
        var take = new TakeBulletsCommand(Caliber, level, want);
        // 命令同步从库存预扣，库存优先取 Supply；取到的批次保留来源用于退款或掉落。
        mArchitecture.SendCommand(take);
        if (take.Taken.Count < want)
        {
            mArchitecture.SendEvent(new AmmoShortageEvent
            {
                SlotIndex = SlotIndex,
                Loaded = take.Taken.Count,
                Wanted = want
            });
        }
        // 不足可部分装填，完全无弹则不进入装填状态。
        if (take.Taken.Count == 0) return;
        // B 只改下一次选择，不能改变已经预扣的等级。
        mPendingLevel = level;
        mPendingAmmo = take.Taken;
        mReloadTimer = ReloadTime;
        mReloading = true;
        State = WeaponState.Reloading;
    }

    // 作用：发射当前等级子弹，更新补给份额与耐久，并在空夹且未报废时自动换弹；返回：无返回值。
    protected override void DoAttack(Transform owner)
    {
        // Resource 已由基类扣除，此处只扣补给份额；Supply 耗尽后才消耗战利品份额。
        mLoadedSupplyCount = Mathf.Max(0, mLoadedSupplyCount - 1);
        var bulletConfig = BulletConfigTable.Get(AmmoTypes.BulletId(Caliber, LoadedLevel));
        var hit = new DamageInfo(Damage * AmmoTypes.DamageMultiplier(LoadedLevel), LoadedLevel,
            CombatFaction.Player, true);
        var poolKey = AmmoTypes.PoolKey(Caliber);
        // 玩家根高 1m、敌人根高 0.6m；统一弹道平面，并从脚下 XZ 开始以免越过贴身掩体。
        var muzzle = owner.position - Vector3.up * 0.4f;
        var bulletObject = mPool.Spawn(poolKey, muzzle, owner.rotation, mBulletParent);
        bulletObject.GetComponent<Bullet>().Setup(poolKey, hit, bulletConfig.Speed, owner.forward);
        // 先发射再磨损，耐久归零的最后一发仍保留飞行；报废枪不再启动自动换弹。
        Wear(AmmoTypes.WearFactor(LoadedLevel));
        if (Resource <= 0f && !IsBroken) BeginReload();
    }
}
