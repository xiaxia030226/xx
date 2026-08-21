using QFramework;

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

public interface IGameStateModel : IModel
{
    BindableProperty<GameState> State { get; }
    BindableProperty<int> CurrentWave { get; }
}

public class GameStateModel : AbstractModel, IGameStateModel
{
    public BindableProperty<GameState> State { get; } = new BindableProperty<GameState>(GameState.Playing);
    public BindableProperty<int> CurrentWave { get; } = new BindableProperty<int>(0);

    protected override void OnInit()
    {
    }
}
