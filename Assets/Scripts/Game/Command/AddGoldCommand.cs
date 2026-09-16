using QFramework;
using UnityEngine;

/// <summary>
/// 增加金币命令。金币变动统一走命令入口，Model 的落盘订阅会自动把新值写入存档。
/// </summary>
public class AddGoldCommand : AbstractCommand
{
    public int Amount { get; }

    public AddGoldCommand(int amount)
    {
        Amount = amount;
    }

    protected override void OnExecute()
    {
        var model = this.GetModel<IEconomyModel>();

        // Mathf.Max 保证金币不会变负（当前只有增加场景，防御性处理）。
        model.Gold.Value = Mathf.Max(0, model.Gold.Value + Amount);
    }
}
