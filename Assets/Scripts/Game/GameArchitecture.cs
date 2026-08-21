using QFramework;

public class GameArchitecture : Architecture<GameArchitecture>
{
    protected override void Init()
    {
        RegisterModel<IPlayerModel>(new PlayerModel());
        RegisterModel<IGameStateModel>(new GameStateModel());
    }
}
