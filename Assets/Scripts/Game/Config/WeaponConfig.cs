using UnityEngine;

/// <summary>武器静态配置资产，由 WeaponConfigTable 从 Resources/Configs/Weapons 加载。</summary>
[CreateAssetMenu(fileName = "NewWeapon", menuName = "Game/武器配置", order = 0)]
public class WeaponConfig : ScriptableObject
{
    [Header("身份")]

    [SerializeField] private string mId; // 武器唯一标识，用于配置查询及拾取。

    [SerializeField] private string mName; // 武器显示名，供 HUD 等界面使用。

    [Header("弹药")]

    [SerializeField] private Caliber mCaliber; // 装填口径，决定库存分类和子弹对象池。

    [SerializeField] private int mMagazine; // 弹夹最大弹数。

    [SerializeField] private float mReloadTime; // 换弹耗时秒数，换弹期间不能射击。

    [Header("耐久")]

    [SerializeField] private float mDurabilityMax; // 初始耐久上限，每发按弹药等级磨损，耗尽后报废。

    [Header("伤害与射速")]

    [SerializeField] private float mDamage; // 武器基础伤害，发射时乘弹药等级倍率。

    [SerializeField, Min(0f)] private float mRoundsPerMinute; // 自动武器每分钟射速，发射间隔取 60 除以此值。
    [SerializeField, Min(0.01f)] private float mSemiAutoInterval = 0.25f; // 非自动武器的最短射击间隔秒数。

    [SerializeField] private bool mIsAutomatic; // true 使用长按连发与每分钟射速；false 使用单发间隔。

    public string Id => mId; // 武器唯一标识。
    public string Name => mName; // 武器显示名。
    public Caliber Caliber => mCaliber; // 武器装填口径。
    public int Magazine => mMagazine; // 弹夹容量。
    public float ReloadTime => mReloadTime; // 换弹时间秒数。
    public float DurabilityMax => mDurabilityMax; // 最大耐久。
    public float Damage => mDamage; // 基础伤害。
    public float RoundsPerMinute => mRoundsPerMinute; // 自动武器每分钟发数。
    public float SemiAutoInterval => mSemiAutoInterval; // 非自动武器射击间隔秒数。
    public bool IsAutomatic => mIsAutomatic; // 是否使用自动射击模式。
}
