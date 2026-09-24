using QFramework;
using UnityEngine;

public partial class Enemy
{
    private readonly Enemy[] mSupportTargets = new Enemy[3]; // 鼓手最多三个支援目标槽，用于显示与维护连线。
    private int mBossThresholds; // 首领已触发的召唤阈值位：1 为七成血，2 为三成半血。
    private int mBossJumpsRemaining; // 史莱姆王当前连跳尚未完成的次数。
    private bool mBossRollPending; // 后摇结束后是否衔接史莱姆王滚动冲锋。
    private Vector3 mLeapOrigin; // 当前跳跃的起点，也是落点受阻时的回退搜索中心。
    private float mLeapTime; // 本次跳跃已持续的秒数。
    private const float LeapDuration = 0.45f; // 每次跳跃从起点到落点的时间。

    public bool CanSupport => mIsAlive && !mCombatCancelled && mConfig.AIType == EnemyAIType.ShieldDrummer
        && mPhase != AIPhase.Stunned; // 存活、未停战且未硬直的护盾鼓手才可支援。

    // 作用：按锁定方向开启新一轮冲锋并清除预告；返回：无返回值。
    private void BeginCharge()
    {
        // 切入冲锋时补满本轮行程并恢复一次命中机会，撤销预告后沿已锁定方向出发。
        mPhase = AIPhase.Charge;
        mChargeRemaining = ChargeDistance;
        mSkillHit = false;
        mTelegraph?.ClearAttack();
        Face(mLockedDirection);
    }

    // 作用：连续扫掠推进冲锋、检查沿途命中并处理撞停；返回：无返回值。
    private void TickCharge(float deltaTime)
    {
        var start = transform.position;
        var step = Mathf.Min(mChargeRemaining, ChargeSpeed * deltaTime);
        if (step <= 0f)
        {
            EndCharge(false);
            return;
        }
        var blocked = false;
        var brokeRock = false;
        // 先扫掠截短步长；冲撞兽和史莱姆王可碎岩，但本轮仍撞停并进入后摇。
        if (mNavigation != null && mNavigation.TrySweepObstacle(start, mLockedDirection, step,
            mRadius, mBody, out var hit))
        {
            step = Mathf.Max(0f, hit.distance - 0.03f);
            blocked = true;
            if (mConfig.AIType == EnemyAIType.Ram || mConfig.AIType == EnemyAIType.SlimeKing)
            {
                var rock = hit.collider != null ? hit.collider.GetComponentInParent<DestructibleRock>() : null;
                if (rock != null && rock.IsIntact)
                {
                    rock.Shatter();
                    brokeRock = true;
                }
            }
        }

        transform.position = MovePosition(mLockedDirection * step);
        var traveled = Flat(transform.position - start).magnitude;
        mChargeRemaining -= traveled;
        HitAlongCharge(start, transform.position);
        if (blocked || traveled + 0.03f < step || mChargeRemaining <= 0.03f) EndCharge(brokeRock);
    }

    // 作用：沿本帧冲锋线段检查玩家是否被扫中；返回：无返回值。
    private void HitAlongCharge(Vector3 from, Vector3 to)
    {
        // 求玩家到移动线段的最近点，再加玩家半径容差；一轮冲锋至多命中一次。
        if (mSkillHit || mTarget == null) return;
        var segment = Flat(to - from);
        var relative = Flat(mTarget.position - from);
        var fraction = segment.sqrMagnitude > 0.0001f
            ? Mathf.Clamp01(Vector3.Dot(relative, segment) / segment.sqrMagnitude) : 0f;
        var nearest = from + segment * fraction;
        var radius = mRadius + 0.45f;
        if (Flat(mTarget.position - nearest).sqrMagnitude > radius * radius || !HasSight(nearest, mTarget.position)) return;
        mSkillHit = true;
        DealPlayerDamage();
    }

    // 作用：结束冲锋并按敌人类型及碎岩情况选择后摇；返回：无返回值。
    private void EndCharge(bool brokeRock)
    {
        // 首领滚动后解除技能冷却；非冲锋史莱姆撞碎岩石会承受更长后摇。
        mChargeRemaining = 0f;
        if (mConfig.AIType == EnemyAIType.SlimeKing) mSkillCooldown = 0f;
        BeginRecovery(mConfig.AIType == EnemyAIType.SlimeCharge ? RecoveryDuration : brokeRock ? 2f : 1f);
    }

    // 作用：在锁定落点生成持续三秒的减速毒区；返回：无返回值。
    private void SpawnPoison()
    {
        // 毒区只负责后续减速；落点伤害已由前摇结束时的范围判定结算。
        if (!mIsAlive || mCombatCancelled || !mSpawnSystem.PoisonPoolReady
            || this.GetModel<IGameStateModel>().State.Value != GameState.Playing) return;
        var position = mLockedPoint;
        position.y = 0.03f;
        var area = this.GetSystem<IGameObjectPoolSystem>().Spawn(PoisonArea.PoolKey, position,
            Quaternion.identity, GameRoot.BattleRoot != null ? GameRoot.BattleRoot : transform.parent);
        area.GetComponent<PoisonArea>().OnSpawn(mTarget, AreaRadius, 3f);
    }

    // 作用：在玩家周围寻找合法瞬移落点；返回：找到空位且离原位超过一米时为真，失败时输出原位并返回假。
    private bool TryTeleportPoint(out Vector3 point)
    {
        var distance = mConfig.TeleportDistance > 0f ? mConfig.TeleportDistance : 6f;
        var startAngle = Random.Range(0f, Mathf.PI * 2f);
        // 随机起始角后均匀试探一圈，固定最多十六次避免无界搜索。
        for (var i = 0; i < 16; i++)
        {
            var angle = startAngle + i * Mathf.PI * 2f / 16f;
            point = mTarget.position + new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * distance;
            point.y = transform.position.y;
            if (IsFreeLanding(point) && Flat(point - transform.position).sqrMagnitude > 1f) return true;
        }
        point = transform.position;
        return false;
    }

    // 作用：复核落点后瞬移并进入到达等待阶段；返回：无返回值。
    private void FinishTeleport()
    {
        // 前摇期间占用可能变化，落点受阻则不瞬移，直接进入后摇。
        if (!IsFreeLanding(mLockedPoint))
        {
            BeginRecovery(RecoveryDuration);
            return;
        }
        transform.position = mLockedPoint;
        Face(DirectionToTarget());
        mPathTimer = 0f;
        mPhase = AIPhase.TeleportArrival;
        mPhaseTime = 0.6f;
        mTelegraph?.ShowCircle(mLockedPoint, mRadius + 0.4f);
    }

    // 作用：为跳跃寻找请求点或附近替代落点；返回：找到合法位置为真，失败时输出调整高度后的请求点并返回假。
    private bool TryLandingPoint(Vector3 requested, out Vector3 point)
    {
        requested.y = transform.position.y;
        if (IsFreeLanding(requested))
        {
            point = requested;
            return true;
        }
        // 请求点不可用时按两圈、每圈十二个候选寻找最近一圈的可用位置。
        for (var ring = 1; ring <= 2; ring++)
            for (var i = 0; i < 12; i++)
            {
                var angle = i * Mathf.PI * 2f / 12f;
                point = requested + new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * (ring * 1.2f);
                if (IsFreeLanding(point)) return true;
            }
        point = requested;
        return false;
    }

    // 作用：检查落点能否容纳当前敌人；返回：在边界内、通过可用导航的障碍检查且不与其他存活敌人重叠时为真。
    private bool IsFreeLanding(Vector3 point)
    {
        // 先检查场地与障碍，再遍历存活敌人补足导航不负责的角色间占用。
        var stage = mSpawnSystem.Environment;
        var center = stage != null ? stage.transform.position : Vector3.zero;
        var limit = stage != null ? stage.HalfSize - mRadius : 44f;
        if (Mathf.Abs(point.x - center.x) > limit || Mathf.Abs(point.z - center.z) > limit) return false;
        if (mNavigation != null && !mNavigation.IsFree(point, mRadius, mBody)) return false;
        var enemies = mSpawnSystem.AliveEnemies;
        for (var i = 0; i < enemies.Count; i++)
        {
            var other = enemies[i];
            if (other == null || other == this || !other.IsAlive) continue;
            var distance = mRadius + other.CollisionRadius;
            if (Flat(point - other.transform.position).sqrMagnitude < distance * distance) return false;
        }
        return true;
    }

    // 作用：复核目标落点并记录首领跳跃起点；返回：无返回值。
    private void BeginLeap()
    {
        // 前摇后落点已被占用则取消剩余连跳，不强行挤入。
        if (!IsFreeLanding(mLockedPoint))
        {
            mBossJumpsRemaining = 0;
            BeginRecovery(1.5f);
            return;
        }
        mLeapOrigin = transform.position;
        mLeapTime = 0f;
        mPhase = AIPhase.Leap;
    }

    // 作用：推进正弦弧线跳跃并在落地后伤害、连跳或准备滚动；返回：无返回值。
    private void TickLeap(float deltaTime)
    {
        mLeapTime += deltaTime;
        var progress = Mathf.Clamp01(mLeapTime / LeapDuration);
        // 跳跃只跨越空中路径；起跳和落地均复核占用，不把目标挤进岩石。
        transform.position = Vector3.Lerp(mLeapOrigin, mLockedPoint, progress) + Vector3.up * (Mathf.Sin(progress * Mathf.PI) * 2f);
        if (progress < 1f) return;
        if (!IsFreeLanding(mLockedPoint))
        {
            // 落点受阻则尝试起点附近；仍无位置时留在目标上方，下帧继续复核。
            if (!TryLandingPoint(mLeapOrigin, out var fallback))
            {
                transform.position = mLockedPoint + Vector3.up * 2f;
                return;
            }
            transform.position = fallback;
            mBossJumpsRemaining = 0;
            BeginRecovery(1.5f);
            return;
        }
        transform.position = mLockedPoint;
        DamageArea(mLockedPoint, AreaRadius);
        mBossJumpsRemaining--;
        if (mBossJumpsRemaining > 0)
        {
            BeginWindup();
            return;
        }
        BeginRecovery(1.5f);
        mBossRollPending = true;
        mLockedDirection = DirectionToTarget();
        mTelegraph?.ShowLine(transform.position, transform.position + mLockedDirection * ChargeDistance, 0.25f);
    }

    // 作用：在史莱姆王血量跨过阈值时登记子怪生成任务；返回：无返回值。
    private void RegisterBossSummons()
    {
        // 两个位各触发一次；单次伤害跨过两个阈值会分别登记两批，不捕获延迟使用的母体对象。
        if (mChild || mConfig.AIType != EnemyAIType.SlimeKing || mCurrentHP <= 0f) return;
        if ((mBossThresholds & 1) == 0 && mCurrentHP <= mMaxHP * 0.7f)
        {
            mBossThresholds |= 1;
            mSpawnSystem.ScheduleChildren(EnemyConfigTable.SlimeGreenId, 6, 0f,
                transform.position, 30f, mSpawnMultiplier);
        }
        if ((mBossThresholds & 2) == 0 && mCurrentHP <= mMaxHP * 0.35f)
        {
            mBossThresholds |= 2;
            mSpawnSystem.ScheduleChildren(EnemyConfigTable.SlimeGreenId, 6, 0f,
                transform.position, 30f, mSpawnMultiplier);
        }
    }

    // 作用：判断鼓手能否支援指定敌人；返回：自身可支援、目标存活带盾且六米内有视线，并满足可选修盾条件时为真。
    public bool CanSupportTarget(Enemy target, bool requireMissingShield = true)
    {
        // 选择修复对象时要求盾有缺口；刷新连线时可关闭该限制，允许满盾目标保留连线。
        return CanSupport && target != null && target != this && target.IsAlive && target.ShieldCurrent > 0f
            && (!requireMissingShield || target.CanRepairShield)
            && Flat(target.transform.position - transform.position).sqrMagnitude <= 36f
            && HasSight(transform.position, target.transform.position);
    }

    // 作用：设置有效支援槽并立即刷新连线；返回：无返回值。
    public void SetSupportTarget(int index, Enemy target)
    {
        // 越界槽位直接忽略；写入后统一刷新连线，由刷新逻辑复核目标支援资格。
        if (index < 0 || index >= mSupportTargets.Length) return;
        mSupportTargets[index] = target;
        RefreshSupportLinks();
    }

    // 作用：清空所有支援目标及对应连线；返回：无返回值。
    public void ClearSupportTargets()
    {
        // 固定遍历全部槽位，保证停战、硬直或池复用后没有残留支援引用。
        for (var i = 0; i < mSupportTargets.Length; i++)
        {
            mSupportTargets[i] = null;
            mTelegraph?.ClearSupport(i);
        }
    }

    // 作用：从所有支援槽移除指定目标并隐藏相关连线；返回：无返回值。
    public void RemoveSupportTarget(Enemy target)
    {
        // 遍历而非首次匹配即退出，同一目标占据多个槽时也能完整移除。
        for (var i = 0; i < mSupportTargets.Length; i++)
            if (mSupportTargets[i] == target)
            {
                mSupportTargets[i] = null;
                mTelegraph?.ClearSupport(i);
            }
    }

    // 作用：复核各支援目标并更新或清除连线，不直接修盾；返回：无返回值。
    private void RefreshSupportLinks()
    {
        // 目标死亡、破盾、越界或失去视线时解除；仍有效的连线跟随双方位置。
        for (var i = 0; i < mSupportTargets.Length; i++)
        {
            var target = mSupportTargets[i];
            if (!CanSupportTarget(target, false))
            {
                mSupportTargets[i] = null;
                mTelegraph?.ClearSupport(i);
            }
            else mTelegraph?.ShowSupport(i, transform.position, target.transform.position);
        }
    }
}
