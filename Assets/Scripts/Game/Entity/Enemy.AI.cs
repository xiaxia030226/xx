using QFramework;
using UnityEngine;

public partial class Enemy
{
    private enum AIPhase
    {
        Chase, // 追击或等待攻击条件满足。
        Windup, // 攻击前摇及范围预告。
        Charge, // 沿锁定方向冲锋。
        Recovery, // 攻击后摇，结束后追击或衔接滚动。
        Stunned, // 破盾硬直，暂不行动。
        TeleportArrival, // 瞬移到达后等待发射。
        Leap // 史莱姆王跳跃飞行。
    }

    private AIPhase mPhase; // 当前 AI 流程阶段。
    private float mPhaseTime; // 前摇、后摇、硬直或瞬移到达阶段的剩余秒数。
    private float mWindupDuration; // 本轮前摇总时长，用于表现进度。
    private float mSkillCooldown; // 距离下次可发起技能的剩余秒数。
    private float mContactCooldown; // 距离下次可结算接触伤害的剩余秒数。
    private float mPathTimer; // 下次允许刷新寻路方向的倒计时。
    private Vector3 mPathDirection; // 低频寻路返回并跨帧复用的方向。
    private Vector3 mPathTarget; // 上次寻路目标，用于检测目标明显移动。
    private Vector3 mLockedDirection; // 当前技能预告或冲锋锁定的方向。
    private Vector3 mLockedPoint; // 范围攻击、瞬移或跳跃锁定的位置。
    private float mChargeRemaining; // 当前冲锋尚可前进的距离。
    private bool mSkillHit; // 本轮技能是否已消耗伤害判定，避免重复结算。
    private int mFlankSide; // 绕侧追击方向符号，取正一或负一。
    private static int sFlankSequence; // 全部绕侧敌人共用的生成序号，用于交替左右侧。

    // 作用：清除上一轮 AI、首领与支援状态并恢复追击；返回：无返回值。
    private void ResetAI()
    {
        // 池复用时清除计时和锁定信息；首次寻路错帧，绕侧敌人交替分流。
        mPhase = AIPhase.Chase;
        mPhaseTime = 0f;
        mWindupDuration = 0f;
        mSkillCooldown = 0f;
        mContactCooldown = 0f;
        mPathTimer = Mathf.Abs(GetInstanceID() % 100) * 0.002f;
        mPathDirection = Vector3.zero;
        mPathTarget = transform.position;
        mLockedDirection = Vector3.forward;
        mLockedPoint = transform.position;
        mChargeRemaining = 0f;
        mSkillHit = false;
        mFlankSide = mConfig.AIType == EnemyAIType.Flank && (++sFlankSequence & 1) == 0 ? -1 : 1;
        mBossThresholds = 0;
        mBossJumpsRemaining = 0;
        mBossRollPending = false;
        mLeapOrigin = transform.position;
        mLeapTime = 0f;
        ClearSupportTargets();
        ResetPose();
    }

    // 作用：推进阶段计时并按 AI 类型执行追击或攻击；返回：无返回值。
    private void TickAI(float deltaTime)
    {
        // 各技能阶段独占本帧；先更新冷却，再处理阶段转换，避免追击逻辑抢占攻击。
        mSkillCooldown = Mathf.Max(0f, mSkillCooldown - deltaTime);
        mContactCooldown = Mathf.Max(0f, mContactCooldown - deltaTime);
        mPathTimer -= deltaTime;
        switch (mPhase)
        {
            case AIPhase.Stunned:
                mPhaseTime -= deltaTime;
                if (mPhaseTime <= 0f) mPhase = AIPhase.Chase;
                return;
            case AIPhase.Windup:
                TickWindup(deltaTime);
                return;
            case AIPhase.Charge:
                TickCharge(deltaTime);
                return;
            case AIPhase.Leap:
                TickLeap(deltaTime);
                return;
            case AIPhase.TeleportArrival:
                mPhaseTime -= deltaTime;
                if (mPhaseTime <= 0f)
                {
                    FireProjectile(DirectionToTarget());
                    BeginRecovery(RecoveryDuration);
                }
                return;
            case AIPhase.Recovery:
                mPhaseTime -= deltaTime;
                if (mPhaseTime <= 0f)
                {
                    if (mBossRollPending)
                    {
                        mBossRollPending = false;
                        BeginCharge();
                    }
                    else
                    {
                        mTelegraph?.ClearAttack();
                        mPhase = AIPhase.Chase;
                    }
                }
                return;
        }

        var distance = Flat(mTarget.position - transform.position).magnitude;
        if (UsesContactAttack)
        {
            // 接触型按体型留出停步距离；绕侧型只在较远时偏移追击目标。
            var stopDistance = Mathf.Max(mConfig.AttackRange, mRadius + 0.4f);
            if (distance > stopDistance)
            {
                var goal = mTarget.position;
                if (mConfig.AIType == EnemyAIType.Flank && distance > stopDistance + 1.5f)
                {
                    var away = Flat(transform.position - goal).normalized;
                    goal += Vector3.Cross(Vector3.up, away) * (mFlankSide * Mathf.Min(3.5f, distance * 0.45f));
                }
                MoveTowards(goal, deltaTime);
            }
            if (distance <= stopDistance + 0.15f && mContactCooldown <= 0f && HasSight(transform.position, mTarget.position))
            {
                mContactCooldown = Mathf.Max(0.05f, mConfig.AttackInterval);
                DealPlayerDamage();
            }
            return;
        }

        // 技能型先追到射程且有视线的位置，再等待技能冷却完成。
        var range = SkillRange;
        if (distance > range || !HasSight(transform.position, mTarget.position))
        {
            MoveTowards(mTarget.position, deltaTime);
            return;
        }
        if (mSkillCooldown <= 0f) BeginWindup();
    }

    private bool UsesContactAttack => mConfig.AIType == EnemyAIType.ChaseMelee
        || mConfig.AIType == EnemyAIType.Flank || mConfig.AIType == EnemyAIType.ShieldDrummer
        || mConfig.AIType == EnemyAIType.Split; // 是否采用接近后按间隔结算的接触攻击。

    private float SkillRange // 允许技能开始前摇的距离。
    {
        // 作用：综合攻击距离及技能覆盖范围求触发距离；返回：当前 AI 的有效技能射程。
        get
        {
            // 按技能形态设最低触发距离，再与配置射程取较大值，确保能覆盖冲锋或范围攻击。
            switch (mConfig.AIType)
            {
                case EnemyAIType.SlimeCharge: return Mathf.Max(mConfig.AttackRange, ChargeDistance);
                case EnemyAIType.Ram: return Mathf.Max(mConfig.AttackRange, ChargeDistance);
                case EnemyAIType.Slam: return Mathf.Max(mConfig.AttackRange, AreaRadius);
                case EnemyAIType.SlimeKing: return Mathf.Max(mConfig.AttackRange, 10f);
                default: return Mathf.Max(mConfig.AttackRange, 8f);
            }
        }
    }

    private float AreaRadius => mConfig.AreaRadius > 0f ? mConfig.AreaRadius
        : mConfig.AIType == EnemyAIType.Poison ? 2f : 3f; // 范围半径，优先配置，否则毒区为 2、其他为 3。
    private float ChargeDistance => mConfig.ChargeDistance > 0f ? mConfig.ChargeDistance
        : mConfig.AIType == EnemyAIType.SlimeCharge ? 4f : mConfig.AIType == EnemyAIType.Ram ? 10f : 12f; // 单次冲锋距离，未配置时按类型回退。
    private float ChargeSpeed => mConfig.ChargeSpeed > 0f ? mConfig.ChargeSpeed
        : mConfig.AIType == EnemyAIType.Ram ? 10f : 8f; // 冲锋速度，未配置时冲撞兽为 10、其他为 8。
    private float RecoveryDuration => mConfig.Recovery > 0f ? mConfig.Recovery : 0.35f; // 常规后摇秒数，未配置时为 0.35。

    private float WindupDuration // 当前类型的技能前摇秒数。
    {
        // 作用：优先读取配置前摇，再按 AI 类型给出默认值；返回：本次蓄力应持续的秒数。
        get
        {
            // 正数配置优先，未指定时按技能类型选择默认时长，不让技能跳过预告阶段。
            if (mConfig.Windup > 0f) return mConfig.Windup;
            switch (mConfig.AIType)
            {
                case EnemyAIType.SlimeCharge: return 0.7f;
                case EnemyAIType.Archer: return 0.8f;
                case EnemyAIType.Slam:
                case EnemyAIType.Ram: return 1f;
                case EnemyAIType.Teleport: return 0.6f;
                case EnemyAIType.SlimeKing: return 1.2f;
                default: return 0.4f;
            }
        }
    }

    // 作用：开始前摇，锁定攻击参数并显示对应预告；返回：无返回值。
    private void BeginWindup()
    {
        // 冷却从前摇开始计时；瞬移和跳跃需先找到可用落点，否则直接后摇。
        mPhase = AIPhase.Windup;
        mWindupDuration = WindupDuration;
        mPhaseTime = mWindupDuration;
        mSkillCooldown = Mathf.Max(0.05f, mConfig.AttackInterval);
        mSkillHit = false;
        mLockedDirection = DirectionToTarget();
        mLockedPoint = mTarget.position;
        mLockedPoint.y = transform.position.y;
        Face(mLockedDirection);
        switch (mConfig.AIType)
        {
            case EnemyAIType.Teleport:
                if (!TryTeleportPoint(out mLockedPoint))
                {
                    BeginRecovery(0.4f);
                    return;
                }
                mTelegraph?.ShowTeleport(transform.position, mLockedPoint, mRadius + 0.4f);
                break;
            case EnemyAIType.Poison:
                mTelegraph?.ShowCircle(mLockedPoint, AreaRadius);
                break;
            case EnemyAIType.Slam:
                mLockedPoint = transform.position;
                mTelegraph?.ShowCircle(mLockedPoint, AreaRadius);
                break;
            case EnemyAIType.SlimeKing:
                if (mBossJumpsRemaining == 0) mBossJumpsRemaining = mCurrentHP < mMaxHP * 0.5f ? 2 : 1;
                if (!TryLandingPoint(mLockedPoint, out mLockedPoint))
                {
                    mBossJumpsRemaining = 0;
                    BeginRecovery(0.5f);
                    return;
                }
                mTelegraph?.ShowCircle(mLockedPoint, AreaRadius);
                break;
            default:
                var length = mConfig.AIType == EnemyAIType.SlimeCharge || mConfig.AIType == EnemyAIType.Ram
                    ? ChargeDistance : SkillRange + 4f;
                mTelegraph?.ShowLine(transform.position, transform.position + mLockedDirection * length,
                    mConfig.AIType == EnemyAIType.Ram ? 0.22f : 0.1f);
                break;
        }
    }

    // 作用：推进蓄力表现并在前摇结束时执行对应技能；返回：无返回值。
    private void TickWindup(float deltaTime)
    {
        mPhaseTime -= deltaTime;
        // 冲锋史莱姆蓄力期间持续追踪方向；其他已锁定参数不随此前摇逻辑更新。
        if (mConfig.AIType == EnemyAIType.SlimeCharge)
        {
            mLockedDirection = DirectionToTarget();
            Face(mLockedDirection);
            mTelegraph?.ShowLine(transform.position, transform.position + mLockedDirection * ChargeDistance);
        }
        if (mVisual != null)
        {
            var progress = 1f - Mathf.Clamp01(mPhaseTime / Mathf.Max(0.01f, mWindupDuration));
            if (mConfig.AIType == EnemyAIType.SlimeCharge || mConfig.AIType == EnemyAIType.SlimeKing)
                mVisual.localScale = Vector3.Scale(mVisualScale, new Vector3(1f + progress * 0.18f, 1f - progress * 0.4f, 1f + progress * 0.18f));
            else if (mConfig.AIType == EnemyAIType.Slam)
                mVisual.localPosition = mVisualPosition + Vector3.up * (progress * 0.6f);
        }
        if (mPhaseTime > 0f) return;
        ResetPose();
        switch (mConfig.AIType)
        {
            case EnemyAIType.SlimeCharge:
            case EnemyAIType.Ram:
                BeginCharge();
                break;
            case EnemyAIType.Poison:
                DamageArea(mLockedPoint, AreaRadius);
                SpawnPoison();
                BeginRecovery(RecoveryDuration);
                break;
            case EnemyAIType.Slam:
                DamageArea(mLockedPoint, AreaRadius);
                BeginRecovery(RecoveryDuration);
                break;
            case EnemyAIType.Teleport:
                FinishTeleport();
                break;
            case EnemyAIType.SlimeKing:
                BeginLeap();
                break;
            default:
                FireProjectile(mConfig.AIType == EnemyAIType.ThrowStone ? DirectionToTarget() : mLockedDirection);
                BeginRecovery(RecoveryDuration);
                break;
        }
    }

    // 作用：进入指定时长的后摇并清理攻击表现；返回：无返回值。
    private void BeginRecovery(float duration)
    {
        // 后摇时长截为非负值，同时撤销预告与蓄力姿态，交给阶段计时推进后续动作。
        mPhase = AIPhase.Recovery;
        mPhaseTime = Mathf.Max(0f, duration);
        mTelegraph?.ClearAttack();
        ResetPose();
    }

    // 作用：打断当前技能及支援并进入硬直；返回：无返回值。
    private void InterruptAttack(float duration)
    {
        // 冲锋距离和命中机会一并清除，硬直结束后不续接旧攻击。
        mPhase = AIPhase.Stunned;
        mPhaseTime = duration;
        mChargeRemaining = 0f;
        mSkillHit = true;
        mBossRollPending = false;
        mTelegraph?.ClearAttack();
        ClearSupportTargets();
        ResetPose();
    }

    // 作用：结合低频寻路与同伴分离向目标移动；返回：无返回值。
    private void MoveTowards(Vector3 target, float deltaTime)
    {
        var offset = Flat(target - transform.position);
        if (offset.sqrMagnitude < 0.01f) return;
        // 定时、无可用方向或目标明显偏移才重算路径，实例差异让计算分散到不同帧。
        if (mPathTimer <= 0f || mPathDirection.sqrMagnitude < 0.01f || Flat(target - mPathTarget).sqrMagnitude > 4f)
        {
            mPathTimer = 0.25f + Mathf.Abs(GetInstanceID() % 7) * 0.02f;
            mPathTarget = target;
            mPathDirection = mNavigation != null
                ? mNavigation.GetNextDirection(transform.position, target, mRadius) : offset.normalized;
        }
        var direction = Flat(mPathDirection);
        var separation = Vector3.zero;
        // 对过近的存活同伴累加分离方向，减轻拥挤；最终位移仍由导航阻挡检查裁决。
        var enemies = mSpawnSystem.AliveEnemies;
        for (var i = 0; i < enemies.Count; i++)
        {
            var other = enemies[i];
            if (other == null || other == this || !other.IsAlive) continue;
            var away = Flat(transform.position - other.transform.position);
            var minDistance = mRadius + other.CollisionRadius + 0.25f;
            var distance = away.magnitude;
            if (distance < minDistance && distance > 0.001f)
                separation += away / distance * (1f - distance / minDistance);
        }
        direction = (direction + separation * 0.8f).normalized;
        var displacement = direction * Mathf.Min(mConfig.MoveSpeed * deltaTime, offset.magnitude);
        transform.position = MovePosition(displacement);
        Face(direction);
    }

    // 作用：计算经导航避障或仅边界裁剪后的移动位置；返回：本次位移可到达的位置。
    private Vector3 MovePosition(Vector3 displacement)
    {
        if (mNavigation != null) return mNavigation.Move(transform.position, displacement, mRadius, mBody);
        // 缺少导航时仅限制场地 XZ 边界，不执行障碍扫掠。
        var position = transform.position + displacement;
        var stage = mSpawnSystem.Environment;
        var center = stage != null ? stage.transform.position : Vector3.zero;
        var limit = stage != null ? Mathf.Max(0f, stage.HalfSize - mRadius) : 44f;
        position.x = Mathf.Clamp(position.x, center.x - limit, center.x + limit);
        position.z = Mathf.Clamp(position.z, center.z - limit, center.z + limit);
        return position;
    }

    // 作用：查询静态障碍视线；返回：无导航或两点间无静态遮挡时为真。
    private bool HasSight(Vector3 from, Vector3 to) => mNavigation == null || mNavigation.HasLineOfSight(from, to); // 无导航时短路放行，否则委托导航检查两点间遮挡。
    // 作用：求朝向目标的水平单位方向；返回：有效方向，目标过近时返回当前前向。
    private Vector3 DirectionToTarget()
    {
        // 去除高度差后再归一化；水平距离过小时沿用当前前向，避免生成近零攻击方向。
        var direction = Flat(mTarget.position - transform.position);
        return direction.sqrMagnitude > 0.0001f ? direction.normalized : transform.forward;
    }
    // 作用：去除向量高度分量；返回：Y 为零且 XZ 不变的向量。
    private static Vector3 Flat(Vector3 value) { /* 只将传入副本的 Y 清零，保留 XZ 供平面运算。 */ value.y = 0f; return value; }
    // 作用：在方向足够大时直接调整敌人朝向；返回：无返回值。
    private void Face(Vector3 direction)
    {
        // 过滤近零方向，避免 LookRotation 缺少有效前向；有效时直接应用朝向。
        if (direction.sqrMagnitude > 0.001f) transform.rotation = Quaternion.LookRotation(direction);
    }

    // 作用：在敌人可战斗时向玩家提交配置中的接触伤害；返回：无返回值。
    private void DealPlayerDamage()
    {
        // 死亡或停战后不再发出伤害命令，其余情况将配置伤害与攻击等级封装为敌方伤害。
        if (!mIsAlive || mCombatCancelled) return;
        this.SendCommand(new PlayerTakeDamageCommand(new DamageInfo(mConfig.ContactDamage,
            mConfig.AttackLevel, CombatFaction.Enemy)));
    }

    // 作用：对圆形范围内且有视线的玩家执行一次伤害判定；返回：无返回值。
    private void DamageArea(Vector3 center, float radius)
    {
        // 无论命中与否都消耗本轮判定；范围加上玩家半径容差，避免只按中心漏判。
        if (mSkillHit) return;
        mSkillHit = true;
        if (Flat(mTarget.position - center).sqrMagnitude <= (radius + 0.45f) * (radius + 0.45f)
            && HasSight(center, mTarget.position)) DealPlayerDamage();
    }

    // 作用：在战斗状态下从对象池发射敌方弹丸；返回：无返回值。
    private void FireProjectile(Vector3 direction)
    {
        // 死亡、停战、资源未就绪都不发射；出口位于障碍后方时也放弃，避免隔墙出生。
        if (!mIsAlive || mCombatCancelled || !mSpawnSystem.ProjectilePoolReady
            || this.GetModel<IGameStateModel>().State.Value != GameState.Playing) return;
        var origin = transform.position + direction * (mRadius + 0.15f);
        if (!HasSight(transform.position, origin)) return;
        var parent = GameRoot.BattleRoot != null ? GameRoot.BattleRoot.Find("BulletRoot") : transform.parent;
        var projectile = this.GetSystem<IGameObjectPoolSystem>().Spawn(EnemySpawnSystem.ProjectilePoolKey,
            origin, Quaternion.LookRotation(direction), parent);
        projectile.GetComponent<Bullet>().Setup(EnemySpawnSystem.ProjectilePoolKey,
            new DamageInfo(mConfig.ContactDamage, mConfig.AttackLevel, CombatFaction.Enemy, true),
            mConfig.ProjectileSpeed > 0f ? mConfig.ProjectileSpeed : 12f, direction);
    }
}
