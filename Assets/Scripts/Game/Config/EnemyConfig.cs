using UnityEngine;

/// <summary>
/// 敌人 AI 行为类型。配置只保存类型，Enemy 再根据类型执行对应行为。
/// </summary>
public enum EnemyAIType
{
    // 直线追向玩家，进入攻击范围后停下并进行近战接触伤害。
    ChaseMelee = 0,
    SlimeCharge = 1,
    ThrowStone = 2,
    Archer = 3,
    Flank = 4,
    Poison = 5,
    Slam = 6,
    Teleport = 7,
    Ram = 8,
    ShieldDrummer = 9,
    Split = 10,
    SlimeKing = 11
}

public enum EnemyCategory
{
    Normal = 0,
    Mechanism = 1,
    Elite = 2,
    Boss = 3
}

/// <summary>
/// 单种敌人的静态配置（ScriptableObject 资产）。
/// 创建方式：Project 窗口右键 → Create → Game → 敌人配置，
/// 资产统一放在 Resources/Configs/Enemies/ 文件夹下，运行时由 EnemyConfigTable 加载。
/// 新增敌人只需新建一个资产并填好字段，代码零改动。
/// </summary>
[CreateAssetMenu(fileName = "NewEnemy", menuName = "Game/敌人配置", order = 0)]
public class EnemyConfig : ScriptableObject
{
    [Header("身份")]

    // mId：敌人唯一标识。与对象池的 key、波次表中的 EnemyId 保持一致，重复会在加载时报错。
    [SerializeField] private string mId;

    // mName：显示名，用于实例化后在 Hierarchy 中命名敌人物体。
    [SerializeField] private string mName;

    [Header("属性")]

    // mMaxHP：最大生命值，敌人每次从对象池取出时重置为该值。
    [SerializeField] private int mMaxHP;

    // mMoveSpeed：追击玩家的移动速度（米/秒）。
    [SerializeField] private float mMoveSpeed;

    [Header("攻击")]

    // mContactDamage：接触玩家时每次造成的伤害。
    [SerializeField] private int mContactDamage;

    // mAttackInterval：两次接触伤害之间的间隔（秒）。
    [SerializeField] private float mAttackInterval;

    // mAttackRange：进入该距离后停止移动并开始接触攻击。
    [SerializeField] private float mAttackRange;

    [SerializeField] private EnemyAIType mAIType;

    [Header("行为")]

    [SerializeField] private EnemyCategory mCategory;
    [SerializeField, Range(0, 5)] private int mAttackLevel;
    [SerializeField, Min(0f)] private float mWindup;
    [SerializeField, Min(0f)] private float mRecovery;
    [SerializeField, Min(0f)] private float mProjectileSpeed = 12f;
    [SerializeField, Min(0f)] private float mChargeDistance;
    [SerializeField, Min(0f)] private float mChargeSpeed;
    [SerializeField, Min(0f)] private float mAreaRadius;
    [SerializeField, Min(0f)] private float mTeleportDistance;

    [Header("掉落")]

    // mGoldMin / mGoldMax：死亡掉落金币的数量区间（含端点），掷点后乘以召唤倍率取整。
    [SerializeField] private int mGoldMin;
    [SerializeField] private int mGoldMax;

    // mAmmoChance：掉落子弹包的概率（0~1），掷点时乘以召唤倍率但封顶 1。
    [SerializeField, Range(0f, 1f)] private float mAmmoChance;

    // mAmmoLevelMin / mAmmoLevelMax：子弹包穿甲等级区间（含端点，0~5）。
    // 召唤倍率只提高掉落概率与数量，不提高等级上限。
    [SerializeField] private int mAmmoLevelMin;
    [SerializeField] private int mAmmoLevelMax;

    // mAmmoCountMin / mAmmoCountMax：子弹包含弹量区间（含端点）。
    [SerializeField] private int mAmmoCountMin;
    [SerializeField] private int mAmmoCountMax;

    [SerializeField, Range(0f, 1f)] private float mShieldChance;
    [SerializeField, Range(0f, 1f)] private float mWeaponChance;
    [SerializeField, Range(1, 5)] private int mShieldLevelMin;
    [SerializeField, Range(1, 5)] private int mShieldLevelMax;

    [Header("预制体")]

    // mPrefabPath：敌人预制体在 Resources 下的相对路径（不带扩展名）。
    // 保留字符串寻址是为后续迁移 Addressables 预留——届时把该值改为 AA 地址，
    // 加载层从 Resources.Load 换成 Addressables.LoadAssetAsync 即可，数据层不用动。
    [SerializeField] private string mPrefabPath;

    public string Id => mId;
    public string Name => mName;
    public int MaxHP => mMaxHP;
    public float MoveSpeed => mMoveSpeed;
    public int ContactDamage => mContactDamage;
    public float AttackInterval => mAttackInterval;
    public float AttackRange => mAttackRange;
    public EnemyAIType AIType => mAIType;
    public EnemyCategory Category => mCategory;
    public int AttackLevel => mAttackLevel;
    public float Windup => mWindup;
    public float Recovery => mRecovery;
    public float ProjectileSpeed => mProjectileSpeed;
    public float ChargeDistance => mChargeDistance;
    public float ChargeSpeed => mChargeSpeed;
    public float AreaRadius => mAreaRadius;
    public float TeleportDistance => mTeleportDistance;
    public float ShieldChance => mShieldChance;
    public float WeaponChance => mWeaponChance;
    public int ShieldLevelMin => mShieldLevelMin;
    public int ShieldLevelMax => mShieldLevelMax;
    public int GoldMin => mGoldMin;
    public int GoldMax => mGoldMax;
    public float AmmoChance => mAmmoChance;
    public int AmmoLevelMin => mAmmoLevelMin;
    public int AmmoLevelMax => mAmmoLevelMax;
    public int AmmoCountMin => mAmmoCountMin;
    public int AmmoCountMax => mAmmoCountMax;
    public string PrefabPath => mPrefabPath;
}
