using QFramework;
using UnityEngine;

/// <summary>
/// 阶段二敌人实体：追击玩家、按间隔造成接触伤害、受伤后死亡回池。
/// </summary>
public class Enemy : MonoBehaviour, IController, ICanSendEvent
{
    private const float MapLimit = 49f;

    private EnemyConfig mConfig;
    private Transform mTarget;
    private int mCurrentHP;
    private float mNextAttackTime;
    private bool mIsAlive;

    public bool IsAlive => mIsAlive;
    public string EnemyId => mConfig?.Id;

    public IArchitecture GetArchitecture() => GameArchitecture.Interface;

    /// <summary>
    /// 每次从对象池取出时调用。必须在这里重置生命、目标和攻击计时等运行状态。
    /// </summary>
    public void OnSpawn(EnemyConfig config, Transform target)
    {
        mConfig = config;
        mTarget = target;
        mCurrentHP = config.MaxHP;
        mNextAttackTime = 0f;
        mIsAlive = true;
    }

    /// <summary>
    /// 武器命中入口，只负责发送 Command，不在碰撞判定处直接结算生命。
    /// </summary>
    public void TakeHit(int damage)
    {
        if (!mIsAlive || damage <= 0) return;
        this.SendCommand(new EnemyTakeDamageCommand(this, damage));
    }

    /// <summary>
    /// 由 EnemyTakeDamageCommand 调用，执行真正的扣血和死亡判断。
    /// </summary>
    public void ApplyDamage(int damage)
    {
        if (!mIsAlive) return;

        mCurrentHP = Mathf.Max(0, mCurrentHP - damage);
        if (mCurrentHP == 0) Die();
    }

    private void Update()
    {
        if (!mIsAlive || mTarget == null) return;
        if (this.GetModel<IGameStateModel>().State.Value != GameState.Playing) return;

        var offset = mTarget.position - transform.position;
        offset.y = 0f;

        // 使用平方距离省去开平方；进入攻击范围后停止移动，接触伤害由 OnTriggerStay 处理。
        if (offset.sqrMagnitude <= mConfig.AttackRange * mConfig.AttackRange) return;

        var position = transform.position + offset.normalized * (mConfig.MoveSpeed * Time.deltaTime);
        position.x = Mathf.Clamp(position.x, -MapLimit, MapLimit);
        position.z = Mathf.Clamp(position.z, -MapLimit, MapLimit);
        transform.position = position;
        transform.rotation = Quaternion.LookRotation(offset);
    }

    private void OnTriggerStay(Collider other)
    {
        if (!mIsAlive || Time.time < mNextAttackTime) return;
        if (other.GetComponent<Player>() == null) return;

        // OnTriggerStay 每个物理帧都会触发，用绝对时间限制为配置中的攻击间隔。
        mNextAttackTime = Time.time + mConfig.AttackInterval;
        this.SendCommand(new PlayerTakeDamageCommand(mConfig.ContactDamage));
    }

    private void Die()
    {
        mIsAlive = false;

        var model = this.GetModel<IEnemyModel>();
        model.AliveCount.Value = Mathf.Max(0, model.AliveCount.Value - 1);
        model.KillCount.Value++;

        // 先广播死亡事件，让经验、音效等模块响应；最后才将实体回收到对象池。
        this.SendEvent(new EnemyDiedEvent
        {
            EnemyId = mConfig.Id,
            Position = transform.position,
            ExpValue = mConfig.ExpValue
        });

        this.GetSystem<IGameObjectPoolSystem>().Recycle(mConfig.Id, gameObject);
    }
}
