using QFramework;
using UnityEngine;

public class PoisonArea : MonoBehaviour, IController
{
    public const string PoolKey = "enemy_poison_area"; // 敌人毒区的对象池键。
    public const string PrefabPath = "Prefabs/PoisonArea"; // 毒区预制体的 Resources 路径。
    public const float SlowMultiplier = 0.8f; // 对外提供的减速倍率；玩家移动当前直接使用 0.8f。

    [SerializeField] private Renderer mRenderer; // 用于设置毒区颜色的渲染器。
    private Transform mTarget; // 以 XZ 距离判定是否在毒区内的目标。
    private Player mPlayer; // 接收本毒区减速来源登记的玩家组件。
    private float mRadius; // 本次生成的有效毒区半径。
    private float mRemaining; // 毒区剩余生存秒数，暂停时不扣减。
    private bool mActive; // 本轮毒区是否仍生效，防止重复回收。
    private Vector3 mBaseScale; // 首次唤醒时的缩放，供生成和回收恢复使用。
    private MaterialPropertyBlock mProperties; // 独立调整渲染颜色而不复制材质的属性块。

    // 作用：接入游戏架构；返回：游戏架构实例。
    public IArchitecture GetArchitecture() => GameArchitecture.Interface; // 直接取游戏架构入口，供框架扩展方法访问模型和系统。

    // 作用：缓存初始缩放、渲染器与材质属性块；返回：无返回值。
    private void Awake()
    {
        // 保存缩放基准，仅在未绑定时查找渲染器，并准备后续着色可复用的属性块。
        mBaseScale = transform.localScale;
        if (mRenderer == null) mRenderer = GetComponentInChildren<Renderer>();
        mProperties = new MaterialPropertyBlock();
    }

    // 作用：重置池中毒区的目标、范围、寿命和外观；返回：无返回值。
    public void OnSpawn(Transform player, float radius, float lifetime)
    {
        // 先注销上次关联的玩家来源，防止复用后留下旧减速。
        ClearSource();
        mTarget = player;
        mPlayer = player != null ? player.GetComponentInParent<Player>() : null;
        mRadius = Mathf.Max(0f, radius);
        mRemaining = Mathf.Max(0f, lifetime);
        mActive = true;
        // prefab 使用直径 1 的扁圆；只改变 XZ，不拉高视觉。
        transform.localScale = new Vector3(mBaseScale.x * mRadius * 2f, mBaseScale.y,
            mBaseScale.z * mRadius * 2f);
        if (mRenderer != null)
        {
            mRenderer.GetPropertyBlock(mProperties);
            var color = new Color(0.3f, 0.75f, 0.12f, 0.45f);
            mProperties.SetColor("_Color", color);
            mProperties.SetColor("_BaseColor", color);
            mRenderer.SetPropertyBlock(mProperties);
        }
        UpdateSource();
    }

    // 作用：按游戏状态推进毒区寿命并同步减速覆盖；返回：无返回值。
    private void Update()
    {
        // 暂停保留毒区；非战斗状态或目标丢失则注销来源并回池。
        if (!mActive) return;
        var state = this.GetModel<IGameStateModel>().State.Value;
        if (state == GameState.Paused) return;
        if (state != GameState.Playing || mTarget == null)
        {
            Recycle();
            return;
        }

        mRemaining -= Time.deltaTime;
        if (mRemaining <= 0f)
        {
            Recycle();
            return;
        }
        UpdateSource();
    }

    // 作用：依据 XZ 范围登记或移除本毒区的减速来源；返回：无返回值。
    private void UpdateSource()
    {
        // 引用齐备时按水平距离判定覆盖，只有毒区生效且目标在半径内才保留此减速来源。
        if (mPlayer == null || mTarget == null) return;
        var offset = mTarget.position - transform.position;
        offset.y = 0f;
        mPlayer.SetPoisonSource(this, mActive && offset.sqrMagnitude <= mRadius * mRadius);
    }

    // 作用：停止本轮毒区效果并安全回池；返回：无返回值。
    public void Recycle()
    {
        // 先失效再注销，后续禁用回调不会再次回收。
        if (!mActive) return;
        mActive = false;
        ClearSource();
        this.GetSystem<IGameObjectPoolSystem>().Recycle(PoolKey, gameObject);
    }

    // 作用：从原玩家移除本来源并断开目标引用；返回：无返回值。
    private void ClearSource()
    {
        // 先通过旧玩家引用注销本毒区，再清空引用，避免失去解除减速的对象。
        if (mPlayer != null) mPlayer.SetPoisonSource(this, false);
        mPlayer = null;
        mTarget = null;
    }

    // 作用：禁用时清除毒区状态并恢复初始缩放；返回：无返回值。
    private void OnDisable()
    {
        // 同时覆盖正常回池与外部直接禁用，防止残留减速及复用缩放累积。
        mActive = false;
        mRemaining = 0f;
        mRadius = 0f;
        ClearSource();
        transform.localScale = mBaseScale;
    }
}
