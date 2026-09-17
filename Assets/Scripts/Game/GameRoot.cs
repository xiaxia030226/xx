using Game.UI;
using QFramework;
using UnityEngine;
using UnityEngine.SceneManagement;

public partial class GameRoot : MonoBehaviour, IController
{
    public static Transform BattleRoot { get; private set; }
    public static Player PlayerInstance { get; private set; }
    public static StageEnvironment Environment { get; private set; }
    private static GameRoot sCurrent;
    private StageConfig mStage;
    private Transform mPickupRoot;
    private Transform mBulletRoot;
    private IEnemySpawnSystem mEnemySpawnSystem;
    private IWeaponSystem mWeaponSystem;
    private bool mResultEntered;
    private GameState mBeforePause;

    public IArchitecture GetArchitecture() => GameArchitecture.Interface;

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
            mStage = StageConfigTable.Get(state.SelectedLevel.Value);
            this.GetSystem<IGameObjectPoolSystem>().ClearAll();
            this.GetModel<IPlayerModel>().Reset();
            this.GetModel<IEnemyModel>().Reset();
            this.GetModel<IBulletInventoryModel>().Reset();
            this.GetModel<IEconomyModel>().RunGold.Value = 0;
            state.CurrentWave.Value = 0;
            state.SummonMultiplier.Value = 1f;
            mResultEntered = false;
            CreateDirectionalLight();
            CreateBattleEnvironment();
            PlayerInstance = CreatePlayer();
            CreateMainCamera(PlayerInstance.transform);
            SetupSystems();
            RegisterBattleEvents();
            state.State.Value = GameState.Playing;
            UIKit.OpenPanel<GameHUD>();
            mEnemySpawnSystem.StartWave(1);
        }
        catch (System.Exception error)
        {
            state.State.Value = GameState.Boot;
            enabled = false;
            Debug.LogError($"[GameRoot] 战斗装配失败，请先执行 Game/阶段三/校验资源：{error}");
        }
    }

    private void Update()
    {
        var state = this.GetModel<IGameStateModel>();
        if (GameInput.Pause.WasPressedThisFrame())
        {
            if (state.State.Value == GameState.Playing || state.State.Value == GameState.SafeLoot) PauseGame();
            else if (state.State.Value == GameState.Paused) ResumeGame();
        }
        if (state.State.Value != GameState.Playing) return;
        if (GameInput.CallNextWave.WasPressedThisFrame()) mEnemySpawnSystem.CallNextWaveEarly();
        HandleDebugKeys();
        mEnemySpawnSystem.Tick(Time.deltaTime);
        mWeaponSystem.Tick(Time.deltaTime);
    }

    private Player CreatePlayer()
    {
        var player = Instantiate(Resources.Load<GameObject>("Prefabs/Player"), BattleRoot);
        player.name = "Player";
        player.transform.position = new Vector3(0f, 1f, 0f);
        return player.GetComponent<Player>();
    }

    private void CreateMainCamera(Transform target)
    {
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

    private void SetupSystems()
    {
        mEnemySpawnSystem = this.GetSystem<IEnemySpawnSystem>();
        mWeaponSystem = this.GetSystem<IWeaponSystem>();
        mWeaponSystem.Setup(PlayerInstance.transform, mBulletRoot);
        mEnemySpawnSystem.Setup(PlayerInstance.transform, BattleRoot.Find("EnemyRoot"), mStage, Environment);
        RegisterPickup(GoldPickup.PoolKey, "Prefabs/StageThree/GoldPickup");
        RegisterPickup(AmmoPackPickup.PoolKey, "Prefabs/StageThree/AmmoPackPickup");
        RegisterPickup(ShieldPickup.PoolKey, "Prefabs/StageThree/ShieldPickup");
        RegisterPickup(WeaponPickup.PoolKey, "Prefabs/StageThree/WeaponPickup");
    }

    private void RegisterPickup(string key, string path)
    {
        var prefab = Resources.Load<GameObject>(path);
        if (prefab == null) throw new System.InvalidOperationException($"缺少拾取物：{path}");
        this.GetSystem<IGameObjectPoolSystem>().Register(key, () => Instantiate(prefab, mPickupRoot), 16);
    }

    private void RegisterBattleEvents()
    {
        this.RegisterEvent<EnemyDiedEvent>(OnEnemyDied).UnRegisterWhenGameObjectDestroyed(gameObject);
        this.RegisterEvent<AllWavesClearedEvent>(_ => EnterSafeLoot()).UnRegisterWhenGameObjectDestroyed(gameObject);
        this.RegisterEvent<PlayerDiedEvent>(_ => EnterResult(false)).UnRegisterWhenGameObjectDestroyed(gameObject);
    }

    private void OnEnemyDied(EnemyDiedEvent e)
    {
        var pool = this.GetSystem<IGameObjectPoolSystem>();
        var position = new Vector3(e.Position.x, 0.4f, e.Position.z);
        if (e.Gold > 0)
        {
            var pickup = pool.Spawn(GoldPickup.PoolKey, position, Quaternion.identity, mPickupRoot);
            pickup.GetComponent<GoldPickup>().OnSpawn(e.Gold, PlayerInstance.transform);
        }
        if (e.DropAmmo && e.AmmoCount > 0)
        {
            var pickup = pool.Spawn(AmmoPackPickup.PoolKey, position + Vector3.right * 0.5f, Quaternion.identity, mPickupRoot);
            pickup.GetComponent<AmmoPackPickup>().OnSpawn(e.AmmoCaliber, e.AmmoLevel, e.AmmoCount, PlayerInstance.transform);
        }
        if (e.ShieldLevel > 0)
        {
            var pickup = pool.Spawn(ShieldPickup.PoolKey, position + Vector3.left * 0.5f, Quaternion.identity, mPickupRoot);
            pickup.GetComponent<ShieldPickup>().OnSpawn(e.ShieldLevel, ShieldConfigTable.Get(e.ShieldLevel).Capacity, PlayerInstance.transform);
        }
        if (!string.IsNullOrEmpty(e.WeaponId))
        {
            var pickup = pool.Spawn(WeaponPickup.PoolKey, position + Vector3.forward * 0.5f, Quaternion.identity, mPickupRoot);
            pickup.GetComponent<WeaponPickup>().OnSpawn(e.WeaponId, PlayerInstance.transform);
        }
    }

    private void EnterSafeLoot()
    {
        if (mResultEntered || this.GetModel<IGameStateModel>().State.Value != GameState.Playing) return;
        this.GetModel<IGameStateModel>().State.Value = GameState.SafeLoot;
        StopCombat();
    }

    public static void CompleteSafeLoot()
    {
        if (sCurrent == null || GameArchitecture.Interface.GetModel<IGameStateModel>().State.Value != GameState.SafeLoot) return;
        sCurrent.EnterResult(true);
    }

    private void StopCombat()
    {
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

    private void PauseGame()
    {
        var state = this.GetModel<IGameStateModel>();
        mBeforePause = state.State.Value;
        state.State.Value = GameState.Paused;
        Time.timeScale = 0f;
        UIKit.OpenPanel<PausePanel>(UILevel.PopUI);
    }

    public static void ResumeGame()
    {
        if (sCurrent == null) return;
        var state = GameArchitecture.Interface.GetModel<IGameStateModel>();
        if (state.State.Value != GameState.Paused) return;
        state.State.Value = sCurrent.mBeforePause;
        Time.timeScale = 1f;
        UIKit.ClosePanel<PausePanel>();
    }

    private void EnterResult(bool victory)
    {
        if (mResultEntered) return;
        mResultEntered = true;
        this.GetModel<IGameStateModel>().State.Value = GameState.Result;
        StopCombat();
        var dropGold = this.GetModel<IEconomyModel>().RunGold.Value;
        var clearBonus = victory ? mStage.ClearBonus : 0;
        this.SendCommand(new AddGoldCommand(dropGold + clearBonus));
        UIKit.OpenPanel<ResultPanel>(UILevel.PopUI, new ResultPanelData
        {
            Victory = victory,
            Kills = this.GetModel<IEnemyModel>().KillCount.Value,
            DropGold = dropGold,
            ClearBonus = clearBonus,
            GoldEarned = dropGold + clearBonus
        });
    }

    public static void ReturnToMainMenu()
    {
        GameArchitecture.Interface.GetModel<IGameStateModel>().State.Value = GameState.MainMenu;
        if (sCurrent != null) sCurrent.StopCombat();
        Time.timeScale = 1f;
        UIKit.ClosePanel<GameHUD>();
        UIKit.ClosePanel<PausePanel>();
        UIKit.ClosePanel<ResultPanel>();
        SceneManager.LoadScene("MainMenu");
    }

    private void OnDestroy()
    {
        if (sCurrent != this) return;
        mEnemySpawnSystem?.CancelPendingSpawns();
        sCurrent = null;
        BattleRoot = null;
        PlayerInstance = null;
        Environment = null;
        Time.timeScale = 1f;
    }

    private void HandleDebugKeys()
    {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        if (GameInput.DebugDamage.WasPressedThisFrame())
            this.SendCommand(new PlayerTakeDamageCommand(new DamageInfo(10f, 0, CombatFaction.Enemy)));
        if (GameInput.DebugHeal.WasPressedThisFrame()) this.SendCommand(new PlayerHealCommand(10f));
        if (GameInput.DebugAmmo.WasPressedThisFrame())
        {
            this.SendCommand(new AddBulletsCommand(Caliber.S, 0, 60));
            this.SendCommand(new AddBulletsCommand(Caliber.AR, 0, 60));
        }
#endif
    }
}
