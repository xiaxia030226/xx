using QFramework;
using UnityEngine;

namespace Game.UI
{
    /// <summary>
    /// 打开 GameHUD 时可传入的数据。当前 HUD 直接订阅 Model，暂时没有额外参数。
    /// </summary>
    public class GameHUDData : UIPanelData
    {
    }

    /// <summary>
    /// 游戏内 HUD：血条实时刷新，下方武器格子条显示武器名与资源量并高亮当前槽。
    /// 控件引用（HpFill/HpText/Slot1/Slot2）在 GameHUD.Designer.cs 中声明，
    /// 由 prefab 上的 Bind 组件 + "生成代码" 自动赋值，勿在此重复声明。
    /// </summary>
    public partial class GameHUD : UIPanel, IController
    {
        private int mCurrentHp;
        private int mCurrentMaxHp;

        // mSlots：全部武器格子数组，按槽位下标访问（预制体提供 Slot1~Slot9 共九格，与武器槽上限一致）。
        private WeaponBarSlot[] mSlots;

        public IArchitecture GetArchitecture() => GameArchitecture.Interface;

        /// <summary>
        /// 面板实例创建时执行一次：订阅血量数据并初始化武器格子。
        /// </summary>
        protected override void OnInit(IUIData uiData = null)
        {
            mData = uiData as GameHUDData ?? new GameHUDData();

            // 第一步：订阅血量数据刷新血条。
            InitHpBar();

            // 第二步：初始化武器格子并订阅武器事件。
            InitWeaponSlots();
        }

        /// <summary>
        /// 订阅玩家血量与上限，变化时刷新血条。
        /// </summary>
        private void InitHpBar()
        {
            var model = this.GetModel<IPlayerModel>();

            // RegisterWithInitValue 会先用当前值回调一次，之后每次 HP 变化都会再次回调。
            model.HP.RegisterWithInitValue(hp =>
                {
                    mCurrentHp = hp;
                    RefreshHpBar();
                })
                // HUD 销毁时自动取消订阅，避免回调继续引用已销毁对象。
                .UnRegisterWhenGameObjectDestroyed(gameObject);

            model.MaxHP.RegisterWithInitValue(maxHp =>
                {
                    mCurrentMaxHp = maxHp;
                    RefreshHpBar();
                })
                .UnRegisterWhenGameObjectDestroyed(gameObject);
        }

        /// <summary>
        /// 初始化武器格子：设置编号与武器名、隐藏没有武器的空格、
        /// 订阅武器切换与资源变化事件并立即刷新一次。
        /// </summary>
        private void InitWeaponSlots()
        {
            // 把 Designer 绑定的九个格子组织成数组，便于按槽位下标访问。
            mSlots = new[] { Slot1, Slot2, Slot3, Slot4, Slot5, Slot6, Slot7, Slot8, Slot9 };

            var weaponSystem = this.GetSystem<IWeaponSystem>();

            // 遍历格子：有武器的格子初始化编号与武器名，没有武器的格子隐藏。
            for (var i = 0; i < mSlots.Length; i++)
            {
                if (i < weaponSystem.Weapons.Count)
                {
                    mSlots[i].Setup(i, weaponSystem.Weapons[i].Name);
                    mSlots[i].gameObject.SetActive(true);
                }
                else
                {
                    // 当前只有一把铁剑，多余的格子隐藏；将来获得新武器时需要额外逻辑重新显示。
                    mSlots[i].gameObject.SetActive(false);
                }
            }

            // 监听"切换武器"事件，用于更新格子高亮。
            this.RegisterEvent<WeaponSwitchedEvent>(OnWeaponSwitched)
                .UnRegisterWhenGameObjectDestroyed(gameObject);

            // 监听"武器资源变化"事件，用于更新能量/弹药条。
            this.RegisterEvent<WeaponResourceChangedEvent>(OnResourceChanged)
                .UnRegisterWhenGameObjectDestroyed(gameObject);

            // 立即用初始数据刷新一次高亮与资源条，面板打开时就显示正确状态。
            OnWeaponSwitched(new WeaponSwitchedEvent { SlotIndex = weaponSystem.CurrentIndex });
            OnResourceChanged(new WeaponResourceChangedEvent
            {
                SlotIndex = 0,
                Current = weaponSystem.Weapons[0].Resource,
                Max = weaponSystem.Weapons[0].ResourceMax
            });
        }

        /// <summary>
        /// 切换武器时刷新格子高亮：当前选中格用亮色，其余恢复深色。
        /// </summary>
        /// <param name="e">携带当前选中槽位下标的事件数据。</param>
        private void OnWeaponSwitched(WeaponSwitchedEvent e)
        {
            for (var i = 0; i < mSlots.Length; i++)
            {
                mSlots[i].SetSelected(i == e.SlotIndex);
            }
        }

        /// <summary>
        /// 武器资源变化时刷新对应格子的资源条比例。
        /// </summary>
        /// <param name="e">携带槽位下标与当前/最大资源值的事件数据。</param>
        private void OnResourceChanged(WeaponResourceChangedEvent e)
        {
            // 槽位下标越界时直接忽略（格子数量可能比武器少）。
            if (e.SlotIndex < 0 || e.SlotIndex >= mSlots.Length) return;

            mSlots[e.SlotIndex].SetResource(e.Current, e.Max);
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

            // 修改右侧锚点来表现比例：ratio=1 时铺满，ratio=0 时宽度为 0。
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
