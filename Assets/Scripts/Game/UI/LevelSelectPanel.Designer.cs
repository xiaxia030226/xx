using System;
using UnityEngine;
using UnityEngine.UI;
using QFramework;

namespace Game.UI
{
	// Generate Id:831dfcc4-b6b7-4f6a-ab04-f16806eead25
	public partial class LevelSelectPanel
	{
		public const string Name = "LevelSelectPanel";
		
		[SerializeField]
		public UnityEngine.UI.Button Level1Button;
		[SerializeField]
		public UnityEngine.UI.Button Level2Button;
		[SerializeField]
		public UnityEngine.UI.Button BackButton;
		
		private LevelSelectPanelData mPrivateData = null;
		
		protected override void ClearUIComponents()
		{
			Level1Button = null;
			Level2Button = null;
			BackButton = null;
			
			mData = null;
		}
		
		public LevelSelectPanelData Data
		{
			get
			{
				return mData;
			}
		}
		
		LevelSelectPanelData mData
		{
			get
			{
				return mPrivateData ?? (mPrivateData = new LevelSelectPanelData());
			}
			set
			{
				mUIData = value;
				mPrivateData = value;
			}
		}
	}
}
