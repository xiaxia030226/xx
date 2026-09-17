using QFramework;
using UnityEngine;

public partial class Enemy : MonoBehaviour, IController, ICanSendEvent
{
    [SerializeField] private EnemyTelegraph mTelegraph;
    [SerializeField] private Transform mVisual;

    private EnemyConfig mConfig;
    private Transform mTarget;
    private Collider mBody;
    private BattleObstacle mObstacle;
    private BattleNavigation mNavigation;
    private IEnemySpawnSystem mSpawnSystem;
    private Vector3 mBaseScale;
    private Vector3 mVisualScale;
    private Vector3 mVisualPosition;
    private float mCurrentHP;
    private float mMaxHP;
    private float mShieldCurrent;
    private float mShieldMax;
    private int mShieldLevel;
    private float mSpawnMultiplier;
    private float mRadius;
    private bool mIsAlive;
    private bool mChild;
    private bool mRecycled = true;
    private bool mCombatCancelled;
    private float mDeathTimer;
    private EnemyDropData mDrops;

    public bool IsAlive => mIsAlive;
    public string EnemyId => mConfig != null ? mConfig.Id : null;
    public EnemyAIType AIType => mConfig != null ? mConfig.AIType : EnemyAIType.ChaseMelee;
    public float CurrentHP => mCurrentHP;
    public float MaxHP => mMaxHP;
    public int ShieldLevel => mShieldLevel;
    public float ShieldCurrent => mShieldCurrent;
    public float ShieldMax => mShieldMax;
    public float SpawnMultiplier => mSpawnMultiplier;
    public float CollisionRadius => mRadius;
    public bool IsChild => mChild;
    public bool CanRepairShield => mIsAlive && !mCombatCancelled && mShieldCurrent > 0f && mShieldCurrent < mShieldMax;

    public IArchitecture GetArchitecture() => GameArchitecture.Interface;

    private void Awake()
    {
        mBaseScale = transform.localScale;
        mBody = GetComponent<Collider>();
        if (mBody == null) mBody = GetComponentInChildren<Collider>();
        mObstacle = GetComponent<BattleObstacle>();
        if (mTelegraph == null) mTelegraph = GetComponentInChildren<EnemyTelegraph>();
        if (mVisual != null)
        {
            mVisualScale = mVisual.localScale;
            mVisualPosition = mVisual.localPosition;
        }
    }

    public void OnSpawn(EnemyConfig config, Transform target, float multiplier, int shieldLevel,
        float healthOverride = 0f, bool child = false)
    {
        mConfig = config;
        mTarget = target;
        mSpawnSystem = this.GetSystem<IEnemySpawnSystem>();
        mNavigation = (mSpawnSystem.Environment != null ? mSpawnSystem.Environment : GameRoot.Environment)?.Navigation;
        mMaxHP = healthOverride > 0f ? healthOverride : config.MaxHP;
        mCurrentHP = mMaxHP;
        mSpawnMultiplier = multiplier;
        mChild = child;
        mShieldLevel = child ? 0 : Mathf.Clamp(shieldLevel, 0, 5);
        mShieldMax = mShieldLevel > 0 ? ShieldConfigTable.Get(mShieldLevel).Capacity : 0f;
        mShieldCurrent = mShieldMax;
        mIsAlive = true;
        mRecycled = false;
        mCombatCancelled = false;
        mDeathTimer = 0f;
        var smallChild = child && healthOverride > 0f && healthOverride < config.MaxHP;
        transform.localScale = mBaseScale * (smallChild ? 0.65f : 1f);
        if (mBody != null) mBody.enabled = true;
        if (mObstacle != null) mObstacle.enabled = config.AIType == EnemyAIType.Slam;
        Physics.SyncTransforms();
        mRadius = mBody != null
            ? Mathf.Max(0.2f, Mathf.Max(mBody.bounds.extents.x, mBody.bounds.extents.z))
            : GetSpawnRadius(config, smallChild);
        ResetAI();
        mTelegraph?.ResetVisuals();
        mTelegraph?.RefreshShield(transform.position, mRadius, mShieldLevel, mShieldCurrent, mShieldMax, 0f);
        mDrops = EnemyDropData.Roll(config, multiplier, mSpawnSystem.Stage,
            this.GetSystem<IWeaponSystem>().Weapons, child);
    }

    public static float GetSpawnRadius(EnemyConfig config, bool child = false)
    {
        if (child) return 0.35f;
        if (config.AIType == EnemyAIType.SlimeKing) return 1.5f;
        if (config.AIType == EnemyAIType.Slam) return 1f;
        return 0.5f;
    }

    public void TakeHit(DamageInfo damage)
    {
        if (!CanTakeDamage(damage)) return;
        this.SendCommand(new EnemyTakeDamageCommand(this, damage));
    }

    public void ApplyDamage(DamageInfo damage)
    {
        if (!CanTakeDamage(damage)) return;
        var result = DamageResolver.Calculate(damage, mShieldLevel, mShieldCurrent);
        mShieldCurrent = Mathf.Max(0f, mShieldCurrent - result.ShieldDamage);
        mCurrentHP = Mathf.Max(0f, mCurrentHP - result.HealthDamage);
        if (mCurrentHP <= 0f)
        {
            Die();
            return;
        }

        RegisterBossSummons();
        if (!result.BrokeShield) return;
        mTelegraph?.FlashShieldBreak();
        if (mConfig.Category == EnemyCategory.Boss) return;
        InterruptAttack(mConfig.Category == EnemyCategory.Elite ? 0.2f : 0.4f);
    }

    private bool CanTakeDamage(DamageInfo damage)
    {
        return mIsAlive && !mCombatCancelled && damage.Amount > 0f && damage.Faction == CombatFaction.Player
            && this.GetModel<IGameStateModel>().State.Value == GameState.Playing;
    }

    public bool RepairShield(float amount)
    {
        if (!CanRepairShield || amount <= 0f) return false;
        mShieldCurrent = Mathf.Min(mShieldMax, mShieldCurrent + amount);
        return true;
    }

    private void Update()
    {
        if (mRecycled) return;
        var state = this.GetModel<IGameStateModel>().State.Value;
        if (state == GameState.Paused) return;
        if (state != GameState.Playing)
        {
            CancelCombat();
            if (!mIsAlive) RecycleSelf();
            return;
        }

        var deltaTime = Time.deltaTime;
        if (!mIsAlive)
        {
            mDeathTimer -= deltaTime;
            if (mVisual != null) mVisual.localScale = mVisualScale * (1f + (0.8f - mDeathTimer) * 0.55f);
            if (mDeathTimer <= 0f) RecycleSelf();
            return;
        }
        if (mCombatCancelled || mTarget == null) return;
        TickAI(deltaTime);
        RefreshSupportLinks();
        mTelegraph?.RefreshShield(transform.position, mRadius, mShieldLevel, mShieldCurrent, mShieldMax, deltaTime);
    }

    public void CancelCombat()
    {
        if (mCombatCancelled) return;
        mCombatCancelled = true;
        ClearSupportTargets();
        ResetPose();
        mTelegraph?.ResetVisuals();
        mPhase = AIPhase.Chase;
        mPhaseTime = 0f;
        mSkillHit = true;
        mBossRollPending = false;
    }

    private void Die()
    {
        if (!mIsAlive) return;
        mIsAlive = false;
        var split = !mChild && mConfig.AIType == EnemyAIType.Split;
        // 先登记纯数据任务，再减 Alive；回池的母体绝不被延迟任务捕获。
        if (split)
            mSpawnSystem.ScheduleChildren(EnemyConfigTable.SlimeGreenId, 3, 0.8f,
                transform.position, 10f, mSpawnMultiplier);

        CancelCombat();
        if (mBody != null) mBody.enabled = false;
        if (mObstacle != null) mObstacle.enabled = false;
        mSpawnSystem.UnregisterEnemy(this);
        this.SendEvent(new EnemyDiedEvent
        {
            EnemyId = mConfig.Id,
            Position = transform.position,
            Gold = mDrops.Gold,
            DropAmmo = mDrops.DropAmmo,
            AmmoCaliber = mDrops.AmmoCaliber,
            AmmoLevel = mDrops.AmmoLevel,
            AmmoCount = mDrops.AmmoCount,
            ShieldLevel = mDrops.ShieldLevel,
            WeaponId = mDrops.WeaponId
        });

        if (split)
        {
            mDeathTimer = 0.8f;
            mTelegraph?.ShowCircle(transform.position, mRadius * 1.5f);
        }
        else RecycleSelf();
    }

    private void RecycleSelf()
    {
        if (mRecycled) return;
        mRecycled = true;
        this.GetSystem<IGameObjectPoolSystem>().Recycle(mConfig.Id, gameObject);
    }

    private void ResetPose()
    {
        if (mVisual == null) return;
        mVisual.localScale = mVisualScale;
        mVisual.localPosition = mVisualPosition;
    }

    private void OnDisable()
    {
        // 场景清理不计作击杀；死亡路径在此之前已经注销。
        if (mIsAlive) mSpawnSystem?.UnregisterEnemy(this);
        mIsAlive = false;
        mRecycled = true;
        mTarget = null;
        mDeathTimer = 0f;
        ClearSupportTargets();
        ResetPose();
        mTelegraph?.ResetVisuals();
        transform.localScale = mBaseScale;
    }
}
