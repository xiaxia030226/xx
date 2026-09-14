using UnityEngine;
using UnityEngine.UI;
using QFramework;

namespace Game.UI
{
	/// <summary>
	/// 武器格子元素：显示编号、武器名与资源条，支持选中高亮。
	/// 纯展示组件——不订阅事件，由 GameHUD 订阅武器事件后调用本类方法刷新。
	/// 控件引用（Number/WeaponBarName/ResourceBarFill）由 WeaponBarSlot.Designer.cs 提供，
	/// 由格子内子物体上的 Bind 组件生成代码自动赋值，勿在此重复声明。
	/// </summary>
	public partial class WeaponBarSlot : UIElement
	{
		// SelectedColor：格子被选中（当前武器）时的背景色，亮灰蓝。
		private static readonly Color SelectedColor = new Color(0.45f, 0.5f, 0.6f, 0.95f);

		// NormalColor：格子未选中时的背景色，深色。
		private static readonly Color NormalColor = new Color(0.12f, 0.14f, 0.18f, 0.9f);

		// mBackground：格子根物体自身的背景图，用于切换选中高亮。
		private Image mBackground;

		private void Awake()
		{
			// 背景图挂在格子根物体自身上，Awake 时取一次缓存，避免每次刷新都查找组件。
			mBackground = GetComponent<Image>();
		}

		/// <summary>
		/// 初始化格子的静态显示：编号与武器名。槽位下标从 0 开始，显示给玩家的编号是下标 + 1。
		/// </summary>
		/// <param name="slotIndex">槽位下标，从 0 开始。</param>
		/// <param name="weaponName">该槽位武器的名称。</param>
		public void Setup(int slotIndex, string weaponName)
		{
			Number.text = (slotIndex + 1).ToString();
			WeaponBarName.text = weaponName;
		}

		/// <summary>
		/// 切换选中状态：选中用亮色背景，未选中恢复深色。
		/// </summary>
		/// <param name="selected">是否为当前选中的武器槽。</param>
		public void SetSelected(bool selected)
		{
			if (mBackground == null) return;
			mBackground.color = selected ? SelectedColor : NormalColor;
		}

		/// <summary>
		/// 刷新资源条填充比例。
		/// </summary>
		/// <param name="current">当前资源值。</param>
		/// <param name="max">资源上限。</param>
		public void SetResource(float current, float max)
		{
			// ratio：剩余资源占比，范围限制在 0-1 之间。
			var ratio = max > 0f ? Mathf.Clamp01(current / max) : 0f;

			// 与 GameHUD 血条相同：修改右侧锚点表现填充比例，ratio=1 铺满，ratio=0 为空。
			var rect = ResourceBarFill.rectTransform;
			rect.anchorMin = Vector2.zero;
			rect.anchorMax = new Vector2(ratio, 1f);
			rect.offsetMin = Vector2.zero;
			rect.offsetMax = Vector2.zero;
		}

		protected override void OnBeforeDestroy()
		{
		}
	}
}
