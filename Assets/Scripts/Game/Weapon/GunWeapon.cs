using QFramework;
using UnityEngine;

public class GunWeapon : WeaponBase
{
    private readonly IArchitecture mArchitecture;
    private readonly IGameObjectPoolSystem mPool;
    private readonly Transform mBulletParent;
    private bool mReloading;
    private float mReloadTimer;
    private int mPendingLevel;
    private AmmoBatch mPendingAmmo;
    private int mLoadedSupplyCount;

    public Caliber Caliber { get; }
    public ItemOrigin Origin { get; }
    public float ReloadTime { get; }
    public int LoadedLevel { get; private set; }
    public int NextLoadLevel { get; private set; }
    public bool IsReloading => mReloading;
    public AmmoBatch LoadedAmmo => new AmmoBatch(Mathf.RoundToInt(Resource), mLoadedSupplyCount);
    public AmmoBatch PendingAmmo => mPendingAmmo;

    public GunWeapon(WeaponConfig config, IArchitecture architecture, Transform bulletParent, ItemOrigin origin)
        : base(config.Id, config.Name, config.Magazine,
            config.IsAutomatic ? 60f / config.RoundsPerMinute : config.SemiAutoInterval,
            config.Damage, config.DurabilityMax, config.IsAutomatic)
    {
        Caliber = config.Caliber;
        Origin = origin;
        ReloadTime = config.ReloadTime;
        mArchitecture = architecture;
        mPool = architecture.GetSystem<IGameObjectPoolSystem>();
        mBulletParent = bulletParent;
    }

    public void CycleNextLoadLevel()
    {
        NextLoadLevel = (NextLoadLevel + 1) % AmmoTypes.LevelCount;
    }

    public void RequestReload()
    {
        if (mReloading || IsBroken) return;
        if (Resource >= ResourceMax && LoadedLevel == NextLoadLevel) return;
        BeginReload();
    }

    public void RefundPendingLoad()
    {
        if (!mReloading) return;
        var ammo = mPendingAmmo;
        var level = mPendingLevel;
        mPendingAmmo = default;
        mReloadTimer = 0f;
        mReloading = false;
        State = WeaponState.Ready;
        mArchitecture.SendCommand(new AddBulletsCommand(Caliber, level, ammo));
    }

    public AmmoBatch UnloadMagazine()
    {
        var ammo = LoadedAmmo;
        Resource = 0f;
        mLoadedSupplyCount = 0;
        return ammo;
    }

    public override void Tick(float deltaTime)
    {
        base.Tick(deltaTime);
        if (!mReloading) return;
        if (IsBroken)
        {
            RefundPendingLoad();
            return;
        }
        mReloadTimer -= deltaTime;
        if (mReloadTimer > 0f) return;
        LoadedLevel = mPendingLevel;
        Resource += mPendingAmmo.Count;
        mLoadedSupplyCount += mPendingAmmo.SupplyCount;
        mPendingAmmo = default;
        mReloading = false;
        State = WeaponState.Ready;
    }

    private void BeginReload()
    {
        if (IsBroken || mReloading) return;
        if (LoadedLevel != NextLoadLevel && Resource > 0f)
        {
            var returned = UnloadMagazine();
            mArchitecture.SendCommand(new AddBulletsCommand(Caliber, LoadedLevel, returned));
        }
        var want = Mathf.RoundToInt(ResourceMax - Resource);
        if (want <= 0) return;
        var level = NextLoadLevel;
        var take = new TakeBulletsCommand(Caliber, level, want);
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
        if (take.Taken.Count == 0) return;
        // B 只改下一次选择，不能改变已经预扣的等级。
        mPendingLevel = level;
        mPendingAmmo = take.Taken;
        mReloadTimer = ReloadTime;
        mReloading = true;
        State = WeaponState.Reloading;
    }

    protected override void DoAttack(Transform owner)
    {
        // Resource 已由基类扣除，此处只扣其中的补给份额。
        mLoadedSupplyCount = Mathf.Max(0, mLoadedSupplyCount - 1);
        var bulletConfig = BulletConfigTable.Get(AmmoTypes.BulletId(Caliber, LoadedLevel));
        var hit = new DamageInfo(Damage * AmmoTypes.DamageMultiplier(LoadedLevel), LoadedLevel,
            CombatFaction.Player, true);
        var poolKey = AmmoTypes.PoolKey(Caliber);
        // 玩家根高 1m、敌人根高 0.6m；统一弹道平面，并从脚下 XZ 开始以免越过贴身掩体。
        var muzzle = owner.position - Vector3.up * 0.4f;
        var bulletObject = mPool.Spawn(poolKey, muzzle, owner.rotation, mBulletParent);
        bulletObject.GetComponent<Bullet>().Setup(poolKey, hit, bulletConfig.Speed, owner.forward);
        Wear(AmmoTypes.WearFactor(LoadedLevel));
        if (Resource <= 0f && !IsBroken) BeginReload();
    }
}
