using QFramework;
using UnityEngine;

public partial class Enemy
{
    private enum AIPhase { Chase, Windup, Charge, Recovery, Stunned, TeleportArrival, Leap }

    private AIPhase mPhase;
    private float mPhaseTime;
    private float mWindupDuration;
    private float mSkillCooldown;
    private float mContactCooldown;
    private float mPathTimer;
    private Vector3 mPathDirection;
    private Vector3 mPathTarget;
    private Vector3 mLockedDirection;
    private Vector3 mLockedPoint;
    private float mChargeRemaining;
    private bool mSkillHit;
    private int mFlankSide;
    private static int sFlankSequence;

    private void ResetAI()
    {
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

    private void TickAI(float deltaTime)
    {
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
        || mConfig.AIType == EnemyAIType.Split;

    private float SkillRange
    {
        get
        {
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
        : mConfig.AIType == EnemyAIType.Poison ? 2f : 3f;
    private float ChargeDistance => mConfig.ChargeDistance > 0f ? mConfig.ChargeDistance
        : mConfig.AIType == EnemyAIType.SlimeCharge ? 4f : mConfig.AIType == EnemyAIType.Ram ? 10f : 12f;
    private float ChargeSpeed => mConfig.ChargeSpeed > 0f ? mConfig.ChargeSpeed
        : mConfig.AIType == EnemyAIType.Ram ? 10f : 8f;
    private float RecoveryDuration => mConfig.Recovery > 0f ? mConfig.Recovery : 0.35f;

    private float WindupDuration
    {
        get
        {
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

    private void BeginWindup()
    {
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

    private void TickWindup(float deltaTime)
    {
        mPhaseTime -= deltaTime;
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

    private void BeginRecovery(float duration)
    {
        mPhase = AIPhase.Recovery;
        mPhaseTime = Mathf.Max(0f, duration);
        mTelegraph?.ClearAttack();
        ResetPose();
    }

    private void InterruptAttack(float duration)
    {
        mPhase = AIPhase.Stunned;
        mPhaseTime = duration;
        mChargeRemaining = 0f;
        mSkillHit = true;
        mBossRollPending = false;
        mTelegraph?.ClearAttack();
        ClearSupportTargets();
        ResetPose();
    }

    private void MoveTowards(Vector3 target, float deltaTime)
    {
        var offset = Flat(target - transform.position);
        if (offset.sqrMagnitude < 0.01f) return;
        if (mPathTimer <= 0f || mPathDirection.sqrMagnitude < 0.01f || Flat(target - mPathTarget).sqrMagnitude > 4f)
        {
            mPathTimer = 0.25f + Mathf.Abs(GetInstanceID() % 7) * 0.02f;
            mPathTarget = target;
            mPathDirection = mNavigation != null
                ? mNavigation.GetNextDirection(transform.position, target, mRadius) : offset.normalized;
        }
        var direction = Flat(mPathDirection);
        var separation = Vector3.zero;
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

    private Vector3 MovePosition(Vector3 displacement)
    {
        if (mNavigation != null) return mNavigation.Move(transform.position, displacement, mRadius, mBody);
        var position = transform.position + displacement;
        var stage = mSpawnSystem.Environment;
        var center = stage != null ? stage.transform.position : Vector3.zero;
        var limit = stage != null ? Mathf.Max(0f, stage.HalfSize - mRadius) : 44f;
        position.x = Mathf.Clamp(position.x, center.x - limit, center.x + limit);
        position.z = Mathf.Clamp(position.z, center.z - limit, center.z + limit);
        return position;
    }

    private bool HasSight(Vector3 from, Vector3 to) => mNavigation == null || mNavigation.HasLineOfSight(from, to);
    private Vector3 DirectionToTarget()
    {
        var direction = Flat(mTarget.position - transform.position);
        return direction.sqrMagnitude > 0.0001f ? direction.normalized : transform.forward;
    }
    private static Vector3 Flat(Vector3 value) { value.y = 0f; return value; }
    private void Face(Vector3 direction)
    {
        if (direction.sqrMagnitude > 0.001f) transform.rotation = Quaternion.LookRotation(direction);
    }

    private void DealPlayerDamage()
    {
        if (!mIsAlive || mCombatCancelled) return;
        this.SendCommand(new PlayerTakeDamageCommand(new DamageInfo(mConfig.ContactDamage,
            mConfig.AttackLevel, CombatFaction.Enemy)));
    }

    private void DamageArea(Vector3 center, float radius)
    {
        if (mSkillHit) return;
        mSkillHit = true;
        if (Flat(mTarget.position - center).sqrMagnitude <= (radius + 0.45f) * (radius + 0.45f)
            && HasSight(center, mTarget.position)) DealPlayerDamage();
    }

    private void FireProjectile(Vector3 direction)
    {
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
