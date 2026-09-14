using System.Collections.Generic;
using QFramework;
using UnityEngine;

/// <summary>
/// 刷怪系统对外接口。GameRoot 负责提供玩家和敌人父节点，并在每帧驱动 Tick。
/// </summary>
public interface IEnemySpawnSystem : ISystem
{
    void Setup(Transform player, Transform parent);
    void StartWave(int wave);
    void Tick(float deltaTime);
}

/// <summary>
/// 按波次配置生成敌人，并在本波清场后推进流程。
/// </summary>
public class EnemySpawnSystem : AbstractSystem, IEnemySpawnSystem
{
    private const float SpawnRadiusMin = 15f;
    private const float SpawnRadiusMax = 20f;
    private const float MapLimit = 48f;

    private Transform mPlayer;
    private Transform mParent;
    private WaveConfig mWaveConfig;
    private int mCurrentWave;
    private int mGroupIndex;
    private int mSpawnedInGroup;
    // SpawnTimer：距离本组下一只敌人诞生还剩多少秒。
    private float mSpawnTimer;

    // NextWaveTimer：本波清空后等待多少秒才开启下一波。
    private float mNextWaveTimer;

    // WaveRunning：当前是否正在生成某一波次。
    private bool mWaveRunning;

    // WaitingNextWave：是否处于两波之间的等待间隔。
    private bool mWaitingNextWave;

    // mPrefabCache：敌人预制体缓存（敌人 id → 预制体）。每种敌人首次生成时从 Resources 加载一次，之后复用。
    private readonly Dictionary<string, GameObject> mPrefabCache = new Dictionary<string, GameObject>();

    public void Setup(Transform player, Transform parent)
    {
        mPlayer = player;
        mParent = parent;
        RegisterEnemyPools();
    }

    public void StartWave(int wave)
    {
        if (mPlayer == null)
        {
            Debug.LogError("[Wave] 尚未设置玩家，无法开始波次");
            return;
        }

        if (!WaveConfigTable.TryGet(wave, out mWaveConfig))
        {
            this.SendEvent<AllWavesClearedEvent>();
            Debug.Log("[Wave] 所有波次已清空");
            return;
        }

        mCurrentWave = wave;
        mGroupIndex = 0;
        mSpawnedInGroup = 0;
        mSpawnTimer = 0f;
        mWaveRunning = true;
        mWaitingNextWave = false;

        this.GetModel<IGameStateModel>().CurrentWave.Value = wave;
        this.SendEvent(new WaveStartedEvent { Wave = wave });
        Debug.Log($"[Wave] 第 {wave} 波开始");
    }

    /// <summary>
    /// 推进生成计时和波次状态。System 不是 MonoBehaviour，因此由外部每帧调用。
    /// </summary>
    public void Tick(float deltaTime)
    {
        if (mPlayer == null) return;
        if (this.GetModel<IGameStateModel>().State.Value != GameState.Playing) return;

        if (mWaitingNextWave)
        {
            mNextWaveTimer -= deltaTime;
            if (mNextWaveTimer <= 0f) StartWave(mCurrentWave + 1);
            return;
        }

        if (!mWaveRunning) return;

        SpawnBySchedule(deltaTime);

        // 必须同时满足“配置中的敌人已生成完”和“场上存活数为 0”，才算真正清场。
        if (AllGroupsSpawned() && this.GetModel<IEnemyModel>().AliveCount.Value == 0)
        {
            CompleteCurrentWave();
        }
    }

    protected override void OnInit()
    {
    }

    private void RegisterEnemyPools()
    {
        var pool = this.GetSystem<IGameObjectPoolSystem>();

        // 遍历配置表注册全部敌人的对象池：每种敌人预热 8 个。
        // 新增敌人只需在 EnemyConfigTable 中加一行配置，这里无需改动。
        foreach (var enemyId in EnemyConfigTable.AllIds)
        {
            pool.Register(enemyId, () => CreateEnemy(enemyId), 8);
        }
    }

    /// <summary>
    /// 通用敌人工厂：按敌人 id 从配置表取预制体路径，加载并缓存后实例化。
    /// 预制体上已挂好 Enemy、Collider、Rigidbody 和材质，无需代码设置。
    /// </summary>
    /// <param name="enemyId">敌人配置 id，同时作为对象池的 key。</param>
    private GameObject CreateEnemy(string enemyId)
    {
        // 第一步：取配置，从中读取预制体路径。
        var config = EnemyConfigTable.Get(enemyId);

        // 第二步：首次生成该类型敌人时，从 Resources 加载预制体并缓存，避免重复 IO。
        if (!mPrefabCache.TryGetValue(enemyId, out var prefab))
        {
            prefab = Resources.Load<GameObject>(config.PrefabPath);
            mPrefabCache[enemyId] = prefab;
        }

        // 第三步：从预制体克隆新实例，命名为敌人配置名便于 Hierarchy 中辨认。
        var enemyObject = Object.Instantiate(prefab);
        enemyObject.name = config.Name;
        return enemyObject;
    }

    private void SpawnBySchedule(float deltaTime)
    {
        if (AllGroupsSpawned()) return;

        var group = mWaveConfig.Groups[mGroupIndex];

        // 倒计时归零时只生成一只，然后重新设置为该组的生成间隔。
        mSpawnTimer -= deltaTime;
        if (mSpawnTimer > 0f) return;

        SpawnEnemy(group.EnemyId);
        mSpawnedInGroup++;
        mSpawnTimer = group.Interval;

        if (mSpawnedInGroup >= group.Count)
        {
            mGroupIndex++;
            mSpawnedInGroup = 0;
            mSpawnTimer = 0f;
        }
    }

    private void SpawnEnemy(string enemyId)
    {
        var angle = Random.Range(0f, Mathf.PI * 2f);
        var radius = Random.Range(SpawnRadiusMin, SpawnRadiusMax);
        var position = mPlayer.position + new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * radius;
        position.x = Mathf.Clamp(position.x, -MapLimit, MapLimit);
        position.z = Mathf.Clamp(position.z, -MapLimit, MapLimit);
        position.y = 0.6f;

        var enemyObject = this.GetSystem<IGameObjectPoolSystem>()
            .Spawn(enemyId, position, Quaternion.identity, mParent);
        enemyObject.GetComponent<Enemy>().OnSpawn(EnemyConfigTable.Get(enemyId), mPlayer);
        this.GetModel<IEnemyModel>().AliveCount.Value++;
    }

    private bool AllGroupsSpawned()
    {
        return mWaveConfig == null || mGroupIndex >= mWaveConfig.Groups.Count;
    }

    private void CompleteCurrentWave()
    {
        mWaveRunning = false;
        this.SendEvent(new WaveClearedEvent { Wave = mCurrentWave });
        Debug.Log($"[Wave] 第 {mCurrentWave} 波清空");

        if (mCurrentWave >= WaveConfigTable.WaveCount)
        {
            this.SendEvent<AllWavesClearedEvent>();
            Debug.Log("[Wave] AllWavesCleared");
            return;
        }

        mWaitingNextWave = true;
        mNextWaveTimer = WaveConfigTable.WaveInterval;
    }
}
