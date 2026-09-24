using UnityEngine;

/// <summary>武器当前是否可用，或正在换弹装填。</summary>
public enum WeaponState
{
    Ready, // 就绪状态，仍需检查耐久、冷却和弹量才能攻击。
    Reloading // 装填计时中，暂时禁止攻击。
}

/// <summary>
/// 武器的纯 C# 基类，统一处理攻击冷却与耐久。
/// 弹夹与换弹状态机由子类 GunWeapon 实现（各枪独立记录已装等级与下次装填等级）。
/// 新设计无武器升级——伤害成长来自子弹穿甲等级，不来自武器本身。
/// </summary>
public abstract class WeaponBase
{
    public const float DurabilityWarningRatio = 0.2f; // 未报废武器触发低耐久警示的剩余比例上限。

    private float mAttackCooldown; // 距离允许下次攻击的剩余冷却秒数。

    public string Id { get; } // 武器配置标识。
    public string Name { get; } // 武器显示名称。

    public float ResourceMax { get; } // 弹夹容量上限。
    public float Resource { get; protected set; } // 当前已装弹量，每次有效攻击由基类扣除一发。

    public float AttackInterval { get; } // 相邻两次攻击之间的最短秒数。

    public float Damage { get; } // 武器基础伤害，子类发射时再乘子弹等级倍率。

    public float DurabilityMax { get; } // 初始耐久及耐久上限。
    public float Durability { get; private set; } // 当前耐久，按射击磨损降低，归零报废。

    public WeaponState State { get; protected set; } = WeaponState.Ready; // 就绪或装填状态，不代替其他攻击门控。

    public bool IsAutomatic { get; } // true 由玩家长按连发，false 由单次按下触发射击。

    public int SlotIndex { get; set; } = -1; // 武器栏槽位下标，由 WeaponSystem 写入，-1 表示尚未装配。

    public bool IsBroken => Durability <= 0f; // 耐久归零时为 true，系统腾空此槽但不自动切枪。

    public bool DurabilityWarning => !IsBroken && Durability <= DurabilityMax * DurabilityWarningRatio; // 未报废且耐久不超过警示比例时为 true。

    // 作用：保存武器配置并以满耐久、空弹夹创建基础状态；返回：无返回值（构造函数）。
    protected WeaponBase(string id, string name, int magazine, float attackInterval, float damage,
        float durabilityMax, bool isAutomatic)
    {
        Id = id;
        Name = name;
        ResourceMax = magazine;

        // 初始弹夹为空：首次装填从子弹库存预扣（初始库存已包含首装份额）。
        Resource = 0f;
        AttackInterval = attackInterval;
        Damage = damage;
        DurabilityMax = durabilityMax;
        Durability = durabilityMax;
        IsAutomatic = isAutomatic;
    }

    public bool CanAttack => !IsBroken && State == WeaponState.Ready && mAttackCooldown <= 0f && Resource >= 1f; // 未报废、就绪、冷却结束且至少有一发时为 true。

    // 作用：校验攻击条件，统一扣弹和设置冷却后委托子类攻击；返回：true 表示已执行攻击，false 表示条件不满足或持有者为空。
    public bool TryAttack(Transform owner)
    {
        if (!CanAttack || owner == null) return false;

        // 先扣总弹量再调用子类，子类只能更新来源份额，不能重复扣 Resource。
        Resource = Mathf.Max(0f, Resource - 1f);
        mAttackCooldown = AttackInterval;
        DoAttack(owner);
        return true;
    }

    // 作用：推进攻击冷却，供子类在调用 base.Tick 后追加装填计时；返回：无返回值。
    public virtual void Tick(float deltaTime)
    {
        // 冷却钳制到零，避免空闲时间累积成额外攻击机会。
        mAttackCooldown = Mathf.Max(0f, mAttackCooldown - deltaTime);
    }

    // 作用：按传入磨损量扣减耐久并将下限限制为零；返回：无返回值。
    public void Wear(float amount)
    {
        // 扣减后钳制到零，让报废判断使用稳定下限而不是负耐久。
        Durability = Mathf.Max(0f, Durability - amount);
    }

    // 作用：由子类执行具体攻击及后续装填逻辑，不重复扣总弹量和设置冷却；返回：无返回值。
    protected abstract void DoAttack(Transform owner);
}
