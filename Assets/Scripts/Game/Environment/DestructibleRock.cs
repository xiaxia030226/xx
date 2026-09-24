using UnityEngine;

/// <summary>场景内一次性岩石；伤害由弹丸/技能命中入口转交，不掉落、不接入对象池。</summary>
public class DestructibleRock : MonoBehaviour
{
    [SerializeField] private float mMaxHP = 30f; // 岩石初始生命值配置。

    private float mCurrentHP; // 当前剩余生命值，最低为零。
    private bool mShattered; // 是否已碎裂，防止重复受击和重建导航。
    private Collider[] mColliders; // 根及子层级全部碰撞体，含未激活节点。
    private StageEnvironment mStage; // 岩石所属场地，用于碎裂后重建导航。

    public bool IsIntact => !mShattered; // 为真表示尚未执行碎裂，不单凭当前生命值判断。

    // 作用：初始化生命并缓存碰撞体及所属场地；返回：无返回值。
    private void Awake()
    {
        // 生命不低于零，并缓存含未激活子节点的碰撞体，碎裂时可统一移除阻挡。
        mCurrentHP = Mathf.Max(0f, mMaxHP);
        mColliders = GetComponentsInChildren<Collider>(true);
        mStage = GetComponentInParent<StageEnvironment>();
    }

    // 作用：扣除有效正伤害并在生命耗尽时碎裂；返回：无返回值。
    public void TakeDamage(float damage)
    {
        // 已碎裂、非数值和非正伤害不再参与生命结算。
        if (mShattered || float.IsNaN(damage) || damage <= 0f) return;
        mCurrentHP = Mathf.Max(0f, mCurrentHP - damage);
        if (mCurrentHP <= 0f) Shatter();
    }

    // 作用：一次性移除岩石阻挡及外观并重建导航；返回：无返回值。
    public void Shatter()
    {
        if (mShattered) return;
        mShattered = true;
        mCurrentHP = 0f;

        // 包含根碰撞体和子碰撞体，先关闭阻挡再使导航缓存失效。
        if (mColliders == null) mColliders = GetComponentsInChildren<Collider>(true);
        foreach (var shape in mColliders)
        {
            if (shape != null) shape.enabled = false;
        }

        if (mStage == null) mStage = GetComponentInParent<StageEnvironment>();
        gameObject.SetActive(false);
        if (mStage != null && mStage.Navigation != null) mStage.Navigation.Rebuild();
    }
}
