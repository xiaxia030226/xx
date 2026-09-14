using UnityEngine;

/// <summary>
/// 枪械武器：手枪与机枪共用的实现，差异全部由 WeaponConfig 数据驱动。
/// 手枪：IsAutomatic=false，点击单发，攻击间隔为固定的 SemiAutoInterval；
/// 机枪：IsAutomatic=true，长按连发，攻击间隔由 RPM 换算（60 / RPM）。
/// 命中伤害 = 武器配置伤害 + 子弹配置伤害。
/// </summary>
public class GunWeapon : WeaponBase
{
    // SemiAutoInterval：非自动武器（手枪）的固定攻击间隔（秒），防止极端连点。
    private const float SemiAutoInterval = 0.15f;

    // MuzzleOffset：子弹出膛位置相对持有者的偏移，前方 0.8 米、高 0.5 米，避免贴脸自碰。
    private static readonly Vector3 MuzzleOffset = new Vector3(0f, 0.5f, 0.8f);

    // mBulletConfig：本武器使用的子弹配置（伤害、速度、对象池 key）。
    private readonly BulletConfig mBulletConfig;

    // mPool：对象池系统引用，发射时取子弹、构造时注册子弹池。
    private readonly IGameObjectPoolSystem mPool;

    // mBulletParent：子弹实例的挂载点（BulletRoot），保持 Hierarchy 整洁。
    private readonly Transform mBulletParent;

    /// <summary>
    /// 从武器配置与子弹配置组装一把枪。
    /// 基类参数映射：弹药型资源（弹夹容量）、每发耗 1 发子弹、耗尽后按 ReloadTime 换弹。
    /// </summary>
    public GunWeapon(WeaponConfig config, BulletConfig bulletConfig,
        IGameObjectPoolSystem pool, Transform bulletParent)
        : base(config.Id, config.Name, WeaponResourceType.Ammo,
            config.Magazine, 1f, 0f, config.ReloadTime,
            config.RoundsPerMinute > 0f ? 60f / config.RoundsPerMinute : SemiAutoInterval,
            config.Damage, config.IsAutomatic)
    {
        mBulletConfig = bulletConfig;
        mPool = pool;
        mBulletParent = bulletParent;
    }

    /// <summary>
    /// 发射一颗子弹：从对象池取出，摆到枪口位置，按持有者朝向初始化飞行参数。
    /// </summary>
    protected override void DoAttack(Transform owner)
    {
        // muzzle：枪口世界坐标——持有者位置 + 按朝向旋转后的偏移。
        var muzzle = owner.position + owner.rotation * MuzzleOffset;

        // 子弹对象由对象池负责创建与复用，池的注册在 WeaponSystem.Setup 中完成。
        var bulletObject = mPool.Spawn(mBulletConfig.Id, muzzle, owner.rotation, mBulletParent);
        var bullet = bulletObject.GetComponent<Bullet>();

        // 命中伤害 = 武器伤害 + 子弹伤害；方向取持有者当前朝向（玩家已面向鼠标）。
        bullet.Setup(mBulletConfig.Id, Mathf.RoundToInt(Damage) + mBulletConfig.Damage,
            mBulletConfig.Speed, owner.forward);
    }
}
