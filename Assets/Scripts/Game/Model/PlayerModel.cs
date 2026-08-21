using QFramework;

public interface IPlayerModel : IModel
{
    BindableProperty<int> HP { get; }
    BindableProperty<int> MaxHP { get; }
    BindableProperty<float> MoveSpeed { get; }
    BindableProperty<int> Level { get; }
    BindableProperty<int> Exp { get; }
    BindableProperty<int> ExpNeed { get; }
}

public class PlayerModel : AbstractModel, IPlayerModel
{
    public BindableProperty<int> HP { get; } = new BindableProperty<int>(100);
    public BindableProperty<int> MaxHP { get; } = new BindableProperty<int>(100);
    public BindableProperty<float> MoveSpeed { get; } = new BindableProperty<float>(5f);
    public BindableProperty<int> Level { get; } = new BindableProperty<int>(1);
    public BindableProperty<int> Exp { get; } = new BindableProperty<int>(0);
    public BindableProperty<int> ExpNeed { get; } = new BindableProperty<int>(5);

    protected override void OnInit()
    {
    }
}
