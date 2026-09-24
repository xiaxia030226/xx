using QFramework;
using UnityEngine;

namespace Game.UI
{
    /// <summary>
    /// 打开结算面板时传入的本局战绩，由 GameRoot 在结算时填充。
    /// </summary>
    public class ResultPanelData : UIPanelData
    {
        public bool Victory; // 本局胜负：true 表示通关胜利，false 表示失败。

        public int Kills; // 本局击杀总数。

        public int DropGold; // 本局掉落入账金币，由 RunGold 统计。

        public int ClearBonus; // 本局结算的通关奖励金币。

        public int GoldEarned; // 本局所得金币合计，即掉落金币与通关奖励之和。
    }

    /// <summary>
    /// 结算面板：战斗结束时弹出，展示胜负、击杀数与本局金币，可返回主菜单。
    /// 控件引用（Title/KillText/GoldText/TotalGoldText/MenuButton）由 Designer 提供。
    /// </summary>
    public partial class ResultPanel : UIPanel, IController
    {
        // 作用：提供读取结算后经济数据所用的架构；返回：游戏架构接口。
        public IArchitecture GetArchitecture() => GameArchitecture.Interface; // 直接返回共享架构入口。

        // 作用：初始化结算数据并绑定返回主菜单操作；返回：无返回值。
        protected override void OnInit(IUIData uiData = null)
        {
            // 优先接收本局战绩，缺少有效数据时使用默认值。
            mData = uiData as ResultPanelData ?? new ResultPanelData();

            // 返回主菜单：恢复时间流速、关闭战斗面板、加载主菜单场景。
            MenuButton.onClick.AddListener(GameRoot.ReturnToMainMenu);
        }

        // 作用：每次打开时刷新本局战绩和累计金币；返回：无返回值。
        protected override void OnOpen(IUIData uiData = null)
        {
            // 先根据面板保存的结算数据显示胜负、击杀数和本局金币组成。
            Title.text = Data.Victory ? "胜利！" : "失败……";
            KillText.text = $"击杀数：{Data.Kills}";
            GoldText.text = $"金币：掉落 {Data.DropGold} + 通关奖励 {Data.ClearBonus} = {Data.GoldEarned}";

            // 总金币从 Model 读取——结算命令已在面板打开前执行，这里读到的是入账后的值。
            TotalGoldText.text = $"总金币：{this.GetModel<IEconomyModel>().Gold.Value}";
        }

        // 作用：接收结算面板显示回调；返回：无返回值。
        protected override void OnShow()
        {
            // 战绩已在 OnOpen 刷新，显示时无需重复赋值。
        }

        // 作用：接收结算面板隐藏回调；返回：无返回值。
        protected override void OnHide()
        {
            // 结算展示没有需在隐藏时暂停的内部流程。
        }

        // 作用：接收结算面板关闭回调；返回：无返回值。
        protected override void OnClose()
        {
            // 返回菜单由按钮委托 GameRoot 执行，此处不重复触发。
        }
    }
}
