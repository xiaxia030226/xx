using System;
using UnityEngine;
using UnityEngine.UI;
using QFramework;

namespace Game.UI
{
	// Generate Id:5ef6e266-2258-4a62-9800-ee6c441bed0d
	public partial class GameHUD
	{
		public const string Name = "GameHUD";
		
		[SerializeField]
		public UnityEngine.UI.Image HpFill;
		[SerializeField]
		public TMPro.TextMeshProUGUI HpText;
		[SerializeField]
		public WeaponBarSlot Slot1;
		[SerializeField]
		public WeaponBarSlot Slot2;
		[SerializeField]
		public WeaponBarSlot Slot3;
		[SerializeField]
		public WeaponBarSlot Slot4;
		[SerializeField]
		public WeaponBarSlot Slot5;
		[SerializeField]
		public WeaponBarSlot Slot6;
		[SerializeField]
		public WeaponBarSlot Slot7;
		[SerializeField]
		public WeaponBarSlot Slot8;
		[SerializeField]
		public WeaponBarSlot Slot9;
		
		private GameHUDData mPrivateData = null;
		
		protected override void ClearUIComponents()
		{
			HpFill = null;
			HpText = null;
			Slot1 = null;
			Slot2 = null;
			Slot3 = null;
			Slot4 = null;
			Slot5 = null;
			Slot6 = null;
			Slot7 = null;
			Slot8 = null;
			Slot9 = null;
			
			mData = null;
		}
		
		public GameHUDData Data
		{
			get
			{
				return mData;
			}
		}
		
		GameHUDData mData
		{
			get
			{
				return mPrivateData ?? (mPrivateData = new GameHUDData());
			}
			set
			{
				mUIData = value;
				mPrivateData = value;
			}
		}
	}
}
