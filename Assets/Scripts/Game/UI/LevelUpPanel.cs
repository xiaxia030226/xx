using System;
using UnityEngine;
using UnityEngine.UI;
using QFramework;

namespace Game.UI
{
	public class LevelUpPanelData : UIPanelData
	{
	}

	/// <summary>
	/// 升级三选一面板：玩家升级时弹出，从三个强化中选一个，选中后立即生效并关闭。
	/// 控件引用（Title/Option1~3 及其 Label）由 LevelUpPanel.Designer.cs 提供，
	/// 由预制体上的 Bind 组件生成代码自动赋值，勿在逻辑文件中重复声明。
	/// 实现 IController 访问架构；额外实现 ICanSendEvent 才能在关闭时广播事件。
	/// </summary>
	public partial class LevelUpPanel : UIPanel, IController, ICanSendEvent
	{
		public IArchitecture GetArchitecture() => GameArchitecture.Interface;

		protected override void OnInit(IUIData uiData = null)
		{
			mData = uiData as LevelUpPanelData ?? new LevelUpPanelData();
			// UI 元素已由预制体提供，无需代码创建；选项内容在 OnOpen 中刷新。
		}

		/// <summary>
		/// 每次打开面板都会执行：连升多级时会复用同一面板实例，
		/// 因此选项必须在这里刷新，保证每次都展示最新的三个强化。
		/// </summary>
		protected override void OnOpen(IUIData uiData = null)
		{
			// weapon：当前玩家持有的第一把（也是唯一一把）武器，升级直接作用在它身上。
			var weapon = this.GetSystem<IWeaponSystem>().Weapons[0];

			// 依次刷新三个选项的文字与点击回调。
			BindOption(Option1, Option1Label, "铁剑伤害 +5", () => weapon.UpgradeDamage(5f));
			BindOption(Option2, Option2Label, "铁剑能量上限 +20", () => weapon.UpgradeResource(20f));
			BindOption(Option3, Option3Label, "恢复 30 生命", () => this.SendCommand(new PlayerHealCommand(30)));
		}

		protected override void OnShow()
		{
		}

		protected override void OnHide()
		{
		}

		/// <summary>
		/// 面板关闭时广播通知：让 GameRoot 判断是否还有待处理的升级。
		/// </summary>
		protected override void OnClose()
		{
			this.SendEvent<LevelUpPanelClosedEvent>();
		}

		/// <summary>
		/// 给一个选项按钮绑定文字与点击回调：
		/// 点击后先执行强化效果，再关闭面板（关闭会触发 LevelUpPanelClosedEvent）。
		/// </summary>
		/// <param name="button">预制体上绑定的按钮控件。</param>
		/// <param name="label">按钮上的文字控件，用于显示强化说明。</param>
		/// <param name="text">本次强化的说明文字。</param>
		/// <param name="onPick">点击按钮时要执行的强化逻辑。</param>
		private void BindOption(Button button, TMPro.TextMeshProUGUI label, string text, Action onPick)
		{
			// 第一步：设置按钮文字，说明该强化的效果。
			label.text = text;

			// 第二步：先清空旧监听——面板复用时 OnOpen 会多次绑定，不清空会重复执行。
			button.onClick.RemoveAllListeners();

			// 第三步：绑定新回调——先执行强化，再关闭面板。
			button.onClick.AddListener(() =>
			{
				onPick?.Invoke();
				CloseSelf();
			});
		}
	}
}
