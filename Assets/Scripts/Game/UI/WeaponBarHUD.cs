using UnityEngine;
using UnityEngine.UI;
using QFramework;

namespace Game.UI
{
	public class WeaponBarHUDData : UIPanelData
	{
	}

	/// <summary>
	/// 屏幕下方的武器格子条：显示武器名与资源条，高亮当前选中格。
	/// 控件引用（Slot1/Number/WeaponBarName/ResourceBarFill）由 WeaponBarHUD.Designer.cs 提供，
	/// 由预制体上的 Bind 组件生成代码自动赋值，勿在逻辑文件中重复声明。
	/// 监听 WeaponSwitchedEvent 与 WeaponResourceChangedEvent 自动刷新。
	/// </summary>
	public partial class WeaponBarHUD : UIPanel, IController
	{
		// mSlotBackgrounds：所有武器格的背景图数组，用于切换高亮（当前只有 Slot1 一格）。
		private Image[] mSlotBackgrounds;

		// mResourceFills：所有武器格的资源条填充图数组，用于刷新剩余资源。
		private Image[] mResourceFills;

		public IArchitecture GetArchitecture() => GameArchitecture.Interface;

		protected override void OnInit(IUIData uiData = null)
		{
			mData = uiData as WeaponBarHUDData ?? new WeaponBarHUDData();

			// 第一步：把 Designer 绑定的单个控件组织成数组，便于按槽位下标访问。
			mSlotBackgrounds = new[] { Slot1 };
			mResourceFills = new[] { ResourceBarFill };

			// 第二步：初始化格子的静态显示——编号固定为 1，武器名从武器系统读取。
			var weaponSystem = this.GetSystem<IWeaponSystem>();
			Number.text = "1";
			WeaponBarName.text = weaponSystem.Weapons[0].Name;

			// 第三步：监听"切换武器"事件，用于更新格子高亮。
			this.RegisterEvent<WeaponSwitchedEvent>(OnWeaponSwitched)
				.UnRegisterWhenGameObjectDestroyed(gameObject);

			// 第四步：监听"武器资源变化"事件，用于更新能量/弹药条。
			this.RegisterEvent<WeaponResourceChangedEvent>(OnResourceChanged)
				.UnRegisterWhenGameObjectDestroyed(gameObject);

			// 第五步：立即用初始数据刷新一次高亮与资源条，面板打开时就显示正确状态。
			OnWeaponSwitched(new WeaponSwitchedEvent { SlotIndex = weaponSystem.CurrentIndex });
			OnResourceChanged(new WeaponResourceChangedEvent
			{
				SlotIndex = 0,
				Current = weaponSystem.Weapons[0].Resource,
				Max = weaponSystem.Weapons[0].ResourceMax
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

		/// <summary>
		/// 面板关闭时执行：无需要清理的资源，事件注销已交给 UnRegisterWhenGameObjectDestroyed。
		/// </summary>
		protected override void OnClose()
		{
		}

		/// <summary>
		/// 切换武器时刷新格子高亮：选中格用亮色，其余保持深色。
		/// </summary>
		/// <param name="e">携带当前选中槽位下标的事件数据。</param>
		private void OnWeaponSwitched(WeaponSwitchedEvent e)
		{
			// 遍历全部格子背景，按是否选中分别设置颜色。
			for (var i = 0; i < mSlotBackgrounds.Length; i++)
			{
				mSlotBackgrounds[i].color = i == e.SlotIndex
					? new Color(0.45f, 0.5f, 0.6f, 0.95f)   // 选中格：亮灰蓝。
					: new Color(0.12f, 0.14f, 0.18f, 0.9f);  // 未选中格：深色。
			}
		}

		/// <summary>
		/// 武器资源变化时刷新对应格子的资源条比例。
		/// </summary>
		/// <param name="e">携带槽位下标与当前/最大资源值的事件数据。</param>
		private void OnResourceChanged(WeaponResourceChangedEvent e)
		{
			// 槽位下标越界时直接忽略（当前只有一格，只处理下标 0）。
			if (e.SlotIndex < 0 || e.SlotIndex >= mResourceFills.Length) return;

			// ratio：剩余资源占比，范围限制在 0-1 之间。
			var ratio = e.Max > 0f ? Mathf.Clamp01(e.Current / e.Max) : 0f;

			// 与 GameHUD 血条相同：修改右侧锚点表现填充比例，ratio=1 铺满，ratio=0 为空。
			var rect = mResourceFills[e.SlotIndex].rectTransform;
			rect.anchorMin = Vector2.zero;
			rect.anchorMax = new Vector2(ratio, 1f);
			rect.offsetMin = Vector2.zero;
			rect.offsetMax = Vector2.zero;
		}
	}
}
