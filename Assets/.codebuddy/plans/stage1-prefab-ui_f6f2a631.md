---
name: stage1-prefab-ui
overview: 阶段一开发改为预制体 UI 方案：GameHUD 在编辑器制作成 prefab（挂脚本、拖引用），场景中手工搭建 UIRoot 画布，通过 Resources 加载实例化到画布层级下；架构/输入/实体/相机部分不变。
todos:
  - id: arch-models
    content: 创建 GameArchitecture 与 IPlayerModel/PlayerModel、IGameStateModel/GameStateModel 数据层
    status: completed
  - id: input-commands
    content: 实现 GameInput 静态输入封装（13 Action）及扣血/回血两个 Command
    status: completed
    dependencies:
      - arch-models
  - id: entities
    content: 实现 Player 实体（移动/朝向/边界 Clamp）与 CameraFollow 平滑跟随
    status: completed
    dependencies:
      - arch-models
  - id: ui-loader
    content: 实现 GameUIKitConfig：PanelType.Name 映射 Resources 路径的极简加载器
    status: completed
    dependencies:
      - arch-models
  - id: game-hud
    content: 实现 GameHUD 脚本：SerializeField 控件引用 + BindableProperty 订阅刷新
    status: completed
    dependencies:
      - ui-loader
  - id: game-root
    content: 实现 GameRoot 启动编排：环境/玩家/相机/打开 HUD/KH 调试键
    status: completed
    dependencies:
      - input-commands
      - entities
      - game-hud
  - id: verify
    content: 编译自检全部脚本，输出 UIRoot 画布搭建与 GameHUD.prefab 制作指引及验收清单
    status: completed
    dependencies:
      - game-root
---

## 用户需求

执行阶段一开发：基于 QFramework 搭建项目地基，实现玩家 WASD 移动 + 鼠标朝向、GameHUD 血条数据链路，达成里程碑"架构初始化、数据绑定、UI 自动刷新全流程跑通"。遵循 docs/DesignDetails.md 已确认的设计。

**本次关键变更**：放弃纯代码 UI 方案，改为传统预制体工作流——GameHUD 在编辑器中制作成 prefab 并挂好脚本、拖好控件引用；场景中手工搭建画布；运行时加载 prefab 实例化到画布层级下。

## 产品概述

2.5D 卡通风格关卡制 Roguelike 割草游戏（PC 键鼠）。阶段一交付：Play 后场景自动搭好（地面/边界墙/胶囊玩家/斜俯视相机平滑跟随），WASD 八方向移动且边界拦截，鼠标控制朝向，K/H 调试键扣血回血且 HUD 血条实时刷新；HUD 为编辑器制作的预制体，由 UIKit 加载挂到场景画布下。

## 核心功能

- GameArchitecture + PlayerModel/GameStateModel（BindableProperty 数据层）
- GameInput 静态输入封装（新版 Input System，纯代码 Action，13 个一次建全）
- Player 实体：Transform 位移、Clamp ±49、数学平面求交鼠标朝向
- CameraFollow：SmoothDamp 平滑跟随（偏移 0,12,-9，FOV 50）
- GameHUD.prefab：血条（双层 Image + 文本）+ Lv/波次/EXP/武器格占位框，脚本通过 SerializeField 引用控件
- 场景 UIRoot 画布（手工搭建，UIKit 自动采纳）+ Resources prefab 加载链路
- GameRoot 启动编排 + K/H 调试键（编辑器/开发包生效）

## 技术栈

- Unity 2022.3.10f1 + C#，QFramework（Framework + Toolkits/UIKit，asmdef 均 autoReferenced，游戏代码放 Assembly-CSharp）
- 新版 Input System 包（`UnityEngine.InputSystem`，需先安装，否则代码无法编译）
- uGUI 预制体工作流：GameHUD.prefab 放 `Assets/Resources/UI/` 下，运行时 Resources 加载

## 实现方案

### 核心策略

废弃纯代码 UI（删除 UIFactory/UIColorPalette/纯代码加载器），回归 UIKit 默认的 prefab 加载链：编辑器制作 GameHUD.prefab 并挂脚本拖引用 → 场景手工搭 UIRoot 画布 → `UIKit.OpenPanel<GameHUD>()` 加载实例化到画布层级下。

### 已验证的关键事实（读源码确认）

1. **默认 prefab 加载链**（UIKitConfig.cs 41-56, 134-154）：`UIKitConfig.LoadPanel` → `DefaultPanelLoader.LoadPanelPrefab` → `Resources.Load<GameObject>(GameObjName)` → Instantiate → `GetComponent<UIPanel>()` → 赋值 Loader 返回。
2. **prefabName 必传问题**（UIKit.cs 56-63）：`OpenPanel<T>` 不传 prefabName 时 GameObjName 为 null，`Resources.Load(null)` 失败。方案：写一个极简 `GameUIKitConfig` 重写 LoadPanel，把 `PanelType.Name` 映射为 `"UI/" + 类型名` 路径，调用侧保持 `UIKit.OpenPanel<GameHUD>()` 干净。
3. **场景画布自动采纳**（UIRoot.cs 29-47）：`UIRoot.Instance` 先 `FindObjectOfType<UIRoot>()`，找到场景里挂 UIRoot 组件的画布就直接用，不会重复创建。UIRoot 公有字段需在 Inspector 拖好：Canvas、CanvasScaler、GraphicRaycaster、Bg/Common/PopUI/CanvasPanel 四个全屏 RectTransform 层级节点。
4. **面板挂载流程**（UIManager.cs 201-214）：OpenUI 内部 `SetLevelOfPanel` 把面板 SetParent 到层级节点 → `SetDefaultSizeOfPanel` 拉满 → 命名 → `panel.Init(uiData)`。
5. **生命周期不变**：`OnInit(IUIData)` 仅首次、`OnOpen` 每次打开、`OnClose()` abstract 必须实现；订阅用 `RegisterWithInitValue` + `UnRegisterWhenGameObjectDestroyed`。

### 数据流

GameRoot(调试键 K/H) → SendCommand(PlayerTakeDamage/PlayerHealCommand) → 改 PlayerModel.HP（Clamp 0~MaxHP）→ BindableProperty 通知 → GameHUD 刷新血条填充与文本。

### 性能与可靠性

- 每帧仅轮询 `InputAction.ReadValue`（零分配），鼠标朝向用 Plane.Raycast 数学求交（无物理射线）。
- 事件订阅全部配对注销防泄漏；调试键 `#if UNITY_EDITOR || DEVELOPMENT_BUILD` 包裹。
- GameHUD 的 SerializeField 引用在 OnInit 做空检查并输出错误日志，避免 prefab 漏拖引用时静默失败。

## 架构设计

分层遵守 QFramework 五约束：输入/调试键 → Command → Model(BindableProperty) → View 订阅刷新。GameRoot 为唯一场景入口；UI 资产（prefab、画布）与逻辑（脚本订阅）分离，prefab 负责表现，脚本只拿引用刷新数据。

## 目录结构

```
Game/
├── GameArchitecture.cs            # [NEW] 架构注册中心：注册 IPlayerModel/IGameStateModel
├── GameRoot.cs                    # [NEW] 场景入口：Awake 编排 输入→架构→UIKit.Config 替换→灯光→BattleRoot→Player→Camera→OpenPanel<GameHUD>→K/H 调试键（不再代码建 Canvas）
├── GameUIKitConfig.cs             # [NEW] 极简重写 LoadPanel：PanelType.Name 映射 Resources 路径 "UI/{Name}"，其余走 base
├── Input/
│   └── GameInput.cs               # [NEW] 静态类：13 个 InputAction 纯代码创建（Move 用 2DVector 复合绑定 WASD）+ Init/Enable
├── Model/
│   ├── IPlayerModel.cs            # [NEW] 接口：HP/MaxHP/MoveSpeed/Level/Exp/ExpNeed
│   ├── PlayerModel.cs             # [NEW] 初始值 100/100/5/1/0/5
│   ├── IGameStateModel.cs         # [NEW] State 枚举 + CurrentWave
│   └── GameStateModel.cs          # [NEW] 实现
├── Command/
│   ├── PlayerTakeDamageCommand.cs # [NEW] HP=Max(0,HP-dmg)
│   └── PlayerHealCommand.cs       # [NEW] HP=Min(MaxHP,HP+amount)
├── Entity/
│   ├── Player.cs                  # [NEW] IController；Update 轮询移动+Clamp±49+鼠标朝向
│   └── CameraFollow.cs            # [NEW] LateUpdate SmoothDamp(0.15s) 跟随
└── View/
    └── GameHUD.cs                 # [NEW] UIPanel+IController：[SerializeField] 血条 Image/Text 等引用（prefab 上拖好）；OnInit 空检查+初始刷新，OnOpen 订阅 HP/MaxHP，OnClose 实现

Assets/Resources/UI/
└── GameHUD.prefab                 # [手工制作] 根节点挂 GameHUD 脚本；左上血条 300×24（底 Image + Filled 前景 Image + 文本）、Lv/波次/EXP 占位、底部 9 格武器栏
```

## 用户手工步骤（代码写好后执行）

1. Package Manager 安装 Input System 包（弹窗 Yes 重启）
2. Game.unity 场景：建 Canvas（挂 UIRoot 组件）+ 4 个全屏层级子节点 Bg/Common/PopUI/CanvasPanel，Inspector 拖好 UIRoot 全部字段；EventSystem 换挂 InputSystemUIInputModule
3. 制作 GameHUD.prefab 放 `Assets/Resources/UI/`，挂 GameHUD 脚本并拖好 SerializeField 引用
4. 场景挂 GameRoot 组件；Build Settings 加入 Game.unity