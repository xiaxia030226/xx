using QFramework;

/// <summary>
/// 敌人运行期统计数据接口。
/// </summary>
public interface IEnemyModel : IModel
{
    BindableProperty<int> AliveCount { get; }
    BindableProperty<int> KillCount { get; }
    void Reset();
}

/// <summary>
/// 记录当前存活敌人数和本局击杀数。
/// </summary>
public class EnemyModel : AbstractModel, IEnemyModel
{
    public BindableProperty<int> AliveCount { get; } = new BindableProperty<int>(0);
    public BindableProperty<int> KillCount { get; } = new BindableProperty<int>(0);

    /// <summary>
    /// 开始新一局时清空运行期统计。修改 Value 会通知所有 HUD 订阅者。
    /// </summary>
    public void Reset()
    {
        AliveCount.Value = 0;
        KillCount.Value = 0;
    }

    /// <summary>
    /// Model 注册进 GameArchitecture 时执行一次。
    /// </summary>
    protected override void OnInit()
    {
        Reset();
    }
}
