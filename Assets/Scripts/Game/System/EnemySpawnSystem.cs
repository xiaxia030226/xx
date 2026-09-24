using System.Collections.Generic;
using QFramework;
using UnityEngine;

public interface IEnemySpawnSystem : ISystem
{
    int NextWave { get; } // 下一波配置中的波号，没有下一波时为零。
    float NextWaveCountdown { get; } // 下一波自然启动的剩余秒数，未计时或无下一波时为 -1。
    IReadOnlyList<SpawnGroup> NextWavePreview { get; } // 下一波生成组预览，无下一波时为 null。
    IReadOnlyList<Enemy> AliveEnemies { get; } // 当前登记的存活敌人只读列表。
    int PendingSpawnCount { get; } // 所有任务中尚未生成的敌人总数，包含分裂子体。
    StageConfig Stage { get; } // 当前关卡配置。
    StageEnvironment Environment { get; } // 当前战斗环境，提供边界、入口和导航信息。
    bool ProjectilePoolReady { get; } // true 表示敌方投射物池已通过预制体验证并注册。
    bool PoisonPoolReady { get; } // true 表示毒区池已通过预制体验证并注册。

    // 作用：绑定场景引用，重置波次状态并注册敌方攻击池；返回：无返回值。
    void Setup(Transform player, Transform parent, StageConfig stage, StageEnvironment environment);
    // 作用：按自然召唤规则启动指定的下一波；返回：无返回值。
    void StartWave(int wave);
    // 作用：推进生成任务、波次倒计时和护盾支援，并检查清场；返回：无返回值。
    void Tick(float deltaTime);
    // 作用：在允许时提升召唤倍率并提前启动下一波；返回：无返回值。
    void CallNextWaveEarly();
    // 作用：取消待生成任务和现有敌人的战斗行为；返回：无返回值。
    void CancelPendingSpawns();
    // 作用：在给定位置租用并初始化敌人，可覆盖生命并标记子体；返回：成功生成的 Enemy，条件或资源不满足时为 null。
    Enemy SpawnEnemy(string enemyId, Vector3 position, int shieldLevel, float multiplier,
        float healthOverride = 0f, bool child = false);
    // 作用：添加指定位置附近的延迟子体生成任务；返回：无返回值。
    void ScheduleChildren(string enemyId, int count, float delay, Vector3 position, float health, float multiplier);
    // 作用：移除敌人登记与支援关联并同步存活、击杀统计；返回：无返回值。
    void UnregisterEnemy(Enemy enemy);
}

public class EnemySpawnSystem : AbstractSystem, IEnemySpawnSystem
{
    public const string ProjectilePoolKey = "enemy_projectile"; // 敌方投射物共用的对象池标识。
    public const string ProjectilePrefabPath = "Prefabs/Bullet"; // 敌方投射物的 Resources 预制体路径。
    private const float SafePlayerDistance = 12f; // 常规敌人出生点与玩家之间的最小水平距离。

    private sealed class SpawnBatch
    {
        public string EnemyId; // 此组使用的敌人配置标识。
        public int Count; // 此组计划生成的总数量。
        public float Interval; // 同组相邻敌人的生成间隔秒数。
        public int ShieldLevel; // 此组敌人出生时的护盾等级。
    }

    private sealed class SpawnTask
    {
        public int Wave; // 创建任务时记录的所属波号。
        public SpawnBatch[] Groups; // 按顺序处理的生成组快照。
        public int GroupIndex; // 当前处理的组下标。
        public int SpawnedInGroup; // 当前组已经成功生成的数量。
        public int Remaining; // 整个任务尚未成功生成的数量。
        public float Timer; // 下一次生成前的剩余秒数，可为负以保留帧余量。
        public float Multiplier; // 创建任务时锁定的召唤倍率。
        public bool Child; // true 表示在原位置附近生成的分裂子体任务。
        public Vector3 Position; // 子体生成使用的中心位置，常规任务不使用。
        public float Health; // 子体生成时传入的生命覆盖值。
    }

    private readonly List<SpawnTask> mTasks = new List<SpawnTask>(); // 可并行推进的波次及子体待生成任务。
    private readonly List<Enemy> mAlive = new List<Enemy>(); // 已生成并登记、尚未注销的敌人列表。
    private readonly HashSet<Enemy> mRepairedThisRound = new HashSet<Enemy>(); // 本轮已选作修盾目标的敌人，防止多鼓手重复选中。
    private readonly Dictionary<string, GameObject> mPrefabCache = new Dictionary<string, GameObject>(); // 按敌人标识缓存加载的预制体资产。
    private readonly HashSet<string> mRegisteredPools = new HashSet<string>(); // 本次场景绑定后已成功注册的敌人池标识。
    private readonly HashSet<string> mReportedMissing = new HashSet<string>(); // 本次绑定已报告的缺失标识，避免逐帧重复报错。
    private Transform mPlayer; // 玩家变换，供敌人初始化和出生安全距离判断。
    private Transform mParent; // 敌人实例在场景中的挂载节点。
    private int mNextWaveIndex; // 下一条待启动波配置的零基下标。
    private int mStartedWaves; // 本局已启动的波数，用于首波守卫和最终清场判断。
    private int mEntranceIndex; // 下一次选择入口或边界时使用的轮转游标。
    private float mNextWaveCountdown = -1f; // 下一波自然启动的倒计时，-1 表示尚未安排。
    private float mRepairTimer; // 距下一轮两秒周期护盾支援的剩余秒数。
    private bool mAllClearedSent; // 是否已经发送本局全部波次清空事件。
    private bool mCancelled; // 是否取消战斗调度，取消后需 Setup 重新启用。

    public StageConfig Stage { get; private set; } // 本次 Setup 绑定的关卡配置。
    public StageEnvironment Environment { get; private set; } // 本次 Setup 绑定的环境及导航入口。
    public bool ProjectilePoolReady { get; private set; } // true 表示投射物预制体验证及池注册成功。
    public bool PoisonPoolReady { get; private set; } // true 表示毒区预制体验证及池注册成功。
    public IReadOnlyList<Enemy> AliveEnemies => mAlive; // 存活登记列表的只读视图。
    private WaveConfig NextConfig => !mCancelled && Stage?.Waves != null && mNextWaveIndex < Stage.Waves.Count
        ? Stage.Waves[mNextWaveIndex] : null; // 取消或波次耗尽时为 null，否则为下一波配置。
    public int NextWave => NextConfig?.Wave ?? 0; // 下一波号，无可用配置时为零。
    public float NextWaveCountdown => NextConfig == null ? -1f : mNextWaveCountdown; // 下一波剩余秒数，无配置时固定为 -1。
    public IReadOnlyList<SpawnGroup> NextWavePreview => NextConfig?.Groups; // 下一波组配置预览，无配置时为 null。
    public int PendingSpawnCount // 全部未完成任务的待生成数量，每次读取重新汇总。
    {
        // 作用：累加各任务的剩余数量，包含子体；返回：当前待生成总数。
        get
        {
            // 从任务剩余量即时汇总，避免另存计数后与任务增删失去同步。
            var count = 0;
            for (var i = 0; i < mTasks.Count; i++) count += mTasks[i].Remaining;
            return count;
        }
    }

    // 作用：取消旧战斗调度，重新绑定场景并重置波次和攻击池状态；返回：无返回值。
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
        // 清完旧登记后替换场景引用，重置计时和一次性事件守卫，恢复可调度状态。
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
        // 敌人池按需注册；攻击池先验证并记录可用性，预制体资产缓存则可继续复用。
        ProjectilePoolReady = RegisterCombatPool(ProjectilePoolKey, ProjectilePrefabPath, true);
        PoisonPoolReady = RegisterCombatPool(PoisonArea.PoolKey, PoisonArea.PrefabPath, false);
    }

    // 作用：按自然召唤规则请求启动指定下一波并重置倍率；返回：无返回值。
    public void StartWave(int wave) => StartWaveInternal(wave, true); // 直接委托内部启动流程及其条件守卫。

    // 作用：在首波启动后提前召唤下一波，并逐档提高召唤倍率；返回：无返回值。
    public void CallNextWaveEarly()
    {
        if (!IsPlaying || mCancelled || mStartedWaves == 0 || NextConfig == null) return;
        var multiplier = this.GetModel<IGameStateModel>().SummonMultiplier;
        // 先提升全局倍率，再以非自然启动锁定到新任务，不影响已经排队任务的倍率。
        multiplier.Value = StepUpMultiplier(multiplier.Value);
        StartWaveInternal(NextConfig.Wave, false);
    }

    private bool IsPlaying => this.GetModel<IGameStateModel>().State.Value == GameState.Playing; // true 表示处于可战斗、可生成的游戏阶段。

    // 作用：由 GameRoot 显式推进生成、自然召唤、护盾修复与最终清场检查；返回：无返回值。
    public void Tick(float deltaTime)
    {
        if (mPlayer == null || mCancelled) return;
        var state = this.GetModel<IGameStateModel>().State.Value;
        // 暂停保留任务；若在其他非战斗阶段被调用，则取消调度而不是继续倒计时。
        if (state == GameState.Paused) return;
        if (state != GameState.Playing)
        {
            CancelPendingSpawns();
            return;
        }
        deltaTime = Mathf.Max(0f, deltaTime);
        // 各波和子体任务并行推进；移除完成任务时不递增下标，避免跳过后移元素。
        for (var i = 0; i < mTasks.Count;)
        {
            AdvanceTask(mTasks[i], deltaTime);
            if (mTasks[i].Remaining == 0) mTasks.RemoveAt(i);
            else i++;
        }

        // 下一波仅按时间启动，不等待前一波清空；首波由外部 StartWave 发起。
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

    // 作用：仅启动配置顺序中的下一波，natural 为 true 时重置倍率并排入生成任务；返回：无返回值。
    private void StartWaveInternal(int wave, bool natural)
    {
        if (!IsPlaying || mCancelled || mPlayer == null || NextConfig == null || NextConfig.Wave != wave) return;
        var config = NextConfig;
        var state = this.GetModel<IGameStateModel>();
        if (natural) state.SummonMultiplier.Value = 1f;
        // 将组配置和当前倍率复制到任务，之后提前召唤的新倍率不追溯修改本任务。
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
        // 空波也算已启动；先推进游标与倒计时，再广播，订阅者读取到的是更新后的下一波。
        mStartedWaves++;
        mNextWaveIndex++;
        mNextWaveCountdown = NextConfig != null ? Mathf.Max(0f, NextConfig.IntervalFromPrev) : -1f;
        state.CurrentWave.Value = config.Wave;
        this.SendEvent(new WaveStartedEvent { Wave = config.Wave });
    }

    // 作用：按指定延迟、生命和倍率排入原位置附近的无盾子体生成任务；返回：无返回值。
    public void ScheduleChildren(string enemyId, int count, float delay, Vector3 position, float health, float multiplier)
    {
        if (mCancelled || !IsPlaying || count <= 0) return;
        // 子体只在首次生成前等待，组内间隔为零；实际点位仍由出生位置校验负责避障。
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

    // 作用：按帧余量推进单个生成任务，每次最多处理 64 次生成尝试；返回：无返回值。
    private void AdvanceTask(SpawnTask task, float deltaTime)
    {
        task.Timer -= deltaTime;
        // 限制零间隔或大帧延迟造成的单帧生成量，未完成部分留到下一帧。
        var budget = 64;
        while (task.Timer <= 0f && task.Remaining > 0 && budget-- > 0)
        {
            // 跳过空组和已完成组，保持配置顺序，不为组间额外增加等待。
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
            // 配置、点位或预制体暂不可用时不扣剩余数，下帧重试，防止漏刷后误判清场。
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

    // 作用：在给定位置租用敌人、应用生成参数并登记存活数量；返回：生成的 Enemy，非战斗、已取消、无玩家或池不可用时为 null。
    public Enemy SpawnEnemy(string enemyId, Vector3 position, int shieldLevel, float multiplier,
        float healthOverride = 0f, bool child = false)
    {
        if (mCancelled || mPlayer == null || !IsPlaying || !EnsureEnemyPool(enemyId)) return null;
        var config = EnemyConfigTable.Get(enemyId);
        // 此入口使用调用方提供的位置，不另做点位校验；租用后先重置业务状态，再登记统计。
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

    // 作用：注销敌人和支援引用，更新存活数并对死亡对象累计击杀；返回：无返回值。
    public void UnregisterEnemy(Enemy enemy)
    {
        // 移除成功才处理后续统计，避免重复注销多算击杀；实际对象回收不由此方法执行。
        if (!mAlive.Remove(enemy)) return;
        for (var i = 0; i < mAlive.Count; i++)
            if (mAlive[i] != null) mAlive[i].RemoveSupportTarget(enemy);
        mRepairedThisRound.Remove(enemy);
        var model = this.GetModel<IEnemyModel>();
        model.AliveCount.Value = mAlive.Count;
        if (enemy != null && !enemy.IsAlive) model.KillCount.Value++;
    }

    // 作用：取消后续生成和波次倒计时，并停止现有敌人的战斗行为；返回：无返回值。
    public void CancelPendingSpawns()
    {
        // 取消是本次调度的终止标志，不等同于暂停；不在此销毁或注销存活列表。
        mCancelled = true;
        mTasks.Clear();
        mNextWaveCountdown = -1f;
        mRepairedThisRound.Clear();
        for (var i = 0; i < mAlive.Count; i++)
            if (mAlive[i] != null) mAlive[i].CancelCombat();
    }

    // 作用：在全部波次启动且任务、敌人均清空时发送一次全清事件；返回：无返回值。
    private void CheckAllCleared()
    {
        // 待生成子体和资源缺失而保留的任务也会阻止清场；无波次配置不视为通关。
        if (mAllClearedSent || mCancelled || Stage?.Waves == null || Stage.Waves.Count == 0
            || mStartedWaves < Stage.Waves.Count || mTasks.Count > 0 || mAlive.Count > 0) return;
        mAllClearedSent = true;
        this.SendEvent<AllWavesClearedEvent>();
    }

    // 作用：最多尝试 48 个候选出生点并检查边界、玩家、障碍和敌人间距；返回：true 时 out position 为可用世界坐标，false 时为 default。
    private bool TrySpawnPosition(EnemyConfig config, bool child, float health, Vector3 origin, out Vector3 position)
    {
        // 低于标准生命的子体按缩小后的出生半径校验，环境缺省时使用中心为零的默认边界。
        var radius = Enemy.GetSpawnRadius(config, child && health > 0f && health < config.MaxHP);
        var halfSize = Environment != null ? Environment.HalfSize : 45f;
        var center = Environment != null ? Environment.transform.position : Vector3.zero;
        var entrances = Environment != null ? Environment.Entrances : null;
        var entranceCount = entrances?.Count ?? 0;
        for (var attempt = 0; attempt < 48; attempt++)
        {
            // 子体围绕原位置散布；常规敌人轮询入口，反复失败时逐轮扩大入口周边散布半径。
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
                // 没有入口配置时轮流使用四条边，预留出生半径与边缘余量。
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
            // 高度统一后先排除越界与贴脸出生，距离只比较 XZ 平面，子体允许更靠近玩家。
            position.y = center.y + 0.6f;
            if (Mathf.Abs(position.x - center.x) + radius > halfSize
                || Mathf.Abs(position.z - center.z) + radius > halfSize) continue;
            var playerDistance = child ? 2.5f : SafePlayerDistance;
            var offset = position - mPlayer.position;
            offset.y = 0f;
            if (offset.sqrMagnitude < playerDistance * playerDistance) continue;
            if (Environment?.Navigation != null && !Environment.Navigation.IsFree(position, radius)) continue;
            // 导航通过后再按双方碰撞半径排除现有敌人，额外留出少量间隙。
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
            // 仅成功选点才推进轮转游标；完全失败时交由任务下一帧重试。
            mEntranceIndex = (mEntranceIndex + 1) % Mathf.Max(4, entranceCount);
            return true;
        }
        position = default;
        return false;
    }

    // 作用：为每个可支援敌人选择最多三个最近目标并尝试修复护盾，本轮目标全局去重；返回：无返回值。
    private void RepairShieldsRound()
    {
        mRepairedThisRound.Clear();
        for (var i = 0; i < mAlive.Count; i++)
        {
            var drummer = mAlive[i];
            if (drummer == null) continue;
            // 每轮先清旧支援显示，不能继续支援的敌人不保留过期连线。
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
                // 先占用本轮目标名额，成功修复才建立支援显示，其他支援者不重复尝试该目标。
                mRepairedThisRound.Add(nearest);
                if (nearest.RepairShield(10f)) drummer.SetSupportTarget(slot, nearest);
            }
        }
    }

    // 作用：加载并验证敌人预制体后按敌人标识懒注册对象池；返回：true 表示池已可用，false 表示配置或预制体组件不满足要求。
    private bool EnsureEnemyPool(string enemyId)
    {
        if (mRegisteredPools.Contains(enemyId)) return true;
        if (!EnemyConfigTable.TryGet(enemyId, out var config)) return false;
        // 资产缓存可跨次 Setup 复用，但失效引用仍需重新加载；场景实例始终交给对象池创建。
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
        // 砸击型树精还必须提供动态障碍及碰撞形状，避免生成无法正确参与环境碰撞的实例。
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

    // 作用：按 projectile 选择验证 Bullet 或 PoisonArea 组件并注册敌方攻击池；返回：true 表示注册可用，false 表示预制体或目标组件缺失。
    private bool RegisterCombatPool(string key, string path, bool projectile)
    {
        var prefab = Resources.Load<GameObject>(path);
        // 只注册满足对应攻击类型的资源；失败记录日志和可用性，不创建替代攻击对象。
        if (prefab == null || (projectile ? prefab.GetComponent<Bullet>() == null : prefab.GetComponent<PoisonArea>() == null))
        {
            ReportMissing(key, $"[Enemy] 缺少攻击 prefab 或组件：Resources/{path}。");
            return false;
        }
        this.GetSystem<IGameObjectPoolSystem>().Register(key, () => Object.Instantiate(prefab));
        return true;
    }

    // 作用：同一标识在本次 Setup 周期内只输出一次缺失错误；返回：无返回值。
    private void ReportMissing(string key, string message)
    {
        // 集合仅在首次加入时返回 true，使同一缺失资源不会持续刷屏。
        if (mReportedMissing.Add(key)) Debug.LogError(message);
    }

    // 作用：计算提前召唤时的下一档倍率；返回：按当前值依次取 1.5、1.75，达到后固定取 2。
    private static float StepUpMultiplier(float current)
    {
        // 按阈值逐档推进而非直接相加，最高档固定为 2 倍。
        if (current < 1.5f) return 1.5f;
        if (current < 1.75f) return 1.75f;
        return 2f;
    }

    // 作用：响应 QFramework 系统初始化，场景和波次留待 Setup 绑定；返回：无返回值。
    protected override void OnInit()
    {
        // 依赖当前关卡的初始化统一放在 Setup，不在持久架构创建时启动刷怪。
    }
}
