using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 铁剑：命中玩家正前方 90 度、半径 2.5 米内的敌人。
/// 挥剑特效只创建一个对象反复复用，避免每次攻击都新建/销毁物体并泄漏材质实例。
/// </summary>
public class SwordWeapon : WeaponBase
{
    private const float Radius = 2.5f;
    private const float HalfAngle = 45f;

    // PreviewDuration：挥剑特效的显示时长（秒），与攻速 0.4 秒匹配，远小于攻击间隔。
    private const float PreviewDuration = 0.1f;

    // mHitBuffer：球形检测的复用数组，避免每次挥剑分配新数组。
    private readonly Collider[] mHitBuffer = new Collider[32];

    // mHitEnemies：一次挥剑中已命中的敌人集合，防止同一敌人的多个 Collider 重复结算。
    private readonly HashSet<Enemy> mHitEnemies = new HashSet<Enemy>();

    // mPreview：挥剑特效对象缓存。只在首次攻击时创建一次，之后每次攻击仅做显隐切换，
    // 避免旧实现中"每次挥剑 CreatePrimitive + Destroy"的高频创建销毁开销。
    private GameObject mPreview;

    // mPreviewTimer：特效剩余显示时间（秒），由 Tick 每帧推进，归零时隐藏特效。
    private float mPreviewTimer;

    public SwordWeapon()
        : base("sword", "铁剑", WeaponResourceType.Energy, 100f, 30f, 30f, 0f, 0.4f, 20f)
    {
    }

    protected override void DoAttack(Transform owner)
    {
        mHitEnemies.Clear();

        // NonAlloc 版本把结果写入复用数组，避免每次挥剑都创建新的 Collider 数组。
        var count = Physics.OverlapSphereNonAlloc(owner.position, Radius, mHitBuffer);

        for (var i = 0; i < count; i++)
        {
            var enemy = mHitBuffer[i].GetComponentInParent<Enemy>();

            // HashSet 防止同一敌人的多个 Collider 在一次挥剑中被重复结算。
            if (enemy == null || !enemy.IsAlive || !mHitEnemies.Add(enemy)) continue;

            var direction = enemy.transform.position - owner.position;
            direction.y = 0f;
            if (direction.sqrMagnitude <= 0.001f) continue;

            // OverlapSphere 先筛半径，再用夹角筛出玩家正前方 90 度的扇形区域。
            if (Vector3.Angle(owner.forward, direction) > HalfAngle) continue;

            enemy.TakeHit(Mathf.RoundToInt(Damage));
        }

        ShowPreview(owner);
    }

    /// <summary>
    /// 在基类的攻击冷却与资源恢复之外，额外推进挥剑特效的隐藏倒计时。
    /// </summary>
    public override void Tick(float deltaTime)
    {
        // 第一步：执行基类逻辑——推进攻击冷却、能量恢复，不能省略。
        base.Tick(deltaTime);

        // 第二步：特效不存在（还没攻击过）或已隐藏时，无需推进倒计时。
        if (mPreview == null || !mPreview.activeSelf) return;

        // 第三步：倒计时归零后隐藏特效，等待下一次挥剑重新显示。
        mPreviewTimer -= deltaTime;
        if (mPreviewTimer <= 0f)
        {
            mPreview.SetActive(false);
        }
    }

    /// <summary>
    /// 显示挥剑特效：首次调用时创建特效对象并缓存，之后每次仅重置计时并显示。
    /// </summary>
    /// <param name="owner">武器持有者，特效挂在其身上跟随移动与转向。</param>
    private void ShowPreview(Transform owner)
    {
        // 首次攻击时创建特效对象；之后整个游戏过程复用同一个，不再创建销毁。
        if (mPreview == null)
        {
            mPreview = GameObject.CreatePrimitive(PrimitiveType.Cube);
            mPreview.name = "SwordAttackPreview";
            mPreview.transform.SetParent(owner, false);
            mPreview.transform.localPosition = new Vector3(0f, 0.5f, 1.5f);
            mPreview.transform.localScale = new Vector3(2.8f, 0.08f, 2f);
            Object.Destroy(mPreview.GetComponent<Collider>());

            // 材质颜色只在创建时设置一次。
            // renderer.material 首次访问会克隆一份新材质实例，若每次攻击都设置就会不断泄漏，
            // 因此只在这一次访问。
            var renderer = mPreview.GetComponent<Renderer>();
            renderer.material.color = new Color(1f, 0.85f, 0.2f, 0.45f);
        }

        // 重置显示计时并确保特效处于激活状态（上一次攻击后可能已被隐藏）。
        mPreviewTimer = PreviewDuration;
        mPreview.SetActive(true);
    }
}
