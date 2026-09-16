using QFramework;
using UnityEngine;

namespace Game.UI
{
    /// <summary>
    /// 打开暂停面板时可传入的数据。当前没有额外参数。
    /// </summary>
    public class PausePanelData : UIPanelData
    {
    }

    /// <summary>
    /// 暂停面板：战斗中按 ESC 弹出，提供继续游戏与返回主菜单。
    /// 暂停/恢复的时间冻结与状态切换由 GameRoot 统一管理，面板只负责转发意图。
    /// </summary>
    public partial class PausePanel : UIPanel, IController
    {
        public IArchitecture GetArchitecture() => GameArchitecture.Interface;

        protected override void OnInit(IUIData uiData = null)
        {
            mData = uiData as PausePanelData ?? new PausePanelData();

            // 继续游戏：恢复状态与时间流速，由 GameRoot 统一处理并关闭本面板。
            ResumeButton.onClick.AddListener(GameRoot.ResumeGame);

            // 返回主菜单：恢复时间流速、关闭战斗面板、加载主菜单场景。
            MenuButton.onClick.AddListener(GameRoot.ReturnToMainMenu);
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
