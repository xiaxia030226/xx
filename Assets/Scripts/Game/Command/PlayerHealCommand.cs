using QFramework;
using UnityEngine;

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
        model.HP.Value = Mathf.Min(model.MaxHP.Value, model.HP.Value + Amount);
    }
}
