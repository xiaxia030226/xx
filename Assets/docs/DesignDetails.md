# 详细设计文档（DesignDetails）

> 本文档是开发的直接依据，与 `GameDesign.md`（玩法）、`DevelopmentPlan.md`（总纲）配合使用。
> **若本文档与旧文档冲突，以本文档为准**（本文档包含了最新的技术选型决策）。
> 文档结构：决策记录 → 全局架构 → 阶段一~四详细设计 → 全局数值总表 → 手工操作清单。

---

## 一、技术选型决策记录（已确认）

| # | 决策点 | 结论 |
|---|--------|------|
| 1 | 相机视角 | 斜 45° 俯视 3D（透视相机） |
| 2 | 输入方案 | 新版 Input System 包（纯代码创建 Action，不用 .inputactions 资产） |
| 3 | 场景组织 | 单场景 `Game.unity` + UI 面板切换全流程 |
| 4 | 局外存档 | PlayerPrefs（封装 SaveManager，预留迁移 JSON 的能力） |
| 5 | 命名空间 | 不使用，所有类在全局命名空间 |
| 6 | UI 创建 | 预制体 + 场景画布 + QF 代码生成：面板 prefab 控件挂 Bind 组件，点"生成代码"自动产出字段并赋值；场景中手工搭 UIRoot 画布，运行时 UIKit 从 Resources 加载实例化（面板脚本例外地使用 `Game.UI` 命名空间，QF 生成器强制要求） |
| 7 | 对象池 | 阶段二起即接入 QFramework PoolKit（敌人/水晶/弹幕全走池） |
| 8 | 数值管理 | 阶段一、二硬编码在 Model/Config 静态类；阶段三起统一代码配置表 |
| 9 | 玩家占位 | 胶囊体 + 正前方朝向指示器（小圆锥/方块） |
| 10 | 相机跟随 | 平滑跟随（LateUpdate + SmoothDamp） |
| 11 | 阶段一 HUD | 血条 + 经验条/等级/波次灰色占位框（提前定好布局） |
| 12 | 地图边界 | 固定 100×100 范围，位置 Clamp + 可视边界墙 |
| 13 | 移动方案 | Transform 直接位移；碰撞靠 Trigger（挂 kinematic Rigidbody） |
| 14 | 文档粒度 | 四个阶段全部详细设计 |
| 15 | 调试手段 | 简单调试键（K 扣血 / H 回血 / L 加经验） |

---

## 二、全局架构设计

### 2.1 分层与数据流

```
输入(InputAction) / 碰撞(OnTriggerEnter)
        ↓
   Controller（Player/Enemy 等 MonoBehaviour，实现 IController）
        ↓ this.SendCommand<T>()
   Command（改数据的唯一入口）
        ↓
   Model（BindableProperty<T> 承载数据）
        ↓ 值变化自动通知
   View（UIPanel 订阅刷新）     Event（TypeEventSystem 跨模块通知）
```

System 层承载无状态业务逻辑（波次调度、武器管理、对象池），Command 通过 `this.GetSystem<T>()` 调用。

### 2.2 GameArchitecture 最终注册清单（阶段四完成形态）

```csharp
public class GameArchitecture : Architecture<GameArchitecture>
{
    protected override void Init()
    {
        // Model
        RegisterModel<IPlayerModel>(new PlayerModel());       // 阶段一
        RegisterModel<IGameStateModel>(new GameStateModel()); // 阶段一
        RegisterModel<IEnemyModel>(new EnemyModel());         // 阶段二
        RegisterModel<IWeaponModel>(new WeaponModel());       // 阶段三
        RegisterModel<IPassiveModel>(new PassiveModel());     // 阶段三
        RegisterModel<IMetaModel>(new MetaModel());           // 阶段四（金币/解锁进度）

        // System
        RegisterSystem<IGameObjectPoolSystem>(new GameObjectPoolSystem()); // 阶段二
        RegisterSystem<IEnemySpawnSystem>(new EnemySpawnSystem());         // 阶段二
        RegisterSystem<IWeaponSystem>(new WeaponSystem());                 // 阶段二
        RegisterSystem<IUpgradeSystem>(new UpgradeSystem());               // 阶段三
        RegisterSystem<IGameFlowSystem>(new GameFlowSystem());             // 阶段四
    }
}
```

### 2.3 场景结构（单场景，UI 为场景手动内容）

场景中**手动内容**：`GameRoot` 空物体、`UIRoot` 画布、`EventSystem`。其余全部运行时生成：

```
Game.unity
├── GameRoot（手动：空物体挂 GameRoot 组件）
├── UIRoot（手动：Canvas + CanvasScaler + GraphicRaycaster + UIRoot 组件，下挂 Bg/Common/PopUI/CanvasPanel 四层）
├── EventSystem（手动：挂 InputSystemUIInputModule，注意：新输入系统专用模块）
└── GameRoot 运行时子节点：
    ├── [运行时] Main Camera（挂 CameraFollow）
    ├── [运行时] Directional Light
    ├── [运行时] BattleRoot（战斗容器，整个可销毁重建）
    │   ├── Ground（Plane 缩放至 100×100）
    │   ├── BoundaryWall ×4（可视边界，细长 Cube）
    │   ├── Player（Capsule + 朝向指示器）
    │   ├── EnemyRoot（敌人父节点）
    │   ├── PickupRoot（水晶父节点）
    │   └── BulletRoot（弹幕父节点，后续扩展）
    └── [运行时] MenuCamera/背景等（阶段四按需）
```

### 2.4 输入系统设计（GameInput）

纯代码创建 Action，不依赖 .inputactions 资产。静态类 `GameInput`，由 `GameRoot` 初始化并全局 Enable。

| Action | 类型 | 绑定 | 用途 |
|--------|------|------|------|
| Move | Vector2 | WASD 2DVector 复合 | 八方向移动 |
| MousePosition | Vector2 | `<Mouse>/position` | 鼠标朝向 |
| Attack | Button | 鼠标左键 | 攻击（支持长按，用 `started/canceled` 判定按住状态） |
| Skill1 | Button | Space | 主动技能 1 |
| Skill2 | Button | E | 主动技能 2 |
| SwitchSlot | Button ×9 | 数字键 1-9 | 直接选中武器格 |
| ScrollWeapon | Vector2 | `<Mouse>/scroll` | 滚轮切武器（读 y 正负） |
| Pause | Button | ESC | 暂停菜单 |
| BuildView | Button | Tab | 查看当前 Build |
| DebugDamage | Button | K | 调试：扣血 10 |
| DebugHeal | Button | H | 调试：回血 10 |
| DebugExp | Button | L | 调试：经验直接加满（阶段三启用） |

约定：
- 调试键回调用 `#if UNITY_EDITOR || DEVELOPMENT_BUILD` 包裹，正式包不生效。
- 实体不直接订阅 Action 回调做每帧逻辑；每帧逻辑在 `Update` 中用 `GameInput.Move.ReadValue<Vector2>()` 轮询，保证暂停（timeScale=0）时行为一致。

### 2.5 相机设计

| 参数 | 值 | 说明 |
|------|-----|------|
| 投射方式 | 透视（Perspective） | FOV 50 |
| 偏移 | (0, 12, -9) | 玩家后上方，约 53° 俯角（斜 45° 观感，略陡利于看清弹道） |
| 旋转 | x = 53°, y = 0 | 由 `LookRotation(target - pos)` 计算，不手写角度 |
| 跟随 | `LateUpdate` + `Vector3.SmoothDamp` | smoothTime = 0.15s |
| 注视点 | 玩家位置 | 阶段一不做鼠标偏移牵引，保持简单 |

`CameraFollow` 为普通 MonoBehaviour（不是 Controller，不参与架构），由 GameRoot 创建并指定目标。

### 2.6 鼠标朝向实现

不用物理射线（避免依赖地面碰撞体），直接数学平面求交：

```csharp
// 每帧：相机射线与地面 y=0 平面求交点，玩家朝向该点
var ray = cam.ScreenPointToRay(GameInput.MousePosition.ReadValue<Vector2>());
var plane = new Plane(Vector3.up, Vector3.zero);
if (plane.Raycast(ray, out float enter))
{
    var hit = ray.GetPoint(enter);
    var dir = hit - transform.position; dir.y = 0;
    if (dir.sqrMagnitude > 0.001f) transform.rotation = Quaternion.LookRotation(dir);
}
```

### 2.7 移动与碰撞约定（Transform 方案）

- 所有可动实体（玩家/敌人/水晶）移动一律 `transform.position += dir * speed * dt`。
- 为获得 `OnTriggerEnter/Stay` 物理事件：实体挂 `Rigidbody`（**isKinematic = true**，freezeRotation 全勾）+ `Collider`（isTrigger 按需）。
- 伤害判定用 Trigger 触发器；不指望物理推挤（击退效果后续用手动位移实现）。
- 玩家位置每帧 Clamp 在 ±49（100×100 地图留 1m 边距）。

### 2.8 对象池设计（GameObjectPoolSystem）

基于 QFramework PoolKit 的 `SimpleObjectPool<GameObject>` 封装为 System，统一管理高频对象：

```csharp
public interface IGameObjectPoolSystem : ISystem
{
    GameObject Spawn(string poolKey);            // 取出（无则调工厂新建）
    void Recycle(string poolKey, GameObject go); // 归还（Deactivate + 入池）
}
```

- 每种对象注册一个工厂（`Func<GameObject>`，纯代码拼占位几何体）+ 初始预填数量。
- 池键约定：`"Enemy_Slime"`、`"Pickup_ExpCrystal"`、后续 `"Bullet_Arrow"` 等。
- 归还时重置位置/父节点/激活状态；实体脚本提供 `OnSpawn()` / `OnRecycle()` 复位钩子（重置 HP 等运行时状态）。
- 预填量：敌人 30、水晶 50（阶段三）。

### 2.9 UI 框架约定

- 面板做成 **prefab**：prefab 统一放 `Assets/Resources/UI/` 下，命名与面板类名一致（如 `GameHUD.prefab`）；面板脚本（继承 `UIPanel`）挂 prefab 根节点。
- **控件引用走 QF 代码生成**：需要在代码里访问的子物体挂 `Bind` 组件（Add Component → QFramework/CodeGenKit/Bind），选中面板（prefab 内或场景中实例）点 Inspector 底部"生成代码"——自动在 `Scripts/Game/UI/` 生成/重写 `XxxPanel.Designer.cs`（partial 类声明 `[SerializeField]` 字段，字段名 = 挂 Bind 的 GameObject 名，类型自动识别），编译后自动给 prefab 挂面板脚本并赋好引用。逻辑写在 `XxxPanel.cs`（**仅首次不存在时生成**，之后不被覆盖）；Designer 文件与字段**禁止手改**。
- 面板脚本位于 `namespace Game.UI`（QF 生成器强制要求命名空间，配置存于 `Assets/QFrameworkData/ProjectConfig/ProjectConfig.json`；决策 #5 的"全局命名空间"对面板脚本例外）；每个面板配一个 `XxxPanelData : UIPanelData`。
- `GameUIKitConfig`（继承 `UIKitConfig`）只做路径映射：`PanelType.Name → "UI/" + 类名`，复用默认 `Resources.Load` 加载链；调用侧保持 `UIKit.OpenPanel<T>()`。
- 场景画布：`UIRoot`（挂 QFramework `UIRoot` 组件）手工建在场景中，Canvas 用 **Screen Space - Overlay**；`UIRoot.Instance` 会 `FindObjectOfType` 自动采纳场景画布，GameRoot 不创建 UI。
- CanvasScaler：Scale With Screen Size，参考分辨率 1920×1080，Match Width Or Height = 0.5。
- 面板在 `OnInit` 订阅 BindableProperty（`RegisterWithInitValue` + `UnRegisterWhenGameObjectDestroyed`）、`OnClose` 清理，不再代码搭建控件（遵守核心约束第 5 条）。

### 2.10 存档设计（SaveManager，阶段四）

静态类封装 PlayerPrefs，key 统一管理，预留 `MigrateToJson()` 接口：

| Key | 类型 | 说明 |
|-----|------|------|
| `SAVE_GOLD` | int | 金币总额 |
| `SAVE_UNLOCKED_LEVEL` | int | 已解锁到第几关（1 起） |
| `SAVE_TALENT_{节点id}` | int | 天赋节点等级（局外天赋扩展时启用） |
| `SAVE_FIRST_PLAYED` | int | 是否首次游玩（预留，教学用） |

写入时机：结算时立即 `PlayerPrefs.Save()`；天赋加点后立即保存。

### 2.11 全局事件清单（TypeEventSystem）

| 事件 | 参数 | 发送方 | 订阅方 |
|------|------|--------|--------|
| `PlayerDiedEvent` | — | PlayerTakeDamageCommand | GameFlowSystem（阶段四）、HUD |
| `EnemyDiedEvent` | 敌人类型、死亡位置、经验值 | EnemyTakeDamageCommand | EnemySpawnSystem、水晶生成、HUD 击杀数 |
| `WaveStartedEvent` | 波次号 | EnemySpawnSystem | HUD |
| `WaveClearedEvent` | 波次号 | EnemySpawnSystem | HUD |
| `AllWavesClearedEvent` | — | EnemySpawnSystem | GameFlowSystem（阶段四判定胜利） |
| `LevelUpEvent` | 新等级 | GainExpCommand | UpgradeSystem（弹 3 选 1） |
| `WeaponSwitchedEvent` | 新格子索引 | WeaponSystem | WeaponBarHUD |
| `WeaponResourceChangedEvent` | 格子、当前/上限 | WeaponSystem | WeaponBarHUD |

> HP/经验/波次等**单值状态走 BindableProperty 订阅**，不走事件；事件只用于"发生的事"（死亡、升级、波次推进）。

### 2.12 目录结构（在 DevelopmentPlan 基础上补充）

```
Game/
├── GameArchitecture.cs
├── GameRoot.cs
├── Config/
│   ├── EnemyConfigTable.cs      # 敌人参数静态表（阶段二）
│   ├── WaveConfigTable.cs       # 波次表静态数据（阶段二）
│   ├── WeaponConfigTable.cs     # 武器数值表（阶段三）
│   └── PassiveConfigTable.cs    # 被动数值表（阶段三）
├── Input/
│   └── GameInput.cs             # Input System 封装（阶段一）
├── Pool/
│   └── GameObjectPoolSystem.cs  # 对象池（阶段二）
├── Save/
│   └── SaveManager.cs           # PlayerPrefs 封装（阶段四）
├── Utility/
│   └── GameUIKitConfig.cs       # UIKit 配置：面板类名 → Resources/UI/ 路径映射（阶段一）
├── Model/  System/  Command/  Query/  Entity/  View/   # 同 DevelopmentPlan
└── Entity/CameraFollow.cs       # 相机平滑跟随（阶段一）
```

---

## 三、阶段一详细设计：地基 + 玩家移动 + HUD

**目标**：跑通 QFramework 全流程——架构初始化、数据绑定、UI 自动刷新。

### 3.1 类清单

| 类 | 文件 | 职责 |
|----|------|------|
| `GameArchitecture` | GameArchitecture.cs | 注册 PlayerModel、GameStateModel |
| `GameRoot` | GameRoot.cs | 场景入口：初始化输入→架构→环境→相机→HUD→调试键 |
| `GameInput` | Input/GameInput.cs | 创建并持有全部 InputAction |
| `PlayerModel` | Model/PlayerModel.cs | HP/MaxHP/MoveSpeed/Level/Exp/ExpNeed |
| `GameStateModel` | Model/GameStateModel.cs | State、CurrentWave |
| `Player` | Entity/Player.cs | WASD 移动、鼠标朝向、边界 Clamp |
| `CameraFollow` | Entity/CameraFollow.cs | SmoothDamp 跟随 |
| `PlayerTakeDamageCommand` | Command/ | 扣血（阶段一供调试键用，阶段二接碰撞） |
| `PlayerHealCommand` | Command/ | 回血（调试用） |
| `GameHUD` | View/GameHUD.cs | 血条 + 占位元素 |
| `UIFactory` / `UIColorPalette` | Utility/ | UI 控件工厂与色板 |

### 3.2 PlayerModel 字段（阶段一数值）

| 字段 | 类型 | 初始值 |
|------|------|--------|
| HP | `BindableProperty<int>` | 100 |
| MaxHP | `BindableProperty<int>` | 100 |
| MoveSpeed | `BindableProperty<float>` | 5 |
| Level | `BindableProperty<int>` | 1 |
| Exp | `BindableProperty<int>` | 0 |
| ExpNeed | `BindableProperty<int>` | 5 |

### 3.3 GameRoot 启动流程（Awake 顺序）

1. `GameInput.Init()` —— 创建所有 Action 并 Enable
2. 访问 `GameArchitecture.Interface` —— 触发架构初始化
3. `UIKit.Config = new GameUIKitConfig()` —— 面板按类名映射 Resources/UI/ 路径（EventSystem 与 UIRoot 画布已在场景中手工搭好）
4. 创建 Directional Light（旋转 50°/-30°，柔和阴影可选）
5. 创建 BattleRoot → Ground（Plane ×10 缩放 = 100×100）→ 4 面边界墙（Cube，高 1，贴边）
6. 创建 Player：Capsule（高 2）+ 子物体"朝向指示器"（小 Cube 位于正前方 1m）；挂 kinematic Rigidbody + CapsuleCollider(trigger)、`Player` 脚本
7. 创建 Main Camera（透视 FOV 50，tag MainCamera），挂 `CameraFollow` 并设目标
8. `UIKit.OpenPanel<GameHUD>()`
9. 注册调试键：K → `PlayerTakeDamageCommand(10)`；H → `PlayerHealCommand(10)`

### 3.4 Player 行为细节

- `Update`：读 `GameInput.Move` → 归一化方向 × MoveSpeed × dt 位移（Transform 直接位移）；位置 Clamp ±49。
- 鼠标朝向：按 2.6 节实现。
- 朝向指示器随父物体旋转即可（无需额外逻辑）。
- 阶段一不做：攻击、动画、受击反馈。

### 3.5 GameHUD 布局（1920×1080 参考系）

```
┌──────────────────────────────────────────┐
│ [HP 条 300×24]  [Lv.1 灰框]   [波次 -- 灰框] │  ← 左上角排布
│ [EXP 条 300×10 灰框]                         │
│                                            │
│              （游戏画面）                    │
│                                            │
│         [武器格 ×9 灰框占位]                 │  ← 底部居中（阶段二实装）
└──────────────────────────────────────────┘
```

- 血条：底层深灰 Image + 上层绿色 Image（filled 或手动改宽度），右侧叠 `100/100` 文本。
- 订阅：`PlayerModel.HP`、`MaxHP` 的 `RegisterWithInitValue`；EXP/波次灰框暂不订阅（占位）。
- 灰框统一 40% 透明度，标明"预留"。

### 3.6 数据链路验证（里程碑验收）

| 步骤 | 预期 |
|------|------|
| Play | 场景自动搭好：地面、边界墙、胶囊玩家、相机斜俯视跟随 |
| WASD | 玩家八方向移动，到边界被挡住；相机平滑跟随无抖动 |
| 移动鼠标 | 玩家与朝向指示器始终指向鼠标地面投影点 |
| 按 K | HP -10，HUD 血条与文本实时刷新 |
| 按 H | HP +10，不超过 MaxHP |
| 血归零 | HP Clamp 到 0（阶段一不触发死亡流程） |

---

## 四、阶段二详细设计：敌人 + 战斗闭环

**目标**：敌人追玩家、铁剑清怪、玩家掉血、波次自动推进、对象池上线。

### 4.1 新增类清单

| 类 | 职责 |
|----|------|
| `EnemyModel` | 存活敌人总数、击杀数（BindableProperty） |
| `Enemy` | 敌人实体：追击 AI、接触伤害、受击、死亡回收 |
| `EnemyConfigTable` | 敌人参数静态表（id → HP/移速/伤害/经验） |
| `WaveConfigTable` | 波次静态表（每波：敌人 id + 数量 + 生成间隔） |
| `EnemySpawnSystem` | 波次调度：开波 → 按间隔生成 → 全灭判定 → 下一波 |
| `GameObjectPoolSystem` | 对象池（见 2.8） |
| `WeaponSystem` | 武器持有列表、当前格子、切换（含耗尽自动切换） |
| `WeaponBase` | 武器抽象基类：资源（弹药/能量）、攻击、冷却/恢复 |
| `SwordWeapon` | 铁剑：面前扇形横扫 |
| `EnemyTakeDamageCommand` | 敌人受伤/死亡结算 |
| `WeaponBarHUD` | 底部 9 格武器栏：高亮当前格、资源量显示 |

### 4.2 敌人配置（EnemyConfigTable，静态字典）

| id | 名称 | HP | 移速 | 接触伤害 | 攻击间隔 | 经验值 | 占位表现 |
|----|------|-----|------|----------|----------|--------|----------|
| slime_green | 绿史莱姆 | 30 | 2.0 | 10 | 1s | 1 | 绿色 Sphere（压扁 0.7） |

> 阶段二只实装绿史莱姆一种 AI（直线追击近战）。红史莱姆冲锋、哥布林远程等作为后续扩展，但配置表结构一次到位（预留 `aiType`、`attackRange` 字段）。

### 4.3 波次表（WaveConfigTable）

```csharp
// 每波定义：敌人生成组列表 + 组内间隔；波次间间隔 3s
Wave 1: slime_green × 8,  生成间隔 1.0s
```

- 阶段二实装波次 1（对齐 DevelopmentPlan 验收）。
- 表结构支持多组混编：`List<SpawnGroup>`，`SpawnGroup { enemyId, count, interval }`，关卡一完整 4 波数据在后续扩展录入。

### 4.4 EnemySpawnSystem 逻辑

```
StartWave(i)
  → 发 WaveStartedEvent(i)
  → 协程/计时器：按 SpawnGroup 顺序，每 interval 从对象池 Spawn 一只
  → 生成位置：以玩家为圆心、半径 15~20 的圆环上随机取点（Clamp 进地图）
Update
  → 本波生成完毕 && 存活数 == 0 → 发 WaveClearedEvent → 3s 后 StartWave(i+1)
  → 已是最后一波 → 发 AllWavesClearedEvent
```

- 敌人死亡即时回收进池，`EnemyModel.AliveCount` 同步增减。
- System 的计时在 `OnUpdate`？QFramework System 无 Update——由 GameRoot 每帧驱动 `this.GetSystem<IEnemySpawnSystem>().Tick(dt)`，暂停时不调用。

### 4.5 Enemy 实体行为

- `OnSpawn()`：按 config 重置 HP、速度；订阅无。
- `Update`（BattleRoot 存活且 State==Playing 时）：朝玩家位置水平移动（Transform 位移，不穿模靠触发器，暂不实现敌人间避让——同屏数量小，重叠可接受，记入后续优化）。
- 接触玩家：`OnTriggerStay` 检测 Player 层 → 按攻击间隔（1s 冷却）发 `PlayerTakeDamageCommand(10)`。
- 受击：武器命中调用 `Enemy.TakeHit(damage)` → 发 `EnemyTakeDamageCommand` → HP<=0 时发 `EnemyDiedEvent`、击杀数+1、回池。
- 死亡反馈（占位）：瞬间销毁即可，特效后续补。

### 4.6 武器系统设计

**WeaponBase（抽象基类，非 MonoBehaviour 纯 C# 数据 + 逻辑）**：

| 成员 | 说明 |
|------|------|
| `Id / Name / Level` | 标识与等级（阶段三用） |
| `ResourceType` | 枚举 Ammo（弹药制）/ Energy（能量制） |
| `ResourceMax / Resource` | 上限与当前值 |
| `CostPerAttack` | 每次攻击消耗 |
| `RegenPerSec` | 能量制每秒恢复 |
| `RefillCooldown / CooldownTimer` | 弹药制耗尽后的回满冷却 |
| `State` | Ready / Refilling |
| `TryAttack(origin, dir)` | 检查资源 → 扣资源 → 执行 `DoAttack()`（子类实现） |
| `Tick(dt)` | 能量恢复 / 弹药回满计时 |

- **SwordWeapon（W1 铁剑）**：能量 100 / 消耗 30 / 每秒恢复 30；攻击 = 以玩家正前方 90° 扇形、半径 2.5m，用 `Physics.OverlapSphere` + 角度过滤取敌人（敌人 Collider 非 trigger 的受击体，或单独 HitBox），对每个调用 `TakeHit(20)`。攻击间隔 0.4s（防每帧连发）。占位表现：攻击瞬间在面前生成一个半透扇形/方块闪 0.1s 后回池。
- **WeaponSystem**：持有 `List<WeaponBase>`（≤9）、`CurrentIndex`；`Tick(dt)` 驱动所有武器；输入切枪（1-9/滚轮）→ 发 `WeaponSwitchedEvent`；当前武器资源耗尽且无法攻击 → 自动切到下一个 Ready 武器；全部不可用 → 等待恢复（HUD 提示）。
- **Player 攻击输入**：`Attack` Action 按住期间，每帧 `WeaponSystem.TryAttackCurrent()`（内部有攻击间隔限制）。

### 4.7 HUD 更新

- 波次灰框实装：订阅 `GameStateModel.CurrentWave`；开波时闪一下"第 N 波"大字（简单协程渐隐）。
- 击杀数文本：订阅 `EnemyModel.KillCount`。
- WeaponBarHUD：9 格横排；每格 = 底框 + 武器名首字 + 资源条；当前格高亮描边；订阅 `WeaponSwitchedEvent` / `WeaponResourceChangedEvent`；格子为空时灰色。

### 4.8 阶段二验收

| 步骤 | 预期 |
|------|------|
| Play 数秒后 | 第 1 波 8 只绿史莱姆从四周生成并追击玩家 |
| 左键点击/长按 | 铁剑扇形横扫，命中敌人 2 下击杀（HP30/伤20），能量按 30/次消耗并以 30/s 恢复 |
| 敌人贴脸 | 玩家每秒掉 10 血，血条实时下降 |
| 全灭一波 | 3 秒提示后自动开下一波（阶段二仅 1 波则显示 AllWavesCleared 日志） |
| 观察 | 敌人从对象池复用（池统计日志）、无 GC 突刺报错 |

---

## 五、阶段三详细设计：经验 + 升级系统

**目标**：杀怪 → 掉水晶 → 吸附拾取 → 升级 3 选 1 → 变强；武器/被动数据化。

### 5.1 新增类清单

| 类 | 职责 |
|----|------|
| `ExperienceCrystal` | 水晶实体：吸附飞行、拾取判定 |
| `GainExpCommand` | 加经验、处理连续升级、发 LevelUpEvent |
| `WeaponModel` | 已持有武器/被动快照（供 UI 与升级池查询） |
| `PassiveModel` | 已持有被动列表与等级、聚合属性加成 |
| `WeaponConfigTable` | 10 武器 × 5 级数值表（阶段三先录 W1 铁剑 5 级 + 占位结构） |
| `PassiveConfigTable` | 8 被动 × 5 级效果表 |
| `UpgradeSystem` | 生成 3 选 1 候选、应用选择 |
| `LevelUpPanel` | 3 选 1 面板 |
| `PassiveBase` | 被动基类：应用/移除属性修正 |

### 5.2 经验水晶

- 生成：`EnemyDiedEvent` 携带死亡位置与经验值 → 从对象池 `Spawn("Pickup_ExpCrystal")` 放到该位置。占位：蓝色小八面体/Sphere，挂 trigger Collider。
- 吸附：玩家进入 3m 半径 → 水晶进入吸附态，以 12 m/s 加速飞向玩家（Transform 位移）；接触（0.5m）→ 发 `GainExpCommand(exp)` → 回池。
- 磁铁被动（P5）后续按百分比放大 3m 基础半径——吸附半径从 `PassiveModel.PickupRangeMultiplier` 读取，阶段三先恒为 1。

### 5.3 经验与升级

- 经验曲线：`ExpNeed(level) = 5 + (level - 1) * 4`（Lv1→5，Lv2→9，Lv3→13 …线性，割草前期节奏快）。
- `GainExpCommand`：Exp += 值；`while (Exp >= ExpNeed)` 连续升级：升级后 Exp 减去消耗、ExpNeed 重算、发 `LevelUpEvent(newLevel)`。
- 升级暂停：弹出 3 选 1 期间 `Time.timeScale = 0`（打开面板时置 0，选择后恢复 1），连续升级时面板逐次弹出（队列）。

### 5.4 升级候选生成（UpgradeSystem.RollChoices）

候选池构建规则：
1. 已有武器且 <5 级 → "升级 {武器} 到 Lv.N+1"
2. 武器栏 <9 → 从未持有武器池随机 1~2 个新武器
3. 已有被动且 <5 级 → "升级 {被动}"
4. 被动 <6 个 → 新被动
5. 兜底：消耗品（烤鸡：回 50% 血）

从池中带权重随机取 3 个**不重复**候选：已有升级权重 3、新武器 2、新被动 2、消耗品 1。池不足 3 个时以消耗品补齐。

### 5.5 武器数值表结构（WeaponConfigTable）

```csharp
// 每武器一条曲线：按等级索引
WeaponConfig {
  id, name, resourceType,
  int[]   damage,        // [Lv1..Lv5]
  float[] attackInterval,
  int[]   resourceMax,
  float[] costOrDrain,
  float[] regenOrRefill,
}
```

阶段三录入：W1 铁剑 5 级完整数值（伤害 20/26/34/44/58，间隔 0.4→0.3s，能量上限 100→120，消耗/恢复不变）；其余 9 把武器只录 Lv1 占位，后续扩展补全。

### 5.6 被动系统

- `PassiveBase`：`Apply(PassiveModel)` / `Remove()`，内部修改对应 Model 的乘区/加区字段（如移速加成系数）。
- `PassiveModel` 聚合对外暴露只读系数：`MoveSpeedMultiplier`、`DamageMultiplier`、`PickupRangeMultiplier`、`ExpGainMultiplier`、`MaxHPMultiplier` 等；被动升级时重算。
- 玩家实际移速 = 基础 × `MoveSpeedMultiplier`（Player 每帧读，或订阅变化缓存）。
- 阶段三实装 P7 疾跑鞋、P8 生命护符两个（影响直观、易验证），其余 6 个录配置占位。

### 5.7 LevelUpPanel

- 布局：屏幕中央半透明黑底 + 3 张竖向卡片横排；卡片 = 图标占位色块 + 名称 + 描述 + "Lv.x → Lv.y"。
- 打开时 timeScale=0；点击卡片 → `UpgradeSystem.Apply(choice)` → 关面板恢复 timeScale；队列中还有升级则立刻再开。
- HUD 经验条/等级实装：订阅 Exp/ExpNeed/Level。

### 5.8 阶段三验收

| 步骤 | 预期 |
|------|------|
| 击杀史莱姆 | 掉落蓝水晶，走近 3m 内水晶飞来，拾取后经验条增长 |
| 经验满 | 时间暂停，弹出 3 选 1；连续溢出经验连续升级 |
| 选"升级铁剑" | 铁剑伤害/射速提升（打怪由 2 下变 1~2 下可感知） |
| 选"疾跑鞋" | 移速明显变快 |
| 按 L | 经验直接满，弹面板（调试链路） |

---

## 六、阶段四详细设计：完整流程 UI

**目标**：主菜单 → 选角 → 选关 → 战斗 → 结算 → 主菜单，可循环游玩，金币跨局保留。

### 6.1 游戏流程状态机（GameStateModel.State 扩展）

```
Boot → MainMenu → CharacterSelect → LevelSelect → Playing ⇄ Paused
                                                      ↓
                                            Result(Victory/GameOver) → MainMenu
```

- `GameFlowSystem` 负责状态迁移与各状态的进场/清场：
  - 进 MainMenu：销毁 BattleRoot（若存在）→ 打开 MainMenuPanel。
  - 选角/选关：写入 `MetaModel.SelectedCharacter / SelectedLevel` → 进 Playing：重建 BattleRoot（地面/边界/玩家/刷怪系统 Reset）→ 开 GameHUD。
  - Victory/GameOver：停生成、清敌人 → 结算 → ResultPanel → 回 MainMenu。
- 单场景方案下"重开一局"= 销毁并重建 BattleRoot + 重置所有局内 Model（PlayerModel.Reset() 等），**不重新加载场景**，保证切换速度。

### 6.2 MetaModel（局外数据）

| 字段 | 类型 | 说明 |
|------|------|------|
| Gold | `BindableProperty<int>` | 金币（读档初始化） |
| UnlockedLevel | `BindableProperty<int>` | 已解锁关卡数 |
| SelectedCharacter | int | 当前选中角色索引（阶段四只有剑士可用，其余灰色"敬请期待"） |
| SelectedLevel | int | 当前选中关卡（只有关卡一） |

初始化时通过 SaveManager 读档；变更时写档。

### 6.3 各面板详细布局

**MainMenuPanel**：居中标题文本（游戏名）+ 纵向按钮组：开始游戏 / 天赋（灰色置灰"敬请期待"）/ 退出（Editor 下 Stop）。底部显示金币数（订阅 MetaModel.Gold）。

**CharacterSelectPanel**：4 张角色卡片横排（剑士可点，其余置灰）；卡片 = 色块 + 角色名 + 一句话定位；底部"确认/返回"。

**LevelSelectPanel**：2 张关卡卡片（关卡一可点，关卡二置灰锁形图标）；显示通关奖励；底部"出发/返回"。

**PausePanel**：ESC 打开（timeScale=0）：继续 / 重新开始 / 返回主菜单；附当前 Build 简表（武器+被动列表文本）。Tab 的 Build 查看并入此面板。

**ResultPanel**：胜利/失败标题 + 本局统计（击杀数、等级、存活时间）+ 金币奖励数值 + "返回主菜单"。

### 6.4 暂停实现

- `PausePanel` 打开：`Time.timeScale = 0`；关闭恢复 1。
- 所有 Update 驱动的逻辑（Player/Enemy/SpawnSystem.Tick）天然被 dt=0 冻结；输入轮询不受影响，ESC 可正常关闭面板。
- 暂停时禁止攻击/切枪：WeaponSystem.Tick 由 GameRoot 驱动，暂停不调用。

### 6.5 结算与金币

- 胜利条件：收到 `AllWavesClearedEvent`（阶段四波次表仍是 1 波，可打通；完整 4 波 + Boss 在后续扩展）。
- 失败条件：HP<=0（PlayerTakeDamageCommand 内判定，发 PlayerDiedEvent）。
- 金币公式：`基础通关奖 500（胜利）/ 击杀数 × 5（失败保底）`；写入 `SAVE_GOLD` 并 `PlayerPrefs.Save()`。
- 首次通关关卡一：写 `SAVE_UNLOCKED_LEVEL = 2`（解锁位先写上，关卡二内容后续填充）。

### 6.6 角色选择的落地方式

- 角色参数表 `CharacterConfigTable`：4 角色录全（初始 HP/移速/初始武器 id/被动修正），但选择界面只开放剑士。
- Player 重建时按选中角色读表初始化 Model——为后续开放角色零改动。

### 6.7 阶段四验收

| 步骤 | 预期 |
|------|------|
| 启动 | 进主菜单，点击流转：开始 → 选角 → 选关 → 进入战斗 |
| ESC | 暂停，画面冻结；继续/重开/回主菜单均正常 |
| 打通波次 | 弹胜利结算，金币 +500，回主菜单金币显示更新 |
| 战死 | 弹失败结算，按击杀给保底金币 |
| 重开一局 | 玩家满血、波次从 1 开始、经验归零；金币保留 |
| 关掉重进 Play | 金币与解锁进度仍在 |

---

## 七、全局数值总表

### 7.1 基础

| 项 | 值 |
|----|-----|
| 地图尺寸 | 100×100（可活动 ±49） |
| 相机 | 透视 FOV 50，偏移 (0,12,-9)，SmoothDamp 0.15s |
| 玩家基础 | HP 100，移速 5 |
| 经验曲线 | ExpNeed = 5 + (Lv-1) × 4 |
| 水晶吸附 | 半径 3m，飞行速度 12 m/s |

### 7.2 战斗（阶段二三实装部分）

| 项 | 值 |
|----|-----|
| 绿史莱姆 | HP 30 / 速 2.0 / 接触伤害 10 / 攻击间隔 1s / 经验 1 |
| 铁剑 Lv1 | 伤害 20 / 间隔 0.4s / 能量 100 / 耗 30 / 恢复 30/s / 90° 扇形半径 2.5m |
| 波次 1 | 绿史莱姆 ×8，间隔 1s，波次间隔 3s |
| 刷怪圈 | 玩家半径 15~20m 圆环随机 |

### 7.3 经济（阶段四）

| 项 | 值 |
|----|-----|
| 通关奖励 | 500 金币 |
| 失败保底 | 击杀数 × 5 |

> GameDesign.md 中的 10 武器 / 8 被动 / 2 关卡 / Boss / 天赋树 / 双人合作完整数值不在前四阶段范围内，按 DevelopmentPlan 的"后续扩展"顺序补入本表。

---

## 八、手工操作清单（更新版，替代 DevelopmentPlan 第七节）

| 时机 | 操作 |
|------|------|
| 阶段一开发前 | 1. Package Manager 安装 **Input System** 包（弹窗提示启用新输入系统并重启 Editor，选 Yes）<br>2. 新建场景 `Game.unity`<br>3. 场景中建空物体命名 GameRoot，挂 `GameRoot` 组件<br>4. 场景搭建 UIRoot 画布（Canvas=Overlay + UIRoot 组件 + Bg/Common/PopUI/CanvasPanel 四层节点拖引用）与 EventSystem（挂 InputSystemUIInputModule）<br>5. 制作 `GameHUD.prefab`（挂 GameHUD 脚本、拖好血条引用）放入 `Assets/Resources/UI/`<br>6. Build Settings 加入 Game.unity |
| 每阶段完成后 | Play 按本文档各阶段验收表逐项验证，报错发给我 |
| 接入美术时 | 占位物体保存为预制体替换工厂方法，或告诉我替换方式 |

---

## 九、风险与注意事项

1. **Input System 切换**：安装包后 Active Input Handling 变更会重启 Editor；若项目中有旧 `Input.GetAxis` 代码会编译报错（QFramework 自身不依赖旧输入，AudioKit/UIKit 无影响；UIKit 的 EventSystem 需配 `InputSystemUIInputModule`，已在 GameRoot 启动流程处理）。
2. **timeScale 暂停的一致性**：所有每帧逻辑必须使用 `Time.deltaTime` 且由受暂停控制的入口驱动；UI 动画（如"第 N 波"渐隐）用 `unscaledDeltaTime`。
3. **对象池复位**：任何从池中取出的实体必须走 `OnSpawn()` 完整重置，禁止残留上局状态（HP、订阅、协程）。
4. **Trigger 碰撞可靠性**：kinematic Rigidbody + Transform 位移下，双方至少一方有 Rigidbody 才能收到 Trigger 回调——玩家与敌人/水晶均按此约定配置。
5. **事件泄漏**：所有 `RegisterEvent` 返回的 `IUnRegister` 必须配 `UnRegisterWhenGameObjectDestroyed`（实体/面板）或在 Architecture 销毁时统一释放（Model/System 内部）。
