using QFramework;
using UnityEngine;

namespace Game.UI
{
    /// <summary>
    /// 打开结算面板时传入的本局战绩，由 GameRoot 在结算时填充。
    /// </summary>
    public class ResultPanelData : UIPanelData
    {
        // Victory：true 为通关胜利，false 为死亡失败。
        public bool Victory;

        // Kills：本局击杀总数。
        public int Kills;

        // DropGold：本局掉落入账（RunGold 统计）。
        public int DropGold;

        // ClearBonus：通关奖励（胜 500 / 败 0）。
        public int ClearBonus;

        // GoldEarned：本局结算获得的金币合计（DropGold + ClearBonus）。
        public int GoldEarned;
    }

    /// <summary>
    /// 结算面板：战斗结束时弹出，展示胜负、击杀数与本局金币，可返回主菜单。
    /// 控件引用（Title/KillText/GoldText/TotalGoldText/MenuButton）由 Designer 提供。
    /// </summary>
    public partial class ResultPanel : UIPanel, IController
    {
        public IArchitecture GetArchitecture() => GameArchitecture.Interface;

        protected override void OnInit(IUIData uiData = null)
        {
            mData = uiData as ResultPanelData ?? new ResultPanelData();

            // 返回主菜单：恢复时间流速、关闭战斗面板、加载主菜单场景。
            MenuButton.onClick.AddListener(GameRoot.ReturnToMainMenu);
        }

        /// <summary>
        /// 每次打开时按本局战绩刷新显示。
        /// </summary>
        protected override void OnOpen(IUIData uiData = null)
        {
            Title.text = Data.Victory ? "胜利！" : "失败……";
            KillText.text = $"击杀数：{Data.Kills}";
            GoldText.text = $"金币：掉落 {Data.DropGold} + 通关奖励 {Data.ClearBonus} = {Data.GoldEarned}";

            // 总金币从 Model 读取——结算命令已在面板打开前执行，这里读到的是入账后的值。
            TotalGoldText.text = $"总金币：{this.GetModel<IEconomyModel>().Gold.Value}";
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
