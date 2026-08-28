using QFramework;

/// <summary>
/// 游戏的 QFramework 架构入口。
/// Controller、Command 和 UI 都通过 GameArchitecture.Interface 获取已注册的 Model。
/// </summary>
public class GameArchitecture : Architecture<GameArchitecture>
{
    /// <summary>
    /// 架构第一次创建时调用，在这里集中注册全局数据模型。
    /// 接口与实现分开注册，业务代码只依赖接口，后续替换实现会更方便。
    /// </summary>
    protected override void Init()
    {
        RegisterModel<IPlayerModel>(new PlayerModel());
        RegisterModel<IGameStateModel>(new GameStateModel());
    }
}
