using QFramework;
using UnityEngine;

/// <summary>
/// 子弹包掉落物：敌人死亡时按掷点生成，玩家靠近后磁吸飞行，接触入库并回池。
/// 占位 prefab 由 Editor 菜单 Game/阶段三/生成缺失资源 产出（Resources/Prefabs/StageThree/AmmoPackPickup），
/// GameRoot.RegisterPickup 加载并注册入池。
/// </summary>
public class AmmoPackPickup : MonoBehaviour, IController
{
    // PoolKey：对象池 key，GameRoot 注册与生成、本类回收共用。
    public const string PoolKey = "ammo_pack_pickup";

    // MagnetRadius：进入该半径后开始磁吸飞向玩家。
    private const float MagnetRadius = 5f;

    // CollectRadius：进入该半径后视为拾取成功。
    private const float CollectRadius = 0.7f;

    // FlySpeed：磁吸飞行速度（米/秒）。
    private const float FlySpeed = 9f;

    // mCaliber / mLevel / mCount：子弹包含弹的口径、穿甲等级与数量（掉落掷点结果）。
    private Caliber mCaliber;
    private int mLevel;
    private int mCount;

    // mTarget：吸附目标（玩家）。
    private Transform mTarget;

    public IArchitecture GetArchitecture() => GameArchitecture.Interface;

    /// <summary>
    /// 每次从对象池取出时调用，重置含弹信息与吸附目标。
    /// </summary>
    public void OnSpawn(Caliber caliber, int level, int count, Transform target)
    {
        mCaliber = caliber;
        mLevel = level;
        mCount = count;
        mTarget = target;
    }

    private void Update()
    {
        if (mTarget == null) return;
        var state = this.GetModel<IGameStateModel>().State.Value;
        if (state != GameState.Playing && state != GameState.SafeLoot) return;

        var offset = mTarget.position - transform.position;

        // 磁吸半径内：朝玩家匀速飞行。
        if (offset.sqrMagnitude <= MagnetRadius * MagnetRadius)
        {
            transform.position = Vector3.MoveTowards(transform.position, mTarget.position, FlySpeed * Time.deltaTime);
        }

        // 拾取半径内：子弹入库并回池。
        if (offset.sqrMagnitude <= CollectRadius * CollectRadius)
        {
            this.SendCommand(new AddBulletsCommand(mCaliber, mLevel, mCount));
            this.GetSystem<IGameObjectPoolSystem>().Recycle(PoolKey, gameObject);
        }
    }
}
