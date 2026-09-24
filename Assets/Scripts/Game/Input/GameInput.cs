using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// 游戏输入的统一入口。
/// 这里把具体键位包装成有业务含义的 InputAction，其他脚本只关心“移动”“攻击”等动作。
/// </summary>
public static class GameInput
{
    public static InputAction Move { get; private set; } // WASD 合成的二维移动输入。
    public static InputAction MousePosition { get; private set; } // 鼠标屏幕坐标输入，供瞄准逻辑读取。
    public static InputAction Attack { get; private set; } // 鼠标左键攻击输入，单发或连发由调用者决定。
    public static InputAction Skill1 { get; private set; } // 空格触发的第一技能输入。
    public static InputAction Skill2 { get; private set; } // E 键触发的第二技能输入。
    public static InputAction[] SwitchSlots { get; private set; } // 数字键 1～9 对应的九个武器槽动作，下标从零开始。
    public static InputAction ScrollWeapon { get; private set; } // 鼠标滚轮二维输入，供调用者判断切枪方向。
    public static InputAction Reload { get; private set; } // R 键主动换弹请求，实际预扣由武器逻辑处理。
    public static InputAction CycleBulletLevel { get; private set; } // B 键循环选择下次装填等级，不直接改当前弹夹。
    public static InputAction CallNextWave { get; private set; } // F 键提前召唤下一波的输入。
    public static InputAction Pause { get; private set; } // Escape 暂停或恢复输入。
    public static InputAction BuildView { get; private set; } // Tab 构筑查看输入。
    public static InputAction DebugDamage { get; private set; } // K 键调试受伤输入，仅调试逻辑消费。
    public static InputAction DebugHeal { get; private set; } // H 键调试治疗输入，仅调试逻辑消费。
    public static InputAction DebugAmmo { get; private set; } // N 键调试补弹输入，仅调试逻辑消费。

    private static bool mInitialized; // 静态初始化守卫，跨场景重复进入时不重建动作。

    // 作用：幂等创建并启用输入动作，关闭会干扰鼠标的触摸模拟；返回：无返回值。
    public static void Init()
    {
        if (mInitialized) return;
        mInitialized = true;

        // 将 W、A、S、D 四个按钮合成为一个 Vector2，读取时可直接得到移动方向。
        Move = new InputAction("Move", InputActionType.Value, expectedControlType: "Vector2");
        Move.AddCompositeBinding("2DVector")
            .With("Up", "<Keyboard>/w")
            .With("Down", "<Keyboard>/s")
            .With("Left", "<Keyboard>/a")
            .With("Right", "<Keyboard>/d");

        MousePosition = new InputAction("MousePosition", InputActionType.Value, "<Mouse>/position",
            expectedControlType: "Vector2");
        Attack = new InputAction("Attack", InputActionType.Button, "<Mouse>/leftButton");
        Skill1 = new InputAction("Skill1", InputActionType.Button, "<Keyboard>/space");
        Skill2 = new InputAction("Skill2", InputActionType.Button, "<Keyboard>/e");

        // 数字键 1~9 使用相同规则生成，避免重复写九组绑定代码。
        SwitchSlots = new InputAction[9];
        for (var i = 0; i < SwitchSlots.Length; i++)
        {
            SwitchSlots[i] = new InputAction($"SwitchSlot{i + 1}", InputActionType.Button, $"<Keyboard>/{i + 1}");
        }

        ScrollWeapon = new InputAction("ScrollWeapon", InputActionType.Value, "<Mouse>/scroll",
            expectedControlType: "Vector2");
        Reload = new InputAction("Reload", InputActionType.Button, "<Keyboard>/r");
        CycleBulletLevel = new InputAction("CycleBulletLevel", InputActionType.Button, "<Keyboard>/b");
        CallNextWave = new InputAction("CallNextWave", InputActionType.Button, "<Keyboard>/f");
        Pause = new InputAction("Pause", InputActionType.Button, "<Keyboard>/escape");
        BuildView = new InputAction("BuildView", InputActionType.Button, "<Keyboard>/tab");
        DebugDamage = new InputAction("DebugDamage", InputActionType.Button, "<Keyboard>/k");
        DebugHeal = new InputAction("DebugHeal", InputActionType.Button, "<Keyboard>/h");
        DebugAmmo = new InputAction("DebugAmmo", InputActionType.Button, "<Keyboard>/n");

        EnableAll();

        // 防御：编辑器中开启“触摸模拟”（Input Debugger 的 Simulate Touch Input From Mouse or Pen）
        // 会禁用 Mouse 设备，导致游戏里鼠标位置和按键全部失效；这里强制关闭模拟并确保鼠标处于启用状态。
        if (UnityEngine.InputSystem.EnhancedTouch.TouchSimulation.instance != null)
        {
            UnityEngine.InputSystem.EnhancedTouch.TouchSimulation.Disable();
        }
        if (Mouse.current != null && !Mouse.current.enabled)
        {
            InputSystem.EnableDevice(Mouse.current);
        }
    }

    // 作用：启用所有已创建动作，使调用者能够读取按键和值；返回：无返回值。
    private static void EnableAll()
    {
        // 动作创建后默认不可用，统一启用，避免某个已绑定按键始终无法被读取。
        Move.Enable();
        MousePosition.Enable();
        Attack.Enable();
        Skill1.Enable();
        Skill2.Enable();
        foreach (var slot in SwitchSlots) slot.Enable();
        ScrollWeapon.Enable();
        Reload.Enable();
        CycleBulletLevel.Enable();
        CallNextWave.Enable();
        Pause.Enable();
        BuildView.Enable();
        DebugDamage.Enable();
        DebugHeal.Enable();
        DebugAmmo.Enable();
    }
}
