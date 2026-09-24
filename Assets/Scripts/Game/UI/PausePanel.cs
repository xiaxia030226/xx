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
        // 作用：提供暂停面板所属的架构；返回：游戏架构接口。
        public IArchitecture GetArchitecture() => GameArchitecture.Interface; // 直接返回共享架构入口。

        // 作用：初始化面板数据并绑定继续与返回菜单按钮；返回：无返回值。
        protected override void OnInit(IUIData uiData = null)
        {
            // 使用传入数据或默认数据，按钮仅转发操作，不在面板内维护暂停状态。
            mData = uiData as PausePanelData ?? new PausePanelData();

            // 继续游戏：恢复状态与时间流速，由 GameRoot 统一处理并关闭本面板。
            ResumeButton.onClick.AddListener(GameRoot.ResumeGame);

            // 返回主菜单：恢复时间流速、关闭战斗面板、加载主菜单场景。
            MenuButton.onClick.AddListener(GameRoot.ReturnToMainMenu);
        }

        // 作用：接收暂停面板打开回调；返回：无返回值。
        protected override void OnOpen(IUIData uiData = null)
        {
            // 暂停状态与时间流速由 GameRoot 管理，此处不重复设置。
        }

        // 作用：接收暂停面板显示回调；返回：无返回值。
        protected override void OnShow()
        {
            // 按钮已在初始化时绑定，无需在显示时重复绑定。
        }

        // 作用：接收暂停面板隐藏回调；返回：无返回值。
        protected override void OnHide()
        {
            // 隐藏面板不直接恢复游戏，恢复操作交给 GameRoot。
        }

        // 作用：接收暂停面板关闭回调；返回：无返回值。
        protected override void OnClose()
        {
            // 关闭面板不额外切换状态，避免与 GameRoot 的流程重复。
        }
    }
}
