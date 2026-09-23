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

    // mCaliber：该武器装填的子弹口径（S/AR/L），决定消耗哪种子弹库存与使用哪个子弹池。
    [SerializeField] private Caliber mCaliber;

    // mMagazine：弹夹容量，打空后进入换弹。
    [SerializeField] private int mMagazine;

    // mReloadTime：换弹时间（秒），换弹期间该武器无法射击，切后台仍继续计时。
    [SerializeField] private float mReloadTime;

    [Header("耐久")]

    // mDurabilityMax：耐久上限。每发子弹磨损 1 × 子弹等级磨损系数，归零报废腾格。
    [SerializeField] private float mDurabilityMax;

    [Header("伤害与射速")]

    // mDamage：武器基础伤害，命中伤害 = 基础伤害 × 子弹等级倍率（× 肉弹对无盾加成）。
    [SerializeField] private float mDamage;

    [SerializeField, Min(0f)] private float mRoundsPerMinute;
    [SerializeField, Min(0.01f)] private float mSemiAutoInterval = 0.25f;

    // mIsAutomatic：true 表示长按连发（机枪），false 表示点击单发（手枪）。
    [SerializeField] private bool mIsAutomatic;

    public string Id => mId;
    public string Name => mName;
    public Caliber Caliber => mCaliber;
    public int Magazine => mMagazine;
    public float ReloadTime => mReloadTime;
    public float DurabilityMax => mDurabilityMax;
    public float Damage => mDamage;
    public float RoundsPerMinute => mRoundsPerMinute;
    public float SemiAutoInterval => mSemiAutoInterval;
    public bool IsAutomatic => mIsAutomatic;
}
