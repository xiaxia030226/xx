using Game.UI;
using QFramework;
using UnityEngine;

/// <summary>
/// 主菜单场景入口。负责输入与架构的幂等初始化，并打开主菜单面板。
/// 从战斗场景返回主菜单时本组件会再次执行：GameInput.Init 有重复调用守卫，
/// 架构单例只创建一次，因此重复进入是安全的。
/// </summary>
public class MainMenuRoot : MonoBehaviour, IController
{
    // 作用：提供菜单控制器所属的 QFramework 架构；返回：全局游戏架构接口。
    public IArchitecture GetArchitecture() => GameArchitecture.Interface; // 直接委托给架构单例入口。

    // 作用：在主菜单场景唤醒时初始化公共入口并打开菜单；返回：无返回值。
    private void Awake()
    {
        // 第一步：输入与架构初始化。GameInput.Init 幂等，架构单例跨场景只创建一次。
        GameInput.Init();
        _ = GameArchitecture.Interface;

        // 第二步：设置 UIKit 配置，面板类型按名字映射到 Resources/UI 路径。
        UIKit.Config = new GameUIKitConfig();

        // 第三步：进入主菜单状态并打开主菜单面板。
        this.GetModel<IGameStateModel>().State.Value = GameState.MainMenu;
        UIKit.OpenPanel<MainMenuPanel>();
    }
}
