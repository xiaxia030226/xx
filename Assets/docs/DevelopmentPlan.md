# 割草 Roguelike 开发计划

> 本文件是 xx 项目后续开发的唯一总纲。开发严格按阶段推进，每阶段完成后对照验证标准确认效果。
> 玩法设计见同目录 `GameDesign.md`。

---

## 一、项目概述

**产品定位**：2.5D 卡通风格关卡制 Roguelike 割草动作游戏。PC 键鼠操作，4 角色 + 10 武器，首批 2 个关卡。

**核心循环**：

```
主菜单 → 选角色 → 选关卡 → 战斗（清理敌人波次 → 击败Boss）
                              ↓
                         通关奖励金币
                              ↓
                 金币解锁局外天赋 → 回到主菜单
```

**技术路线**：基于 QFramework 框架（已引入 `Assets\Scripts\QFramework\`），纯代码创建游戏物体与 UI，占位几何体先行，美术素材后续替换。

---

## 二、技术架构（QFramework 分层）

### 各层职责

| 层 | 载体 | 职责 | 对应 MVC |
|----|------|------|----------|
| Architecture | `Architecture<T>` 子类 | 模块总管（单例），注册并分发 Model/System/Command | 容器 |
| Model | `AbstractModel` 子类 | 存数据，字段用 `BindableProperty<T>` 承载，变化自动通知 UI | M |
| System | `AbstractSystem` 子类 | 无状态业务逻辑（波次生成、武器管理） | 服务层 |
| Command | `AbstractCommand` 子类 | 写操作（扣血、加经验），唯一允许改数据的入口 | 命令 |
| Query | `AbstractQuery<T>` 子类 | 读操作（查询当前生命值等） | 查询 |
| Controller | MonoBehaviour + `IController` | Unity 与架构的桥梁（Player、Enemy 等实体脚本） | C |
| View | `UIPanel` 子类 | 界面展示，订阅 Model 的 BindableProperty 自动刷新 | V |
| Event | `TypeEventSystem` | 跨模块通知（如"敌人死亡"），注册后必须注销 | 事件总线 |

### 五条核心约束（所有游戏代码必须遵守）

1. 实体脚本（MonoBehaviour）实现 `IController`，只能通过 `this.GetModel<T>()`、`this.GetSystem<T>()`、`this.SendCommand<T>()` 访问架构能力
2. 数据只存在于 Model，外部不允许直接持有可变数据副本
3. 修改数据的逻辑必须封装成 Command，由 Command 统一发送事件通知
4. UI 只读数据（订阅 BindableProperty / 发 Query），不直接改数据
5. 事件注册必须返回 `IUnRegister` 并配合 `UnRegisterWhenGameObjectDestroyed` 注销，防止内存泄漏

### 工具包使用

- **UIKit**：所有界面继承 `UIPanel`，通过 `UIKit.OpenPanel<T>()` 打开，Canvas 由框架自动创建
- **AudioKit**：音效统一走 `AudioKit.PlaySound("名字")` / `PlayMusic("名字")`
- **ResKit**：美术资源接入后由 ResKit 管理加载

---

## 三、目录结构设计

全部游戏代码新建在 `Assets\Scripts\Game\` 下：

```
Game/
├── GameArchitecture.cs          # 模块总管，注册所有 Model/System
├── GameRoot.cs                  # 场景入口（唯一需手动挂到场景的组件）
├── Model/
│   ├── IPlayerModel.cs          # 玩家数据接口
│   ├── PlayerModel.cs           # HP、移速、等级、经验
│   ├── IGameStateModel.cs       # 游戏状态接口
│   ├── GameStateModel.cs        # 进行中/暂停/结束
│   ├── IEnemyModel.cs           # 敌人数据接口（阶段二）
│   └── EnemyModel.cs            # 敌人总表、击杀数
├── System/
│   ├── IEnemySpawnSystem.cs     # 波次生成接口（阶段二）
│   ├── EnemySpawnSystem.cs      # 波次调度与生成
│   ├── IWeaponSystem.cs         # 武器管理接口（阶段二）
│   └── WeaponSystem.cs          # 武器持有与升级
├── Command/
│   ├── PlayerTakeDamageCommand.cs   # 玩家受伤（阶段二）
│   ├── EnemyTakeDamageCommand.cs    # 敌人受伤（阶段二）
│   └── GainExpCommand.cs            # 获得经验（阶段三）
├── Query/
│   └── (按需新增，如查询当前武器伤害)
├── Entity/
│   ├── Player.cs                # 玩家：WASD 移动、朝向
│   ├── Enemy.cs                 # 敌人：追击 AI（阶段二）
│   ├── ExperienceCrystal.cs     # 经验水晶：吸附拾取（阶段三）
│   └── Weapons/
│       ├── WeaponBase.cs        # 武器抽象基类：攻击间隔、伤害来源
│       └── SwordWeapon.cs       # 铁剑：面前扇形横扫（阶段二）
└── View/
    ├── GameHUD.cs               # 战斗 HUD：血条、经验条、波次（阶段一）
    ├── LevelUpPanel.cs          # 升级 3 选 1（阶段三）
    ├── MainMenuPanel.cs         # 主菜单（阶段四）
    ├── CharacterSelectPanel.cs  # 选角色（阶段四）
    ├── LevelSelectPanel.cs      # 选关卡（阶段四）
    ├── PausePanel.cs            # 暂停菜单（阶段四）
    └── ResultPanel.cs           # 结算面板（阶段四）
```

---

## 四、核心模块设计

### GameArchitecture（模块总管）

```csharp
public class GameArchitecture : Architecture<GameArchitecture>
{
    protected override void Init()
    {
        RegisterModel<IPlayerModel>(new PlayerModel());
        RegisterModel<IGameStateModel>(new GameStateModel());
        // 阶段二追加：
        // RegisterModel<IEnemyModel>(new EnemyModel());
        // RegisterSystem<IEnemySpawnSystem>(new EnemySpawnSystem());
        // RegisterSystem<IWeaponSystem>(new WeaponSystem());
    }
}
```

访问方式：`GameArchitecture.Interface`（首次访问自动初始化）。

### PlayerModel（玩家数据）

| 字段 | 类型 | 说明 |
|------|------|------|
| HP | `BindableProperty<int>` | 当前生命，UI 订阅刷新 |
| MaxHP | `BindableProperty<int>` | 最大生命 |
| MoveSpeed | `BindableProperty<float>` | 移动速度（基础 5） |
| Level | `BindableProperty<int>` | 等级 |
| Exp | `BindableProperty<int>` | 当前经验 |
| ExpNeed | `BindableProperty<int>` | 升级所需经验 |

### GameStateModel（游戏状态）

| 字段 | 类型 | 说明 |
|------|------|------|
| State | `BindableProperty<GameState>` | 枚举：Playing / Paused / GameOver / Victory |
| CurrentWave | `BindableProperty<int>` | 当前波次 |

### 实体层协作方式

- **Player**（Controller）：每帧读输入移动；碰撞处理通过 `OnTriggerEnter` 收到伤害信号后发送 `PlayerTakeDamageCommand`
- **Enemy**（阶段二）：`Update` 中向玩家方向移动；由 `EnemySpawnSystem` 定时生成
- **WeaponBase**：持有攻击间隔、伤害数值，子类实现 `Attack()`；由 `WeaponSystem` 统一调度触发
- **ExperienceCrystal**（阶段三）：敌人死亡时由敌人发送事件触发生成；玩家靠近时加速吸附

### View 层清单

| 面板 | 打开时机 | 内容 |
|------|----------|------|
| GameHUD | 战斗开始 | 血条、经验条、当前波次（纯代码创建 UI 元素） |
| LevelUpPanel | 升级时 | 3 选 1（武器升级 / 新武器 / 被动） |
| MainMenuPanel | 启动 | 开始游戏、天赋入口 |
| CharacterSelectPanel | 主菜单 → 开始 | 4 角色选择 |
| LevelSelectPanel | 选完角色 | 关卡选择 |
| PausePanel | 按 ESC | 继续、返回主菜单 |
| ResultPanel | 通关/失败 | 结算奖励、返回 |

---

## 五、分阶段开发路线

### 阶段一：地基 + 玩家移动 + HUD（第一个里程碑）

**目标**：跑通 QFramework 全流程——架构初始化、数据绑定、UI 自动刷新。

1. `GameArchitecture.cs`：注册 PlayerModel、GameStateModel
2. `GameRoot.cs`：Awake 初始化架构；代码创建地面（Plane）、玩家（Cube 占位）、正交摄像机跟随逻辑
3. `Player.cs`：WASD 八方向移动
4. `GameHUD.cs`：`UIKit.OpenPanel<GameHUD>()` 打开；OnInit 中代码创建 HP 文本；订阅 `PlayerModel.HP` 变化自动刷新
5. 临时测试：按空格键发送扣血 Command，验证 HP → HUD 的整条数据链路

**验证**：Play 后方块随 WASD 移动；按空格 HP 减少且 HUD 文本实时变化；无报错。

### 阶段二：敌人 + 战斗闭环（第二个里程碑）

**目标**：形成"敌人追玩家、铁剑清怪、玩家掉血"的最小战斗循环。

1. `Enemy.cs`：Sphere 占位，向玩家直线追击；触碰玩家发送 `PlayerTakeDamageCommand`
2. `EnemyModel` + `EnemySpawnSystem`：按波次表定时生成（波次 1：8 只史莱姆参数）；全灭进入下一波
3. `SwordWeapon.cs`（铁剑）：自动攻击，每 1.5 秒对面前 120° 扇形内敌人造成伤害
4. `EnemyTakeDamageCommand`：敌人 HP 归零 → 销毁 + 击杀数 +1 + 发送死亡事件
5. GameHUD 增加波次显示与敌人击杀数

**验证**：敌人持续生成并追击；铁剑能扫杀进入范围的敌人；敌人触碰后玩家血条下降；清空一波后自动刷下一波。

### 阶段三：经验 + 升级系统（第三个里程碑）

**目标**：形成"杀怪 → 拾取 → 升级 → 变强"的成长循环。

1. `ExperienceCrystal.cs`：敌人死亡掉落水晶；玩家靠近自动吸附；拾取发送 `GainExpCommand`
2. 经验满升级：升级时打开 `LevelUpPanel`（3 选 1：已有武器升一级 / 随机新武器 / 被动增益）
3. `WeaponSystem` 数据化：武器列表、等级、伤害成长曲线
4. GameHUD 增加经验条与等级显示

**验证**：杀怪掉水晶，靠近拾取；经验条满后弹出 3 选 1；选择后武器伤害/属性提升可感知。

### 阶段四：完整流程 UI（第四个里程碑）

**目标**：拼齐"主菜单 → 选角 → 选关 → 战斗 → 结算 → 主菜单"的完整游戏流程。

1. `MainMenuPanel` / `CharacterSelectPanel` / `LevelSelectPanel`：纯代码 UI，选中的角色与关卡写入 Model
2. `PausePanel`：ESC 暂停（Time.timeScale = 0），继续/返回主菜单
3. `ResultPanel`：通关或死亡后显示金币奖励，金币用 PlayerPrefs 存档
4. 结算金币 → 回主菜单可再次开局

**验证**：全流程无报错可循环游玩；重开一局数据正确重置；金币跨局保留。

### 后续扩展（前四阶段跑通后按序加入）

- 武器扩充：W2 长枪 → W10 圣光（设计文档 10 武器）
- 关卡二：幽暗森林（新波次表 + 新敌人 AI）
- Boss 战：史莱姆王、远古树精（技能循环）
- 局外天赋树（金币消耗、逐层解锁）
- 双人合作模式（第二输入、镜头自适应）

---

## 六、阶段验证标准汇总

| 阶段 | 验收一句话标准 |
|------|----------------|
| 一 | WASD 移动 + HUD 实时显示 HP，数据链路完整 |
| 二 | 敌人追击、铁剑扫杀、玩家掉血，波次自动推进 |
| 三 | 杀怪 → 吸水晶 → 升级 3 选 1 → 变强，成长闭环成立 |
| 四 | 完整流程可循环游玩，金币跨局保留 |

---

## 七、手工操作清单（需要你在 Unity 编辑器完成的事）

| 阶段 | 操作 |
|------|------|
| 阶段一开发前 | 1. 新建场景 `Game.unity`（与 Loading.unity 同级）<br>2. 场景中创建空物体，挂上 `GameRoot` 组件<br>3. File → Build Settings 将 Game.unity 加入场景列表 |
| 每阶段完成后 | Play 验证，把报错信息发给我 |
| 接入美术时 | 把代码创建的占位物体在编辑器里保存为预制体，或告诉我替换方式 |

> 除上述操作外，其余全部由代码完成，无需在编辑器手动创建物体。

---

## 八、代码规范约定

1. **命名**：Model 接口以 `I` 开头（如 `IPlayerModel`），实现类去掉 `I`；面板以 `Panel` 结尾；Command 以 `Command` 结尾
2. **创建物体**：游戏物体、UI 全部纯代码创建（占位几何体 + 代码 UI），不依赖场景预摆放
3. **数据流**：单向——输入/碰撞 → Command → Model 变更 → 事件通知 → View 刷新
4. **注释**：每个类头部一行注释说明职责；关键逻辑（如攻击范围计算）加注释说明思路
5. **日志**：用 `Debug.Log`，临时调试日志标记 `[Debug]` 前缀，验证通过后删除
6. **数值**：阶段二之前数值先硬编码在 Model/System 中，后续再考虑 ScriptableObject 配置化
