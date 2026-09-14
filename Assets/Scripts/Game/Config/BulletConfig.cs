using UnityEngine;

/// <summary>
/// 单种子弹的静态配置（ScriptableObject 资产）。
/// 创建方式：Project 窗口右键 → Create → Game → 子弹配置，
/// 资产与武器配置一起放在 Resources/Configs/Weapons/ 文件夹下，
/// 运行时由 BulletConfigTable 按类型加载（与武器配置互不干扰）。
/// </summary>
[CreateAssetMenu(fileName = "NewBullet", menuName = "Game/子弹配置", order = 1)]
public class BulletConfig : ScriptableObject
{
    [Header("身份")]

    // mId：子弹唯一标识，兼作对象池的 key，武器配置中的 BulletId 引用它。
    [SerializeField] private string mId;

    // mName：显示名，用于实例化后在 Hierarchy 中命名子弹物体。
    [SerializeField] private string mName;

    [Header("属性")]

    // mDamage：子弹伤害，命中时与武器配置的伤害相加后结算。
    [SerializeField] private int mDamage;

    // mSpeed：飞行速度（米/秒）。
    [SerializeField] private float mSpeed;

    [Header("预制体")]

    // mPrefabPath：子弹预制体在 Resources 下的相对路径（不带扩展名）。
    // 保留字符串寻址是为后续迁移 Addressables 预留——届时把该值改为 AA 地址即可。
    [SerializeField] private string mPrefabPath;

    public string Id => mId;
    public string Name => mName;
    public int Damage => mDamage;
    public float Speed => mSpeed;
    public string PrefabPath => mPrefabPath;
}
