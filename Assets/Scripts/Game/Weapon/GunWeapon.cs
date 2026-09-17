using QFramework;
using UnityEngine;

public class GunWeapon : WeaponBase
{
    private const float SemiAutoInterval = 0.15f;
    private readonly IArchitecture mArchitecture;
    private readonly IGameObjectPoolSystem mPool;
    private readonly Transform mBulletParent;
    private bool mReloading;
    private float mReloadTimer;
    private int mPendingLevel;
    private int mPendingCount;

    public Caliber Caliber { get; }
    public float ReloadTime { get; }
    public int LoadedLevel { get; private set; }
    public int NextLoadLevel { get; private set; }
    public bool IsReloading => mReloading;

    public GunWeapon(WeaponConfig config, IArchitecture architecture, Transform bulletParent)
        : base(config.Id, config.Name, config.Magazine,
            config.RoundsPerMinute > 0f ? 60f / config.RoundsPerMinute : SemiAutoInterval,
            config.Damage, config.DurabilityMax, config.IsAutomatic)
    {
        Caliber = config.Caliber;
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
        mArchitecture.SendCommand(new AddBulletsCommand(Caliber, mPendingLevel, mPendingCount));
        mPendingCount = 0;
        mReloadTimer = 0f;
        mReloading = false;
        State = WeaponState.Ready;
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
        Resource += mPendingCount;
        mPendingCount = 0;
        mReloading = false;
        State = WeaponState.Ready;
    }

    private void BeginReload()
    {
        if (IsBroken || mReloading) return;
        if (LoadedLevel != NextLoadLevel && Resource > 0f)
        {
            mArchitecture.SendCommand(new AddBulletsCommand(Caliber, LoadedLevel, Mathf.RoundToInt(Resource)));
            Resource = 0f;
        }
        var want = Mathf.RoundToInt(ResourceMax - Resource);
        if (want <= 0) return;
        var level = NextLoadLevel;
        var take = new TakeBulletsCommand(Caliber, level, want);
        mArchitecture.SendCommand(take);
        if (take.Taken < want)
        {
            mArchitecture.SendEvent(new AmmoShortageEvent
            {
                SlotIndex = SlotIndex,
                Loaded = take.Taken,
                Wanted = want
            });
        }
        if (take.Taken <= 0) return;
        // B 只改下一次选择，不能改变已经预扣的等级。
        mPendingLevel = level;
        mPendingCount = take.Taken;
        mReloadTimer = ReloadTime;
        mReloading = true;
        State = WeaponState.Reloading;
    }

    protected override void DoAttack(Transform owner)
    {
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
