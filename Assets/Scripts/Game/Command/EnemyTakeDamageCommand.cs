using QFramework;

public class EnemyTakeDamageCommand : AbstractCommand
{
    private readonly Enemy mEnemy;
    private readonly DamageInfo mHit;

    public EnemyTakeDamageCommand(Enemy enemy, DamageInfo hit)
    {
        mEnemy = enemy;
        mHit = hit;
    }

    protected override void OnExecute()
    {
        if (this.GetModel<IGameStateModel>().State.Value != GameState.Playing) return;
        if (mEnemy == null || !mEnemy.IsAlive) return;
        mEnemy.ApplyDamage(mHit);
    }
}
