using QFramework;

/// <summary>全局流程状态，供输入、UI 和战斗逻辑判断当前允许的操作。</summary>
public enum GameState
{
    Boot, // 架构启动后的初始状态。
    MainMenu, // 主菜单阶段。
    CharacterSelect, // 角色选择阶段。
    LevelSelect, // 关卡选择阶段。
    Playing, // 战斗进行中，允许伤害及刷怪。
    Paused, // 暂停阶段，恢复时返回暂停前的状态。
    Result, // 胜负结算阶段。
    SafeLoot // 清场后的安全拾取阶段，停止战斗但仍可拾取武器和护盾。
}

/// <summary>全局流程数据接口，与玩家自身属性分开管理。</summary>
public interface IGameStateModel : IModel
{
    BindableProperty<GameState> State { get; } // 当前全局流程状态。
    BindableProperty<int> CurrentWave { get; } // 当前已启动的波次编号。

    BindableProperty<int> SelectedLevel { get; } // 选中的关卡编号，用于加载关卡资产。

    BindableProperty<float> SummonMultiplier { get; } // 提前召唤累进的掉落倍率，自然启动波次时重置为一。
}

/// <summary>保存可订阅的流程状态、关卡选择、波次和召唤倍率。</summary>
public class GameStateModel : AbstractModel, IGameStateModel
{
    public BindableProperty<GameState> State { get; } = new BindableProperty<GameState>(GameState.Boot); // 初始启动状态，后续由场景入口及流程逻辑推进。
    public BindableProperty<int> CurrentWave { get; } = new BindableProperty<int>(0); // 当前波次编号，零表示尚未启动。
    public BindableProperty<int> SelectedLevel { get; } = new BindableProperty<int>(1); // 当前关卡选择，默认第一关。
    public BindableProperty<float> SummonMultiplier { get; } = new BindableProperty<float>(1f); // 当前召唤倍率，刷怪任务启动时保存快照供该波掉落使用。

    // 作用：响应模型初始化生命周期，当前不执行额外操作；返回：无返回值。
    protected override void OnInit()
    {
        // 默认值已由属性初始化器提供，此处不覆盖外部流程状态。
    }
}
