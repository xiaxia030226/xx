using Game.UI;
using QFramework;
using UnityEngine;

/// <summary>
/// 当前阶段的场景启动入口。
/// 负责初始化输入和架构、搭建测试环境，并打开战斗 HUD。
/// </summary>
public class GameRoot : MonoBehaviour, IController
{
    public static Transform BattleRoot { get; private set; }
    public static Player PlayerInstance { get; private set; }

    public IArchitecture GetArchitecture() => GameArchitecture.Interface;

    private void Awake()
    {
        // 先初始化输入和架构，后续创建的 Player、Command 与 HUD 才能访问对应对象。
        GameInput.Init();
        _ = GameArchitecture.Interface;

        // 用项目配置替换 UIKit 默认配置，让面板类型自动映射到 Resources/UI 路径。
        UIKit.Config = new GameUIKitConfig();

        CreateDirectionalLight();

        // 阶段一暂时用代码生成测试场景，后续可替换为正式关卡预制体。
        CreateBattleEnvironment();
        PlayerInstance = CreatePlayer();
        CreateMainCamera(PlayerInstance.transform);

        // GameUIKitConfig 会把 GameHUD 映射到 Resources/UI/GameHUD.prefab。
        UIKit.OpenPanel<GameHUD>();

        RegisterDebugKeys();
    }

    private void CreateDirectionalLight()
    {
        var go = new GameObject("Directional Light");
        go.transform.SetParent(transform, false);
        go.transform.rotation = Quaternion.Euler(50f, -30f, 0f);

        var light = go.AddComponent<Light>();
        light.type = LightType.Directional;
        light.color = new Color(1f, 0.96f, 0.9f);
        light.intensity = 1.1f;
        light.shadows = LightShadows.Soft;
    }

    private void CreateBattleEnvironment()
    {
        var root = new GameObject("BattleRoot");
        root.transform.SetParent(transform, false);
        BattleRoot = root.transform;

        var ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
        ground.name = "Ground";
        ground.transform.SetParent(root.transform, false);
        ground.transform.localScale = new Vector3(10f, 1f, 10f);
        ground.GetComponent<Renderer>().material.color = new Color(0.22f, 0.25f, 0.28f);

        CreateChild(root.transform, "EnemyRoot");
        CreateChild(root.transform, "PickupRoot");
        CreateChild(root.transform, "BulletRoot");

        CreateBoundaryWall(root.transform, "WallNorth", new Vector3(0f, 0.5f, 50f), new Vector3(102f, 1f, 1f));
        CreateBoundaryWall(root.transform, "WallSouth", new Vector3(0f, 0.5f, -50f), new Vector3(102f, 1f, 1f));
        CreateBoundaryWall(root.transform, "WallEast", new Vector3(50f, 0.5f, 0f), new Vector3(1f, 1f, 102f));
        CreateBoundaryWall(root.transform, "WallWest", new Vector3(-50f, 0.5f, 0f), new Vector3(1f, 1f, 102f));
    }

    private void CreateChild(Transform parent, string name)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
    }

    private void CreateBoundaryWall(Transform parent, string name, Vector3 position, Vector3 scale)
    {
        var wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
        wall.name = name;
        wall.transform.SetParent(parent, false);
        wall.transform.localPosition = position;
        wall.transform.localScale = scale;
        wall.GetComponent<Renderer>().material.color = new Color(0.35f, 0.37f, 0.4f);
    }

    private Player CreatePlayer()
    {
        var playerObj = GameObject.CreatePrimitive(PrimitiveType.Capsule);
        playerObj.name = "Player";
        playerObj.transform.SetParent(BattleRoot, false);
        playerObj.transform.position = new Vector3(0f, 1f, 0f);
        playerObj.GetComponent<Renderer>().material.color = new Color(0.3f, 0.55f, 0.85f);

        var rb = playerObj.AddComponent<Rigidbody>();
        // Player 直接修改 Transform，因此刚体只参与触发器检测，不接受物理系统推动。
        rb.isKinematic = true;
        rb.constraints = RigidbodyConstraints.FreezeRotation;

        playerObj.GetComponent<CapsuleCollider>().isTrigger = true;

        var indicator = GameObject.CreatePrimitive(PrimitiveType.Cube);
        indicator.name = "DirectionIndicator";
        Destroy(indicator.GetComponent<Collider>());
        indicator.transform.SetParent(playerObj.transform, false);
        indicator.transform.localPosition = new Vector3(0f, -0.5f, 1f);
        indicator.transform.localScale = new Vector3(0.25f, 0.25f, 0.5f);
        indicator.GetComponent<Renderer>().material.color = new Color(1f, 0.55f, 0.1f);

        return playerObj.AddComponent<Player>();
    }

    private void CreateMainCamera(Transform target)
    {
        var go = new GameObject("Main Camera");
        go.transform.SetParent(transform, false);
        go.tag = "MainCamera";
        go.transform.position = target.position + new Vector3(0f, 12f, -9f);
        go.transform.rotation = Quaternion.LookRotation(target.position - go.transform.position);

        var cam = go.AddComponent<Camera>();
        cam.fieldOfView = 50f;
        cam.nearClipPlane = 0.3f;
        cam.farClipPlane = 200f;
        go.AddComponent<AudioListener>();

        var follow = go.AddComponent<CameraFollow>();
        follow.Target = target;
    }

    /// <summary>
    /// 注册阶段一数据链路测试键：K 扣血、H 回血。
    /// 只在编辑器和 Development Build 中编译，不进入正式发布包。
    /// </summary>
    private void RegisterDebugKeys()
    {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        GameInput.DebugDamage.performed += _ => this.SendCommand(new PlayerTakeDamageCommand(10));
        GameInput.DebugHeal.performed += _ => this.SendCommand(new PlayerHealCommand(10));
#endif
    }
}
