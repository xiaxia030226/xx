using QFramework;
using UnityEngine;

namespace Game.UI
{
    /// <summary>
    /// 打开主菜单面板时可传入的数据。当前没有额外参数。
    /// </summary>
    public class MainMenuPanelData : UIPanelData
    {
    }

    /// <summary>
    /// 主菜单面板：游戏标题 + 开始游戏（进选关）+ 退出游戏。
    /// 控件引用（Title/StartButton/QuitButton）由 MainMenuPanel.Designer.cs 提供，
    /// 由预制体上的 Bind 组件生成代码自动赋值，勿在逻辑文件中重复声明。
    /// </summary>
    public partial class MainMenuPanel : UIPanel, IController
    {
        // 作用：提供主菜单访问模型所用的架构；返回：游戏架构接口。
        public IArchitecture GetArchitecture() => GameArchitecture.Interface; // 直接返回共享架构入口。

        // 作用：初始化面板数据并绑定开始和退出按钮；返回：无返回值。
        protected override void OnInit(IUIData uiData = null)
        {
            // 使用传入的面板数据，类型不匹配或为空时使用默认数据。
            mData = uiData as MainMenuPanelData ?? new MainMenuPanelData();

            // 开始游戏：进入选关状态并打开选关面板，主菜单面板随之关闭。
            StartButton.onClick.AddListener(() =>
            {
                this.GetModel<IGameStateModel>().State.Value = GameState.LevelSelect;
                UIKit.OpenPanel<LevelSelectPanel>();
                CloseSelf();
            });

            // 退出游戏：编辑器中点击无效，打包后可正常退出。
            QuitButton.onClick.AddListener(Application.Quit);
        }

        // 作用：接收主菜单打开回调；返回：无返回值。
        protected override void OnOpen(IUIData uiData = null)
        {
            // 主菜单没有每次打开时需要刷新的动态数据，保留空回调。
        }

        // 作用：接收主菜单显示回调；返回：无返回值。
        protected override void OnShow()
        {
            // 按钮已在初始化时绑定，显示时无需重复处理。
        }

        // 作用：接收主菜单隐藏回调；返回：无返回值。
        protected override void OnHide()
        {
            // 面板没有需在隐藏时暂停的自有流程。
        }

        // 作用：接收主菜单关闭回调；返回：无返回值。
        protected override void OnClose()
        {
            // 本面板未持有需在此释放的额外资源。
        }
    }
}
