using System;
using UnityEngine;
using UnityEngine.UI;
using QFramework;

namespace Game.UI
{
	// Generate Id:731f0178-7070-405b-ab93-3ad13689d743
	public partial class ResultPanel
	{
		public const string Name = "ResultPanel";
		
		[SerializeField]
		public TMPro.TextMeshProUGUI Title;
		[SerializeField]
		public TMPro.TextMeshProUGUI KillText;
		[SerializeField]
		public TMPro.TextMeshProUGUI GoldText;
		[SerializeField]
		public TMPro.TextMeshProUGUI TotalGoldText;
		[SerializeField]
		public UnityEngine.UI.Button MenuButton;
		
		private ResultPanelData mPrivateData = null;
		
		protected override void ClearUIComponents()
		{
			Title = null;
			KillText = null;
			GoldText = null;
			TotalGoldText = null;
			MenuButton = null;
			
			mData = null;
		}
		
		public ResultPanelData Data
		{
			get
			{
				return mData;
			}
		}
		
		ResultPanelData mData
		{
			get
			{
				return mPrivateData ?? (mPrivateData = new ResultPanelData());
			}
			set
			{
				mUIData = value;
				mPrivateData = value;
			}
		}
	}
}
