using System;
using UnityEngine;
using UnityEngine.UI;
using QFramework;

namespace Game.UI
{
	// Generate Id:bbac1d32-d27d-427e-a907-db635f9ece55
	public partial class WeaponBarHUD
	{
		public const string Name = "WeaponBarHUD";
		
		[SerializeField]
		public UnityEngine.UI.Image Slot1;
		[SerializeField]
		public TMPro.TextMeshProUGUI Number;
		[SerializeField]
		public TMPro.TextMeshProUGUI WeaponBarName;
		[SerializeField]
		public UnityEngine.UI.Image ResourceBarFill;
		
		private WeaponBarHUDData mPrivateData = null;
		
		protected override void ClearUIComponents()
		{
			Slot1 = null;
			Number = null;
			WeaponBarName = null;
			ResourceBarFill = null;
			
			mData = null;
		}
		
		public WeaponBarHUDData Data
		{
			get
			{
				return mData;
			}
		}
		
		WeaponBarHUDData mData
		{
			get
			{
				return mPrivateData ?? (mPrivateData = new WeaponBarHUDData());
			}
			set
			{
				mUIData = value;
				mPrivateData = value;
			}
		}
	}
}
