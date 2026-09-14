using UnityEngine;

/// <summary>
/// 敌人 AI 行为类型。配置只保存类型，Enemy 再根据类型执行对应行为。
/// </summary>
public enum EnemyAIType
{
    // 直线追向玩家，进入攻击范围后停下并进行近战接触伤害。
    ChaseMelee
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

    // mAIType：AI 行为类型，目前只有追击近战一种。
    [SerializeField] private EnemyAIType mAIType;

    [Header("掉落与预制体")]

    // mExpValue：死亡时掉落经验水晶携带的经验值。
    [SerializeField] private int mExpValue;

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
    public int ExpValue => mExpValue;
    public string PrefabPath => mPrefabPath;
}
