using QFramework;
using UnityEngine;

public class PoisonArea : MonoBehaviour, IController
{
    public const string PoolKey = "enemy_poison_area";
    public const string PrefabPath = "Prefabs/PoisonArea";
    public const float SlowMultiplier = 0.8f;

    [SerializeField] private Renderer mRenderer;
    private Transform mTarget;
    private Player mPlayer;
    private float mRadius;
    private float mRemaining;
    private bool mActive;
    private Vector3 mBaseScale;
    private MaterialPropertyBlock mProperties;

    public IArchitecture GetArchitecture() => GameArchitecture.Interface;

    private void Awake()
    {
        mBaseScale = transform.localScale;
        if (mRenderer == null) mRenderer = GetComponentInChildren<Renderer>();
        mProperties = new MaterialPropertyBlock();
    }

    public void OnSpawn(Transform player, float radius, float lifetime)
    {
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

    private void Update()
    {
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

    private void UpdateSource()
    {
        if (mPlayer == null || mTarget == null) return;
        var offset = mTarget.position - transform.position;
        offset.y = 0f;
        mPlayer.SetPoisonSource(this, mActive && offset.sqrMagnitude <= mRadius * mRadius);
    }

    public void Recycle()
    {
        if (!mActive) return;
        mActive = false;
        ClearSource();
        this.GetSystem<IGameObjectPoolSystem>().Recycle(PoolKey, gameObject);
    }

    private void ClearSource()
    {
        if (mPlayer != null) mPlayer.SetPoisonSource(this, false);
        mPlayer = null;
        mTarget = null;
    }

    private void OnDisable()
    {
        mActive = false;
        mRemaining = 0f;
        mRadius = 0f;
        ClearSource();
        transform.localScale = mBaseScale;
    }
}
