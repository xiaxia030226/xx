using QFramework;
using UnityEngine;

/// <summary>玩家回血命令；HP 变化通过 BindableProperty 通知订阅者。</summary>
public class PlayerHealCommand : AbstractCommand
{
    public float Amount { get; } // 本次生命增量，命令不校验其正负。

    // 作用：保存本次生命恢复量；返回：无返回值（构造函数）。
    public PlayerHealCommand(float amount)
    {
        // 构造时不修改生命，执行命令时才应用增量。
        Amount = amount;
    }

    // 作用：增加玩家生命并限制最大值；返回：无返回值。
    protected override void OnExecute()
    {
        var model = this.GetModel<IPlayerModel>();

        // 只钳制生命上限，此处没有阶段、存活状态或生命下限判定。
        model.HP.Value = Mathf.Min(model.MaxHP.Value, model.HP.Value + Amount);
    }
}
