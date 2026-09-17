/****************************************************************************
 * 2026.9 DESKTOP-LEEHV4H
 ****************************************************************************/

using UnityEngine;
using UnityEngine.UI;
using QFramework;

namespace Game.UI
{
	public partial class WeaponBarSlot
	{
		[SerializeField] public TMPro.TextMeshProUGUI Number;
		[SerializeField] public TMPro.TextMeshProUGUI WeaponBarName;
		[SerializeField] public UnityEngine.UI.Image ResourceBarFill;
		[SerializeField] public TMPro.TextMeshProUGUI AmmoText;
		[SerializeField] public TMPro.TextMeshProUGUI DurabilityText;
		[SerializeField] public UnityEngine.UI.Image ReloadMask;

		public void Clear()
		{
			Number = null;
			WeaponBarName = null;
			ResourceBarFill = null;
			AmmoText = null;
			DurabilityText = null;
			ReloadMask = null;
		}

		public override string ComponentName
		{
			get { return "WeaponBarSlot";}
		}
	}
}
