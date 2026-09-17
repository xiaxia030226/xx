using QFramework;
using UnityEngine;

public class PlayerTakeDamageCommand : AbstractCommand
{
    private readonly DamageInfo mHit;

    public PlayerTakeDamageCommand(DamageInfo hit)
    {
        mHit = hit;
    }

    protected override void OnExecute()
    {
        if (this.GetModel<IGameStateModel>().State.Value != GameState.Playing) return;
        var model = this.GetModel<IPlayerModel>();
        if (model.HP.Value <= 0f) return;

        var result = DamageResolver.Calculate(mHit, model.ShieldLevel.Value, model.Shield.Value);
        model.Shield.Value = Mathf.Max(0f, model.Shield.Value - result.ShieldDamage);
        model.HP.Value = Mathf.Max(0f, model.HP.Value - result.HealthDamage);
        if (model.HP.Value <= 0f)
        {
            this.SendEvent<PlayerDiedEvent>();
            return;
        }
        if (result.BrokeShield) this.SendEvent<PlayerShieldBrokenEvent>();
    }
}
