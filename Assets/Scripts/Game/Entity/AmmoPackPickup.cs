using QFramework;
using UnityEngine;

// 弹药拾取物：承载生成方传入的批次，靠近玩家后磁吸、入库并回池。
public class AmmoPackPickup : MonoBehaviour, IController
{
    public const string PoolKey = "ammo_pack_pickup"; // 弹药拾取物注册、生成和回收共用的池键。

    private const float MagnetRadius = 5f; // 开始磁吸的空间距离半径。

    private const float CollectRadius = 0.7f; // 判定入库的空间距离半径。

    private const float FlySpeed = 9f; // 磁吸移动速度，单位为米/秒。

    private Caliber mCaliber; // 此次生成弹药的口径。
    private int mLevel; // 此次生成弹药的穿甲等级。
    private AmmoBatch mAmmo; // 待入库批次，含数量及来源分类。

    private Transform mTarget; // 本次磁吸和拾取的玩家目标。

    // 作用：接入游戏架构；返回：游戏架构实例。
    public IArchitecture GetArchitecture() => GameArchitecture.Interface; // 直接取游戏架构入口，供框架扩展方法访问模型和系统。

    // 作用：覆盖池对象本次携带的弹药与目标；返回：无返回值。
    public void OnSpawn(Caliber caliber, int level, AmmoBatch ammo, Transform target)
    {
        // 批次来源由调用方传入，不沿用池中上一轮掉落的来源。
        mCaliber = caliber;
        mLevel = level;
        mAmmo = ammo;
        mTarget = target;
    }

    // 作用：在允许拾取的状态下磁吸并提交弹药入库；返回：无返回值。
    private void Update()
    {
        // 战斗与安全拾取阶段才推进；暂停及其他状态保留掉落物不动。
        if (mTarget == null) return;
        var state = this.GetModel<IGameStateModel>().State.Value;
        if (state != GameState.Playing && state != GameState.SafeLoot) return;

        var offset = mTarget.position - transform.position;

        // 使用移动前的三维距离判定磁吸与拾取，本帧移动后不重新测距。
        if (offset.sqrMagnitude <= MagnetRadius * MagnetRadius)
        {
            transform.position = Vector3.MoveTowards(transform.position, mTarget.position, FlySpeed * Time.deltaTime);
        }

        // 先清空批次与目标，再发命令，避免回调期间重复提交。
        if (offset.sqrMagnitude <= CollectRadius * CollectRadius)
        {
            var ammo = mAmmo;
            mAmmo = default;
            mTarget = null;
            this.SendCommand(new AddBulletsCommand(mCaliber, mLevel, ammo));
            this.GetSystem<IGameObjectPoolSystem>().Recycle(PoolKey, gameObject);
        }
    }
}
