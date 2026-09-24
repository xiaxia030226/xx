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

    [SerializeField] private string mId; // 子弹唯一标识，按 AmmoTypes.BulletId 的口径和等级规则命名。

    [SerializeField] private string mName; // 子弹配置显示名。

    [Header("弹道")]

    [SerializeField] private Caliber mCaliber; // 子弹口径，同口径各等级共用预制体和对象池。

    [SerializeField] private int mPenetrationLevel; // 穿甲等级，约定为 0～5。

    [SerializeField] private float mSpeed; // 子弹飞行速度，单位为米/秒。

    [Header("预制体")]

    [SerializeField] private string mPrefabPath; // 预制体在 Resources 下不含扩展名的相对路径。

    public string Id => mId; // 子弹配置唯一标识。
    public string Name => mName; // 子弹显示名。
    public Caliber Caliber => mCaliber; // 子弹所属口径。
    public int PenetrationLevel => mPenetrationLevel; // 配置的穿甲等级。
    public float Speed => mSpeed; // 子弹飞行速度。
    public string PrefabPath => mPrefabPath; // 子弹预制体资源路径。

}
