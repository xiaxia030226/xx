using QFramework;
using UnityEngine;

/// <summary>
/// 玩家回血命令。HP 改变后，BindableProperty 会自动通知 HUD 刷新。
/// </summary>
public class PlayerHealCommand : AbstractCommand
{
    public int Amount { get; }

    public PlayerHealCommand(int amount)
    {
        Amount = amount;
    }

    protected override void OnExecute()
    {
        var model = this.GetModel<IPlayerModel>();

        // Mathf.Min 保证回血后不超过最大生命值。
        model.HP.Value = Mathf.Min(model.MaxHP.Value, model.HP.Value + Amount);
    }
}
