using QFramework;
using UnityEngine;

public partial class Enemy : MonoBehaviour, IController, ICanSendEvent
{
    [SerializeField] private EnemyTelegraph mTelegraph; // 攻击预告、支援连线及护盾显示组件。
    [SerializeField] private Transform mVisual; // 可做蓄力、跳跃等表现的外观节点。

    private EnemyConfig mConfig; // 本轮生成采用的敌人配置。
    private Transform mTarget; // 当前追击和攻击的玩家目标。
    private Collider mBody; // 敌人代表碰撞体，用于半径估计及自身排除。
    private BattleObstacle mObstacle; // 可选障碍标记，生成时仅为 Slam 类型启用。
    private BattleNavigation mNavigation; // 所属战场的导航服务，可为空以使用边界回退。
    private IEnemySpawnSystem mSpawnSystem; // 维护存活列表、子怪调度及场地信息的生成系统。
    private Vector3 mBaseScale; // 根节点初始缩放，用于子怪缩放及池复用恢复。
    private Vector3 mVisualScale; // 外观节点初始局部缩放。
    private Vector3 mVisualPosition; // 外观节点初始局部位置。
    private float mCurrentHP; // 本轮剩余生命值。
    private float mMaxHP; // 本轮最大生命值，可由子怪生成参数覆盖。
    private float mShieldCurrent; // 当前剩余护盾容量。
    private float mShieldMax; // 本轮护盾容量上限。
    private int mShieldLevel; // 护盾防御等级，子怪固定为零。
    private float mSpawnMultiplier; // 本轮生成倍率，用于掉落及传递给子怪。
    private float mRadius; // 碰撞体世界 XZ 半径或配置推定半径。
    private bool mIsAlive; // 是否仍存活并可参与战斗。
    private bool mChild; // 是否为召唤或分裂子怪，影响护盾、掉落和再分裂。
    private bool mRecycled = true; // 是否已回池，防止重复推进及回收。
    private bool mCombatCancelled; // 是否已停止战斗，阻止后续攻击与受击。
    private float mDeathTimer; // 分裂母体死亡表现的剩余秒数。
    private EnemyDropData mDrops; // 生成时一次性掷定的死亡掉落数据。

    public bool IsAlive => mIsAlive; // 当前存活标记，取消战斗不等于死亡。
    public string EnemyId => mConfig != null ? mConfig.Id : null; // 配置标识，尚未生成时为 null。
    public EnemyAIType AIType => mConfig != null ? mConfig.AIType : EnemyAIType.ChaseMelee; // 当前 AI 类型，无配置时默认追击近战。
    public float CurrentHP => mCurrentHP; // 当前生命值。
    public float MaxHP => mMaxHP; // 本轮最大生命值。
    public int ShieldLevel => mShieldLevel; // 当前护盾防御等级。
    public float ShieldCurrent => mShieldCurrent; // 当前护盾余量。
    public float ShieldMax => mShieldMax; // 当前护盾容量上限。
    public float SpawnMultiplier => mSpawnMultiplier; // 本轮生成倍率。
    public float CollisionRadius => mRadius; // 导航和接触攻击采用的水平半径。
    public bool IsChild => mChild; // 为真表示子怪而非普通波次母体。
    public bool CanRepairShield => mIsAlive && !mCombatCancelled && mShieldCurrent > 0f && mShieldCurrent < mShieldMax; // 存活、未停战且护盾未破但有缺口时才可修复。

    // 作用：接入游戏架构；返回：游戏架构实例。
    public IArchitecture GetArchitecture() => GameArchitecture.Interface; // 直接取游戏架构入口，供框架扩展方法访问模型和系统。

    // 作用：缓存碰撞、预告及初始姿态供生成和复用；返回：无返回值。
    private void Awake()
    {
        // 保存未变形的姿态并补查组件引用；碰撞体优先取根节点，缺失时再查子节点。
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

    // 作用：按生成参数重置池中敌人的属性、AI、外观和掉落；返回：无返回值。
    public void OnSpawn(EnemyConfig config, Transform target, float multiplier, int shieldLevel,
        float healthOverride = 0f, bool child = false)
    {
        // 重新绑定本场目标和导航，不继承上一轮池对象的战斗状态。
        mConfig = config;
        mTarget = target;
        mSpawnSystem = this.GetSystem<IEnemySpawnSystem>();
        mNavigation = (mSpawnSystem.Environment != null ? mSpawnSystem.Environment : GameRoot.Environment)?.Navigation;
        // 正生命覆盖值优先，生成倍率不在此直接乘生命；子怪不带盾。
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
        // 只有低生命覆盖的子怪缩小；同步缩放后再读取世界碰撞半径。
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

    // 作用：按类型估计生成占位半径；返回：子怪 0.35、史莱姆王 1.5、Slam 1，其余 0.5。
    public static float GetSpawnRadius(EnemyConfig config, bool child = false)
    {
        // 子怪优先使用小半径，非子怪再按较大体型分支估计，其余采用普通半径。
        if (child) return 0.35f;
        if (config.AIType == EnemyAIType.SlimeKing) return 1.5f;
        if (config.AIType == EnemyAIType.Slam) return 1f;
        return 0.5f;
    }

    // 作用：校验受击资格后通过命令转交伤害结算；返回：无返回值。
    public void TakeHit(DamageInfo damage)
    {
        // 在入口拦截无效伤害，通过后只提交命令，不在此直接修改护盾或生命。
        if (!CanTakeDamage(damage)) return;
        this.SendCommand(new EnemyTakeDamageCommand(this, damage));
    }

    // 作用：结算护盾与生命伤害并触发死亡、召唤或破盾硬直；返回：无返回值。
    public void ApplyDamage(DamageInfo damage)
    {
        // 命令落地时再次校验，避免同步事件已停战或死亡后继续受伤。
        if (!CanTakeDamage(damage)) return;
        var result = DamageResolver.Calculate(damage, mShieldLevel, mShieldCurrent);
        mShieldCurrent = Mathf.Max(0f, mShieldCurrent - result.ShieldDamage);
        mCurrentHP = Mathf.Max(0f, mCurrentHP - result.HealthDamage);
        if (mCurrentHP <= 0f)
        {
            Die();
            return;
        }

        // 非致死伤害才检查首领召唤阈值；破盾只打断非首领，精英硬直更短。
        RegisterBossSummons();
        if (!result.BrokeShield) return;
        mTelegraph?.FlashShieldBreak();
        if (mConfig.Category == EnemyCategory.Boss) return;
        InterruptAttack(mConfig.Category == EnemyCategory.Elite ? 0.2f : 0.4f);
    }

    // 作用：检查当前伤害能否作用于敌人；返回：战斗中存活且未停战，并收到玩家正伤害时为真。
    private bool CanTakeDamage(DamageInfo damage)
    {
        // 按存活、停战、伤害值和阵营逐项短路筛选，全部通过才查询是否仍处于战斗状态。
        return mIsAlive && !mCombatCancelled && damage.Amount > 0f && damage.Faction == CombatFaction.Player
            && this.GetModel<IGameStateModel>().State.Value == GameState.Playing;
    }

    // 作用：为可修复护盾补充容量且不超过上限；返回：通过修盾条件并执行补充时为真，否则为假。
    public bool RepairShield(float amount)
    {
        // 护盾必须仍有余量且未满，破盾不能通过鼓手重新恢复。
        if (!CanRepairShield || amount <= 0f) return false;
        mShieldCurrent = Mathf.Min(mShieldMax, mShieldCurrent + amount);
        return true;
    }

    // 作用：按游戏状态推进死亡表现、AI 与护盾显示；返回：无返回值。
    private void Update()
    {
        // 暂停冻结计时；离开战斗后取消行为，并立即回收已死但尚在播放表现的母体。
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

    // 作用：幂等停止攻击、支援和技能表现但不直接判死；返回：无返回值。
    public void CancelCombat()
    {
        // 保留实体直到死亡或场景清理，失效标记阻断之后的战斗入口。
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

    // 作用：注销死亡敌人、广播掉落并安排分裂或回收；返回：无返回值。
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

    // 作用：按敌人配置标识将实例归还对象池；返回：无返回值。
    private void RecycleSelf()
    {
        // 先标记回池，避免禁用回调或同步事件重复触发回收。
        if (mRecycled) return;
        mRecycled = true;
        this.GetSystem<IGameObjectPoolSystem>().Recycle(mConfig.Id, gameObject);
    }

    // 作用：恢复外观节点的初始缩放与局部位置；返回：无返回值。
    private void ResetPose()
    {
        // 用缓存姿态覆盖技能造成的缩放与位移，只恢复外观节点，不改敌人根位置。
        if (mVisual == null) return;
        mVisual.localScale = mVisualScale;
        mVisual.localPosition = mVisualPosition;
    }

    // 作用：禁用时注销残留存活登记并清理可复用状态；返回：无返回值。
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
