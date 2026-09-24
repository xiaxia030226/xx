using QFramework;
using UnityEngine;

// 金币拾取物：接收本次金额，靠近玩家后磁吸并计入本局金币。
public class GoldPickup : MonoBehaviour, IController
{
    public const string PoolKey = "gold_pickup"; // 金币拾取物注册、生成和回收共用的池键。

    private const float MagnetRadius = 5f; // 开始磁吸的空间距离半径。

    private const float CollectRadius = 0.7f; // 判定入账的空间距离半径。

    private const float FlySpeed = 9f; // 磁吸移动速度，单位为米/秒。

    private int mGold; // 本次拾取应计入本局的金币数。

    private Transform mTarget; // 本次磁吸和拾取的玩家目标。

    // 作用：接入游戏架构；返回：游戏架构实例。
    public IArchitecture GetArchitecture() => GameArchitecture.Interface; // 直接取游戏架构入口，供框架扩展方法访问模型和系统。

    // 作用：覆盖池对象本次金额与吸附目标；返回：无返回值。
    public void OnSpawn(int gold, Transform target)
    {
        // 每次出池同时替换金额与玩家引用，后续磁吸和入账只使用本轮数据。
        mGold = gold;
        mTarget = target;
    }

    // 作用：在允许拾取时磁吸金币并提交入账；返回：无返回值。
    private void Update()
    {
        // 仅战斗和安全拾取阶段推进，目标丢失时不处理。
        if (mTarget == null) return;
        var state = this.GetModel<IGameStateModel>().State.Value;
        if (state != GameState.Playing && state != GameState.SafeLoot) return;

        var offset = mTarget.position - transform.position;

        // 两次判定共用移动前的三维距离，不在本帧移动后重新测距。
        if (offset.sqrMagnitude <= MagnetRadius * MagnetRadius)
        {
            transform.position = Vector3.MoveTowards(transform.position, mTarget.position, FlySpeed * Time.deltaTime);
        }

        // 进入拾取半径后由命令入账，再将场景对象回池。
        if (offset.sqrMagnitude <= CollectRadius * CollectRadius)
        {
            this.SendCommand(new AddRunGoldCommand(mGold));
            this.GetSystem<IGameObjectPoolSystem>().Recycle(PoolKey, gameObject);
        }
    }
}
