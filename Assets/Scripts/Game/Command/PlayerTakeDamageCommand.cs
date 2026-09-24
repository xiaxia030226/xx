using QFramework;
using UnityEngine;

public class PlayerTakeDamageCommand : AbstractCommand
{
    private readonly DamageInfo mHit; // 玩家本次受击的伤害及攻击来源信息。

    // 作用：保存待应用的玩家受击信息；返回：无返回值（构造函数）。
    public PlayerTakeDamageCommand(DamageInfo hit)
    {
        // 保存命中快照，执行时使用玩家最新的护盾状态结算。
        mHit = hit;
    }

    // 作用：在战斗中扣除玩家护盾和生命并发送死亡或破盾事件；返回：无返回值。
    protected override void OnExecute()
    {
        // 暂停、安全拾取等非战斗阶段不受伤，已死亡玩家也不重复处理。
        if (this.GetModel<IGameStateModel>().State.Value != GameState.Playing) return;
        var model = this.GetModel<IPlayerModel>();
        if (model.HP.Value <= 0f) return;

        // 先按当前护盾分摊，再分别扣减并钳制到零。
        var result = DamageResolver.Calculate(mHit, model.ShieldLevel.Value, model.Shield.Value);
        model.Shield.Value = Mathf.Max(0f, model.Shield.Value - result.ShieldDamage);
        model.HP.Value = Mathf.Max(0f, model.HP.Value - result.HealthDamage);
        // 同一次命中既破盾又致死时，死亡事件优先，随后直接结束。
        if (model.HP.Value <= 0f)
        {
            this.SendEvent<PlayerDiedEvent>();
            return;
        }
        if (result.BrokeShield) this.SendEvent<PlayerShieldBrokenEvent>();
    }
}
