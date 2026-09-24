using UnityEngine;

/// <summary>配置保存 AI 类型，Enemy 根据类型执行对应行为。</summary>
public enum EnemyAIType
{
    ChaseMelee = 0, // 追击并进行近战接触攻击。
    SlimeCharge = 1, // 史莱姆蓄力冲刺。
    ThrowStone = 2, // 投石远程攻击。
    Archer = 3, // 弓手锁定方向后射击。
    Flank = 4, // 侧翼绕行并近战攻击。
    Poison = 5, // 生成毒区攻击。
    Slam = 6, // 范围重击。
    Teleport = 7, // 瞬移后远程攻击。
    Ram = 8, // 撞角兽冲撞。
    ShieldDrummer = 9, // 护盾鼓手支援及接触攻击。
    Split = 10, // 死亡后分裂子体。
    SlimeKing = 11 // 史莱姆王的首领技能行为。
}

public enum EnemyCategory
{
    Normal = 0, // 普通敌人类别。
    Mechanism = 1, // 机制型敌人类别。
    Elite = 2, // 精英类别，掉落物品免概率判定。
    Boss = 3 // 首领类别，掉落物品免概率判定。
}

/// <summary>敌人静态配置资产，由 EnemyConfigTable 从 Resources/Configs/Enemies 加载。</summary>
[CreateAssetMenu(fileName = "NewEnemy", menuName = "Game/敌人配置", order = 0)]
public class EnemyConfig : ScriptableObject
{
    [Header("身份")]

    [SerializeField] private string mId; // 敌人唯一标识，与对象池键及生成组 EnemyId 对应。

    [SerializeField] private string mName; // 敌人显示名。

    [Header("属性")]

    [SerializeField] private int mMaxHP; // 敌人基础最大生命值。

    [SerializeField] private float mMoveSpeed; // 常规移动速度，单位为米/秒。

    [Header("攻击")]

    [SerializeField] private int mContactDamage; // 敌人攻击的基础伤害，供接触及对应技能使用。

    [SerializeField] private float mAttackInterval; // 常规攻击间隔秒数。

    [SerializeField] private float mAttackRange; // 基础攻击距离，具体 AI 可结合技能距离决定停靠位置。

    [SerializeField] private EnemyAIType mAIType; // 敌人运行时采用的 AI 行为类型。

    [Header("行为")]

    [SerializeField] private EnemyCategory mCategory; // 敌人类别，精英及首领享有物品必掉规则。
    [SerializeField, Range(0, 5)] private int mAttackLevel; // 敌人攻击的穿甲等级。
    [SerializeField, Min(0f)] private float mWindup; // 技能前摇秒数，未配置正值时由 AI 使用默认值。
    [SerializeField, Min(0f)] private float mRecovery; // 技能恢复阶段秒数，具体取值由 AI 读取。
    [SerializeField, Min(0f)] private float mProjectileSpeed = 12f; // 远程投射物飞行速度。
    [SerializeField, Min(0f)] private float mChargeDistance; // 冲刺距离配置，未配置正值时由 AI 兜底。
    [SerializeField, Min(0f)] private float mChargeSpeed; // 冲刺移动速度配置。
    [SerializeField, Min(0f)] private float mAreaRadius; // 毒区、重击等范围技能的半径配置。
    [SerializeField, Min(0f)] private float mTeleportDistance; // 瞬移位移距离配置。

    [Header("掉落")]

    [SerializeField] private int mGoldMin; // 金币随机区间下限，随机结果再乘召唤倍率。
    [SerializeField] private int mGoldMax; // 金币随机区间上限，包含该端点。

    [SerializeField, Range(0f, 1f)] private float mAmmoChance; // 普通弹药掉落基础概率，乘倍率后钳制到 0～1。

    [SerializeField] private int mAmmoLevelMin; // 弹药等级低端候选，边界修正后通常以 70% 概率选取。
    [SerializeField] private int mAmmoLevelMax; // 弹药等级高端候选，不从两端之间抽取中间等级。

    [SerializeField] private int mAmmoCountMin; // 弹药包数量下限，含端点且不乘召唤倍率。
    [SerializeField] private int mAmmoCountMax; // 弹药包数量上限，包含该端点。

    [SerializeField, Range(0f, 1f)] private float mShieldChance; // 普通护盾掉落基础概率。
    [SerializeField, Range(0f, 1f)] private float mWeaponChance; // 普通武器掉落基础概率。
    [SerializeField, Range(1, 5)] private int mShieldLevelMin; // 护盾掉落等级随机区间下限。
    [SerializeField, Range(1, 5)] private int mShieldLevelMax; // 护盾掉落等级随机区间上限，包含端点。

    [Header("预制体")]

    [SerializeField] private string mPrefabPath; // 敌人预制体在 Resources 下不含扩展名的相对路径。

    public string Id => mId; // 敌人配置标识。
    public string Name => mName; // 敌人显示名。
    public int MaxHP => mMaxHP; // 基础最大生命。
    public float MoveSpeed => mMoveSpeed; // 常规移动速度。
    public int ContactDamage => mContactDamage; // 攻击基础伤害。
    public float AttackInterval => mAttackInterval; // 常规攻击间隔秒数。
    public float AttackRange => mAttackRange; // 基础攻击距离。
    public EnemyAIType AIType => mAIType; // AI 行为类型。
    public EnemyCategory Category => mCategory; // 敌人类别。
    public int AttackLevel => mAttackLevel; // 攻击穿甲等级。
    public float Windup => mWindup; // 技能前摇配置秒数。
    public float Recovery => mRecovery; // 技能恢复配置秒数。
    public float ProjectileSpeed => mProjectileSpeed; // 投射物速度配置。
    public float ChargeDistance => mChargeDistance; // 冲刺距离配置。
    public float ChargeSpeed => mChargeSpeed; // 冲刺速度配置。
    public float AreaRadius => mAreaRadius; // 范围技能半径配置。
    public float TeleportDistance => mTeleportDistance; // 瞬移距离配置。
    public float ShieldChance => mShieldChance; // 护盾掉落基础概率。
    public float WeaponChance => mWeaponChance; // 武器掉落基础概率。
    public int ShieldLevelMin => mShieldLevelMin; // 护盾掉落等级下限。
    public int ShieldLevelMax => mShieldLevelMax; // 护盾掉落等级上限。
    public int GoldMin => mGoldMin; // 金币随机下限。
    public int GoldMax => mGoldMax; // 金币随机上限。
    public float AmmoChance => mAmmoChance; // 弹药掉落基础概率。
    public int AmmoLevelMin => mAmmoLevelMin; // 弹药等级低端候选。
    public int AmmoLevelMax => mAmmoLevelMax; // 弹药等级高端候选。
    public int AmmoCountMin => mAmmoCountMin; // 弹药数量下限。
    public int AmmoCountMax => mAmmoCountMax; // 弹药数量上限。
    public string PrefabPath => mPrefabPath; // 敌人预制体资源路径。
}
