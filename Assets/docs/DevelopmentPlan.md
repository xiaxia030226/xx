# 割草 Roguelike 开发计划

> 本文件是 xx 项目后续开发的唯一总纲。开发严格按阶段推进，每阶段完成后对照验证标准确认效果。
> 玩法设计见同目录 `GameDesign.md`（摸金掉落经济版）；`DesignDetails.md` 为旧设计，已过期待重写。

---

## 一、项目概述

**产品定位**：2.5D 卡通风格关卡制 Roguelike 割草动作游戏。PC 键鼠操作，**单角色纯枪械**，无局内升级——局内构筑靠摸金掉落（整枪 / 配件 / 护盾 / 子弹 / 金币），局末结算售卖战利品换金币，金币投入局外天赋。首批 5 个关卡分批制作。

**核心循环**：

```
主菜单 → 选关卡 → 战斗（定时波次 + F 提前召唤 → 摸金掉落构筑 → 击败Boss）
                        ↓
              结算：战利品售卖 + 通关奖励 → 金币
                        ↓
           金币点局外天赋 → 回到主菜单
```

**技术路线**：

- QFramework 框架（`Assets\Scripts\QFramework\`）：Architecture/Model/System/Command 分层，UIKit 管理面板，PoolKit 对象池
- Input System 1.7.0：纯代码创建 InputAction（无 .inputactions 资产），见 `Input/GameInput.cs`
- 配置数据一律走 **ScriptableObject + Resources 懒加载静态配置表** 惯例（见 `Config/` 下各 ConfigTable）
- 占位几何体先行，美术素材后续替换；UI 走 prefab + Bind 组件生成 Designer 代码的 UIKit 流程

---

## 二、技术架构（QFramework 分层）

### 各层职责

| 层 | 载体 | 职责 | 对应 MVC |
|----|------|------|----------|
| Architecture | `Architecture<T>` 子类 | 模块总管（单例），注册并分发 Model/System/Command | 容器 |
| Model | `AbstractModel` 子类 | 存数据，字段用 `BindableProperty<T>` 承载，变化自动通知 UI | M |
| System | `AbstractSystem` 子类 | 无状态业务逻辑（波次生成、武器管理）；**无 Update，由 GameRoot 手动 Tick(dt)** | 服务层 |
| Command | `AbstractCommand` 子类 | 写操作（扣血、加子弹），唯一允许改数据的入口 | 命令 |
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

- **UIKit**：所有界面继承 `UIPanel`，通过 `UIKit.OpenPanel<T>()` 打开；控件绑定走 prefab 挂 Bind 组件 → 生成 `XxxPanel.Designer.cs`（禁手改）→ 逻辑写 `XxxPanel.cs`
- **PoolKit**：`SimpleObjectPool<GameObject>` 管理敌人 / 子弹 / 掉落物，预热后复用
- **AudioKit / ResKit**：美术资源接入后启用

---

## 三、目录结构设计

全部游戏代码在 `Assets\Scripts\Game\` 下（无 namespace；UI 面板用 `Game.UI`）：

```
Game/
├── GameArchitecture.cs          # 模块总管，注册所有 Model/System
├── GameRoot.cs                  # 战斗场景入口（+ GameRoot.Environment.cs 环境搭建 partial）
├── Input/
│   └── GameInput.cs             # 纯代码 InputAction 集中定义
├── Event/
│   └── BattleEvents.cs          # 全部战斗事件 struct
├── Model/
│   ├── PlayerModel.cs           # HP、移速
│   ├── GameStateModel.cs        # 游戏状态、当前波次、提前召唤倍率
│   ├── EnemyModel.cs            # 存活数、击杀数
│   ├── EconomyModel.cs          # 金币（PlayerPrefs 存档）+ 本局入账 RunGold
│   └── BulletInventoryModel.cs  # 子弹库存：（口径, 穿甲等级）→ 数量
├── System/
│   ├── EnemySpawnSystem.cs      # 定时波次调度 + F 提前召唤 + 下一波预告
│   └── WeaponSystem.cs          # 武器槽位（可空）、切枪、换弹驱动、按口径子弹池
├── Command/
│   ├── PlayerTakeDamageCommand.cs
│   ├── EnemyTakeDamageCommand.cs
│   ├── AddGoldCommand.cs
│   ├── AddBulletsCommand.cs     # 子弹入库
│   └── TakeBulletsCommand.cs    # 换弹预扣（同步执行读回执）
├── Entity/
│   ├── Player.cs                # WASD 移动、鼠标朝向、攻击/切枪/换弹输入接线
│   ├── Enemy.cs                 # 追击 AI、死亡掉落掷点
│   ├── Bullet.cs                # 子弹飞行与命中
│   ├── GoldPickup.cs            # 金币掉落物：磁吸拾取（代码建占位几何体入池）
│   ├── AmmoPackPickup.cs        # 子弹包掉落物：磁吸拾取
│   └── Weapons/
│       ├── WeaponBase.cs        # 武器抽象基类：耐久、冷却、攻击逻辑
│       └── GunWeapon.cs         # 枪械：弹夹（等级+数量）、换弹状态机、磨损
├── Config/
│   ├── WeaponConfig.cs / WeaponConfigTable.cs      # 枪械 SO + 查询表
│   ├── BulletConfig.cs / BulletConfigTable.cs      # 子弹 SO + 查询表（口径×等级）
│   ├── EnemyConfig.cs / EnemyConfigTable.cs        # 敌人 SO + 查询表（含掉落字段）
│   ├── WaveConfigTable.cs       # 波次表（代码配置）：定时启动、生成组
│   └── AmmoTypes.cs             # 口径枚举 + 等级倍率/磨损系数表
├── Editor/
│   └── BulletAssetGenerator.cs  # MenuItem 一键生成 18 个子弹 SO 资产
└── UI/                          # namespace Game.UI，prefab + Bind 生成 Designer
    ├── GameHUD.cs               # 血条、武器格子、波次倒计时/预告、金币、子弹库存
    ├── GameHUD/WeaponBarSlot.cs # 武器格子：弹量/等级/耐久/换弹置灰
    ├── MainMenuPanel.cs / LevelSelectPanel.cs
    ├── PausePanel.cs / ResultPanel.cs   # 结算：掉落 + 通关奖励
    └── BuildViewPanel.cs        # 装配查看（Tab，阶段四新增）
```

---

## 四、核心模块设计

### GameArchitecture（模块总管）

注册全部 Model/System，访问方式 `GameArchitecture.Interface`（首次访问自动初始化）。当前注册清单：PlayerModel、GameStateModel、EnemyModel、EconomyModel、BulletInventoryModel；EnemySpawnSystem、WeaponSystem。

### 关键 Model 字段

| Model | 字段 | 说明 |
|-------|------|------|
| PlayerModel | HP / MaxHP / MoveSpeed | 无经验、无等级（新设计已删除局内升级） |
| GameStateModel | State / CurrentWave / SummonMultiplier | SummonMultiplier：F 提前召唤掉落倍率（1.0 基准） |
| EconomyModel | Gold / RunGold | Gold 落盘存档；RunGold 本局掉落入账，每局重置 |
| BulletInventoryModel | 字典 (Caliber, Level) → 数量 | 初始 S·0 级 ×60；变更发 BulletInventoryChangedEvent |
| EnemyModel | AliveCount / KillCount | 每局重置 |

### 枪械核心规则（GameDesign 对齐）

- **弹夹制**：每把枪独立记录（已装子弹等级 + 弹量 + 下次装填等级）；打空自动换弹，**不自动切枪**
- **R** 主动换弹、**B** 循环切换下次装填的穿甲等级（0~5），各枪独立记忆；换弹在后台继续（切枪不中断）
- **换弹预扣**：换弹开始时按选定等级从库存预扣子弹，完成时装入弹夹；库存不足部分装填，无弹发缺弹事件
- **伤害** = 枪械基础伤害 × 等级倍率（1.0/1.0/1.1/1.2/1.3/1.4）×（0 级肉弹对无护盾目标 ×1.5）
- **耐久**：每次射击磨损 1 × 等级磨损系数（1.00/0.95/0.90/0.85/0.80/0.75）；≤20% 警示，归零报废腾格（槽位置 null），不自动切枪
- **子弹口径**：S / AR / L 三类，每类 0~5 穿甲等级共 18 种；同口径共享子弹 prefab 对象池

### 波次核心规则（定时制）

- 每波按 `IntervalFromPrev`（距上一波实际启动的秒数）到点必刷，**残兵不清也叠加**
- **F** 提前召唤下一波，本波掉落倍率 1.5 → 1.75 → 2.0 递增；自然到点重置 1.0
- 最后一波生成完且存活数归零 → AllWavesClearedEvent → 结算
- EnemySpawnSystem 暴露只读 `NextWaveCountdown` / `NextWavePreview`（种类+数量）供 HUD 轮询

### 掉落与结算

- 敌人配置携带掉落字段：金币区间、子弹包概率/等级区间/数量区间；死亡时按配置 × 召唤倍率掷点（金币取整、概率封顶 1）
- 掉落物（金币 / 子弹包）为代码创建的占位几何体，磁吸拾取入账（AddGoldCommand → RunGold / AddBulletsCommand → 库存）
- 结算公式：**总金币 = 本局掉落入账 + 通关奖励（胜 500 / 败 0）**

### View 层清单

| 面板 | 打开时机 | 内容 |
|------|----------|------|
| GameHUD | 战斗开始 | 血条、武器格子（弹量/等级/耐久）、下一波倒计时与预告、召唤倍率、本局金币、子弹库存 |
| BuildViewPanel | 按 Tab | 当前装配查看 |
| MainMenuPanel | 主菜单场景 | 开始游戏、退出 |
| LevelSelectPanel | 主菜单 → 开始 | 关卡选择 |
| PausePanel | 按 ESC | 继续、返回主菜单 |
| ResultPanel | 通关/失败 | 掉落 X + 通关奖励 Y = 合计 Z |

---

## 五、分阶段开发路线

### 阶段一：QFramework 地基 + 战斗流程闭环（✅ 已完成）

双场景流程（主菜单/选关/暂停/结算）、玩家移动射击、手枪+机枪、单一史莱姆、对象池、配置表惯例、GameHUD 血条与武器格子。

> 注：该阶段曾实现"经验水晶 → 升级三选一"与"清场制波次"，属旧设计，已在阶段二移除/重做。

### 阶段二：新设计对齐改造（🔄 本轮）

**目标**：删掉/重做已实现但与本版 GameDesign 冲突的功能，经济底座换轨到摸金掉落。

1. 删除局内升级：经验水晶、GainExpCommand、LevelUpPanel、PlayerModel 经验字段
2. 波次改定时制 + F 提前召唤（倍率 1.5/1.75/2.0，自然到点重置 1.0）+ 下一波倒计时/预告数据
3. 武器重做：删自动切枪；R 主动换弹、B 切穿甲等级（各枪独立）；换弹预扣库存、后台继续；耐久磨损→警示→报废腾格
4. 子弹库存：BulletInventoryModel + 18 种子弹配置（口径×等级）+ Editor 一键生成资产
5. 掉落：金币 + 子弹包（磁吸拾取、占位几何体入池）；敌人配置掉落字段
6. 结算公式：掉落入账 + 通关奖励（胜 500 / 败 0）
7. GameHUD 扩展（依赖 prefab 手工步骤后实施）：波次倒计时/预告、金币、子弹库存、格子弹量/耐久/等级

**验证**：见文末 Play 验证清单。

### 阶段三：护盾系统 + 敌人 AI 扩展 + 关卡一完整化

**目标**：伤害分配表落地，关卡一（W1~W5 + B01）完整可玩。

1. 护盾系统：目标护盾层级（0~3）与伤害分配表（穿甲等级 × 护盾层级 → 穿/卡/弹）；掉落新增护盾道具
2. 敌人 AI 扩展：E02~E08（冲锋、远程、自爆、治疗等 8 种行为）
3. 关卡一完整波次表 W1~W5 与 Boss B01；预告列表加护盾列
4. 波次时长/掉落梯度按 GameDesign 关卡一数值校准

**验证**：护盾三态（穿/卡/弹）表现正确；E02~E08 行为各自成立；关卡一全程可通关。

### 阶段四：配件 + 枪械原型扩充 + 整枪掉落 + 售卖

**目标**：构筑深度落地，摸金经济闭环。

1. 配件系统：13 普通配件 + 4 诅咒配件 + 同槽共鸣；装配/卸下、Tab 装配面板完善
2. 枪械原型 G3~G8（霰弹/狙击/冲锋/榴弹/左轮/蜂刺）补齐
3. 整枪 / 配件 / 护盾掉落接入波次掉落表；应急补给箱（赏金兜底）
4. 结算面板升级：战利品清单逐件售卖（含诅咒配件强制售卖）
5. **移除开局机枪**（阶段二的临时测试配置），开局仅手枪

**验证**：配件效果与共鸣生效；整枪掉落可拾取入栏；售卖金额与结算一致；开局只有手枪。

### 阶段五：传奇枪械 + 连携机制

**目标**：3 把传奇枪（各带专属机制）+ 破盾连锁 + 枪斗连携。

1. 传奇枪 ×3：专属词条与获取途径（Boss 掉落/隐藏条件）
2. 破盾连锁：破盾瞬间的范围效果
3. 枪斗连携：近战处决与射击衔接

**验证**：三传奇各自机制成立且不与配件冲突；连锁/连携触发稳定。

### 阶段六：赏金挑战

**目标**：局内随机赏金事件（Y/N 接受、30 秒限时、奖励兑现）。

1. 赏金生成器：条件（武器栏空位 + 未持有枪型 → 预选枪；否则当前口径子弹 ×20，邀请生成时锁定）
2. 赏金目标类型与倒计时 UI；超时/失败处理
3. 奖励兑现与应急补给箱联动

**验证**：邀请条件锁定正确；接受/拒绝/超时三分支奖励与惩罚符合设计。

### 阶段七：局外天赋树 + 存档扩展

**目标**：5 分支 17 节点天赋树，金币消耗逐层解锁。

1. 天赋配置（SO + 配置表）与效果挂载点
2. 天赋面板（主菜单进入）与解锁状态存档
3. 存档结构扩展（金币 + 天赋 + 图鉴进度）

**验证**：天赋效果入局生效；存档读写跨会话正确。

### 阶段八：关卡二 ~ 五

**目标**：幽暗森林 / 熔岩洞窟 / 寒冰要塞 / 深渊裂谷四关，15 新怪 + B02~B05。

1. 四关地图机制（环境伤害/地形机关）与 30 波次表
2. 15 种新敌人 AI + 4 个 Boss 技能循环
3. 各关掉落梯度与枪械组合解锁节奏按 GameDesign 校准

**验证**：每关独立可通关；机制与怪物组合无死局；掉落梯度平滑。

### 阶段九：双人合作

**目标**：本地双人（第二输入、镜头自适应、掉落分配）。

1. 第二套输入绑定与玩家实体复制
2. 镜头跟随/缩放的自适应
3. 掉落拾取归属与结算分账

**验证**：双人全程可玩，输入互不干扰，结算分账正确。

---

## 六、阶段验证标准汇总

| 阶段 | 验收一句话标准 |
|------|----------------|
| 一 ✅ | 完整流程可循环游玩，金币跨局保留 |
| 二 🔄 | 无升级弹窗；定时波次 + F 倍率正确；不自动切枪、R/B 换弹装填正确；子弹库存扣减；耐久报废腾格；金币/子弹包掉落入账；结算 = 掉落 + 奖励 |
| 三 | 护盾三态成立；E02~E08 行为正确；关卡一完整通关 |
| 四 | 配件/共鸣生效；整枪掉落入栏；逐件售卖金额正确；开局仅手枪 |
| 五 | 三传奇机制成立；破盾连锁与枪斗连携稳定 |
| 六 | 赏金 Y/N、30s、奖励兑现与兜底锁定全分支正确 |
| 七 | 天赋入局生效；存档跨会话正确 |
| 八 | 四关独立可通关，机制/怪物/掉落节奏符合设计 |
| 九 | 双人输入互不干扰，结算分账正确 |

### 阶段二 Play 验证清单

| 改动 | 预期 |
|------|------|
| 删升级 | 杀怪不掉水晶、无升级弹窗、无报错 |
| 定时波次 | 倒计时到点必刷且残兵叠加；F 提前 + 倍率 1.5→1.75→2.0；自然到点重置 1.0 |
| 换弹/切枪 | 打空自动换弹但不切枪；R 主动换；B 循环等级且两枪独立记忆；换弹切后台仍继续 |
| 子弹库存 | 库存扣减/部分装填/缺弹提示正确；初始 S·0×60 含首次装填 |
| 耐久 | 磨损 → ≤20% 警示 → 归零报废腾格且不自动切枪 |
| 掉落 | 金币自动吸附入账；子弹包入库存 |
| 结算 | 胜 = 掉落 + 500，败 = 掉落；重开一局全部重置 |

---

## 七、手工操作清单（需要你在 Unity 编辑器完成的事）

> 原则：AI 只改 .cs 文件；prefab / 场景 / .asset 的改动由你手工执行（或跑我提供的 Editor 菜单脚本）。

### 阶段二（本轮）手工清单——按顺序执行

1. **生成子弹资产**：菜单跑 `Editor/BulletAssetGenerator` 提供的 MenuItem，一键生成 18 个子弹 SO 到 `Resources/Configs/Bullets/`；然后**手删旧的 `NormalBullet.asset`**（否则 19 个配置并存，口径查询会乱）
2. **补武器资产字段**：`Pistol.asset` / `MachineGun.asset` 在 Inspector 补 `Caliber`（S / AR）与 `DurabilityMax`（100 / 240）
3. **核对敌人资产掉落字段**：`SlimeGreen.asset` 核对/填写金币区间、子弹包概率与等级/数量区间
4. **择机手删 `LevelUpPanel.prefab`**（代码删除后该 prefab 会变 missing script）
5. **GameHUD.prefab**（及必要时 `ResultPanel.prefab`）：按我给出的元素清单添加 Bind 控件并重新生成代码 → 然后我再写 HUD 逻辑

### 每阶段通用

- 每阶段完成后 Play 验证，把报错信息发给我
- 接入美术时把代码创建的占位物体保存为预制体，或告诉我替换方式

---

## 八、代码规范约定

1. **注释**：每个变量、每个方法、每一步关键逻辑都写中文注释；类头部注释说明职责
2. **命名空间**：游戏代码默认无 namespace；UI 面板统一 `namespace Game.UI`
3. **命名**：Model 接口以 `I` 开头、实现去掉 `I`；面板以 `Panel` 结尾；Command 以 `Command` 结尾；事件以 `Event` 结尾
4. **数据流**：单向——输入/碰撞 → Command → Model 变更（BindableProperty）→ 事件通知 → View 刷新；事件必须可注销（`UnRegisterWhenGameObjectDestroyed`）
5. **System**：无 Update，统一由 `GameRoot.Update` 手动 `Tick(dt)` 驱动
6. **配置**：新增可配置数据一律走 ScriptableObject 资产 + `Resources/Configs/` + 懒加载静态配置表惯例（参照 WeaponConfigTable）；新增资产用 Editor 脚本批量生成
7. **日志**：用 `Debug.Log`，临时调试日志标记 `[Debug]` 前缀，验证通过后删除
