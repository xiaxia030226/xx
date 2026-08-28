using QFramework;

/// <summary>
/// 游戏所处的大阶段。后续可以根据状态决定输入、UI 和战斗系统是否运行。
/// </summary>
public enum GameState
{
    Boot,
    MainMenu,
    CharacterSelect,
    LevelSelect,
    Playing,
    Paused,
    Result
}

/// <summary>
/// 全局流程数据接口，与玩家自身属性分开管理。
/// </summary>
public interface IGameStateModel : IModel
{
    BindableProperty<GameState> State { get; }
    BindableProperty<int> CurrentWave { get; }
}

/// <summary>
/// 保存当前游戏状态和波次。BindableProperty 允许 UI 在数值变化时自动刷新。
/// </summary>
public class GameStateModel : AbstractModel, IGameStateModel
{
    public BindableProperty<GameState> State { get; } = new BindableProperty<GameState>(GameState.Playing);
    public BindableProperty<int> CurrentWave { get; } = new BindableProperty<int>(0);

    /// <summary>
    /// Model 注册时调用，后续需要读取存档或初始化关卡状态时可放在这里。
    /// </summary>
    protected override void OnInit()
    {
    }
}
