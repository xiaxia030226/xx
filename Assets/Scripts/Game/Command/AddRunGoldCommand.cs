using QFramework;

/// <summary>
/// 本局金币入账命令：拾取金币掉落物时累加到 RunGold（本局掉落统计）。
/// RunGold 只统计本局收入，结算时才统一并入存档金币（见 GameRoot.EnterResult）。
/// </summary>
public class AddRunGoldCommand : AbstractCommand
{
    public int Amount { get; }

    public AddRunGoldCommand(int amount)
    {
        Amount = amount;
    }

    protected override void OnExecute()
    {
        var model = this.GetModel<IEconomyModel>();
        model.RunGold.Value += Amount;
    }
}
