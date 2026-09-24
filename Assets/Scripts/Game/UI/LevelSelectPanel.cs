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
        // 作用：提供选关面板访问模型所用的架构；返回：游戏架构接口。
        public IArchitecture GetArchitecture() => GameArchitecture.Interface; // 直接返回共享架构入口。

        // 作用：初始化面板数据、关卡入口与返回按钮；返回：无返回值。
        protected override void OnInit(IUIData uiData = null)
        {
            // 优先使用传入数据，缺省时创建默认数据，再绑定关卡跳转操作。
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

        // 作用：接收选关面板打开回调；返回：无返回值。
        protected override void OnOpen(IUIData uiData = null)
        {
            // 当前关卡入口在初始化时固定，无需逐次打开刷新。
        }

        // 作用：接收选关面板显示回调；返回：无返回值。
        protected override void OnShow()
        {
            // 关卡按钮已绑定，显示时无需额外处理。
        }

        // 作用：接收选关面板隐藏回调；返回：无返回值。
        protected override void OnHide()
        {
            // 没有需随隐藏暂停的面板内部流程。
        }

        // 作用：接收选关面板关闭回调；返回：无返回值。
        protected override void OnClose()
        {
            // 状态或场景跳转由按钮回调处理，关闭时不重复触发。
        }
    }
}
