---
name: phase1-foundation-player-hud
overview: 按 DesignDetails.md 第三节实施阶段一：搭建 QFramework 地基（GameArchitecture/GameRoot/GameInput）、玩家 WASD 移动 + 鼠标朝向 + 平滑跟随相机、纯代码生成 GameHUD 血条与占位元素，并通过 K/H 调试键验证数据链路。
todos:
  - id: arch-models
    content: 创建 GameArchitecture 与 IPlayerModel/PlayerModel、IGameStateModel/GameStateModel 数据层
    status: pending
  - id: input-commands
    content: 实现 GameInput 静态输入封装（13 Action）及扣血/回血两个 Command
    status: pending
    dependencies:
      - arch-models
  - id: entities
    content: 实现 Player 实体（移动/朝向/边界 Clamp）与 CameraFollow 平滑跟随
    status: pending
    dependencies:
      - arch-models
  - id: ui-infra
    content: 实现 GameUIKitConfig 纯代码面板加载器、UIFactory、UIColorPalette
    status: pending
    dependencies:
      - arch-models
  - id: game-hud
    content: 实现 GameHUD 面板：血条+占位灰框+BindableProperty 订阅刷新
    status: pending
    dependencies:
      - ui-infra
  - id: game-root
    content: 实现 GameRoot 启动编排：环境搭建、UIRoot 输入模块改造、HUD 打开、K/H 调试键
    status: pending
    dependencies:
      - input-commands
      - entities
      - game-hud
  - id: verify
    content: 编译自检全部脚本，输出手工操作指引与 3.6 节逐项验收清单
    status: pending
    dependencies:
      - game-root
---

## 用户需求

执行阶段一开发：基于 QFramework 搭建项目地基，实现玩家 WASD 移动 + 鼠标朝向、GameHUD 血条数据链路，达成里程碑"架构初始化、数据绑定、UI 自动刷新全流程跑通"。严格遵循 `docs/DesignDetails.md` 第三节已确认的设计（16 项决策已锁定）。

## 产品概述

2.5D 卡通风格关卡制 Roguelike 割草游戏（PC 键鼠）。阶段一交付：Play 后场景自动搭好（地面/边界墙/胶囊玩家/斜 45° 相机平滑跟随），WASD 八方向移动且边界拦截，鼠标控制朝向，K/H 调试键扣血回血且 HUD 血条实时刷新。

## 核心功能

- GameArchitecture + PlayerModel/GameStateModel（BindableProperty 数据层）
- GameInput 静态输入封装（新版 Input System，纯代码 Action，13 个一次建全）
- Player 实体：Transform 位移、Clamp ±49、数学平面求交鼠标朝向
- CameraFollow：SmoothDamp 平滑跟随（偏移 0,12,-9，FOV 50）
- GameHUD：血条（双层 Image + 文本）+ Lv/波次/EXP/武器格灰色占位框
- GameRoot 启动编排 + K/H 调试键（编辑器/开发包生效）
- 纯代码 UI 面板加载（无 prefab，需自定义 UIKit 加载器）

## 技术栈

- Unity 2022.3.10f1 + C#，QFramework（Framework + Toolkits/UIKit，已引入，asmdef 均 autoReferenced，游戏代码放 Assembly-CSharp 无需新建 asmdef）
- 新版 Input System 包（`UnityEngine.InputSystem`，**当前未安装**：`manifest.json` 无 `com.unity.inputsystem`，`activeInputHandler: 0`）
- uGUI（运行时纯代码构建，字体用内置 `LegacyRuntime.ttf`）

## 实现方案

### 关键探索结论（已验证，直接影响实现）

1. **纯代码面板必须自定义加载器**：UIKit 默认 `DefaultPanelLoader.LoadPanelPrefab` 走 `Resources.Load<GameObject>(prefabName)`，无 prefab 会返回 null。需新建 `GameUIKitConfig : UIKitConfig` 重写 `LoadPanel`：从 `PanelLoaderPool.AllocateLoader()` 取 loader 赋给 `IPanel.Loader`（Close 时会调 `Unload`/`RecycleLoader`，不可缺），然后 `new GameObject(面板类型名, typeof(RectTransform), 面板类型)` 直接返回实例，跳过 prefab 实例化。GameRoot 中首访 UIKit 前执行 `UIKit.Config = new GameUIKitConfig()`。
2. **UIRoot 自动创建**：`UIRoot.Instance` 找不到时会自动实例化 UIKit 自带 `Resources/UIRoot.prefab`（含 Canvas/CanvasScaler 1280×720/GraphicRaycaster/层级节点），**且自带一个挂 StandaloneInputModule（旧输入）的 EventSystem 子物体**。新输入系统下旧模块会报错，且不能出现两个 EventSystem。GameRoot 需：首访 `UIKit.Root` 触发创建 → 调用 `SetResolution(1920,1080,0.5f)` → 找到其 EventSystem 子物体，禁用/移除 StandaloneInputModule 并 AddComponent `InputSystemUIInputModule`。这是对 DesignDetails 3.3 第 3 步的落地修正（不新建 EventSystem/Canvas，改为改造 UIRoot 自带的）。
3. **QFramework API 已核对**：`RegisterModel<T>(T)` 泛型注册（接口作 T）、`AbstractModel.OnInit()`、`AbstractCommand.OnExecute()`、`BindableProperty<T>.RegisterWithInitValue` 返回 `IUnRegister`、扩展 `UnRegisterWhenGameObjectDestroyed(gameObject)` 存在、UIPanel 生命周期 `OnInit(IUIData)/OnOpen/OnClose()`（OnClose 为 abstract 必须实现）、`CloseSelf()` 关面板。
4. **IController 实现**：`public IArchitecture GetArchitecture() => GameArchitecture.Interface;`。GameHUD（UIPanel）同样实现 IController 以便 `this.GetModel<IPlayerModel>()`。

### 数据流

GameRoot(调试键 K/H) → SendCommand(PlayerTakeDamage/PlayerHealCommand) → Command 改 PlayerModel.HP（Clamp 0~MaxHP）→ BindableProperty 通知 → GameHUD 刷新血条宽度与文本。

### 性能与可靠性

- 每帧逻辑仅轮询 `InputAction.ReadValue`（零分配），鼠标朝向用 `Plane.Raycast` 数学求交（无物理射线、无 GC）。
- 事件订阅全部 `UnRegisterWhenGameObjectDestroyed` 配对注销，防泄漏。
- 调试键回调 `#if UNITY_EDITOR || DEVELOPMENT_BUILD` 包裹，正式包零开销。

### 前置手工步骤（用户已完成或需先完成）

安装 Input System 包（Package Manager，弹窗选 Yes 重启 Editor）——**否则代码无法编译**；新建 Game.unity 挂 GameRoot 并加入 Build Settings。代码编写不依赖这些步骤完成，验证依赖。

## 架构设计

分层遵守 QFramework 五约束：输入/调试键 → Command → Model(BindableProperty) → View 订阅刷新。GameRoot 为唯一场景入口，负责启动编排（输入→架构→UIRoot 改造→环境→玩家→相机→HUD→调试键）。

## 目录结构

全部新建于 `Assets/Scripts/Game/`：

```
Game/
├── GameArchitecture.cs            # [NEW] 架构注册中心：注册 IPlayerModel/IGameStateModel（接口泛型注册）
├── GameRoot.cs                    # [NEW] 场景入口 MonoBehaviour：Awake 按序初始化 GameInput→架构→UIKit.Config 替换→UIRoot 分辨率/输入模块改造→灯光→BattleRoot(Plane 100×100+4 边界墙)→Player(胶囊+朝向指示器+kinematic Rigidbody+trigger Collider)→Camera(FOV50+CameraFollow)→OpenPanel<GameHUD>→注册 K/H 调试键
├── GameUIKitConfig.cs             # [NEW] 重写 LoadPanel 实现纯代码面板创建（关键，替代 Resources prefab 加载）
├── Input/
│   └── GameInput.cs               # [NEW] 静态类：13 个 InputAction 纯代码创建（Move 用 2DVector 复合绑定 WASD）+ Init()/Enable 全局
├── Model/
│   ├── IPlayerModel.cs            # [NEW] 玩家数据接口：HP/MaxHP/MoveSpeed/Level/Exp/ExpNeed
│   ├── PlayerModel.cs             # [NEW] 初始值 100/100/5/1/0/5，OnInit 赋值
│   ├── IGameStateModel.cs         # [NEW] State(GameState 枚举 Playing/Paused/GameOver/Victory)+CurrentWave
│   └── GameStateModel.cs          # [NEW] 实现
├── Command/
│   ├── PlayerTakeDamageCommand.cs # [NEW] 带伤害参数构造，HP=Max(0,HP-dmg)
│   └── PlayerHealCommand.cs       # [NEW] HP=Min(MaxHP,HP+amount)
├── Entity/
│   ├── Player.cs                  # [NEW] IController；Update 轮询 Move 位移+Clamp±49+鼠标朝向(Plane 求交)
│   └── CameraFollow.cs            # [NEW] 普通 MonoBehaviour；LateUpdate SmoothDamp(0.15s) 偏移跟随+LookRotation
├── Utility/
│   ├── UIFactory.cs               # [NEW] CreateText/CreateImage/CreateBar/CreatePlaceholderBox 等代码控件工厂
│   └── UIColorPalette.cs          # [NEW] 色板常量（血条绿/底灰/占位灰 40% 透明）
└── View/
    └── GameHUD.cs                 # [NEW] UIPanel+IController：左上 HP 条 300×24+文本、Lv/波次/EXP 灰框、底部 9 格武器栏灰框；OnInit 建 UI+RegisterWithInitValue 订阅 HP/MaxHP；OnClose 实现
```

## 关键代码结构

```
// 纯代码面板加载器（本阶段最易踩坑点）
public class GameUIKitConfig : UIKitConfig
{
    public override IPanel LoadPanel(PanelSearchKeys panelSearchKeys)
    {
        var loader = PanelLoaderPool.AllocateLoader();
        var go = new GameObject(panelSearchKeys.PanelType.Name, typeof(RectTransform), panelSearchKeys.PanelType);
        var panel = go.GetComponent<UIPanel>() as IPanel;
        panel.Loader = loader;
        return panel;
    }
}

// GameHUD 面板骨架（UIPanel + IController）
public class GameHUD : UIPanel, IController
{
    public IArchitecture GetArchitecture() => GameArchitecture.Interface;
    protected override void OnInit(IUIData uiData = null); // UIFactory 建 UI + 订阅
    protected override void OnClose();                     // 必须实现（abstract）
}
```