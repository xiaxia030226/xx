using QFramework;
using UnityEngine;

namespace Game.UI
{
    public class GameHUDData : UIPanelData
    {
    }

    /// <summary>
    /// 游戏内 HUD：血条实时刷新，其余元素（Lv/波次/EXP/武器格）为占位，后续阶段实装。
    /// 控件引用（HpFill / HpText）在 GameHUD.Designer.cs 中声明，
    /// 由 prefab 上的 Bind 组件 + "生成代码" 自动赋值，勿在此重复声明。
    /// </summary>
    public partial class GameHUD : UIPanel, IController
    {
        private int mCurrentHp;
        private int mCurrentMaxHp;

        public IArchitecture GetArchitecture() => GameArchitecture.Interface;

        protected override void OnInit(IUIData uiData = null)
        {
            mData = uiData as GameHUDData ?? new GameHUDData();

            var model = this.GetModel<IPlayerModel>();

            model.HP.RegisterWithInitValue(hp =>
                {
                    mCurrentHp = hp;
                    RefreshHpBar();
                })
                .UnRegisterWhenGameObjectDestroyed(gameObject);

            model.MaxHP.RegisterWithInitValue(maxHp =>
                {
                    mCurrentMaxHp = maxHp;
                    RefreshHpBar();
                })
                .UnRegisterWhenGameObjectDestroyed(gameObject);
        }

        protected override void OnOpen(IUIData uiData = null)
        {
        }

        protected override void OnShow()
        {
        }

        protected override void OnHide()
        {
        }

        protected override void OnClose()
        {
        }

        private void RefreshHpBar()
        {
            if (HpFill == null) return;

            var ratio = mCurrentMaxHp > 0 ? Mathf.Clamp01((float)mCurrentHp / mCurrentMaxHp) : 0f;

            var rect = HpFill.rectTransform;
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = new Vector2(ratio, 1f);
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;

            if (HpText != null)
            {
                HpText.text = $"{mCurrentHp}/{mCurrentMaxHp}";
            }
        }
    }
}
