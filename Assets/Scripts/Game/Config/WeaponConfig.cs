using UnityEngine;

/// <summary>
/// 单把武器的静态配置（ScriptableObject 资产）。
/// 创建方式：Project 窗口右键 → Create → Game → 武器配置，
/// 资产统一放在 Resources/Configs/Weapons/ 文件夹下，运行时由 WeaponConfigTable 加载。
/// 新增武器只需新建一个资产并填好字段，代码零改动。
/// </summary>
[CreateAssetMenu(fileName = "NewWeapon", menuName = "Game/武器配置", order = 0)]
public class WeaponConfig : ScriptableObject
{
    [Header("身份")]

    // mId：武器唯一标识，WeaponSystem 按它从配置表取武器。
    [SerializeField] private string mId;

    // mName：显示名，用于 HUD 武器格子上的文字。
    [SerializeField] private string mName;

    [Header("弹药")]

    // mMagazine：弹夹容量，打空后进入换弹。
    [SerializeField] private int mMagazine;

    // mReloadTime：换弹时间（秒），弹夹打空后经过该时长自动补满。
    [SerializeField] private float mReloadTime;

    [Header("伤害与射速")]

    // mDamage：武器伤害，子弹命中时与子弹配置的伤害相加后结算。
    [SerializeField] private float mDamage;

    // mRoundsPerMinute：射速（发/分钟），仅自动武器使用；0 表示非自动武器（如手枪），
    // 非自动武器的攻击间隔由 GunWeapon 中的固定值决定。
    [SerializeField] private float mRoundsPerMinute;

    // mIsAutomatic：true 表示长按连发（机枪），false 表示点击单发（手枪）。
    [SerializeField] private bool mIsAutomatic;

    [Header("子弹")]

    // mBulletId：该武器使用的子弹配置 id，与 BulletConfig 资产中的 Id 对应。
    [SerializeField] private string mBulletId;

    public string Id => mId;
    public string Name => mName;
    public int Magazine => mMagazine;
    public float ReloadTime => mReloadTime;
    public float Damage => mDamage;
    public float RoundsPerMinute => mRoundsPerMinute;
    public bool IsAutomatic => mIsAutomatic;
    public string BulletId => mBulletId;
}
