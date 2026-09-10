using QFramework;

/// <summary>
/// 获得经验命令。经验满时自动升级并广播 LevelUpEvent，
/// 升级所需经验随等级线性增长：5 + (等级 - 1) * 3。
/// </summary>
public class GainExpCommand : AbstractCommand
{
    public int Amount { get; }

    public GainExpCommand(int amount)
    {
        Amount = amount;
    }

    protected override void OnExecute()
    {
        var model = this.GetModel<IPlayerModel>();
        model.Exp.Value += Amount;

        // while 而非 if：一次拾取多个水晶可能连升多级，每次升级都要发事件。
        while (model.Exp.Value >= model.ExpNeed.Value)
        {
            model.Exp.Value -= model.ExpNeed.Value;
            model.Level.Value++;
            model.ExpNeed.Value = 5 + (model.Level.Value - 1) * 3;

            this.SendEvent(new LevelUpEvent { Level = model.Level.Value });
        }
    }
}
