using QFramework;

/// <summary>
/// 敌人受伤命令，将武器命中与敌人生命结算解耦。
/// </summary>
public class EnemyTakeDamageCommand : AbstractCommand
{
    private readonly Enemy mEnemy;
    private readonly int mDamage;

    public EnemyTakeDamageCommand(Enemy enemy, int damage)
    {
        mEnemy = enemy;
        mDamage = damage;
    }

    protected override void OnExecute()
    {
        // 对象可能在命令执行前已经死亡并回池，因此先验证引用和存活状态。
        if (mEnemy == null || !mEnemy.IsAlive) return;
        mEnemy.ApplyDamage(mDamage);
    }
}
