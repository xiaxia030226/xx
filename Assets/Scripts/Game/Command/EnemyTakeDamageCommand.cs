using QFramework;

public class EnemyTakeDamageCommand : AbstractCommand
{
    private readonly Enemy mEnemy; // 本次受击的敌人实例。
    private readonly DamageInfo mHit; // 待结算的攻击信息。

    // 作用：保存受击目标及攻击数据；返回：无返回值（构造函数）。
    public EnemyTakeDamageCommand(Enemy enemy, DamageInfo hit)
    {
        // 先绑定目标与命中快照，执行时再检查目标是否仍可受伤。
        mEnemy = enemy;
        mHit = hit;
    }

    // 作用：在战斗中向存活敌人应用伤害；返回：无返回值。
    protected override void OnExecute()
    {
        // 非战斗阶段或失效、死亡目标均忽略，避免迟到命中重复结算。
        if (this.GetModel<IGameStateModel>().State.Value != GameState.Playing) return;
        if (mEnemy == null || !mEnemy.IsAlive) return;
        mEnemy.ApplyDamage(mHit);
    }
}
