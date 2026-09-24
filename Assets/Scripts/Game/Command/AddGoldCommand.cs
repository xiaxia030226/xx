using QFramework;
using UnityEngine;

/// <summary>调整存档金币；模型的变更订阅同步更新 PlayerPrefs 中的值。</summary>
public class AddGoldCommand : AbstractCommand
{
    public int Amount { get; } // 本次金币增量，可为负数。

    // 作用：记录待结算的存档金币增量；返回：无返回值（构造函数）。
    public AddGoldCommand(int amount)
    {
        // 构造时只记录增量，不立即修改经济数据。
        Amount = amount;
    }

    // 作用：累加金币增量并防止余额小于零；返回：无返回值。
    protected override void OnExecute()
    {
        var model = this.GetModel<IEconomyModel>();

        // 统一在写入前钳制下限，Value 变化会触发模型订阅。
        model.Gold.Value = Mathf.Max(0, model.Gold.Value + Amount);
    }
}
