using QFramework;
using UnityEngine;

public class Bullet : MonoBehaviour, IController
{
    private const float MaxDistance = 100f; // 弹丸飞行距离上限。
    private const float HitRadius = 0.12f; // 起点重叠及连续球扫的命中半径。
    private string mPoolKey; // 本次弹丸回收时使用的池键。
    private DamageInfo mHit; // 本次命中的伤害数值、等级及阵营等信息。
    private float mSpeed; // 飞行速度，单位为米/秒。
    private Vector3 mDirection; // 归一化后的固定飞行方向。
    private float mTraveledDistance; // 已累计的计划飞行步长，用于射程回收。
    private bool mFlying; // 是否仍可飞行及结算命中，避免重复回收。

    // 作用：接入游戏架构；返回：游戏架构实例。
    public IArchitecture GetArchitecture() => GameArchitecture.Interface; // 直接取游戏架构入口，供框架扩展方法访问模型和系统。

    // 作用：覆盖复用弹丸的攻击数据并开始新一轮飞行；返回：无返回值。
    public void Setup(string poolKey, DamageInfo hit, float speed, Vector3 direction)
    {
        // 覆盖本轮攻击参数，方向归一化并重置累计射程，避免沿用上次飞行状态。
        mPoolKey = poolKey;
        mHit = hit;
        mSpeed = speed;
        mDirection = direction.normalized;
        mTraveledDistance = 0f;
        mFlying = true;
    }

    // 作用：禁用或回池时停止飞行标记；返回：无返回值。
    private void OnDisable()
    {
        // 禁用即撤销飞行资格，重新启用后仍须由 Setup 开启新一轮飞行。
        mFlying = false;
    }

    // 作用：推进弹丸并以起点重叠和球扫检测最近命中；返回：无返回值。
    private void Update()
    {
        // 暂停只冻结，退出战斗则取消飞行并回池。
        if (!mFlying) return;
        var state = this.GetModel<IGameStateModel>().State.Value;
        if (state == GameState.Paused) return;
        if (state != GameState.Playing)
        {
            CancelFlight();
            return;
        }

        // 同步 Transform 后先检查出生点/当前位置重叠，避免球扫漏掉起点内部目标。
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

        // 扫过本帧完整路程并挑选最近有效目标，避免高速弹丸穿过薄碰撞体。
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

    // 作用：筛选本弹丸可命中的对象；返回：玩家弹对存活敌人、敌人弹对玩家或非角色障碍为真，其余为假。
    private bool CanHit(Collider other)
    {
        // 排除弹丸自身及友方；角色判定优先于障碍，避免障碍型敌人被己方弹误击。
        if (other.GetComponentInParent<Bullet>() != null) return false;
        var enemy = other.GetComponentInParent<Enemy>();
        if (enemy != null) return mHit.Faction == CombatFaction.Player && enemy.IsAlive;
        if (other.GetComponentInParent<Player>() != null) return mHit.Faction == CombatFaction.Enemy;
        return other.GetComponentInParent<BattleObstacle>() != null;
    }

    // 作用：按受击对象分派伤害并回收当前弹丸；返回：无返回值。
    private void Hit(Collider other)
    {
        if (!mFlying) return;
        // 先失效，死亡事件可能同步清理场上全部弹丸。
        mFlying = false;
        var enemy = other.GetComponentInParent<Enemy>();
        var rock = other.GetComponentInParent<DestructibleRock>();
        // 只有玩家弹对岩石扣血；其他障碍仍会截停弹丸但不产生伤害。
        if (enemy != null) enemy.TakeHit(mHit);
        else if (other.GetComponentInParent<Player>() != null) this.SendCommand(new PlayerTakeDamageCommand(mHit));
        else if (rock != null && mHit.Faction == CombatFaction.Player) rock.TakeDamage(mHit.Amount);
        this.GetSystem<IGameObjectPoolSystem>().Recycle(mPoolKey, gameObject);
    }

    // 作用：不结算伤害地终止飞行并回池；返回：无返回值。
    public void CancelFlight()
    {
        // 只回收仍在飞行的弹丸，先撤销飞行标记以阻止重复回收，不走命中结算。
        if (!mFlying) return;
        mFlying = false;
        this.GetSystem<IGameObjectPoolSystem>().Recycle(mPoolKey, gameObject);
    }
}
