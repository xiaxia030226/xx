using System;
using UnityEngine;
using UnityEngine.UI;
using QFramework;

namespace Game.UI
{
	// Generate Id:d7c6b0d3-794f-4ded-96a7-12d3a7f17d3d
	public partial class LevelUpPanel
	{
		public const string Name = "LevelUpPanel";
		
		[SerializeField]
		public TMPro.TextMeshProUGUI Title;
		[SerializeField]
		public UnityEngine.UI.Button Option1;
		[SerializeField]
		public TMPro.TextMeshProUGUI Option1Label;
		[SerializeField]
		public UnityEngine.UI.Button Option2;
		[SerializeField]
		public TMPro.TextMeshProUGUI Option2Label;
		[SerializeField]
		public UnityEngine.UI.Button Option3;
		[SerializeField]
		public TMPro.TextMeshProUGUI Option3Label;
		
		private LevelUpPanelData mPrivateData = null;
		
		protected override void ClearUIComponents()
		{
			Title = null;
			Option1 = null;
			Option1Label = null;
			Option2 = null;
			Option2Label = null;
			Option3 = null;
			Option3Label = null;
			
			mData = null;
		}
		
		public LevelUpPanelData Data
		{
			get
			{
				return mData;
			}
		}
		
		LevelUpPanelData mData
		{
			get
			{
				return mPrivateData ?? (mPrivateData = new LevelUpPanelData());
			}
			set
			{
				mUIData = value;
				mPrivateData = value;
			}
		}
	}
}
