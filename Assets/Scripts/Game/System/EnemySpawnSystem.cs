using System.Collections.Generic;
using QFramework;
using UnityEngine;

public interface IEnemySpawnSystem : ISystem
{
    int NextWave { get; }
    float NextWaveCountdown { get; }
    IReadOnlyList<SpawnGroup> NextWavePreview { get; }
    IReadOnlyList<Enemy> AliveEnemies { get; }
    int PendingSpawnCount { get; }
    StageConfig Stage { get; }
    StageEnvironment Environment { get; }
    bool ProjectilePoolReady { get; }
    bool PoisonPoolReady { get; }

    void Setup(Transform player, Transform parent, StageConfig stage, StageEnvironment environment);
    void StartWave(int wave);
    void Tick(float deltaTime);
    void CallNextWaveEarly();
    void CancelPendingSpawns();
    Enemy SpawnEnemy(string enemyId, Vector3 position, int shieldLevel, float multiplier,
        float healthOverride = 0f, bool child = false);
    void ScheduleChildren(string enemyId, int count, float delay, Vector3 position, float health, float multiplier);
    void UnregisterEnemy(Enemy enemy);
}

public class EnemySpawnSystem : AbstractSystem, IEnemySpawnSystem
{
    public const string ProjectilePoolKey = "enemy_projectile";
    public const string ProjectilePrefabPath = "Prefabs/Bullet";
    private const float SafePlayerDistance = 12f;

    private sealed class SpawnBatch
    {
        public string EnemyId;
        public int Count;
        public float Interval;
        public int ShieldLevel;
    }

    private sealed class SpawnTask
    {
        public int Wave;
        public SpawnBatch[] Groups;
        public int GroupIndex;
        public int SpawnedInGroup;
        public int Remaining;
        public float Timer;
        public float Multiplier;
        public bool Child;
        public Vector3 Position;
        public float Health;
    }

    private readonly List<SpawnTask> mTasks = new List<SpawnTask>();
    private readonly List<Enemy> mAlive = new List<Enemy>();
    private readonly HashSet<Enemy> mRepairedThisRound = new HashSet<Enemy>();
    private readonly Dictionary<string, GameObject> mPrefabCache = new Dictionary<string, GameObject>();
    private readonly HashSet<string> mRegisteredPools = new HashSet<string>();
    private readonly HashSet<string> mReportedMissing = new HashSet<string>();
    private Transform mPlayer;
    private Transform mParent;
    private int mNextWaveIndex;
    private int mStartedWaves;
    private int mEntranceIndex;
    private float mNextWaveCountdown = -1f;
    private float mRepairTimer;
    private bool mAllClearedSent;
    private bool mCancelled;

    public StageConfig Stage { get; private set; }
    public StageEnvironment Environment { get; private set; }
    public bool ProjectilePoolReady { get; private set; }
    public bool PoisonPoolReady { get; private set; }
    public IReadOnlyList<Enemy> AliveEnemies => mAlive;
    private WaveConfig NextConfig => !mCancelled && Stage?.Waves != null && mNextWaveIndex < Stage.Waves.Count
        ? Stage.Waves[mNextWaveIndex] : null;
    public int NextWave => NextConfig?.Wave ?? 0;
    public float NextWaveCountdown => NextConfig == null ? -1f : mNextWaveCountdown;
    public IReadOnlyList<SpawnGroup> NextWavePreview => NextConfig?.Groups;
    public int PendingSpawnCount
    {
        get
        {
            var count = 0;
            for (var i = 0; i < mTasks.Count; i++) count += mTasks[i].Remaining;
            return count;
        }
    }

    public void Setup(Transform player, Transform parent, StageConfig stage, StageEnvironment environment)
    {
        CancelPendingSpawns();
        // Setup 也支持同场景重开；不向可能已 ClearAll 的旧对象池回收。
        while (mAlive.Count > 0)
        {
            var last = mAlive.Count - 1;
            var enemy = mAlive[last];
            mAlive.RemoveAt(last);
            if (enemy != null) enemy.gameObject.SetActive(false);
        }
        this.GetModel<IEnemyModel>().AliveCount.Value = 0;
        mPlayer = player;
        mParent = parent;
        Stage = stage;
        Environment = environment;
        mNextWaveIndex = 0;
        mStartedWaves = 0;
        mEntranceIndex = 0;
        mNextWaveCountdown = -1f;
        mRepairTimer = 2f;
        mAllClearedSent = false;
        mCancelled = false;
        mRegisteredPools.Clear();
        mReportedMissing.Clear();
        mRepairedThisRound.Clear();
        ProjectilePoolReady = RegisterCombatPool(ProjectilePoolKey, ProjectilePrefabPath, true);
        PoisonPoolReady = RegisterCombatPool(PoisonArea.PoolKey, PoisonArea.PrefabPath, false);
    }

    public void StartWave(int wave) => StartWaveInternal(wave, true);

    public void CallNextWaveEarly()
    {
        if (!IsPlaying || mCancelled || mStartedWaves == 0 || NextConfig == null) return;
        var multiplier = this.GetModel<IGameStateModel>().SummonMultiplier;
        multiplier.Value = StepUpMultiplier(multiplier.Value);
        StartWaveInternal(NextConfig.Wave, false);
    }

    private bool IsPlaying => this.GetModel<IGameStateModel>().State.Value == GameState.Playing;

    public void Tick(float deltaTime)
    {
        if (mPlayer == null || mCancelled) return;
        var state = this.GetModel<IGameStateModel>().State.Value;
        if (state == GameState.Paused) return;
        if (state != GameState.Playing)
        {
            CancelPendingSpawns();
            return;
        }
        deltaTime = Mathf.Max(0f, deltaTime);
        for (var i = 0; i < mTasks.Count;)
        {
            AdvanceTask(mTasks[i], deltaTime);
            if (mTasks[i].Remaining == 0) mTasks.RemoveAt(i);
            else i++;
        }

        if (mStartedWaves > 0 && NextConfig != null)
        {
            mNextWaveCountdown -= deltaTime;
            if (mNextWaveCountdown <= 0f) StartWaveInternal(NextConfig.Wave, true);
        }

        mRepairTimer -= deltaTime;
        if (mRepairTimer <= 0f)
        {
            mRepairTimer += 2f;
            RepairShieldsRound();
        }
        CheckAllCleared();
    }

    private void StartWaveInternal(int wave, bool natural)
    {
        if (!IsPlaying || mCancelled || mPlayer == null || NextConfig == null || NextConfig.Wave != wave) return;
        var config = NextConfig;
        var state = this.GetModel<IGameStateModel>();
        if (natural) state.SummonMultiplier.Value = 1f;
        var task = new SpawnTask
        {
            Wave = config.Wave,
            Multiplier = state.SummonMultiplier.Value,
            Groups = new SpawnBatch[config.Groups?.Count ?? 0]
        };
        for (var i = 0; i < task.Groups.Length; i++)
        {
            var group = config.Groups[i];
            task.Groups[i] = new SpawnBatch
            {
                EnemyId = group.EnemyId,
                Count = Mathf.Max(0, group.Count),
                Interval = Mathf.Max(0f, group.Interval),
                ShieldLevel = group.ShieldLevel
            };
            task.Remaining += task.Groups[i].Count;
        }
        if (task.Remaining > 0) mTasks.Add(task);
        mStartedWaves++;
        mNextWaveIndex++;
        mNextWaveCountdown = NextConfig != null ? Mathf.Max(0f, NextConfig.IntervalFromPrev) : -1f;
        state.CurrentWave.Value = config.Wave;
        this.SendEvent(new WaveStartedEvent { Wave = config.Wave });
    }

    public void ScheduleChildren(string enemyId, int count, float delay, Vector3 position, float health, float multiplier)
    {
        if (mCancelled || !IsPlaying || count <= 0) return;
        position.y = 0.6f;
        mTasks.Add(new SpawnTask
        {
            Wave = this.GetModel<IGameStateModel>().CurrentWave.Value,
            Groups = new[] { new SpawnBatch { EnemyId = enemyId, Count = count, ShieldLevel = 0, Interval = 0f } },
            Remaining = count,
            Timer = Mathf.Max(0f, delay),
            Multiplier = multiplier,
            Child = true,
            Position = position,
            Health = health
        });
    }

    private void AdvanceTask(SpawnTask task, float deltaTime)
    {
        task.Timer -= deltaTime;
        var budget = 64;
        while (task.Timer <= 0f && task.Remaining > 0 && budget-- > 0)
        {
            while (task.GroupIndex < task.Groups.Length && task.SpawnedInGroup >= task.Groups[task.GroupIndex].Count)
            {
                task.GroupIndex++;
                task.SpawnedInGroup = 0;
            }
            if (task.GroupIndex >= task.Groups.Length) break;
            var group = task.Groups[task.GroupIndex];
            if (!EnemyConfigTable.TryGet(group.EnemyId, out var config))
            {
                ReportMissing(group.EnemyId, $"[Wave] 缺少敌人配置 {group.EnemyId}，保留待生成任务。");
                task.Timer = 0f;
                break;
            }
            if (!TrySpawnPosition(config, task.Child, task.Health, task.Position, out var position)
                || SpawnEnemy(group.EnemyId, position, group.ShieldLevel, task.Multiplier, task.Health, task.Child) == null)
            {
                task.Timer = 0f;
                break;
            }
            task.SpawnedInGroup++;
            task.Remaining--;
            // 一组完成后下一组立即接续；同组间隔保留帧余量。
            if (task.SpawnedInGroup < group.Count) task.Timer += group.Interval;
        }
    }

    public Enemy SpawnEnemy(string enemyId, Vector3 position, int shieldLevel, float multiplier,
        float healthOverride = 0f, bool child = false)
    {
        if (mCancelled || mPlayer == null || !IsPlaying || !EnsureEnemyPool(enemyId)) return null;
        var config = EnemyConfigTable.Get(enemyId);
        var instance = this.GetSystem<IGameObjectPoolSystem>().Spawn(enemyId, position, Quaternion.identity, mParent);
        var enemy = instance.GetComponent<Enemy>();
        enemy.OnSpawn(config, mPlayer, multiplier, shieldLevel, healthOverride, child);
        if (!mAlive.Contains(enemy))
        {
            mAlive.Add(enemy);
            this.GetModel<IEnemyModel>().AliveCount.Value = mAlive.Count;
        }
        return enemy;
    }

    public void UnregisterEnemy(Enemy enemy)
    {
        if (!mAlive.Remove(enemy)) return;
        for (var i = 0; i < mAlive.Count; i++)
            if (mAlive[i] != null) mAlive[i].RemoveSupportTarget(enemy);
        mRepairedThisRound.Remove(enemy);
        var model = this.GetModel<IEnemyModel>();
        model.AliveCount.Value = mAlive.Count;
        if (enemy != null && !enemy.IsAlive) model.KillCount.Value++;
    }

    public void CancelPendingSpawns()
    {
        mCancelled = true;
        mTasks.Clear();
        mNextWaveCountdown = -1f;
        mRepairedThisRound.Clear();
        for (var i = 0; i < mAlive.Count; i++)
            if (mAlive[i] != null) mAlive[i].CancelCombat();
    }

    private void CheckAllCleared()
    {
        if (mAllClearedSent || mCancelled || Stage?.Waves == null || Stage.Waves.Count == 0
            || mStartedWaves < Stage.Waves.Count || mTasks.Count > 0 || mAlive.Count > 0) return;
        mAllClearedSent = true;
        this.SendEvent<AllWavesClearedEvent>();
    }

    private bool TrySpawnPosition(EnemyConfig config, bool child, float health, Vector3 origin, out Vector3 position)
    {
        var radius = Enemy.GetSpawnRadius(config, child && health > 0f && health < config.MaxHP);
        var halfSize = Environment != null ? Environment.HalfSize : 45f;
        var center = Environment != null ? Environment.transform.position : Vector3.zero;
        var entrances = Environment != null ? Environment.Entrances : null;
        var entranceCount = entrances?.Count ?? 0;
        for (var attempt = 0; attempt < 48; attempt++)
        {
            if (child)
            {
                var angle = Random.Range(0f, Mathf.PI * 2f);
                var distance = Random.Range(1f, 5f);
                position = origin + new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * distance;
            }
            else if (entranceCount > 0)
            {
                var index = (mEntranceIndex + attempt) % entranceCount;
                var entrance = entrances[index];
                if (entrance == null) continue;
                var scatter = Random.insideUnitCircle * (1f + attempt / entranceCount * 0.4f);
                position = entrance.position + new Vector3(scatter.x, 0f, scatter.y);
            }
            else
            {
                var edge = halfSize - radius - 1.5f;
                var along = Random.Range(-edge, edge);
                switch ((mEntranceIndex + attempt) % 4)
                {
                    case 0: position = new Vector3(-edge, 0f, along); break;
                    case 1: position = new Vector3(edge, 0f, along); break;
                    case 2: position = new Vector3(along, 0f, -edge); break;
                    default: position = new Vector3(along, 0f, edge); break;
                }
                position += center;
            }
            position.y = center.y + 0.6f;
            if (Mathf.Abs(position.x - center.x) + radius > halfSize
                || Mathf.Abs(position.z - center.z) + radius > halfSize) continue;
            var playerDistance = child ? 2.5f : SafePlayerDistance;
            var offset = position - mPlayer.position;
            offset.y = 0f;
            if (offset.sqrMagnitude < playerDistance * playerDistance) continue;
            if (Environment?.Navigation != null && !Environment.Navigation.IsFree(position, radius)) continue;
            var occupied = false;
            for (var i = 0; i < mAlive.Count; i++)
            {
                var enemy = mAlive[i];
                if (enemy == null || !enemy.IsAlive) continue;
                var separation = radius + enemy.CollisionRadius + 0.2f;
                offset = position - enemy.transform.position;
                offset.y = 0f;
                if (offset.sqrMagnitude >= separation * separation) continue;
                occupied = true;
                break;
            }
            if (occupied) continue;
            mEntranceIndex = (mEntranceIndex + 1) % Mathf.Max(4, entranceCount);
            return true;
        }
        position = default;
        return false;
    }

    private void RepairShieldsRound()
    {
        mRepairedThisRound.Clear();
        for (var i = 0; i < mAlive.Count; i++)
        {
            var drummer = mAlive[i];
            if (drummer == null) continue;
            drummer.ClearSupportTargets();
            if (!drummer.CanSupport) continue;
            for (var slot = 0; slot < 3; slot++)
            {
                Enemy nearest = null;
                var nearestDistance = float.PositiveInfinity;
                for (var j = 0; j < mAlive.Count; j++)
                {
                    var target = mAlive[j];
                    if (target == null || mRepairedThisRound.Contains(target) || !drummer.CanSupportTarget(target)) continue;
                    var distance = (target.transform.position - drummer.transform.position).sqrMagnitude;
                    if (distance >= nearestDistance) continue;
                    nearest = target;
                    nearestDistance = distance;
                }
                if (nearest == null) break;
                mRepairedThisRound.Add(nearest);
                if (nearest.RepairShield(10f)) drummer.SetSupportTarget(slot, nearest);
            }
        }
    }

    private bool EnsureEnemyPool(string enemyId)
    {
        if (mRegisteredPools.Contains(enemyId)) return true;
        if (!EnemyConfigTable.TryGet(enemyId, out var config)) return false;
        if (!mPrefabCache.TryGetValue(enemyId, out var prefab) || prefab == null)
        {
            prefab = Resources.Load<GameObject>(config.PrefabPath);
            if (prefab != null) mPrefabCache[enemyId] = prefab;
        }
        if (prefab == null || prefab.GetComponent<Enemy>() == null)
        {
            ReportMissing(enemyId, $"[Wave] 缺少 Enemy prefab：Resources/{config.PrefabPath}，保留待生成任务。");
            return false;
        }
        if (config.AIType == EnemyAIType.Slam)
        {
            var obstacle = prefab.GetComponent<BattleObstacle>();
            if (obstacle == null || !obstacle.IsDynamic || obstacle.Shape == null)
            {
                ReportMissing(enemyId, $"[Wave] 树精 prefab 需要带 Collider 的动态 BattleObstacle：{config.PrefabPath}。");
                return false;
            }
        }
        this.GetSystem<IGameObjectPoolSystem>().Register(enemyId, () => Object.Instantiate(prefab));
        mRegisteredPools.Add(enemyId);
        return true;
    }

    private bool RegisterCombatPool(string key, string path, bool projectile)
    {
        var prefab = Resources.Load<GameObject>(path);
        if (prefab == null || (projectile ? prefab.GetComponent<Bullet>() == null : prefab.GetComponent<PoisonArea>() == null))
        {
            ReportMissing(key, $"[Enemy] 缺少攻击 prefab 或组件：Resources/{path}。");
            return false;
        }
        this.GetSystem<IGameObjectPoolSystem>().Register(key, () => Object.Instantiate(prefab));
        return true;
    }

    private void ReportMissing(string key, string message)
    {
        if (mReportedMissing.Add(key)) Debug.LogError(message);
    }

    private static float StepUpMultiplier(float current)
    {
        if (current < 1.5f) return 1.5f;
        if (current < 1.75f) return 1.75f;
        return 2f;
    }

    protected override void OnInit() { }
}
