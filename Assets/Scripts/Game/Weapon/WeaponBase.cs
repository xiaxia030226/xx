using UnityEngine;

/// <summary>武器当前是否可用，或正在换弹装填。</summary>
public enum WeaponState
{
    Ready,
    Reloading
}

/// <summary>
/// 武器的纯 C# 基类，统一处理攻击冷却与耐久。
/// 弹夹与换弹状态机由子类 GunWeapon 实现（各枪独立记录已装等级与下次装填等级）。
/// 新设计无武器升级——伤害成长来自子弹穿甲等级，不来自武器本身。
/// </summary>
public abstract class WeaponBase
{
    // DurabilityWarningRatio：耐久低于该比例时进入警示状态（HUD 变色提示）。
    public const float DurabilityWarningRatio = 0.2f;

    // mAttackCooldown：距下次可射击的剩余秒数。
    private float mAttackCooldown;

    public string Id { get; }
    public string Name { get; }

    // ResourceMax / Resource：弹夹容量与当前弹量（每发消耗 1）。
    public float ResourceMax { get; }
    public float Resource { get; protected set; }

    public float AttackInterval { get; }

    // Damage：武器基础伤害，命中伤害 = 基础 × 子弹等级倍率（× 肉弹对无盾加成）。
    public float Damage { get; }

    // DurabilityMax / Durability：耐久上限与当前值。每发磨损 1 × 子弹等级磨损系数，归零报废。
    public float DurabilityMax { get; }
    public float Durability { get; private set; }

    public WeaponState State { get; protected set; } = WeaponState.Ready;

    // IsAutomatic：true 表示长按连发（机枪），false 表示点击单发（手枪）。
    // Player 读取它决定用 IsPressed 还是 WasPressedThisFrame 驱动攻击。
    public bool IsAutomatic { get; }

    // SlotIndex：武器所在槽位下标，由 WeaponSystem 装配时写入，用于事件载荷。
    public int SlotIndex { get; set; } = -1;

    // IsBroken：耐久归零即报废；WeaponSystem 检测后置空槽位，不自动切枪。
    public bool IsBroken => Durability <= 0f;

    // DurabilityWarning：耐久剩余 ≤20% 且未报废时进入警示状态。
    public bool DurabilityWarning => !IsBroken && Durability <= DurabilityMax * DurabilityWarningRatio;

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

    // 一次攻击必须同时满足：未报废、武器就绪、攻击间隔结束、弹夹有弹。
    public bool CanAttack => !IsBroken && State == WeaponState.Ready && mAttackCooldown <= 0f && Resource >= 1f;

    /// <summary>
    /// 通用攻击入口：先校验状态并扣弹，再把具体命中逻辑交给子类 DoAttack。
    /// 打空后的自动换弹由子类在 DoAttack 末尾触发。
    /// </summary>
    public bool TryAttack(Transform owner)
    {
        if (!CanAttack || owner == null) return false;

        Resource = Mathf.Max(0f, Resource - 1f);
        mAttackCooldown = AttackInterval;
        DoAttack(owner);
        return true;
    }

    /// <summary>
    /// 每帧推进攻击冷却。virtual：子类 override 追加换弹计时，重写时必须调用 base.Tick。
    /// </summary>
    public virtual void Tick(float deltaTime)
    {
        mAttackCooldown = Mathf.Max(0f, mAttackCooldown - deltaTime);
    }

    /// <summary>
    /// 磨损耐久：每发子弹由子类按等级磨损系数调用一次。
    /// </summary>
    public void Wear(float amount)
    {
        Durability = Mathf.Max(0f, Durability - amount);
    }

    // 子类只实现武器特有的命中方式，不重复处理冷却与弹量消耗。
    protected abstract void DoAttack(Transform owner);
}
