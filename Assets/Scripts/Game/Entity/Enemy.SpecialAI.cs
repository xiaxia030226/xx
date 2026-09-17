using QFramework;
using UnityEngine;

public partial class Enemy
{
    private readonly Enemy[] mSupportTargets = new Enemy[3];
    private int mBossThresholds;
    private int mBossJumpsRemaining;
    private bool mBossRollPending;
    private Vector3 mLeapOrigin;
    private float mLeapTime;
    private const float LeapDuration = 0.45f;

    public bool CanSupport => mIsAlive && !mCombatCancelled && mConfig.AIType == EnemyAIType.ShieldDrummer
        && mPhase != AIPhase.Stunned;

    private void BeginCharge()
    {
        mPhase = AIPhase.Charge;
        mChargeRemaining = ChargeDistance;
        mSkillHit = false;
        mTelegraph?.ClearAttack();
        Face(mLockedDirection);
    }

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

    private void HitAlongCharge(Vector3 from, Vector3 to)
    {
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

    private void EndCharge(bool brokeRock)
    {
        mChargeRemaining = 0f;
        if (mConfig.AIType == EnemyAIType.SlimeKing) mSkillCooldown = 0f;
        BeginRecovery(mConfig.AIType == EnemyAIType.SlimeCharge ? RecoveryDuration : brokeRock ? 2f : 1f);
    }

    private void SpawnPoison()
    {
        if (!mIsAlive || mCombatCancelled || !mSpawnSystem.PoisonPoolReady
            || this.GetModel<IGameStateModel>().State.Value != GameState.Playing) return;
        var position = mLockedPoint;
        position.y = 0.03f;
        var area = this.GetSystem<IGameObjectPoolSystem>().Spawn(PoisonArea.PoolKey, position,
            Quaternion.identity, GameRoot.BattleRoot != null ? GameRoot.BattleRoot : transform.parent);
        area.GetComponent<PoisonArea>().OnSpawn(mTarget, AreaRadius, 3f);
    }

    private bool TryTeleportPoint(out Vector3 point)
    {
        var distance = mConfig.TeleportDistance > 0f ? mConfig.TeleportDistance : 6f;
        var startAngle = Random.Range(0f, Mathf.PI * 2f);
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

    private void FinishTeleport()
    {
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

    private bool TryLandingPoint(Vector3 requested, out Vector3 point)
    {
        requested.y = transform.position.y;
        if (IsFreeLanding(requested))
        {
            point = requested;
            return true;
        }
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

    private bool IsFreeLanding(Vector3 point)
    {
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

    private void BeginLeap()
    {
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

    private void TickLeap(float deltaTime)
    {
        mLeapTime += deltaTime;
        var progress = Mathf.Clamp01(mLeapTime / LeapDuration);
        // 跳跃只跨越空中路径；起跳和落地均复核占用，不把目标挤进岩石。
        transform.position = Vector3.Lerp(mLeapOrigin, mLockedPoint, progress) + Vector3.up * (Mathf.Sin(progress * Mathf.PI) * 2f);
        if (progress < 1f) return;
        if (!IsFreeLanding(mLockedPoint))
        {
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

    private void RegisterBossSummons()
    {
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

    public bool CanSupportTarget(Enemy target, bool requireMissingShield = true)
    {
        return CanSupport && target != null && target != this && target.IsAlive && target.ShieldCurrent > 0f
            && (!requireMissingShield || target.CanRepairShield)
            && Flat(target.transform.position - transform.position).sqrMagnitude <= 36f
            && HasSight(transform.position, target.transform.position);
    }

    public void SetSupportTarget(int index, Enemy target)
    {
        if (index < 0 || index >= mSupportTargets.Length) return;
        mSupportTargets[index] = target;
        RefreshSupportLinks();
    }

    public void ClearSupportTargets()
    {
        for (var i = 0; i < mSupportTargets.Length; i++)
        {
            mSupportTargets[i] = null;
            mTelegraph?.ClearSupport(i);
        }
    }

    public void RemoveSupportTarget(Enemy target)
    {
        for (var i = 0; i < mSupportTargets.Length; i++)
            if (mSupportTargets[i] == target)
            {
                mSupportTargets[i] = null;
                mTelegraph?.ClearSupport(i);
            }
    }

    private void RefreshSupportLinks()
    {
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
