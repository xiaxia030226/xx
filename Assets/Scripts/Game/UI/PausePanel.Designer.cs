using System;
using UnityEngine;
using UnityEngine.UI;
using QFramework;

namespace Game.UI
{
	// Generate Id:d42841ad-04aa-425a-9129-6aade01a1bd3
	public partial class PausePanel
	{
		public const string Name = "PausePanel";
		
		[SerializeField]
		public TMPro.TextMeshProUGUI Title;
		[SerializeField]
		public UnityEngine.UI.Button ResumeButton;
		[SerializeField]
		public UnityEngine.UI.Button MenuButton;
		
		private PausePanelData mPrivateData = null;
		
		protected override void ClearUIComponents()
		{
			Title = null;
			ResumeButton = null;
			MenuButton = null;
			
			mData = null;
		}
		
		public PausePanelData Data
		{
			get
			{
				return mData;
			}
		}
		
		PausePanelData mData
		{
			get
			{
				return mPrivateData ?? (mPrivateData = new PausePanelData());
			}
			set
			{
				mUIData = value;
				mPrivateData = value;
			}
		}
	}
}
