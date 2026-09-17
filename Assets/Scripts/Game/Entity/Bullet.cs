using QFramework;
using UnityEngine;

public class Bullet : MonoBehaviour, IController
{
    private const float MaxDistance = 100f;
    private const float HitRadius = 0.12f;
    private string mPoolKey;
    private DamageInfo mHit;
    private float mSpeed;
    private Vector3 mDirection;
    private float mTraveledDistance;
    private bool mFlying;

    public IArchitecture GetArchitecture() => GameArchitecture.Interface;

    public void Setup(string poolKey, DamageInfo hit, float speed, Vector3 direction)
    {
        mPoolKey = poolKey;
        mHit = hit;
        mSpeed = speed;
        mDirection = direction.normalized;
        mTraveledDistance = 0f;
        mFlying = true;
    }

    private void OnDisable()
    {
        mFlying = false;
    }

    private void Update()
    {
        if (!mFlying) return;
        var state = this.GetModel<IGameStateModel>().State.Value;
        if (state == GameState.Paused) return;
        if (state != GameState.Playing)
        {
            CancelFlight();
            return;
        }

        Physics.SyncTransforms();
        var origin = transform.position;
        Collider closest = null;
        var distance = float.PositiveInfinity;
        foreach (var overlap in Physics.OverlapSphere(origin, HitRadius, ~0, QueryTriggerInteraction.Collide))
        {
            if (!CanHit(overlap)) continue;
            var squareDistance = (overlap.ClosestPoint(origin) - origin).sqrMagnitude;
            if (squareDistance >= distance) continue;
            closest = overlap;
            distance = squareDistance;
        }
        if (closest != null)
        {
            Hit(closest);
            return;
        }

        var step = Mathf.Min(mSpeed * Time.deltaTime, MaxDistance - mTraveledDistance);
        distance = float.PositiveInfinity;
        foreach (var hit in Physics.SphereCastAll(origin, HitRadius, mDirection, step, ~0, QueryTriggerInteraction.Collide))
        {
            if (!CanHit(hit.collider) || hit.distance >= distance) continue;
            closest = hit.collider;
            distance = hit.distance;
        }
        transform.position += mDirection * (closest != null ? distance : step);
        mTraveledDistance += step;
        if (closest != null) Hit(closest);
        else if (mTraveledDistance >= MaxDistance) CancelFlight();
    }

    private bool CanHit(Collider other)
    {
        if (other.GetComponentInParent<Bullet>() != null) return false;
        var enemy = other.GetComponentInParent<Enemy>();
        if (enemy != null) return mHit.Faction == CombatFaction.Player && enemy.IsAlive;
        if (other.GetComponentInParent<Player>() != null) return mHit.Faction == CombatFaction.Enemy;
        return other.GetComponentInParent<BattleObstacle>() != null;
    }

    private void Hit(Collider other)
    {
        if (!mFlying) return;
        // 先失效，死亡事件可能同步清理场上全部弹丸。
        mFlying = false;
        var enemy = other.GetComponentInParent<Enemy>();
        var rock = other.GetComponentInParent<DestructibleRock>();
        if (enemy != null) enemy.TakeHit(mHit);
        else if (other.GetComponentInParent<Player>() != null) this.SendCommand(new PlayerTakeDamageCommand(mHit));
        else if (rock != null && mHit.Faction == CombatFaction.Player) rock.TakeDamage(mHit.Amount);
        this.GetSystem<IGameObjectPoolSystem>().Recycle(mPoolKey, gameObject);
    }

    public void CancelFlight()
    {
        if (!mFlying) return;
        mFlying = false;
        this.GetSystem<IGameObjectPoolSystem>().Recycle(mPoolKey, gameObject);
    }
}
