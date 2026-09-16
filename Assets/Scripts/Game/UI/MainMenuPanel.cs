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
        public IArchitecture GetArchitecture() => GameArchitecture.Interface;

        protected override void OnInit(IUIData uiData = null)
        {
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

        protected override void OnOpen(IUIData uiData = null)
        {
        }

        protected override void OnShow()
        {
        }

        protected override void OnHide()
        {
        }

        protected override void OnClose()
        {
        }
    }
}
