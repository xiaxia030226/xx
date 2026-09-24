using QFramework;

/// <summary>
/// 游戏的 QFramework 架构入口。
/// Controller、Command 和 UI 都通过 GameArchitecture.Interface 获取已注册的模型与系统。
/// </summary>
public class GameArchitecture : Architecture<GameArchitecture>
{
    // 作用：在架构首次初始化时按接口注册共享模型和系统；返回：无返回值。
    protected override void Init()
    {
        // 先注册数据模型，为命令和系统提供统一的状态来源。
        RegisterModel<IPlayerModel>(new PlayerModel());
        RegisterModel<IGameStateModel>(new GameStateModel());
        RegisterModel<IEnemyModel>(new EnemyModel());
        RegisterModel<IEconomyModel>(new EconomyModel());
        RegisterModel<IBulletInventoryModel>(new BulletInventoryModel());

        // QFramework 系统不是 MonoBehaviour；场景绑定由 GameRoot 完成，逐帧逻辑由其 Update 显式调用 Tick。
        RegisterSystem<IGameObjectPoolSystem>(new GameObjectPoolSystem());
        RegisterSystem<IEnemySpawnSystem>(new EnemySpawnSystem());
        RegisterSystem<IWeaponSystem>(new WeaponSystem());
    }
}
