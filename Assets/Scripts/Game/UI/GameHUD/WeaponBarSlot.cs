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
		private static readonly Color SelectedColor = new Color(0.45f, 0.5f, 0.6f, 0.95f); // 当前选中武器格子的亮灰蓝背景色。

		private static readonly Color NormalColor = new Color(0.12f, 0.14f, 0.18f, 0.9f); // 未选中武器格子的深色背景色。

		private static readonly Color DurabilityWarningColor = new Color(1f, 0.35f, 0.25f); // 耐久未归零且达到警示阈值时使用的橙红文本色。

		private static readonly Color DurabilityNormalColor = Color.white; // 未触发耐久警示时使用的白色文本色。

		private Image mBackground; // 格子根物体的背景图缓存，用于切换选中高亮。

		// 作用：在组件唤醒时缓存格子背景图；返回：无返回值。
		private void Awake()
		{
			// 背景图挂在格子根物体自身上，Awake 时取一次缓存，避免每次刷新都查找组件。
			mBackground = GetComponent<Image>();
		}

		// 作用：设置武器格子的显示编号与名称；返回：无返回值。
		public void Setup(int slotIndex, string weaponName)
		{
			// 槽位下标从零开始，展示编号加一，名称直接使用调用方传入的值。
			Number.text = (slotIndex + 1).ToString();
			WeaponBarName.text = weaponName;
		}

		// 作用：按是否选中切换格子背景色；返回：无返回值。
		public void SetSelected(bool selected)
		{
			// 缺少背景图时跳过显示更新；选中使用亮色，否则恢复深色。
			if (mBackground == null) return;
			mBackground.color = selected ? SelectedColor : NormalColor;
		}

		// 作用：按当前资源与上限刷新弹药条比例；返回：无返回值。
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

		// 作用：显示调用方已拼好的弹量与装填等级文本；返回：无返回值。
		public void SetAmmo(string text)
		{
			// 文本内容与变化检测由 GameHUD 负责，此处只赋值。
			AmmoText.text = text;
		}

		// 作用：刷新耐久数值并按剩余比例设置警示色；返回：无返回值。
		public void SetDurability(float current, float max)
		{
			// 耐久大于零且不超过配置阈值时警示；显示数值向上取整，判断仍使用原始值。
			var warning = max > 0f && current > 0f && current <= max * WeaponBase.DurabilityWarningRatio;
			DurabilityText.text = $"耐久 {Mathf.CeilToInt(current)}";
			DurabilityText.color = warning ? DurabilityWarningColor : DurabilityNormalColor;
		}

		// 作用：按换弹状态显示或隐藏遮罩；返回：无返回值。
		public void SetReloading(bool reloading)
		{
			// 仅在显隐状态不一致时设置，避免轮询重复触发激活操作。
			if (ReloadMask.gameObject.activeSelf != reloading)
			{
				ReloadMask.gameObject.SetActive(reloading);
			}
		}

		// 作用：接收元素销毁前回调；返回：无返回值。
		protected override void OnBeforeDestroy()
		{
			// 此组件只展示数据，没有独立订阅或额外资源需要释放。
		}
	}
}
