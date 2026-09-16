using QFramework;

/// <summary>
/// 玩家数据对外暴露的接口。表现层只通过接口读取数据，避免依赖具体实现。
/// BindableProperty 在 Value 变化时会通知订阅者，因此 HUD 不需要每帧轮询。
/// </summary>
public interface IPlayerModel : IModel
{
    BindableProperty<int> HP { get; }
    BindableProperty<int> MaxHP { get; }
    BindableProperty<float> MoveSpeed { get; }
    BindableProperty<int> Level { get; }
    BindableProperty<int> Exp { get; }
    BindableProperty<int> ExpNeed { get; }

    // Reset：开始新一局时把全部运行期数据恢复为初始值。
    void Reset();
}

/// <summary>
/// 保存玩家运行期数据及其初始值。
/// 数据修改应由 Command 完成，Player 和 GameHUD 等表现层不直接改 Value。
/// </summary>
public class PlayerModel : AbstractModel, IPlayerModel
{
    public BindableProperty<int> HP { get; } = new BindableProperty<int>(100);
    public BindableProperty<int> MaxHP { get; } = new BindableProperty<int>(100);
    public BindableProperty<float> MoveSpeed { get; } = new BindableProperty<float>(5f);
    public BindableProperty<int> Level { get; } = new BindableProperty<int>(1);
    public BindableProperty<int> Exp { get; } = new BindableProperty<int>(0);
    public BindableProperty<int> ExpNeed { get; } = new BindableProperty<int>(5);

    /// <summary>
    /// 开始新一局时恢复全部初始值。Value 变化会自动通知 HUD 刷新，无需额外广播。
    /// </summary>
    public void Reset()
    {
        HP.Value = 100;
        MaxHP.Value = 100;
        MoveSpeed.Value = 5f;
        Level.Value = 1;
        Exp.Value = 0;
        ExpNeed.Value = 5;
    }

    /// <summary>
    /// Model 注册到架构时调用。当前数据已在字段声明处初始化，暂时无需额外处理。
    /// </summary>
    protected override void OnInit()
    {
    }
}
