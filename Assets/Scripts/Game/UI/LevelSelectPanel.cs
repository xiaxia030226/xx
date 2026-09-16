using QFramework;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Game.UI
{
    /// <summary>
    /// 打开选关面板时可传入的数据。当前没有额外参数。
    /// </summary>
    public class LevelSelectPanelData : UIPanelData
    {
    }

    /// <summary>
    /// 选关面板：关卡一可进入战斗；关卡二内容未实装，按钮禁用置灰占位。
    /// 控件引用（Level1Button/Level2Button/BackButton）由 LevelSelectPanel.Designer.cs 提供。
    /// </summary>
    public partial class LevelSelectPanel : UIPanel, IController
    {
        public IArchitecture GetArchitecture() => GameArchitecture.Interface;

        protected override void OnInit(IUIData uiData = null)
        {
            mData = uiData as LevelSelectPanelData ?? new LevelSelectPanelData();

            // 关卡一：记录所选关卡编号，关闭面板后加载战斗场景。
            Level1Button.onClick.AddListener(() =>
            {
                this.GetModel<IGameStateModel>().SelectedLevel.Value = 1;
                CloseSelf();
                SceneManager.LoadScene("Game");
            });

            // 关卡二"幽暗森林"尚未实装：禁用点击，仅作展示占位。
            Level2Button.interactable = false;

            // 返回：回主菜单状态并重新打开主菜单面板。
            BackButton.onClick.AddListener(() =>
            {
                this.GetModel<IGameStateModel>().State.Value = GameState.MainMenu;
                UIKit.OpenPanel<MainMenuPanel>();
                CloseSelf();
            });
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
