using UnityEngine;

/// <summary>场景内一次性岩石；伤害由弹丸/技能命中入口转交，不掉落、不接入对象池。</summary>
public class DestructibleRock : MonoBehaviour
{
    [SerializeField] private float mMaxHP = 30f;

    private float mCurrentHP;
    private bool mShattered;
    private Collider[] mColliders;
    private StageEnvironment mStage;

    public bool IsIntact => !mShattered;

    private void Awake()
    {
        mCurrentHP = Mathf.Max(0f, mMaxHP);
        mColliders = GetComponentsInChildren<Collider>(true);
        mStage = GetComponentInParent<StageEnvironment>();
    }

    public void TakeDamage(float damage)
    {
        if (mShattered || float.IsNaN(damage) || damage <= 0f) return;
        mCurrentHP = Mathf.Max(0f, mCurrentHP - damage);
        if (mCurrentHP <= 0f) Shatter();
    }

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
