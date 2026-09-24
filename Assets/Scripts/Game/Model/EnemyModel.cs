using QFramework;

/// <summary>敌人运行期统计数据接口。</summary>
public interface IEnemyModel : IModel
{
    BindableProperty<int> AliveCount { get; } // 当前场上存活敌人数。
    BindableProperty<int> KillCount { get; } // 本局累计击杀数。
    // 作用：清空敌人存活及击杀统计；返回：无返回值。
    void Reset();
}

/// <summary>记录当前存活敌人数和本局击杀数。</summary>
public class EnemyModel : AbstractModel, IEnemyModel
{
    public BindableProperty<int> AliveCount { get; } = new BindableProperty<int>(0); // 可订阅的当前存活数。
    public BindableProperty<int> KillCount { get; } = new BindableProperty<int>(0); // 可订阅的本局击杀数。

    // 作用：将本局敌人统计恢复为零；返回：无返回值。
    public void Reset()
    {
        // 通过 Value 更新，让已有 UI 等订阅者收到数值变化。
        AliveCount.Value = 0;
        KillCount.Value = 0;
    }

    // 作用：模型初始化时建立空统计；返回：无返回值。
    protected override void OnInit()
    {
        // 初始化复用新局重置逻辑，不单独维护另一套默认值。
        Reset();
    }
}
