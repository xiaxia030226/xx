using UnityEngine;

/// <summary>
/// 单种子弹的静态配置（ScriptableObject 资产），按（口径 × 穿甲等级 0~5）建模，共 18 种。
/// 资产由 Editor 菜单一键生成到 Resources/Configs/Bullets/ 文件夹下，
/// 运行时由 BulletConfigTable 按类型加载。
/// 伤害不由子弹配置决定——命中伤害 = 武器基础伤害 × AmmoTypes 等级倍率。
/// </summary>
[CreateAssetMenu(fileName = "NewBullet", menuName = "Game/子弹配置", order = 1)]
public class BulletConfig : ScriptableObject
{
    [Header("身份")]

    // mId：子弹唯一标识（bullet_s_0 等），约定由 AmmoTypes.BulletId 生成。
    [SerializeField] private string mId;

    // mName：显示名，用于实例化后在 Hierarchy 中命名子弹物体。
    [SerializeField] private string mName;

    [Header("弹道")]

    // mCaliber：子弹口径，同口径 6 个等级共享同一预制体与对象池。
    [SerializeField] private Caliber mCaliber;

    // mPenetrationLevel：穿甲等级（0~5），决定伤害倍率与磨损系数（查 AmmoTypes 表）。
    [SerializeField] private int mPenetrationLevel;

    // mSpeed：飞行速度（米/秒）。
    [SerializeField] private float mSpeed;

    [Header("预制体")]

    // mPrefabPath：子弹预制体在 Resources 下的相对路径（不带扩展名）。
    // 保留字符串寻址是为后续迁移 Addressables 预留——届时把该值改为 AA 地址即可。
    [SerializeField] private string mPrefabPath;

    public string Id => mId;
    public string Name => mName;
    public Caliber Caliber => mCaliber;
    public int PenetrationLevel => mPenetrationLevel;
    public float Speed => mSpeed;
    public string PrefabPath => mPrefabPath;
}
