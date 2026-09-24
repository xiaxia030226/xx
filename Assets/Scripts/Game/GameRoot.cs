using Game.UI;
using QFramework;
using UnityEngine;
using UnityEngine.SceneManagement;

public partial class GameRoot : MonoBehaviour, IController
{
    public static Transform BattleRoot { get; private set; } // 当前战斗的对象总根节点，离场时清空引用。
    public static Player PlayerInstance { get; private set; } // 当前场景创建的玩家实例。
    public static StageEnvironment Environment { get; private set; } // 当前关卡环境实例，提供导航及应急补给箱。
    private static GameRoot sCurrent; // 当前有效战斗入口，供静态 UI 操作转发实例调用。
    private StageConfig mStage; // 本局选中关卡的配置，包含环境、波次及通关奖励。
    private Transform mPickupRoot; // 金币、子弹包等拾取物实例的父节点。
    private Transform mBulletRoot; // 玩家子弹实例的父节点。
    private IEnemySpawnSystem mEnemySpawnSystem; // 显式逐帧推进的敌人生成系统。
    private IWeaponSystem mWeaponSystem; // 显式逐帧推进的武器系统。
    private bool mResultEntered; // 本局是否已经进入结算，防止重复发奖。
    private GameState mBeforePause; // 暂停前的 Playing 或 SafeLoot 状态，恢复时还原。

    // 作用：提供战斗控制器所属的 QFramework 架构；返回：全局游戏架构接口。
    public IArchitecture GetArchitecture() => GameArchitecture.Interface; // 直接委托给架构单例入口。

    // 作用：初始化本局模型、场景及系统，打开 HUD 并启动首波；返回：无返回值。
    private void Awake()
    {
        sCurrent = this;
        GameInput.Init();
        UIKit.Config = new GameUIKitConfig();
        Time.timeScale = 1f;
        var state = this.GetModel<IGameStateModel>();
        state.State.Value = GameState.Boot;
        try
        {
            // 架构跨场景存活，先清旧池并重置本局数据，再把新场景引用交给系统。
            mStage = StageConfigTable.Get(state.SelectedLevel.Value);
            this.GetSystem<IGameObjectPoolSystem>().ClearAll();
            this.GetModel<IPlayerModel>().Reset();
            this.GetModel<IEnemyModel>().Reset();
            // 重置后只有补给 S·0 共 60 发，稍后的手枪首装从这 60 发中预扣，而非额外赠送。
            this.GetModel<IBulletInventoryModel>().Reset();
            this.GetModel<IEconomyModel>().RunGold.Value = 0;
            state.CurrentWave.Value = 0;
            state.SummonMultiplier.Value = 1f;
            mResultEntered = false;
            // 环境节点和玩家先就绪，系统才能绑定生成位置、子弹父节点及持有者。
            CreateDirectionalLight();
            CreateBattleEnvironment();
            PlayerInstance = CreatePlayer();
            CreateMainCamera(PlayerInstance.transform);
            SetupSystems();
            RegisterBattleEvents();
            if (Environment.EmergencySupplyCrate == null)
                throw new System.InvalidOperationException("阶段四装配异常：StageEnvironment 未配置 EmergencySupplyCrate，请通过 Game/阶段四/校验资源 检查并手工装配应急箱。");
            Environment.EmergencySupplyCrate.Initialize(mStage, PlayerInstance.transform, GetArchitecture(), mPickupRoot);
            // 必须先进入战斗状态，首波启动守卫才会放行；HUD 在首波事件之前完成订阅。
            state.State.Value = GameState.Playing;
            UIKit.OpenPanel<GameHUD>();
            mEnemySpawnSystem.StartWave(1);
        }
        catch (System.Exception error)
        {
            // 装配失败时保留 Boot 并停用入口更新，不让半初始化的场景继续推进战斗。
            state.State.Value = GameState.Boot;
            enabled = false;
            Debug.LogError($"[GameRoot] 战斗装配失败，请先执行 Game/阶段四/校验资源：{error}");
        }
    }

    // 作用：处理暂停、提前召唤与调试输入，并显式逐帧驱动战斗系统；返回：无返回值。
    private void Update()
    {
        var state = this.GetModel<IGameStateModel>();
        // 暂停键先于战斗状态守卫处理，才能在时间缩放为零时恢复游戏。
        if (GameInput.Pause.WasPressedThisFrame())
        {
            if (state.State.Value == GameState.Playing || state.State.Value == GameState.SafeLoot) PauseGame();
            else if (state.State.Value == GameState.Paused) ResumeGame();
        }
        if (state.State.Value != GameState.Playing) return;
        if (GameInput.CallNextWave.WasPressedThisFrame()) mEnemySpawnSystem.CallNextWaveEarly();
        HandleDebugKeys();
        // QFramework 系统没有 Unity Update，由此处显式调用 Tick；先刷怪后武器，系统内部仍有状态守卫。
        mEnemySpawnSystem.Tick(Time.deltaTime);
        mWeaponSystem.Tick(Time.deltaTime);
    }

    // 作用：在战斗根节点下生成玩家并放到初始位置；返回：玩家预制体上的 Player 组件。
    private Player CreatePlayer()
    {
        // 从已有预制体创建玩家并统一出生位置，再返回系统装配需要的组件。
        var player = Instantiate(Resources.Load<GameObject>("Prefabs/Player"), BattleRoot);
        player.name = "Player";
        player.transform.position = new Vector3(0f, 1f, 0f);
        return player.GetComponent<Player>();
    }

    // 作用：创建俯视主摄像机、音频监听器并绑定跟随目标；返回：无返回值。
    private void CreateMainCamera(Transform target)
    {
        // 相机属于当前场景入口；先按玩家位置设置初始视角，再由 CameraFollow 持续跟随。
        var go = new GameObject("Main Camera");
        go.transform.SetParent(transform, false);
        go.tag = "MainCamera";
        go.transform.position = target.position + new Vector3(0f, 12f, -9f);
        go.transform.rotation = Quaternion.LookRotation(target.position - go.transform.position);
        var camera = go.AddComponent<Camera>();
        camera.fieldOfView = 50f;
        camera.nearClipPlane = 0.3f;
        camera.farClipPlane = 200f;
        go.AddComponent<AudioListener>();
        go.AddComponent<CameraFollow>().Target = target;
    }

    // 作用：将共享系统重新绑定到本局场景，并注册四类拾取物池；返回：无返回值。
    private void SetupSystems()
    {
        mEnemySpawnSystem = this.GetSystem<IEnemySpawnSystem>();
        mWeaponSystem = this.GetSystem<IWeaponSystem>();
        // 此前库存已重置；武器 Setup 丢弃旧枪记录，只装配手枪并启动首装。
        mWeaponSystem.Setup(PlayerInstance.transform, mBulletRoot);
        mEnemySpawnSystem.Setup(PlayerInstance.transform, BattleRoot.Find("EnemyRoot"), mStage, Environment);
        RegisterPickup(GoldPickup.PoolKey, "Prefabs/StageThree/GoldPickup");
        RegisterPickup(AmmoPackPickup.PoolKey, "Prefabs/StageThree/AmmoPackPickup");
        RegisterPickup(ShieldPickup.PoolKey, "Prefabs/StageThree/ShieldPickup");
        RegisterPickup(WeaponPickup.PoolKey, "Prefabs/StageThree/WeaponPickup");
    }

    // 作用：加载指定拾取物预制体并注册预热 16 个实例的对象池；返回：无返回值。
    private void RegisterPickup(string key, string path)
    {
        // 先确认资源存在再注册创建委托，预热的实例统一挂到本局拾取物节点下。
        var prefab = Resources.Load<GameObject>(path);
        if (prefab == null) throw new System.InvalidOperationException($"缺少拾取物：{path}");
        this.GetSystem<IGameObjectPoolSystem>().Register(key, () => Instantiate(prefab, mPickupRoot), 16);
    }

    // 作用：订阅死亡、报废掉弹与清场事件，并绑定入口销毁时自动退订；返回：无返回值。
    private void RegisterBattleEvents()
    {
        // 订阅属于本局场景而非持久架构，销毁时解除，避免再次进入场景重复处理掉落或结算。
        this.RegisterEvent<EnemyDiedEvent>(OnEnemyDied).UnRegisterWhenGameObjectDestroyed(gameObject);
        this.RegisterEvent<WeaponAmmoDroppedEvent>(OnWeaponAmmoDropped).UnRegisterWhenGameObjectDestroyed(gameObject);
        this.RegisterEvent<AllWavesClearedEvent>(_ => EnterSafeLoot()).UnRegisterWhenGameObjectDestroyed(gameObject);
        this.RegisterEvent<PlayerDiedEvent>(_ => EnterResult(false)).UnRegisterWhenGameObjectDestroyed(gameObject);
    }

    // 作用：将报废武器卸出的弹夹批次转成场景子弹包；返回：无返回值。
    private void OnWeaponAmmoDropped(WeaponAmmoDroppedEvent e)
    {
        if (e.Ammo.Count <= 0) return;
        var position = new Vector3(e.Position.x, 0.4f, e.Position.z);
        var pickup = this.GetSystem<IGameObjectPoolSystem>().Spawn(AmmoPackPickup.PoolKey, position, Quaternion.identity, mPickupRoot);
        // 武器已卸夹，批次所有权交给拾取物；原补给/战利品份额随批次保留，不能改为全战利品。
        pickup.GetComponent<AmmoPackPickup>().OnSpawn(e.Caliber, e.Level, e.Ammo, PlayerInstance.transform);
    }

    // 作用：按敌人死亡载荷生成金币、子弹、护盾和武器掉落；返回：无返回值。
    private void OnEnemyDied(EnemyDiedEvent e)
    {
        var pool = this.GetSystem<IGameObjectPoolSystem>();
        var position = new Vector3(e.Position.x, 0.4f, e.Position.z);
        // 各掉落条件独立，可同时生成；不同类别稍作偏移，敌人产出的子弹和武器标记为战利品。
        if (e.Gold > 0)
        {
            var pickup = pool.Spawn(GoldPickup.PoolKey, position, Quaternion.identity, mPickupRoot);
            pickup.GetComponent<GoldPickup>().OnSpawn(e.Gold, PlayerInstance.transform);
        }
        if (e.DropAmmo && e.AmmoCount > 0)
        {
            var pickup = pool.Spawn(AmmoPackPickup.PoolKey, position + Vector3.right * 0.5f, Quaternion.identity, mPickupRoot);
            pickup.GetComponent<AmmoPackPickup>().OnSpawn(e.AmmoCaliber, e.AmmoLevel, AmmoBatch.Loot(e.AmmoCount), PlayerInstance.transform);
        }
        if (e.ShieldLevel > 0)
        {
            var pickup = pool.Spawn(ShieldPickup.PoolKey, position + Vector3.left * 0.5f, Quaternion.identity, mPickupRoot);
            pickup.GetComponent<ShieldPickup>().OnSpawn(e.ShieldLevel, ShieldConfigTable.Get(e.ShieldLevel).Capacity, PlayerInstance.transform);
        }
        if (!string.IsNullOrEmpty(e.WeaponId))
        {
            var pickup = pool.Spawn(WeaponPickup.PoolKey, position + Vector3.forward * 0.5f, Quaternion.identity, mPickupRoot);
            pickup.GetComponent<WeaponPickup>().OnSpawn(e.WeaponId, ItemOrigin.Loot, PlayerInstance.transform);
        }
    }

    // 作用：在全部敌人清空后进入安全拾取阶段并停止战斗；返回：无返回值。
    private void EnterSafeLoot()
    {
        if (mResultEntered || this.GetModel<IGameStateModel>().State.Value != GameState.Playing) return;
        // 先改状态再停止战斗，退款等同步事件也能看到已离开战斗阶段；不立即发放通关奖励。
        this.GetModel<IGameStateModel>().State.Value = GameState.SafeLoot;
        StopCombat();
    }

    // 作用：从安全拾取阶段确认完成并进入胜利结算；返回：无返回值。
    public static void CompleteSafeLoot()
    {
        // 只有安全拾取阶段能确认胜利，避免按钮回调在其他阶段提前结算。
        if (sCurrent == null || GameArchitecture.Interface.GetModel<IGameStateModel>().State.Value != GameState.SafeLoot) return;
        sCurrent.EnterResult(true);
    }

    // 作用：退还在途装填、取消敌方调度并清除飞行弹、毒区及攻击提示；返回：无返回值。
    private void StopCombat()
    {
        // 预扣弹先退库存，敌人停止后再回收场景攻击对象；已装弹夹和拾取物不在此清除。
        mWeaponSystem?.CancelReloads();
        mEnemySpawnSystem?.CancelPendingSpawns();
        if (BattleRoot != null)
        {
            foreach (var bullet in BattleRoot.GetComponentsInChildren<Bullet>()) bullet.CancelFlight();
            foreach (var poison in BattleRoot.GetComponentsInChildren<PoisonArea>()) poison.Recycle();
            foreach (var telegraph in BattleRoot.GetComponentsInChildren<EnemyTelegraph>()) telegraph.ResetVisuals();
        }
        if (PlayerInstance != null) PlayerInstance.ClearPoisonSources();
    }

    // 作用：保存暂停前状态、冻结缩放时间并打开暂停面板；返回：无返回值。
    private void PauseGame()
    {
        var state = this.GetModel<IGameStateModel>();
        // Playing 和 SafeLoot 都允许暂停，恢复时必须还原原阶段，而非一律回到战斗。
        mBeforePause = state.State.Value;
        state.State.Value = GameState.Paused;
        Time.timeScale = 0f;
        UIKit.OpenPanel<PausePanel>(UILevel.PopUI);
    }

    // 作用：仅在存在战斗入口且当前暂停时恢复原阶段并关闭暂停面板；返回：无返回值。
    public static void ResumeGame()
    {
        // 先确认仍处于暂停，再还原暂停前阶段和时间流速，避免错误地重启已结束战斗。
        if (sCurrent == null) return;
        var state = GameArchitecture.Interface.GetModel<IGameStateModel>();
        if (state.State.Value != GameState.Paused) return;
        state.State.Value = sCurrent.mBeforePause;
        Time.timeScale = 1f;
        UIKit.ClosePanel<PausePanel>();
    }

    // 作用：停止战斗并结算本局金币，victory 决定是否加通关奖励；返回：无返回值。
    private void EnterResult(bool victory)
    {
        // 先占用结算守卫，再退款与发奖，避免同步事件重入造成重复结算。
        if (mResultEntered) return;
        mResultEntered = true;
        this.GetModel<IGameStateModel>().State.Value = GameState.Result;
        StopCombat();
        var dropGold = this.GetModel<IEconomyModel>().RunGold.Value;
        var clearBonus = victory ? mStage.ClearBonus : 0;
        this.SendCommand(new AddGoldCommand(dropGold + clearBonus));
        // 面板使用本次结算快照展示，失败也保留本局已拾取金币，但没有通关加成。
        UIKit.OpenPanel<ResultPanel>(UILevel.PopUI, new ResultPanelData
        {
            Victory = victory,
            Kills = this.GetModel<IEnemyModel>().KillCount.Value,
            DropGold = dropGold,
            ClearBonus = clearBonus,
            GoldEarned = dropGold + clearBonus
        });
    }

    // 作用：停止本局战斗、关闭战斗面板并加载主菜单场景；返回：无返回值。
    public static void ReturnToMainMenu()
    {
        // 先切状态并退预扣资源，再卸载场景，避免旧战斗继续推进或留住暂停时间缩放。
        GameArchitecture.Interface.GetModel<IGameStateModel>().State.Value = GameState.MainMenu;
        if (sCurrent != null) sCurrent.StopCombat();
        Time.timeScale = 1f;
        UIKit.ClosePanel<GameHUD>();
        UIKit.ClosePanel<PausePanel>();
        UIKit.ClosePanel<ResultPanel>();
        SceneManager.LoadScene("MainMenu");
    }

    // 作用：销毁当前入口时取消生成调度、清空静态场景引用并恢复时间缩放；返回：无返回值。
    private void OnDestroy()
    {
        // 只允许当前入口清理全局引用，防止旧入口延迟销毁时抹掉新场景引用。
        if (sCurrent != this) return;
        mEnemySpawnSystem?.CancelPendingSpawns();
        sCurrent = null;
        BattleRoot = null;
        PlayerInstance = null;
        Environment = null;
        Time.timeScale = 1f;
    }

    // 作用：在编辑器或开发构建中处理受伤、治疗和补弹调试键；返回：无返回值。
    private void HandleDebugKeys()
    {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        // 调试操作仍通过命令修改模型；额外补给只用于调试，不属于开局 60 发规则。
        if (GameInput.DebugDamage.WasPressedThisFrame())
            this.SendCommand(new PlayerTakeDamageCommand(new DamageInfo(10f, 0, CombatFaction.Enemy)));
        if (GameInput.DebugHeal.WasPressedThisFrame()) this.SendCommand(new PlayerHealCommand(10f));
        if (GameInput.DebugAmmo.WasPressedThisFrame())
        {
            this.SendCommand(new AddBulletsCommand(Caliber.S, 0, AmmoBatch.Supply(60)));
            this.SendCommand(new AddBulletsCommand(Caliber.AR, 0, AmmoBatch.Supply(60)));
        }
#endif
    }
}
