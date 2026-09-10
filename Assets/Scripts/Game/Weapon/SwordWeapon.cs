using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 铁剑：命中玩家正前方 90 度、半径 2.5 米内的敌人。
/// </summary>
public class SwordWeapon : WeaponBase
{
    private const float Radius = 2.5f;
    private const float HalfAngle = 45f;

    private readonly Collider[] mHitBuffer = new Collider[32];
    private readonly HashSet<Enemy> mHitEnemies = new HashSet<Enemy>();

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

        CreateAttackPreview(owner);
    }

    private static void CreateAttackPreview(Transform owner)
    {
        var preview = GameObject.CreatePrimitive(PrimitiveType.Cube);
        preview.name = "SwordAttackPreview";
        preview.transform.SetParent(owner, false);
        preview.transform.localPosition = new Vector3(0f, 0.5f, 1.5f);
        preview.transform.localScale = new Vector3(2.8f, 0.08f, 2f);
        Object.Destroy(preview.GetComponent<Collider>());

        var renderer = preview.GetComponent<Renderer>();
        renderer.material.color = new Color(1f, 0.85f, 0.2f, 0.45f);
        Object.Destroy(preview, 0.1f);
    }
}
