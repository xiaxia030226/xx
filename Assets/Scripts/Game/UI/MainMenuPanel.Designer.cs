using System;
using UnityEngine;
using UnityEngine.UI;
using QFramework;

namespace Game.UI
{
	// Generate Id:4e17c96f-38f3-4075-95bf-3470648827e7
	public partial class MainMenuPanel
	{
		public const string Name = "MainMenuPanel";
		
		[SerializeField]
		public TMPro.TextMeshProUGUI Title;
		[SerializeField]
		public UnityEngine.UI.Button StartButton;
		[SerializeField]
		public UnityEngine.UI.Button QuitButton;
		
		private MainMenuPanelData mPrivateData = null;
		
		protected override void ClearUIComponents()
		{
			Title = null;
			StartButton = null;
			QuitButton = null;
			
			mData = null;
		}
		
		public MainMenuPanelData Data
		{
			get
			{
				return mData;
			}
		}
		
		MainMenuPanelData mData
		{
			get
			{
				return mPrivateData ?? (mPrivateData = new MainMenuPanelData());
			}
			set
			{
				mUIData = value;
				mPrivateData = value;
			}
		}
	}
}
