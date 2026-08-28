using UnityEngine.InputSystem;

/// <summary>
/// 游戏输入的统一入口。
/// 这里把具体键位包装成有业务含义的 InputAction，其他脚本只关心“移动”“攻击”等动作。
/// </summary>
public static class GameInput
{
    public static InputAction Move { get; private set; }
    public static InputAction MousePosition { get; private set; }
    public static InputAction Attack { get; private set; }
    public static InputAction Skill1 { get; private set; }
    public static InputAction Skill2 { get; private set; }
    public static InputAction[] SwitchSlots { get; private set; }
    public static InputAction ScrollWeapon { get; private set; }
    public static InputAction Pause { get; private set; }
    public static InputAction BuildView { get; private set; }
    public static InputAction DebugDamage { get; private set; }
    public static InputAction DebugHeal { get; private set; }
    public static InputAction DebugExp { get; private set; }

    private static bool mInitialized;

    /// <summary>
    /// 创建并启用全部输入动作。重复调用会直接返回，防止重复绑定按键。
    /// </summary>
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
        Pause = new InputAction("Pause", InputActionType.Button, "<Keyboard>/escape");
        BuildView = new InputAction("BuildView", InputActionType.Button, "<Keyboard>/tab");
        DebugDamage = new InputAction("DebugDamage", InputActionType.Button, "<Keyboard>/k");
        DebugHeal = new InputAction("DebugHeal", InputActionType.Button, "<Keyboard>/h");
        DebugExp = new InputAction("DebugExp", InputActionType.Button, "<Keyboard>/l");

        EnableAll();
    }

    /// <summary>
    /// InputAction 创建后默认不可用，必须 Enable 才能读取按键和值。
    /// </summary>
    private static void EnableAll()
    {
        Move.Enable();
        MousePosition.Enable();
        Attack.Enable();
        Skill1.Enable();
        Skill2.Enable();
        foreach (var slot in SwitchSlots) slot.Enable();
        ScrollWeapon.Enable();
        Pause.Enable();
        BuildView.Enable();
        DebugDamage.Enable();
        DebugHeal.Enable();
        DebugExp.Enable();
    }
}
