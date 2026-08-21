using System;
using UnityEngine;
using UnityEngine.UI;
using QFramework;

namespace Game.UI
{
	// Generate Id:8d8d8122-2d67-48b8-8656-1d1266e409ba
	public partial class GameHUD
	{
		public const string Name = "GameHUD";
		
		[SerializeField]
		public UnityEngine.UI.Image HpFill;
		[SerializeField]
		public TMPro.TextMeshProUGUI HpText;
		
		private GameHUDData mPrivateData = null;
		
		protected override void ClearUIComponents()
		{
			HpFill = null;
			HpText = null;
			
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
