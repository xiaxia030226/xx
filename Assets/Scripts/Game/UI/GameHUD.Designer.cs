using System;
using UnityEngine;
using UnityEngine.UI;
using QFramework;

namespace Game.UI
{
	// Generate Id:fc5c6850-2944-4080-9002-ec70d43d3f47
	public partial class GameHUD
	{
		public const string Name = "GameHUD";
		
		[SerializeField]
		public UnityEngine.UI.Image HpFill;
		[SerializeField]
		public TMPro.TextMeshProUGUI HpText;
		[SerializeField]
		public UnityEngine.UI.Image ShieldBar;
		[SerializeField]
		public UnityEngine.UI.Image ShieldFill;
		[SerializeField]
		public TMPro.TextMeshProUGUI ShieldText;
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
		[SerializeField]
		public TMPro.TextMeshProUGUI WaveCountdownText;
		[SerializeField]
		public TMPro.TextMeshProUGUI WavePreviewText;
		[SerializeField]
		public TMPro.TextMeshProUGUI MultiplierText;
		[SerializeField]
		public TMPro.TextMeshProUGUI RunGoldText;
		[SerializeField]
		public TMPro.TextMeshProUGUI AmmoInventoryText;
		[SerializeField]
		public TMPro.TextMeshProUGUI AmmoShortageText;
		[SerializeField]
		public UnityEngine.UI.Button SafeLootButton;
		
		private GameHUDData mPrivateData = null;
		
		protected override void ClearUIComponents()
		{
			HpFill = null;
			HpText = null;
			ShieldBar = null;
			ShieldFill = null;
			ShieldText = null;
			Slot1 = null;
			Slot2 = null;
			Slot3 = null;
			Slot4 = null;
			Slot5 = null;
			Slot6 = null;
			Slot7 = null;
			Slot8 = null;
			Slot9 = null;
			WaveCountdownText = null;
			WavePreviewText = null;
			MultiplierText = null;
			RunGoldText = null;
			AmmoInventoryText = null;
			AmmoShortageText = null;
			SafeLootButton = null;
			
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
