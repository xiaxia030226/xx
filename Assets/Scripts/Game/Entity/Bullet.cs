using QFramework;
using UnityEngine;

/// <summary>
/// 子弹实体：直线飞行，命中敌人结算伤害后回池，飞出最大距离也回池。
/// 子弹由对象池复用，每次取出时由武器调用 Setup 重置状态。
/// </summary>
public class Bullet : MonoBehaviour, IController
{
    // MaxDistance：子弹的最大飞行距离（米），超过即回收，防止飞出地图后空跑。
    private const float MaxDistance = 60f;

    // mPoolKey：对象池 key（即子弹配置 id），回收时要知道放回哪个池。
    private string mPoolKey;

    // mDamage：本次飞行的命中伤害（武器伤害 + 子弹伤害，由武器在发射时算好传入）。
    private int mDamage;

    // mSpeed：飞行速度（米/秒）。
    private float mSpeed;

    // mDirection：飞行方向（单位向量，发射时取持有者朝向）。
    private Vector3 mDirection;

    // mTraveledDistance：本次飞行已累计的距离，用于判断回收。
    private float mTraveledDistance;

    public IArchitecture GetArchitecture() => GameArchitecture.Interface;

    /// <summary>
    /// 每次从对象池取出时由武器调用，重置本次飞行的全部状态。
    /// </summary>
    /// <param name="poolKey">对象池 key，回收时使用</param>
    /// <param name="damage">命中伤害（武器伤害 + 子弹伤害）</param>
    /// <param name="speed">飞行速度</param>
    /// <param name="direction">飞行方向</param>
    public void Setup(string poolKey, int damage, float speed, Vector3 direction)
    {
        mPoolKey = poolKey;
        mDamage = damage;
        mSpeed = speed;
        mDirection = direction.normalized;
        mTraveledDistance = 0f;
    }

    private void Update()
    {
        // step：本帧飞行距离。
        var step = mSpeed * Time.deltaTime;
        transform.position += mDirection * step;
        mTraveledDistance += step;

        // 飞出最大距离后回池，不销毁（对象池复用）。
        if (mTraveledDistance >= MaxDistance)
        {
            RecycleSelf();
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        // GetComponentInParent：敌人的 Collider 可能挂在子物体上，向上查 Enemy 组件。
        var enemy = other.GetComponentInParent<Enemy>();

        // 命中的不是敌人（围墙、玩家等）或敌人已死（尸体等待回池），直接忽略。
        if (enemy == null || !enemy.IsAlive) return;

        enemy.TakeHit(mDamage);
        RecycleSelf();
    }

    /// <summary>
    /// 把子弹放回对象池。池未满时缓存复用，由 GameObjectPoolSystem 管理。
    /// </summary>
    private void RecycleSelf()
    {
        this.GetSystem<IGameObjectPoolSystem>().Recycle(mPoolKey, gameObject);
    }
}
