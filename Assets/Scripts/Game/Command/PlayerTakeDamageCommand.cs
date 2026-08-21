using QFramework;
using UnityEngine;

public class PlayerTakeDamageCommand : AbstractCommand
{
    public int Damage { get; }

    public PlayerTakeDamageCommand(int damage)
    {
        Damage = damage;
    }

    protected override void OnExecute()
    {
        var model = this.GetModel<IPlayerModel>();
        model.HP.Value = Mathf.Max(0, model.HP.Value - Damage);
    }
}
