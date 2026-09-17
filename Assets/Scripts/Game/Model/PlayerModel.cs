using QFramework;

public interface IPlayerModel : IModel
{
    BindableProperty<float> HP { get; }
    BindableProperty<float> MaxHP { get; }
    BindableProperty<float> MoveSpeed { get; }
    BindableProperty<int> ShieldLevel { get; }
    BindableProperty<float> Shield { get; }
    BindableProperty<float> MaxShield { get; }
    void Reset();
}

public class PlayerModel : AbstractModel, IPlayerModel
{
    public BindableProperty<float> HP { get; } = new BindableProperty<float>(100f);
    public BindableProperty<float> MaxHP { get; } = new BindableProperty<float>(100f);
    public BindableProperty<float> MoveSpeed { get; } = new BindableProperty<float>(5f);
    public BindableProperty<int> ShieldLevel { get; } = new BindableProperty<int>(0);
    public BindableProperty<float> Shield { get; } = new BindableProperty<float>(0f);
    public BindableProperty<float> MaxShield { get; } = new BindableProperty<float>(0f);

    public void Reset()
    {
        HP.Value = 100f;
        MaxHP.Value = 100f;
        MoveSpeed.Value = 5f;
        ShieldLevel.Value = 0;
        Shield.Value = 0f;
        MaxShield.Value = 0f;
    }

    protected override void OnInit()
    {
    }
}
