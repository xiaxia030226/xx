# QFramework 开发参考文档

> QFramework 框架核心 API 速查、分层架构说明、代码示例与最佳实践。
>
> 框架源码位置：`QFramework.Unity2018+/Assets/QFramework/`

## 文档结构

- [架构概述](#架构概述)
- [Architecture 架构注册中心](#architecture-架构注册中心)
- [Model 数据层](#model-数据层)
- [System 逻辑层](#system-逻辑层)
- [Command 命令模式](#command-命令模式)
- [Query 查询](#query-查询)
- [Utility 工具类](#utility-工具类)
- [事件系统 TypeEventSystem / EasyEvent](#事件系统-typeeventsystem--easyevent)
- [Controller 控制器](#controller-控制器)
- [完整数据流与 Demo 代码](#完整数据流与-demo-代码)
- [工具包：AudioKit / UIKit / ResKit](#工具包audiokit--uikit--reskit)
- [常见问题与最佳实践](#常见问题与最佳实践)

---

# 架构概述

## 框架简介

QFramework 是一个 Unity 分层架构框架，核心思想是将数据、逻辑、表现分离：

> **Model 存数据，System 写逻辑，Command/Query 管理读写，Controller 连接 Unity。**

开发者：凉鞋（liangxie）。官网：qframework.cn，GitHub：github.com/liangxiegame/QFramework。

**框架的目录结构**：

```
Assets/QFramework/
├── Framework/          ← 核心（必学，就一个 QFramework.cs 文件）
│   └── Scripts/QFramework.cs
└── Toolkits/           ← 可选工具包
    ├── AudioKit/       ← 音频管理
    ├── UIKit/          ← UI 框架
    ├── ResKit/         ← 资源管理
    └── SupportOldQF/   ← 旧版兼容
```

**引入到自己的项目**：把 `Assets/QFramework` 整个文件夹拷贝进 Unity 工程即可，纯 C# 代码，零配置。

## 分层架构全景图

QFramework 整体结构：

```
┌────────────────── QFramework 分层架构 ──────────────────┐
│                                                        │
│   Unity 世界          ←→        框架世界                │
│                                                        │
│  MonoBehaviour                  ┌──────────┐            │
│  (Controller) ──发命令/查询──→  │Architecture│ ← 模块总管 │
│      ↑ 刷新 UI                 └─┬──┬──┬──┘            │
│      │         ┌────────────────┘  │  └───────┐        │
│      │         ▼                   ▼          ▼        │
│      │    ┌─────────┐       ┌──────────┐ ┌────────┐   │
│      └────│  Model  │◄──────│  System  │ │Utility │   │
│   自动通知 │ (数据)  │ 读写  │  (逻辑)  │ │(工具)  │   │
│           └─────────┘       └──────────┘ └────────┘   │
│                ▲ Write(Command) / Read(Query)          │
│                                                        │
│   事件系统(TypeEventSystem)贯穿全局，模块间广播通信        │
└────────────────────────────────────────────────────────┘
```

---

# Architecture 架构注册中心

## 概述

## 注册示例

每个游戏模块（比如"计数器系统"、"背包系统"）对应**一个** Architecture。你可以把它理解成一个**仓库总管**：

- 它持有这个模块所有的 Model（数据）、System（逻辑）、Utility（工具）
- 任何人想要数据或发命令，都必须先找到这个"总管"
- 它是一个单例，全局只有一份

框架源码 `Architecture<T>` 抽象类的核心部分：

```csharp
public abstract class Architecture<T> : IArchitecture where T : Architecture<T>, new()
{
    protected static T mArchitecture;   // 单例实例

    public static IArchitecture Interface  // 全局访问入口
    {
        get
        {
            if (mArchitecture == null) InitArchitecture();
            return mArchitecture;
        }
    }

    protected abstract void Init();  // 子类必须实现：在这里注册 Model/System
}
```

### 使用示例

以"计数器 Demo"为例（本文档后续示例均基于它），架构类如下：

```csharp
using QFramework;

namespace CounterApp
{
    /// <summary>
    /// 计数器模块的架构：模块内所有 Model/System/Utility 都注册在这里
    /// </summary>
    public class CounterApp : Architecture<CounterApp>
    {
        /// <summary>
        /// 初始化：注册本模块的 Model 和 System
        /// </summary>
        protected override void Init()
        {
            // 注册 Model 与 System（见下文实现）
            RegisterModel(new CounterModel());
            RegisterSystem(new AchievementSystem());
        }
    }
}
```

之后在**任何地方**，一行代码就能拿到这个模块的入口：

```csharp
IArchitecture architecture = CounterApp.Interface; // 第一次访问时自动初始化
```

### 适用场景

- 每个游戏模块（背包、商店、关卡……）建一个自己的 Architecture
- 简单的小项目，整个游戏共用一个 Architecture 也行
- 架构类本身很薄，只做"注册"，逻辑都放在 System 里

# Model 数据层

## 概述

Model 是**纯数据**。规则只有一条：

> **Model 只负责存数据，绝对不能写业务逻辑，也不能碰 UI。**

UI 自动感知数据变化的机制：`BindableProperty<T>`（可绑定属性）。值一变，所有订阅者立刻收到通知。

源码核心：

```csharp
public class BindableProperty<T> : IBindableProperty<T>
{
    public T Value
    {
        get => mValue;
        set
        {
            if (Comparer(value, mValue)) return;  // 值没变就不通知
            mValue = value;
            mOnValueChanged.Trigger(value);       // 值变了，触发所有订阅者
        }
    }

    public IUnRegister Register(Action<T> onValueChanged);          // 订阅
    public IUnRegister RegisterWithInitValue(Action<T> onValueChanged); // 订阅 + 立刻收到当前值
}
```

### 使用示例

```csharp
using QFramework;

namespace CounterApp
{
    /// <summary>
    /// 计数器数据：只存"当前数值"，一个字段就够
    /// </summary>
    public class CounterModel : AbstractModel
    {
        /// <summary>
        /// 可绑定属性：数值变化时自动通知订阅者（UI 刷新全靠它）
        /// </summary>
        public BindableProperty<int> Count { get; } = new BindableProperty<int>(0);

        protected override void OnInit()
        {
            // 初始化逻辑（可选），比如从存档读数据：
            // Count.Value = PlayerPrefs.GetInt("Count", 0);
        }
    }
}
```

以后改数据和监听数据，就这么简单：

```csharp
// 写数据（谁都可以通过架构拿到 Model）
var model = CounterApp.Interface.GetModel<CounterModel>();
model.Count.Value = 10; // 值变了，UI 自动收到通知

// 读数据（UI 侧订阅）
model.Count.RegisterWithInitValue(count =>
{
    Debug.Log($"当前数值：{count}");  // 注册时立刻打印一次，之后每次变化都打印
});
```

对比不使用 BindableProperty 的写法：

| 以前（直接 int） | 现在（BindableProperty） |
|---|---|
| 改值后要手动找 UI 更新 | 值一变，UI 自动刷新 |
| UI 代码和逻辑代码耦合 | 双方只知道"这个属性" |

### 适用场景

- 玩家属性（金币、血量、等级）、背包列表、设置项……凡是"要存起来、UI 要展示"的数据
- 约束：**Model 里只能出现数据字段和 BindableProperty，不允许出现 if-else 业务逻辑**（逻辑放 System）

---

# System 逻辑层

## 概述

Model 只存数据不写逻辑，业务逻辑写在 **System**。

> **System 是"业务逻辑的容器"。它读 Model、写 Model、发事件，但绝不碰 UI。**

它和 Model 的关系就像：Model 是钱包（只装钱），System 是会计（管怎么算钱、什么时候加钱）。

```csharp
public abstract class AbstractSystem : ISystem
{
    public bool Initialized { get; set; }
    protected abstract void OnInit();   // 子类必须实现：初始化逻辑
    public void Deinit();               // 清理
    protected virtual void OnDeinit();  // 清理回调（可选）

    // 通过架构可以做的事（框架自动注入）：
    // GetModel<T>()、GetSystem<T>()、GetUtility<T>()
    // SendEvent<T>()、RegisterEvent<T>()
}
```

### 使用示例

计数器 Demo 的成就系统：金币达到 10 时自动解锁成就。

```csharp
using QFramework;
using UnityEngine;

namespace CounterApp
{
    /// <summary>
    /// 成就系统：监听金币变化，达到条件时解锁成就
    /// </summary>
    public class AchievementSystem : AbstractSystem
    {
        protected override void OnInit()
        {
            // 获取 CounterModel 的金币属性，订阅变化
            var counterModel = this.GetModel<CounterModel>();

            counterModel.Count.Register(count =>
            {
                if (count >= 10)
                {
                    Debug.Log("成就解锁：身家过十！");
                }

                if (count >= 100)
                {
                    Debug.Log("成就解锁：百元户！");
                }
            });
        }

        protected override void OnDeinit()
        {
            // 清理资源（如果有的话）
        }
    }
}
```

关键点：

- `this.GetModel<CounterModel>()` 拿到数据，不需要 new，不需要传引用——框架自动注入
- 直接订阅 `Count.Register(...)`，值一变自动触发检查
- System 里可以写任何逻辑（if-else、循环、计算），但绝不能写 `GetComponent` 或 `GameObject.Find`——那些是 Controller 的事

### 注册到架构

`CounterApp.Init()` 中调用 `RegisterSystem(new AchievementSystem())` 后，框架在初始化时会自动调用 `OnInit()`，成就系统开始监听金币。

# Command 命令模式

## 概述

直接在 Controller 里写 `model.Count.Value++` 也能实现金币+1，但如果金币+1 时还要记录日志、检查成就、更新排行榜……代码会散落在各处，难以维护。

> **Command 把"一个动作"封装成一个独立的类，谁想做这个动作，发命令就行。**

```csharp
// 无返回值命令
public abstract class AbstractCommand : ICommand
{
    protected abstract void OnExecute();   // 子类实现：做什么
}

// 有返回值命令
public abstract class AbstractCommand<TResult> : ICommand<TResult>
{
    protected abstract TResult OnExecute();  // 子类实现：做什么 + 返回什么
}
```

### 使用示例

```csharp
using QFramework;
using UnityEngine;

namespace CounterApp
{
    /// <summary>
    /// 金币 +1 命令：封装"加金币"这个动作
    /// </summary>
    public class AddCountCommand : AbstractCommand
    {
        /// <summary>
        /// 每次加多少（默认 1），调用方可以改
        /// </summary>
        public int Amount { get; set; } = 1;

        protected override void OnExecute()
        {
            // 拿 Model 数据
            var model = this.GetModel<CounterModel>();

            // 修改数据
            model.Count.Value += Amount;

            // 可以在这里加日志、校验、额外逻辑……
            Debug.Log($"金币 +{Amount}，当前：{model.Count.Value}");
        }
    }
}
```

### 发送命令

有两种方式，效果一样：

```csharp
// 方式一：直接构造命令对象
this.SendCommand(new AddCountCommand { Amount = 5 });

// 方式二：无参命令用泛型发送（框架自动 new）
this.SendCommand<AddCountCommand>();  // Amount 默认 1
```

### 有返回值的命令

有时候需要命令返回结果，比如"提交购买请求，返回是否成功"：

```csharp
/// <summary>
/// 购买道具命令：返回是否成功
/// </summary>
public class BuyItemCommand : AbstractCommand<bool>
{
    public int Price { get; set; }

    protected override bool OnExecute()
    {
        var model = this.GetModel<CounterModel>();

        if (model.Count.Value >= Price)
        {
            model.Count.Value -= Price;
            return true;
        }

        return false;  // 钱不够
    }
}

// 调用
bool success = this.SendCommand(new BuyItemCommand { Price = 10 });
if (success)
{
    Debug.Log("购买成功！");
}
```

### Command 的价值

| 问题 | Command 的解法 |
|---|---|
| 修改数据的代码散落各处 | 所有"加金币"走同一个 AddCountCommand |
| 加金币后要做额外操作 | 在 OnExecute 里统一加，不用到处改 |
| 想撤销操作 | 给 Command 加 Undo 方法即可 |
| 想记录操作日志 | 在 Command 基类或发送处统一拦截 |

# Query 查询

## 概述

命令是"写"（改数据），Query 是"读"（取数据）。直接用 `GetModel<CounterModel>().Count.Value` 也能读，但 Query 的价值在于：

> **把"怎么算"封装起来，读的人不用关心数据从哪来、怎么算。**

### 使用示例

```csharp
using QFramework;

namespace CounterApp
{
    /// <summary>
    /// 查询当前金币数
    /// </summary>
    public class CountQuery : AbstractQuery<int>
    {
        protected override int OnDo()
        {
            // 从 Model 读数据
            return this.GetModel<CounterModel>().Count.Value;
        }
    }
}
```

简单场景下优势不明显，考虑以下场景：如果金币数不是直接存的，而是 `总金币 = 现金 + 银行存款`，Query 就体现价值了：

```csharp
public class CountQuery : AbstractQuery<double>
{
    protected override double OnDo()
    {
        var model = this.GetModel<CounterModel>();
        return model.Cash.Value + model.BankDeposit.Value;  // 调用方不需要知道这个公式
    }
}
```

### 调用 Query

```csharp
int currentCount = this.SendQuery(new CountQuery());
Debug.Log($"当前金币：{currentCount}");
```

## 要点

```
                ┌─────────────┐
                │ Architecture │  ← 总管，持有下面三者
                └──────┬──────┘
           ┌───────────┼───────────┐
           ▼           ▼           ▼
    ┌──────────┐ ┌──────────┐ ┌──────────┐
    │  Model   │ │  System  │ │ Utility  │
    │  存数据   │ │  做逻辑   │ │  辅助工具  │
    └────┬─────┘ └────┬─────┘ └──────────┘
         │            │
    ┌────▼────┐  ┌───▼───┐
    │ Command │  │ Query │
    │  写操作  │  │ 读操作  │
    └─────────┘  └───────┘
```

1. **System**：业务逻辑层，读 Model、写 Model、发事件，不碰 UI
2. **Command**：封装"写操作"，一个动作一个类，发命令即可
3. **Query**：封装"读操作"，隐藏数据来源和计算逻辑

---

# Utility 工具类

## 概述

无状态工具类（日志、加密、格式化等静态方法集合）在 QFramework 中统一叫 **Utility**。

> **Utility 是纯工具，不存数据，不依赖具体场景，只提供通用功能。**

它的接口极其简单，就是一个空接口，纯粹是"标记"作用：

```csharp
public interface IUtility
{
}
```

### 使用示例

```csharp
using QFramework;
using UnityEngine;

namespace CounterApp
{
    /// <summary>
    /// 日志工具：统一控制日志输出
    /// </summary>
    public class LoggerUtil : IUtility
    {
        /// <summary>
        /// 是否开启调试日志（对接设置面板）
        /// </summary>
        public bool EnableDebugLog { get; set; } = true;

        public void Log(string msg)
        {
            if (EnableDebugLog)
            {
                Debug.Log($"[CounterApp] {msg}");
            }
        }

        public void LogWarning(string msg)
        {
            Debug.LogWarning($"[CounterApp] {msg}");
        }
    }
}
```

注册和使用：

```csharp
// 在 CounterApp.Init() 里注册
protected override void Init()
{
    RegisterUtility(new LoggerUtil());
    RegisterModel(new CounterModel());
    RegisterSystem(new AchievementSystem());
}

// 在任何地方使用
var logger = this.GetUtility<LoggerUtil>();
logger.Log("玩家登陆了");
```

### 适用场景

- 日志、加密、时间格式化、随机数封装……这些"跟游戏业务无关，但到处要用"的功能
- 和 System 的区别：**System 写业务逻辑，Utility 写通用工具**

# 事件系统 TypeEventSystem / EasyEvent

## 概述

`counterModel.Count.Register(...)` 只能监听单个属性变化，属于 **Model 级别的通知**。

对于"玩家死亡"、"关卡通关"这类**全局事件**，需要多个模块各自响应时，使用事件系统。

**QFramework 有两套事件系统：**

| 事件系统 | 使用场景 | 谁管理 |
|---|---|---|
| TypeEventSystem | 跨模块通信（架构内或全局） | Architecture 统一管理 |
| EasyEvent | 单对象内部的小范围事件 | 自己管理 |

### 2.1 TypeEventSystem——架构级事件

```csharp
public class TypeEventSystem
{
    public static readonly TypeEventSystem Global;  // 全局事件（不依赖架构也能用）

    public void Send<T>(T e);                          // 发事件
    public IUnRegister Register<T>(Action<T> onEvent);  // 订阅事件
    public void UnRegister<T>(Action<T> onEvent);       // 取消订阅
}
```

**关键设计：事件类型可以是任意 struct 或 class**，框架按"类型"来区分事件，不需要字符串 key。

### 使用示例

先定义事件类型（就是一个普通的 struct 或 class）：

```csharp
/// <summary>
/// 金币变化事件：携带变化前后的值
/// </summary>
public struct CounterChangeEvent
{
    public int OldValue;
    public int NewValue;
}
```

然后在 AddCountCommand 里发送事件：

```csharp
public class AddCountCommand : AbstractCommand
{
    public int Amount { get; set; } = 1;

    protected override void OnExecute()
    {
        var model = this.GetModel<CounterModel>();
        int oldValue = model.Count.Value;

        model.Count.Value += Amount;

        // 发送事件，通知所有订阅者
        this.SendEvent(new CounterChangeEvent
        {
            OldValue = oldValue,
            NewValue = model.Count.Value
        });
    }
}
```

AchievementSystem 里订阅这个事件：

```csharp
public class AchievementSystem : AbstractSystem
{
    protected override void OnInit()
    {
        // 订阅 CounterChangeEvent 事件
        this.RegisterEvent<CounterChangeEvent>(e =>
        {
            Debug.Log($"金币从 {e.OldValue} 变成 {e.NewValue}");

            if (e.NewValue >= 10 && e.OldValue < 10)
            {
                Debug.Log("成就解锁：身家过十！");
            }
        });
    }
}
```

### 2.2 全局事件（不依赖架构）

若事件需要完全全局广播，连 Architecture 都不需要：

```csharp
// 定义事件
public struct PlayerDiedEvent { }

// 发送
TypeEventSystem.Global.Send(new PlayerDiedEvent());

// 订阅
TypeEventSystem.Global.Register<PlayerDiedEvent>(e =>
{
    Debug.Log("玩家死了，显示复活界面");
});
```

### 2.3 EasyEvent——轻量级事件

如果你只需要在**一个类内部**做小范围通知，用 EasyEvent 更轻量：

```csharp
public class Timer
{
    // 无参事件
    public EasyEvent OnCompleted = new EasyEvent();

    // 带参事件
    public EasyEvent<float> OnProgressChanged = new EasyEvent<float>();

    public void Start(float duration)
    {
        // 模拟计时...
        OnProgressChanged.Trigger(0.5f);  // 通知进度
        OnCompleted.Trigger();             // 通知完成
    }
}

// 使用
var timer = new Timer();
timer.OnCompleted.Register(() => Debug.Log("计时结束！"));
timer.OnProgressChanged.Register(progress => Debug.Log($"进度：{progress}"));
```

### 2.4 事件注销——防止内存泄漏

订阅事件后，对象销毁时**必须注销**，否则会内存泄漏。QFramework 提供了便捷方法：

```csharp
// 方式一：GameObject 销毁时自动注销（最常用）
this.RegisterEvent<CounterChangeEvent>(OnCounterChanged)
    .UnRegisterWhenGameObjectDestroyed(this);  // this 是 MonoBehaviour

// 方式二：GameObject 禁用时自动注销
this.RegisterEvent<CounterChangeEvent>(OnCounterChanged)
    .UnRegisterWhenDisabled(this);

// 方式三：场景切换时自动注销
this.RegisterEvent<CounterChangeEvent>(OnCounterChanged)
    .UnRegisterWhenCurrentSceneUnloaded();
```

**规则：注册事件后必须跟一个 `.UnRegisterWhenXxx(...)`。**

---

# Controller 控制器

## 概述

Model、System、Command 都是纯 C# 类，不继承 MonoBehaviour。但 Unity 的 UI 按钮、文本、碰撞器**必须挂 MonoBehaviour**，因此需要 Controller 作为桥接。

> **Controller 是 MonoBehaviour 和框架之间的"翻译官"。它让普通的 MonoBehaviour 脚本获得发命令、读模型、订阅事件的能力。**

```csharp
public interface IController : IBelongToArchitecture, ICanSendCommand, ICanGetSystem,
    ICanGetModel, ICanRegisterEvent, ICanSendQuery, ICanGetUtility
{
}
```

只需让脚本实现 `IController`，然后**提供一个 `GetArchitecture()` 方法**，就能获得所有能力。

### 使用示例

```csharp
using QFramework;
using UnityEngine;
using UnityEngine.UI;

namespace CounterApp
{
    /// <summary>
    /// 计数器 UI 控制器：挂到 Canvas 上，负责显示和交互
    /// </summary>
    public class CounterViewController : MonoBehaviour, IController
    {
        [SerializeField] private Text countText;
        [SerializeField] private Button addButton;
        [SerializeField] private Button subButton;

        /// <summary>
        /// 告诉框架：我属于哪个架构
        /// </summary>
        public IArchitecture GetArchitecture() => CounterApp.Interface;

        private void Start()
        {
            // 订阅 Model 数据变化，自动刷新 UI
            var model = this.GetModel<CounterModel>();
            model.Count.RegisterWithInitValue(count =>
            {
                countText.text = $"金币：{count}";
            }).UnRegisterWhenGameObjectDestroyed(gameObject);

            // 按钮绑定命令
            addButton.onClick.AddListener(() =>
            {
                this.SendCommand(new AddCountCommand { Amount = 1 });
            });

            subButton.onClick.AddListener(() =>
            {
                this.SendCommand(new AddCountCommand { Amount = -1 });
            });
        }
    }
}
```

**要点：**

1. `GetArchitecture() => CounterApp.Interface` —— 告诉框架我属于哪个模块
2. `this.GetModel<CounterModel>()` —— 拿到数据
3. `model.Count.RegisterWithInitValue(...)` —— 注册时立刻刷新一次，之后每次变化自动刷新
4. `this.SendCommand(new AddCountCommand { ... })` —— 发命令，不直接改数据
5. `.UnRegisterWhenGameObjectDestroyed(gameObject)` —— 对象销毁时自动注销

## Controller 的能力

实现 `IController` 后，自动获得这些能力（框架通过扩展方法注入）：

```csharp
this.GetModel<CounterModel>();          // 读数据
this.GetSystem<AchievementSystem>();    // 获取系统
this.GetUtility<LoggerUtil>();          // 获取工具
this.SendCommand(new AddCountCommand());// 发命令
this.SendQuery(new CountQuery());       // 发查询
this.RegisterEvent<CounterChangeEvent>(...); // 订阅事件
this.SendEvent(new CounterChangeEvent());    // 发事件
```

## 要点

| 概念 | 一句话 | 代码关键 |
|---|---|---|
| **Utility** | 无状态工具类 | 实现 `IUtility`，框架帮忙管理 |
| **TypeEventSystem** | 跨模块广播 | `this.SendEvent<T>()` / `this.RegisterEvent<T>()` |
| **EasyEvent** | 对象内部小范围通知 | `new EasyEvent()` / `.Trigger()` / `.Register()` |
| **事件注销** | 防止内存泄漏 | `.UnRegisterWhenGameObjectDestroyed(this)` |
| **Controller** | MonoBehaviour 与框架的桥 | 实现 `IController`，提供 `GetArchitecture()` |

---

# 完整数据流与 Demo 代码

## 数据流程图

一个简单操作的完整数据流如下：**玩家点击 "+" 按钮，金币 +1，UI 刷新，成就系统检查成就**。

```
玩家点击按钮
    │
    ▼
CounterViewController (IController)          ← MonoBehaviour，接收 Unity 输入
    │  this.SendCommand(new AddCountCommand())
    ▼
AddCountCommand.OnExecute()                  ← 命令：改数据 + 发事件
    │  model.Count.Value += 1
    ▼
CounterModel.Count (BindableProperty<int>)   ← 数据变化，自动触发通知
    │        │
    │        ├──────────────────────────┐
    ▼                                  ▼
Controller 订阅的回调                    AchievementSystem 订阅的回调
countText.text = "金币：1"              if (count >= 10) 解锁成就
```

注意两点：

1. **数据只在一个地方被修改**（Command 里），UI 和成就系统都是"被动收到通知"
2. **Controller 不碰逻辑，Command 不碰 UI**——这就是分层的意义

## 二、可运行的计数器 Demo

接入方式：

1. 新建 Unity 项目（或打开现有项目）
2. 把 `QFramework/Framework/Scripts/QFramework.cs` 拷到 `Assets/` 下
3. 在 `Assets/` 下创建以下 7 个 .cs 文件，把代码拷进去
4. 场景里建一个 Canvas + 文本 + 两个按钮，挂上 `CounterViewController`，拖拽引用即可运行

### 文件 1：CounterApp.cs（架构）

```csharp
using QFramework;

namespace CounterApp
{
    /// <summary>
    /// 计数器模块架构：注册本模块的 Model、System、Utility
    /// </summary>
    public class CounterApp : Architecture<CounterApp>
    {
        protected override void Init()
        {
            // 注册顺序无关紧要，框架会自动调用它们的 OnInit
            RegisterModel(new CounterModel());
            RegisterSystem(new AchievementSystem());
            RegisterUtility(new LoggerUtil());
        }
    }
}
```

### 文件 2：CounterModel.cs（数据）

```csharp
using QFramework;

namespace CounterApp
{
    /// <summary>
    /// 计数器数据：只存数据
    /// </summary>
    public class CounterModel : AbstractModel
    {
        /// <summary>
        /// 金币数：值一变，自动通知所有订阅者
        /// </summary>
        public BindableProperty<int> Count { get; } = new BindableProperty<int>(0);

        protected override void OnInit()
        {
            // 可选：从存档读取
            // Count.Value = PlayerPrefs.GetInt("CounterApp_Count", 0);
        }
    }
}
```

### 文件 3：AddCountCommand.cs（命令）

```csharp
using QFramework;
using UnityEngine;

namespace CounterApp
{
    /// <summary>
    /// 金币变化命令：封装"加金币"这个动作
    /// </summary>
    public class AddCountCommand : AbstractCommand
    {
        /// <summary>
        /// 变化量（可为负数，表示扣金币）
        /// </summary>
        public int Amount { get; set; } = 1;

        protected override void OnExecute()
        {
            var model = this.GetModel<CounterModel>();
            int oldValue = model.Count.Value;

            model.Count.Value += Amount;

            // 发事件：通知成就系统等订阅者
            this.SendEvent(new CounterChangeEvent
            {
                OldValue = oldValue,
                NewValue = model.Count.Value
            });

            // 可选：保存存档
            // PlayerPrefs.SetInt("CounterApp_Count", model.Count.Value);
        }
    }
}
```

### 文件 4：CounterChangeEvent.cs（事件定义）

```csharp
namespace CounterApp
{
    /// <summary>
    /// 金币变化事件
    /// </summary>
    public struct CounterChangeEvent
    {
        public int OldValue;
        public int NewValue;
    }
}
```

### 文件 5：AchievementSystem.cs（系统）

```csharp
using QFramework;
using UnityEngine;

namespace CounterApp
{
    /// <summary>
    /// 成就系统：监听金币变化，解锁成就
    /// </summary>
    public class AchievementSystem : AbstractSystem
    {
        protected override void OnInit()
        {
            // 订阅金币变化事件
            this.RegisterEvent<CounterChangeEvent>(OnCounterChanged);
        }

        private void OnCounterChanged(CounterChangeEvent e)
        {
            // 只有"跨越"10 的瞬间才解锁（避免每次变化都重复提示）
            if (e.OldValue < 10 && e.NewValue >= 10)
            {
                Debug.Log("成就解锁：身家过十！");
            }

            if (e.OldValue < 100 && e.NewValue >= 100)
            {
                Debug.Log("成就解锁：百元户！");
            }
        }
    }
}
```

### 文件 6：CounterViewController.cs（控制器）

```csharp
using QFramework;
using UnityEngine;
using UnityEngine.UI;

namespace CounterApp
{
    /// <summary>
    /// 计数器 UI 控制器：挂到 Canvas 上，负责显示和交互
    /// </summary>
    public class CounterViewController : MonoBehaviour, IController
    {
        [SerializeField] private Text countText;
        [SerializeField] private Button addButton;
        [SerializeField] private Button subButton;

        /// <summary>
        /// 告诉框架：我属于哪个架构
        /// </summary>
        public IArchitecture GetArchitecture() => CounterApp.Interface;

        private void Start()
        {
            // 1. 订阅数据，自动刷新 UI（注册时立刻刷新一次）
            this.GetModel<CounterModel>()
                .Count
                .RegisterWithInitValue(count =>
                {
                    countText.text = $"金币：{count}";
                })
                .UnRegisterWhenGameObjectDestroyed(gameObject);

            // 2. 按钮绑定命令（注意：UI 层只发命令，不直接改数据）
            addButton.onClick.AddListener(() =>
            {
                this.SendCommand(new AddCountCommand { Amount = 1 });
            });

            subButton.onClick.AddListener(() =>
            {
                this.SendCommand(new AddCountCommand { Amount = -1 });
            });
        }
    }
}
```

### 可选文件 7：LoggerUtil.cs（工具）

```csharp
using QFramework;
using UnityEngine;

namespace CounterApp
{
    /// <summary>
    /// 日志工具：统一控制日志输出
    /// </summary>
    public class LoggerUtil : IUtility
    {
        public bool EnableDebugLog { get; set; } = true;

        public void Log(string msg)
        {
            if (EnableDebugLog)
            {
                Debug.Log($"[CounterApp] {msg}");
            }
        }
    }
}
```

## 三、Unity 场景搭建步骤

1. **场景**：新建场景，创建 `Canvas`（UI → Canvas）
2. **文本**：Canvas 下创建 `Text`（TextMeshPro 或 Legacy Text 均可），命名 `CountText`
3. **按钮**：Canvas 下创建两个 `Button`，命名 `AddButton`、`SubButton`，把按钮文字分别改成 "+" 和 "-"
4. **挂脚本**：把 `CounterViewController.cs` 拖到 Canvas 上
5. **拖引用**：把 `CountText`、`AddButton`、`SubButton` 分别拖到 Inspector 的三个槽位
6. **运行**：点 Play，点 "+" 金币增加，Console 里在金币到 10 时会看到成就解锁日志

## 扩展改造参考

1. 加一个"清零"按钮：新建 `ResetCountCommand`，把 `Count.Value` 设为 0
2. 让金币不能为负数：在 `AddCountCommand` 里加判断，`Count.Value < 0` 时不减
3. 用 Query 读取初始值：在 Start 里用 `this.SendQuery(new CountQuery())` 先手动设一次文本

## 要点

1. **数据流**：Controller 发命令 → Command 改 Model → BindableProperty 自动通知 → UI 和 System 被动响应
2. **分工**：Controller 管输入和显示，Command 管写操作，Query 管读操作，Model 只存数据，System 做后台逻辑
3. **Demo 全部代码已经给出**，拷进项目就能跑

---

# 工具包：AudioKit / UIKit / ResKit

## 工具包概述

前几步学的 `QFramework.cs` 是**核心框架**。除此之外，`Toolkits` 文件夹里还有几个**可选工具包**，解决游戏开发中最常见的三大问题：

| 工具包 | 解决的问题 | 对应旧做法 |
|---|---|---|
| AudioKit | 音频播放管理 | 到处挂 AudioSource、手动找 Clip |
| UIKit | UI 面板管理 | 手动 SetActive 切面板，一团乱 |
| ResKit | 资源加载管理 | Resources.Load / 直接引用 |

它们和核心框架是**解耦**的：你可以只用 AudioKit 而不用架构，也可以全用。

## AudioKit

AudioKit 把音频分成三类，各管各的：

- **Music（音乐）**：背景 BGM，同时只播一个
- **Voice（语音）**：对话语音
- **Sound（音效）**：打击、爆炸等短音效，可同时播多个

### 基本用法

```csharp
// 播放背景音乐（循环），音频文件放 Resources 文件夹
AudioKit.PlayMusic("HomeBg");

// 停止音乐
AudioKit.StopMusic();

// 播放音效（不循环）
AudioKit.PlaySound("EnemyDie");

// 播放语音（不循环）
AudioKit.PlayVoice("SentenceA");

// 停止所有音效
AudioKit.StopAllSound();
```

资源放置规则：把音频文件放在 `Assets/Resources/` 下（任意子目录），播放时传**不带扩展名的路径**。例如文件在 `Resources/Audio/HomeBg.mp3`，就写 `AudioKit.PlayMusic("Audio/HomeBg")`。

### 常用高级功能

```csharp
// 音量与开关（Settings 用的是你学过的 BindableProperty！）
AudioKit.Settings.IsMusicOn.Value = true;    // 开关音乐
AudioKit.Settings.IsSoundOn.Value = false;   // 关闭音效

// 音量条联动（UI Slider 直接绑定）
AudioKit.Settings.MusicVolume.RegisterWithInitValue(v => musicSlider.value = v);
musicSlider.onValueChanged.AddListener(v => { AudioKit.Settings.MusicVolume.Value = v; });

// 循环开关
AudioKit.PlayMusic("HomeBg", loop: false);

// 播放回调
AudioKit.PlaySound("EnemyDie", callBack: player =>
{
    Debug.Log("音效开始播放了");
});
```

> AudioKit 本身就是一个用 QFramework 架构写的模块（含 AudioKitArchitecture、PlaySoundCommand、SettingsModel 等），其源码可直接作为学习范例。

### 适用场景

任何需要播放声音的项目都适用。相比 Unity 原生做法，优点在于：无需手动创建 AudioSource、无需管理生命周期、开关音量的 UI 绑定简单。

## UIKit

### 解决的问题

UI 面板切换时，手动 `SetActive` 容易导致面板一多就找不到谁是谁。

UIKit 的思路：**每个面板是一个 UIPanel 类，通过代码打开/关闭，自动实例化预制体。**

### 基本用法

**第一步**：做一个面板预制体（比如设置面板），放到 `Resources/` 下，然后写一个继承 `UIPanel` 的类：

```csharp
using QFramework;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 设置面板
/// </summary>
public class UISettingPanel : UIPanel
{
    [SerializeField] private Button closeButton;

    protected override void OnInit(IUIData uiData = null)
    {
        // 面板初始化时调用
        closeButton.onClick.AddListener(() =>
        {
            CloseSelf();  // 关闭自己
        });
    }

    protected override void OnClose()
    {
        // 面板关闭时调用
    }
}
```

**第二步**：任意地方打开/关闭：

```csharp
// 打开面板
UIKit.OpenPanel<UISettingPanel>();

// 打开时传数据
UIKit.OpenPanel<UISettingPanel>(new UISettingPanelData
{
    From = "MainPanel"
});

// 关闭面板
UIKit.ClosePanel<UISettingPanel>();
```

### 关键概念

- **UIPanel**：面板基类，`OnInit`（初始化）、`OnClose`（关闭）、`OnOpen`（打开）
- **UILevel**：面板层级（Common 普通、PopUI 弹窗、Const 常驻、Forward 最高层），弹窗自动遮住普通面板
- **IUIData**：打开面板时传的参数（对应"打开前先设属性再 SetActive"的写法）
- UIKit 还自带**代码生成器**（Editor 里一键生成面板绑定代码），用到时再深入了解

### 适用场景

UI 超过 3 个面板的项目都推荐。面板少的小 Demo 用 Unity 原生 Canvas 分组也够。

## ResKit

### 解决的问题

Unity 原生的 `Resources.Load` 简单但有两个坑：

1. 资源多了无法热更新（正式项目必做）
2. 同一个资源重复加载浪费内存，手动管理又容易忘释放

ResKit 用 AssetBundle + 引用计数解决：**谁加载谁释放，加载一次多处共享**。

### 基本用法

```csharp
using QFramework;
using UnityEngine;

/// <summary>
/// 资源加载示例
/// </summary>
public class ResKitExample : MonoBehaviour
{
    private ResLoader mLoader;

    private void Start()
    {
        // 创建一个加载器（相当于一个"借用记录"）
        mLoader = ResLoader.Allocate();

        // 加载预制体（资源先标记为 AssetBundle）
        GameObject prefab = mLoader.LoadSync<GameObject>("Game/Prefabs/Enemy");

        if (prefab != null)
        {
            Instantiate(prefab);
        }
    }

    private void OnDestroy()
    {
        // 释放：加载器归还所有借用的资源
        mLoader.Recycle2Cache();
        mLoader = null;
    }
}
```

### 核心概念

- **ResLoader**：资源的"借用记录单"，一个加载器对应一次资源使用
- **LoadSync**：同步加载；还有异步加载 `LoadAsync`
- **Recycle2Cache**：用完归还（引用计数 -1），计数归零自动释放资源
- **标记 AssetBundle**：在 Editor 里把资源标记成 AssetBundle 名，框架才能按名字找

### 适用场景

- 正式项目、需要热更新的项目：**必用**
- 学习小项目：用 `Resources.Load` 就够，等做正式项目再上 ResKit

## 要点

1. **AudioKit**：`PlayMusic` / `PlaySound` / `PlayVoice` 一行搞定，资源放 Resources 目录
2. **UIKit**：UIPanel 类管理面板生命周期，`OpenPanel<T>()` / `ClosePanel<T>()`
3. **ResKit**：AssetBundle + 引用计数，`ResLoader` 加载与释放成对出现
4. **学习顺序建议**：核心框架 + AudioKit 先上手，UIKit 在 UI 多时用，ResKit 在正式项目用

---

# 常见问题与最佳实践

## 一、常见问题

### 问题 1：注册了事件，忘了注销 → 内存泄漏 + 幽灵回调

这是最最常见的坑。比如你在 MonoBehaviour 里订阅了事件，但对象销毁后订阅还在，导致：

- 对象明明销毁了，事件触发时却还在执行它的回调（报 NullReferenceException）
- 内存泄漏（旧对象被事件系统持有，无法回收）

**错误写法：**

```csharp
private void Start()
{
    this.RegisterEvent<CounterChangeEvent>(OnCounterChanged);
    // 忘了注销！对象销毁后这个订阅还留着
}
```

**正确写法——注册后立刻挂上自动注销：**

```csharp
private void Start()
{
    this.RegisterEvent<CounterChangeEvent>(OnCounterChanged)
        .UnRegisterWhenGameObjectDestroyed(gameObject);  // 对象销毁自动注销
}
```

**规则：`RegisterEvent` 后必须跟 `.UnRegisterWhenXxx(...)`。**

### 问题 2：在 UI 层直接改 Model 数据

```csharp
// 错误：UI 按钮点击时直接改数据
addButton.onClick.AddListener(() =>
{
    this.GetModel<CounterModel>().Count.Value++;  // 逻辑散落在 UI 里
});
```

若后续增加"金币上限 999"、"扣税"等规则，需要修改所有直接改数据的位置。**所有写操作应走 Command**，规则只写在 Command 一处：

```csharp
// 正确：UI 只发命令
addButton.onClick.AddListener(() =>
{
    this.SendCommand(new AddCountCommand { Amount = 1 });
});
```

### 问题 3：忘了实现 GetArchitecture()

写了 `IController` 却不实现 `GetArchitecture()`，编译会直接报错。只需实现：

```csharp
public class MyViewController : MonoBehaviour, IController
{
    // 必须实现：告诉框架我属于哪个架构
    public IArchitecture GetArchitecture() => CounterApp.Interface;
}
```

### 问题 4：Model 里写逻辑

```csharp
// 错误：Model 里出现 if-else 业务逻辑
public class CounterModel : AbstractModel
{
    public BindableProperty<int> Count { get; } = new BindableProperty<int>(0);

    public void AddCount(int amount)
    {
        if (amount > 0)          // ← 这是逻辑，不属于 Model
        {
            Count.Value += amount;
        }
    }
}
```

**判断标准：方法里出现 if / for / 计算，就不该在 Model 里**。数据只存不"想"，逻辑交给 Command 或 System。

## 二、最佳实践清单

### 各层职责速查表

| 层 | 可以做什么 | 绝不能做什么 |
|---|---|---|
| **Model** | 存数据（BindableProperty）、OnInit 读存档 | 写 if-else 业务逻辑、碰 UI |
| **System** | 业务逻辑、订阅事件、读写 Model | 碰 UI（GetComponent 等） |
| **Command** | 改数据的写操作、发事件 | 碰 UI |
| **Query** | 读数据的封装 | 改数据 |
| **Controller** | 接收输入、刷新 UI、发命令/查询 | 直接改数据、写业务逻辑 |
| **Utility** | 通用工具（日志、加密等） | 存状态、写业务 |

### 五条约束

1. **写数据走 Command，读数据走 Query**——避免直接写 `model.X.Value = y`
2. **注册事件必注销**——`RegisterEvent(...).UnRegisterWhenGameObjectDestroyed(gameObject)`
3. **Model 只存数据**——看到 if-else 进 Model，就搬去 System/Command
4. **Controller 是唯一碰 Unity 组件的地方**——`GetComponent`、`Find`、UI 操作只出现在 Controller
5. **一个 Scene 对应一个 Architecture**——场景切换时调用 `Deinit()` 清理

### 场景切换的生命周期

```csharp
// 切换场景前，清理当前模块
CounterApp.Interface.Deinit();  // 反初始化：所有 System/Model 的 OnDeinit 会被调用

// 下次访问 CounterApp.Interface 时会自动重新初始化（单例惰性初始化）
```

## 三、使用建议

1. **跑通计数器 Demo**（见"完整数据流与 Demo 代码"章节），确认数据流理解正确
2. **做三个小改造**：清零按钮、金币不为负、用 Query 读数据
3. **用 QFramework 重写已有项目**——哪怕只把分数系统改成 Model + Command
4. **读官方示例**：框架自带的 `AudioKit` 就是作者用这套架构写的真实项目，遇到问题看它怎么写的

## 四、学习资源

- 官网：qframework.cn（有教程视频和文档）
- GitHub：github.com/liangxiegame/QFramework
- QQ 群：623597263（已满）/ 541745166
- bilibili：搜索"凉鞋 QFramework"有教学视频

## 四、分层架构总览

本文档覆盖内容：

```
┌────────────────── QFramework 分层架构 ──────────────────┐
│                                                        │
│   Unity 世界          ←→        框架世界                │
│                                                        │
│  MonoBehaviour                  ┌──────────┐            │
│  (Controller) ──发命令/查询──→  │Architecture│ ← 模块总管 │
│      ↑ 刷新 UI                 └─┬──┬──┬──┘            │
│      │         ┌────────────────┘  │  └───────┐        │
│      │         ▼                   ▼          ▼        │
│      │    ┌─────────┐       ┌──────────┐ ┌────────┐   │
│      └────│  Model  │◄──────│  System  │ │Utility │   │
│   自动通知 │ (数据)  │ 读写  │  (逻辑)  │ │(工具)  │   │
│           └─────────┘       └──────────┘ └────────┘   │
│                ▲ Write(Command) / Read(Query)          │
│                                                        │
│   事件系统(TypeEventSystem)贯穿全局，模块间广播通信        │
└────────────────────────────────────────────────────────┘
```

**你学到的核心能力：**

1. 用 Architecture 组织模块，Model/System/Utility 各司其职
2. 用 Command 封装写操作、Query 封装读操作
3. 用 BindableProperty 让 UI 自动响应数据变化
4. 用事件系统解耦模块通信
5. 用 Controller 桥接 MonoBehaviour 与框架
6. 用 AudioKit/UIKit/ResKit 解决音频、UI、资源三大实际问题
