using QFramework;
using UnityEngine;

/// <summary>
/// 玩家扣血命令。表现层发送命令，命令负责修改 Model，避免各处直接操作血量。
/// </summary>
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

        // Value 改变后会通知所有订阅者；Mathf.Max 保证生命值不会低于 0。
        model.HP.Value = Mathf.Max(0, model.HP.Value - Damage);

        // 血量归零时广播死亡事件，由 GameRoot 接管进入失败结算。
        // 只在战斗进行中发送——结算状态下敌人继续碰撞不会重复触发。
        if (model.HP.Value <= 0 &&
            this.GetModel<IGameStateModel>().State.Value == GameState.Playing)
        {
            this.SendEvent<PlayerDiedEvent>();
        }
    }
}
