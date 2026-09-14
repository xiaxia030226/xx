using UnityEngine;

/// <summary>武器消耗资源的方式：弹药耗尽后装填，能量则随时间连续恢复。</summary>
public enum WeaponResourceType
{
    Ammo,
    Energy
}

/// <summary>武器当前是否可用，或正在进行弹药装填。</summary>
public enum WeaponState
{
    Ready,
    Refilling
}

/// <summary>
/// 武器的纯 C# 基类，统一处理攻击冷却和资源恢复。
/// </summary>
public abstract class WeaponBase
{
    private float mAttackCooldown;
    private float mRefillTimer;

    public string Id { get; }
    public string Name { get; }
    public int Level { get; protected set; } = 1;
    public WeaponResourceType ResourceType { get; }
    public float ResourceMax { get; private set; }
    public float Resource { get; private set; }
    public float CostPerAttack { get; }
    public float RegenPerSec { get; }
    public float RefillCooldown { get; }
    public float AttackInterval { get; }
    public float Damage { get; private set; }
    public WeaponState State { get; private set; } = WeaponState.Ready;

    protected WeaponBase(string id, string name, WeaponResourceType resourceType, float resourceMax,
        float costPerAttack, float regenPerSec, float refillCooldown, float attackInterval, float damage)
    {
        Id = id;
        Name = name;
        ResourceType = resourceType;
        ResourceMax = resourceMax;
        Resource = resourceMax;
        CostPerAttack = costPerAttack;
        RegenPerSec = regenPerSec;
        RefillCooldown = refillCooldown;
        AttackInterval = attackInterval;
        Damage = damage;
    }

    /// <summary>升级：伤害提升。供升级三选一调用。</summary>
    public void UpgradeDamage(float amount)
    {
        Damage += amount;
        Level++;
    }

    /// <summary>升级：资源上限提升，并补充等量当前资源。</summary>
    public void UpgradeResource(float amount)
    {
        ResourceMax += amount;
        Resource = Mathf.Min(ResourceMax, Resource + amount);
        Level++;
    }

    // 一次攻击必须同时满足：武器就绪、攻击间隔结束、剩余资源足够。
    public bool CanAttack => State == WeaponState.Ready && mAttackCooldown <= 0f && Resource >= CostPerAttack;

    /// <summary>
    /// 通用攻击入口：先校验状态并扣除资源，再把具体命中逻辑交给子类 DoAttack。
    /// </summary>
    public bool TryAttack(Transform owner)
    {
        if (!CanAttack || owner == null) return false;

        Resource = Mathf.Max(0f, Resource - CostPerAttack);
        mAttackCooldown = AttackInterval;
        DoAttack(owner);

        if (ResourceType == WeaponResourceType.Ammo && Resource <= 0f)
        {
            State = WeaponState.Refilling;
            mRefillTimer = RefillCooldown;
        }

        return true;
    }

    /// <summary>
    /// 每帧推进攻击冷却，并根据资源类型执行能量恢复或弹药装填。
    /// virtual：子类（如 SwordWeapon）可 override 追加自身表现逻辑（如挥剑特效的隐藏倒计时），
    /// 重写时必须调用 base.Tick 保证冷却与资源恢复不丢失。
    /// </summary>
    public virtual void Tick(float deltaTime)
    {
        mAttackCooldown = Mathf.Max(0f, mAttackCooldown - deltaTime);

        if (ResourceType == WeaponResourceType.Energy)
        {
            Resource = Mathf.Min(ResourceMax, Resource + RegenPerSec * deltaTime);
            return;
        }

        if (State != WeaponState.Refilling) return;
        mRefillTimer -= deltaTime;
        if (mRefillTimer > 0f) return;

        Resource = ResourceMax;
        State = WeaponState.Ready;
    }

    // 子类只实现武器特有的命中方式，不重复处理冷却与资源消耗。
    protected abstract void DoAttack(Transform owner);
}
