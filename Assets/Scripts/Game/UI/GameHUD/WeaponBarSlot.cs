using UnityEngine;
using UnityEngine.UI;
using QFramework;

namespace Game.UI
{
	/// <summary>
	/// 武器格子元素：显示编号、武器名、弹药条、弹量与装填等级、耐久数字与换弹置灰遮罩，支持选中高亮。
	/// 纯展示组件——不订阅事件，由 GameHUD 订阅武器事件/轮询武器状态后调用本类方法刷新。
	/// 控件引用由 WeaponBarSlot.Designer.cs 提供，由格子内子物体上的 Bind 组件生成代码赋值，勿在此重复声明。
	/// </summary>
	public partial class WeaponBarSlot : UIElement
	{
		// SelectedColor：格子被选中（当前武器）时的背景色，亮灰蓝。
		private static readonly Color SelectedColor = new Color(0.45f, 0.5f, 0.6f, 0.95f);

		// NormalColor：格子未选中时的背景色，深色。
		private static readonly Color NormalColor = new Color(0.12f, 0.14f, 0.18f, 0.9f);

		// DurabilityWarningColor：耐久 ≤20%（WeaponBase.DurabilityWarningRatio）时耐久文本的警示色，橙红。
		private static readonly Color DurabilityWarningColor = new Color(1f, 0.35f, 0.25f);

		// DurabilityNormalColor：耐久正常时耐久文本的颜色，白色。
		private static readonly Color DurabilityNormalColor = Color.white;

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
		/// 刷新资源条填充比例（弹夹余量条）。
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

		/// <summary>
		/// 刷新弹量文本：GameHUD 轮询到弹量/等级要素变化时调用，文本由调用方拼好
		/// （格式 "弹量/容量 L已装>下次"），本组件不关心拼写规则。
		/// </summary>
		/// <param name="text">拼好的弹量与装填等级文本。</param>
		public void SetAmmo(string text)
		{
			AmmoText.text = text;
		}

		/// <summary>
		/// 刷新耐久数字：剩余 ≤20%（且未报废归零）时文本变警示色，否则恢复白色。
		/// </summary>
		/// <param name="current">当前耐久。</param>
		/// <param name="max">耐久上限。</param>
		public void SetDurability(float current, float max)
		{
			// warning：与 WeaponBase.DurabilityWarning 同口径——≤20% 且未报废才警示；
			// 归零即报废，格子随即被 WeaponBrokenEvent 隐藏，无需变色。
			var warning = max > 0f && current > 0f && current <= max * WeaponBase.DurabilityWarningRatio;
			DurabilityText.text = $"耐久 {Mathf.CeilToInt(current)}";
			DurabilityText.color = warning ? DurabilityWarningColor : DurabilityNormalColor;
		}

		/// <summary>
		/// 切换换弹遮罩：换弹中显示半透明遮罩把格子置灰，完成/未换弹时隐藏。
		/// </summary>
		/// <param name="reloading">是否正在换弹。</param>
		public void SetReloading(bool reloading)
		{
			if (ReloadMask.gameObject.activeSelf != reloading)
			{
				ReloadMask.gameObject.SetActive(reloading);
			}
		}

		protected override void OnBeforeDestroy()
		{
		}
	}
}
