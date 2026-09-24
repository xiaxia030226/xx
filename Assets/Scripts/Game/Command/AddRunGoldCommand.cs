using QFramework;

/// <summary>记录本局拾取金币收入，结算时由 GameRoot.EnterResult 并入存档金币。</summary>
public class AddRunGoldCommand : AbstractCommand
{
    public int Amount { get; } // 本次计入单局统计的金币增量。

    // 作用：保存本次单局金币增量；返回：无返回值（构造函数）。
    public AddRunGoldCommand(int amount)
    {
        // 仅保存参数，发送命令后才入账。
        Amount = amount;
    }

    // 作用：累加本局金币统计；返回：无返回值。
    protected override void OnExecute()
    {
        // 这里只更新 RunGold，不直接修改跨局 Gold，也不钳制增量。
        var model = this.GetModel<IEconomyModel>();
        model.RunGold.Value += Amount;
    }
}
