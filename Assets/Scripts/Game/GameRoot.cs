using Game.UI;
using QFramework;
using UnityEngine;

/// <summary>
/// 当前阶段的场景启动入口。
/// 负责初始化输入和架构、创建玩家与相机、驱动系统推进，并监听战斗事件。
/// partial：环境搭建（灯光/地面/围墙/挂点）拆分到 GameRoot.Environment.cs，本文件专注流程编排。
/// </summary>
public partial class GameRoot : MonoBehaviour, IController
{
    // BattleRoot：场景中所有战斗物体的父节点，供其他脚本创建物体时统一挂载。
    public static Transform BattleRoot { get; private set; }

    // PlayerInstance：玩家实例的公开引用，供敌人追踪与水晶吸附等需要玩家位置的脚本使用。
    public static Player PlayerInstance { get; private set; }

    // mPickupRoot：拾取物（经验水晶）的父节点，保持场景层级整洁。
    private Transform mPickupRoot;

    // mEnemySpawnSystem：刷怪系统缓存，每帧调用 Tick 推进波次。
    private IEnemySpawnSystem mEnemySpawnSystem;

    // mWeaponSystem：武器系统缓存，每帧调用 Tick 恢复能量并更新冷却。
    private IWeaponSystem mWeaponSystem;

    // mExpCrystalPrefab：经验水晶预制体缓存，首次生成时从 Resources 加载一次。
    private GameObject mExpCrystalPrefab;

    // mPendingLevelUps：等待玩家选择强化的升级次数（一次拾取多个水晶可能连升多级）。
    private int mPendingLevelUps;

    public IArchitecture GetArchitecture() => GameArchitecture.Interface;

    /// <summary>
    /// 场景加载时执行一次：按顺序初始化输入→架构→战场→玩家→系统→事件→HUD→第一波。
    /// 顺序很重要——后面的步骤依赖前面创建好的对象。
    /// </summary>
    private void Awake()
    {
        // 第 1 步：初始化输入系统并触发架构单例创建。
        GameInput.Init();
        _ = GameArchitecture.Interface;

        // 第 2 步：用项目配置替换 UIKit 默认配置，让面板类型自动映射到 Resources/UI 路径。
        UIKit.Config = new GameUIKitConfig();

        // 第 3 步：搭建战场环境（地面、围墙、挂点）、创建玩家、设置跟随相机。
        // 环境搭建的实现见 GameRoot.Environment.cs（partial 拆分）。
        CreateDirectionalLight();
        CreateBattleEnvironment();
        PlayerInstance = CreatePlayer();
        CreateMainCamera(PlayerInstance.transform);

        // 第 4 步：初始化刷怪与武器系统，并注册经验水晶对象池。
        SetupSystems();

        // 第 5 步：监听战斗事件——敌人死亡掉水晶、玩家升级弹面板、面板关闭继续升级。
        RegisterBattleEvents();

        // 第 6 步：打开战斗 HUD（血条与武器格子条，武器格子已并入 GameHUD）。
        UIKit.OpenPanel<GameHUD>();

        // 第 7 步：开始第一波敌人，战斗正式启动。
        mEnemySpawnSystem.StartWave(1);

        // 第 8 步：注册调试按键（K 扣血、H 回血、L 加经验）。
        RegisterDebugKeys();
    }

    /// <summary>
    /// 每帧调用：驱动刷怪和武器两个非 MonoBehaviour 系统推进逻辑。
    /// System 不会自动 Tick，必须由外部（GameRoot）每帧手动调用。
    /// </summary>
    private void Update()
    {
        // deltaTime：本帧时间间隔，传给两个 System 用于计时推进。
        var deltaTime = Time.deltaTime;

        // 刷怪系统：推进生成计时、检测是否清场、过渡到下一波。
        mEnemySpawnSystem.Tick(deltaTime);

        // 武器系统：推进每个武器的攻击冷却与能量/弹药恢复。
        mWeaponSystem.Tick(deltaTime);
    }

    // ==================== 战场搭建（见 GameRoot.Environment.cs） ====================

    // ==================== 玩家与相机 ====================

    /// <summary>
    /// 从预制体创建玩家实例。
    /// Player.prefab 已挂好 Capsule、Rigidbody(kinematic)、CapsuleCollider(trigger)、
    /// Player 脚本和 DirectionIndicator 子物体。
    /// </summary>
    private Player CreatePlayer()
    {
        // 第一步：从 Resources/Prefabs/Player 加载预制体并实例化。
        var playerPrefab = Resources.Load<GameObject>("Prefabs/Player");
        var playerObj = Instantiate(playerPrefab);
        playerObj.name = "Player";

        // 第二步：挂到 BattleRoot 下，设置世界坐标为地面中心。
        playerObj.transform.SetParent(BattleRoot, false);
        playerObj.transform.position = new Vector3(0f, 1f, 0f);

        // 第三步：预制体上已挂 Player 组件，直接获取返回。
        return playerObj.GetComponent<Player>();
    }

    /// <summary>
    /// 创建主相机并挂载跟随逻辑。
    /// 相机以 12 米高、9 米后的俯视角度跟踪玩家。
    /// 注意：场景中不应保留默认相机，相机的创建与跟随完全由本方法负责。
    /// </summary>
    private void CreateMainCamera(Transform target)
    {
        // 第一步：创建相机物体，tag 标记为 MainCamera 以便 Camera.main 能自动找到。
        var go = new GameObject("Main Camera");
        go.transform.SetParent(transform, false);
        go.tag = "MainCamera";

        // 第二步：相机初始放在玩家上方偏后，形成俯视视角。
        go.transform.position = target.position + new Vector3(0f, 12f, -9f);
        go.transform.rotation = Quaternion.LookRotation(target.position - go.transform.position);

        // 第三步：设置视场角与远近裁剪面，保证 100 米内场景可见。
        var cam = go.AddComponent<Camera>();
        cam.fieldOfView = 50f;
        cam.nearClipPlane = 0.3f;
        cam.farClipPlane = 200f;

        // 第四步：AudioListener 挂在新建的相机上，3D 音效以相机位置为听者。
        // 场景中只保留这一个监听器，因此场景里不应再有默认相机。
        go.AddComponent<AudioListener>();

        // 第五步：挂载 CameraFollow 脚本，Target 设为玩家，Update 里自动跟随。
        var follow = go.AddComponent<CameraFollow>();
        follow.Target = target;
    }

    // ==================== 系统与对象池 ====================

    /// <summary>
    /// 初始化刷怪与武器系统，并注册经验水晶对象池。
    /// 这一步必须在玩家创建之后、HUD 打开之前执行。
    /// </summary>
    private void SetupSystems()
    {
        // 从架构获取系统实例并缓存，供 Update 每帧驱动。
        mEnemySpawnSystem = this.GetSystem<IEnemySpawnSystem>();
        mWeaponSystem = this.GetSystem<IWeaponSystem>();

        // enemyRoot：场景中敌人挂载的父节点，由 CreateBattleEnvironment 创建。
        var enemyRoot = BattleRoot.Find("EnemyRoot");

        // Setup：刷怪系统记录玩家位置作为生成参照，武器系统记录攻击者。
        mEnemySpawnSystem.Setup(PlayerInstance.transform, enemyRoot);
        mWeaponSystem.Setup(PlayerInstance.transform);

        // 注册经验水晶对象池：预创建 16 个隐藏水晶，拾取与掉落循环复用、永不销毁。
        this.GetSystem<IGameObjectPoolSystem>().Register("exp_crystal", CreateExpCrystal, 16);
    }

    /// <summary>
    /// 工厂方法：创建经验水晶实例。
    /// 从预制体实例化一个亮绿色小方块，预制体上已挂好 ExperienceCrystal 脚本和碰撞体。
    /// </summary>
    private GameObject CreateExpCrystal()
    {
        // 首次调用时从 Resources/Prefabs/ExpCrystal 加载预制体并缓存。
        if (mExpCrystalPrefab == null)
        {
            mExpCrystalPrefab = Resources.Load<GameObject>("Prefabs/ExpCrystal");
        }

        // 从预制体克隆新实例：ExperienceCrystal 脚本、颜色、碰撞体均已在预制体上配好。
        var crystalObject = Instantiate(mExpCrystalPrefab);
        crystalObject.name = "ExpCrystal";
        return crystalObject;
    }

    // ==================== 战斗事件监听 ====================

    /// <summary>
    /// 注册三个战斗事件监听并绑定到此 GameObject 的生命周期。
    /// GameObject 销毁时自动取消订阅，防止内存泄漏。
    /// </summary>
    private void RegisterBattleEvents()
    {
        // 事件 1：敌人死亡 → 在死亡位置生成经验水晶。
        this.RegisterEvent<EnemyDiedEvent>(OnEnemyDied)
            .UnRegisterWhenGameObjectDestroyed(gameObject);

        // 事件 2：玩家升级 → 累计待处理次数；若面板未打开则打开升级三选一。
        this.RegisterEvent<LevelUpEvent>(OnLevelUp)
            .UnRegisterWhenGameObjectDestroyed(gameObject);

        // 事件 3：升级面板关闭 → 若还有未处理的升级（连升多级），立即再开面板。
        this.RegisterEvent<LevelUpPanelClosedEvent>(OnLevelUpPanelClosed)
            .UnRegisterWhenGameObjectDestroyed(gameObject);
    }

    /// <summary>
    /// 敌人死亡事件回调：从对象池取水晶放在死亡位置，并注入经验值与玩家引用。
    /// </summary>
    private void OnEnemyDied(EnemyDiedEvent e)
    {
        // 从对象池取出一个水晶，放在敌人死亡位置上方半米处，挂到 PickupRoot 下。
        var crystalObject = this.GetSystem<IGameObjectPoolSystem>()
            .Spawn("exp_crystal", e.Position + Vector3.up * 0.5f, Quaternion.identity, mPickupRoot);

        // 调用 OnSpawn 重置水晶：绑定经验值与玩家引用，激活吸附拾取逻辑。
        crystalObject.GetComponent<ExperienceCrystal>().OnSpawn(e.ExpValue, PlayerInstance.transform);
    }

    /// <summary>
    /// 玩家升级事件回调：累加待处理升级次数。
    /// 面板未打开时立即弹出；已打开时不重复建（复用面板，OnOpen 会刷新选项）。
    /// </summary>
    private void OnLevelUp(LevelUpEvent e)
    {
        // mPendingLevelUps 加一：记录等待玩家选择的升级次数。
        mPendingLevelUps++;

        // 面板未打开 → 用 PopUI 层级打开，保证显示在 GameHUD 之上。
        if (UIKit.GetPanel<LevelUpPanel>() == null)
        {
            UIKit.OpenPanel<LevelUpPanel>(UILevel.PopUI);
        }
    }

    /// <summary>
    /// 升级面板关闭事件回调：处理剩余升级。
    /// 玩家每完成一次三选一，面板关闭触发本回调；还有待处理升级就再开。
    /// </summary>
    private void OnLevelUpPanelClosed(LevelUpPanelClosedEvent e)
    {
        // 完成一次强化选择，待处理次数减一。
        mPendingLevelUps--;

        // 仍有待处理 → 再弹一次面板让玩家继续选择强化。
        if (mPendingLevelUps > 0)
        {
            UIKit.OpenPanel<LevelUpPanel>(UILevel.PopUI);
        }
    }

    // ==================== 调试按键 ====================

    /// <summary>
    /// 注册阶段一数据链路测试键。
    /// 只在编辑器和 Development Build 中编译，不进入正式发布包。
    /// K 扣血、H 回血——验证 HP → HUD 整条数据链路。
    /// L 加 5 经验——验证经验累积 → 升级 → 三选一弹窗的完整升级链路。
    /// </summary>
    private void RegisterDebugKeys()
    {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        GameInput.DebugDamage.performed += _ => this.SendCommand(new PlayerTakeDamageCommand(10));
        GameInput.DebugHeal.performed += _ => this.SendCommand(new PlayerHealCommand(10));
        GameInput.DebugExp.performed += _ => this.SendCommand(new GainExpCommand(5));
#endif
    }
}