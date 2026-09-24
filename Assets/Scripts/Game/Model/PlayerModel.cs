using QFramework;

public interface IPlayerModel : IModel
{
    BindableProperty<float> HP { get; } // 当前生命值。
    BindableProperty<float> MaxHP { get; } // 生命值上限。
    BindableProperty<float> MoveSpeed { get; } // 玩家移动速度。
    BindableProperty<int> ShieldLevel { get; } // 当前护盾等级，是否有效还取决于剩余容量。
    BindableProperty<float> Shield { get; } // 当前剩余护盾容量。
    BindableProperty<float> MaxShield { get; } // 当前护盾最大容量。
    // 作用：恢复玩家新局基础属性并移除护盾；返回：无返回值。
    void Reset();
}

public class PlayerModel : AbstractModel, IPlayerModel
{
    public BindableProperty<float> HP { get; } = new BindableProperty<float>(100f); // 可订阅的当前生命，初始为一百。
    public BindableProperty<float> MaxHP { get; } = new BindableProperty<float>(100f); // 可订阅的生命上限。
    public BindableProperty<float> MoveSpeed { get; } = new BindableProperty<float>(5f); // 可订阅的移动速度，单位为米/秒。
    public BindableProperty<int> ShieldLevel { get; } = new BindableProperty<int>(0); // 当前护盾等级，初始零级表示无盾。
    public BindableProperty<float> Shield { get; } = new BindableProperty<float>(0f); // 当前护盾余量，耗尽时等级不由本属性自动清零。
    public BindableProperty<float> MaxShield { get; } = new BindableProperty<float>(0f); // 护盾容量上限，装备护盾时更新。

    // 作用：将生命、速度及护盾恢复为新局默认值；返回：无返回值。
    public void Reset()
    {
        // 先恢复基础生存及移动属性，再清除护盾等级和容量。
        HP.Value = 100f;
        MaxHP.Value = 100f;
        MoveSpeed.Value = 5f;
        ShieldLevel.Value = 0;
        Shield.Value = 0f;
        MaxShield.Value = 0f;
    }

    // 作用：响应模型初始化生命周期，当前无额外初始化；返回：无返回值。
    protected override void OnInit()
    {
        // 属性初始化器已提供默认值；新局重置由外部显式调用 Reset。
    }
}
