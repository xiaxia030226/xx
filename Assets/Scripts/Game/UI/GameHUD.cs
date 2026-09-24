using System.Collections.Generic;
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
    /// 游戏内 HUD：血条、护盾条（等级/容量/破盾反馈）、武器格子条（弹量/等级/耐久/换弹置灰）、
    /// 波次倒计时与预告、掉落倍率、本局金币、子弹库存与缺弹提示、安全拾取结算按钮。
    /// 波次与槽位弹量由 Update 轮询并缓存文本状态，提示计时也在 Update 检查；其余显示通过事件或模型订阅刷新。
    /// 控件引用在 GameHUD.Designer.cs 中声明，由 prefab 上的 Bind 组件生成代码赋值，勿在此重复声明。
    /// </summary>
    public partial class GameHUD : UIPanel, IController
    {
        private const float ShortageShowSeconds = 2f; // 缺弹提示持续时长，单位为秒。

        private const float ShieldBreakFlashSeconds = 0.6f; // 破盾提示持续时长，单位为秒。

        /// <summary>
        /// 槽位弹量显示要素缓存：任一要素变化才重建文本，避免 Update 每帧字符串分配。
        /// </summary>
        private struct SlotAmmoState
        {
            public int Resource; // 上次显示的向上取整弹量，-1 用于强制下次刷新。
            public int LoadedLevel; // 上次显示的已装填子弹等级。
            public int NextLevel; // 上次显示的下次装填子弹等级。
            public bool Reloading; // 上次显示的换弹状态，true 表示正在换弹。
        }

        private float mCurrentHp; // 血量订阅回调缓存的当前生命值。
        private float mCurrentMaxHp; // 血量上限订阅回调缓存的最大生命值。

        private WeaponBarSlot[] mSlots; // Designer 提供的九个武器格子，按槽位下标访问。

        private IEnemySpawnSystem mEnemySpawnSystem; // 刷怪系统缓存，供每帧读取波次倒计时与预告。
        private IWeaponSystem mWeaponSystem; // 武器系统缓存，供读取当前槽位与各武器状态。

        private IBulletInventoryModel mBulletInventory; // 子弹库存模型缓存，供刷新各口径和等级的库存文本。

        private SlotAmmoState[] mSlotAmmoCache; // 各槽上次显示状态，用于跳过未变化的文本与遮罩刷新。

        private bool mShowingFinalWave; // true 表示已处理无后续波次文案，安全拾取阶段也置 true 以防被最终波文案覆盖。

        private int mLastCountdownSecond = -1; // 上次显示的倒计时整秒，-1 表示尚未显示。

        private int mLastPreviewWave = -1; // 上次生成预告的波次号，-1 表示尚未生成。

        private float mShortageHideTime = -1f; // 缺弹提示的 Time.time 隐藏时刻，-1 表示未安排隐藏。

        private float mShieldBreakUntil = -1f; // 破盾提示的 Time.time 截止时刻，-1 表示无待结束提示。

        // 作用：提供 HUD 访问模型、系统和事件所用的架构；返回：游戏架构接口。
        public IArchitecture GetArchitecture() => GameArchitecture.Interface; // 直接返回共享架构入口。

        // 作用：初始化 HUD 数据、各显示区域的订阅及按钮；返回：无返回值。
        protected override void OnInit(IUIData uiData = null)
        {
            // 按依赖顺序完成初始化；各模型与事件订阅随 HUD 对象销毁自动注销。
            mData = uiData as GameHUDData ?? new GameHUDData();

            // 第一步：订阅血量数据刷新血条。
            InitHpBar();

            // 第二步：初始化武器格子并订阅武器事件（切换/弹量/耐久/报废）。
            InitWeaponSlots();

            // 第三步：订阅掉落倍率与本局金币，缓存刷怪系统供 Update 轮询。
            InitWaveAndEconomy();

            // 第四步：初始化子弹库存显示并订阅缺弹事件。
            InitAmmoInventory();

            // 第五步：订阅护盾数据刷新护盾条，并监听破盾事件做闪烁提示。
            InitShieldBar();

            // 第六步：装配安全拾取结算按钮（仅 SafeLoot 状态显示，点击进结算）。
            InitSafeLootButton();
        }

        // 作用：订阅并缓存玩家生命值与上限以刷新血条；返回：无返回值。
        private void InitHpBar()
        {
            var model = this.GetModel<IPlayerModel>();

            // 两个订阅都先回调当前值，各自更新缓存后用最新缓存组合刷新血条。
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

        // 作用：初始化武器格子和显示缓存并订阅武器事件；返回：无返回值。
        private void InitWeaponSlots()
        {
            // 把 Designer 绑定的九个格子组织成数组，便于按槽位下标访问。
            mSlots = new[] { Slot1, Slot2, Slot3, Slot4, Slot5, Slot6, Slot7, Slot8, Slot9 };
            mSlotAmmoCache = new SlotAmmoState[mSlots.Length];

            mWeaponSystem = this.GetSystem<IWeaponSystem>();

            // 遍历格子：有武器的格子初始化编号/武器名/资源条/耐久，没有武器（或已报废为 null）的格子隐藏。
            for (var i = 0; i < mSlots.Length; i++)
            {
                // Resource 置 -1：强制首帧轮询时刷新一次弹量文本（默认值 0 可能与真实弹量相同而跳过）。
                mSlotAmmoCache[i].Resource = -1;

                if (i < mWeaponSystem.Weapons.Count && mWeaponSystem.Weapons[i] != null)
                {
                    var weapon = mWeaponSystem.Weapons[i];
                    mSlots[i].Setup(i, weapon.Name);
                    mSlots[i].SetResource(weapon.Resource, weapon.ResourceMax);
                    mSlots[i].SetDurability(weapon.Durability, weapon.DurabilityMax);
                    mSlots[i].gameObject.SetActive(true);
                }
                else
                {
                    // 空槽先隐藏，后续新增武器由 OnWeaponAdded 初始化并重新显示对应格子。
                    mSlots[i].gameObject.SetActive(false);
                }
            }

            // 各武器事件随 HUD 对象销毁注销；切换事件只更新选中高亮。
            this.RegisterEvent<WeaponSwitchedEvent>(OnWeaponSwitched)
                .UnRegisterWhenGameObjectDestroyed(gameObject);

            // 监听"武器资源变化"事件，用于更新弹药条。
            this.RegisterEvent<WeaponResourceChangedEvent>(OnResourceChanged)
                .UnRegisterWhenGameObjectDestroyed(gameObject);

            // 监听"耐久变化"事件，用于更新耐久数字与 ≤20% 警示色。
            this.RegisterEvent<WeaponDurabilityChangedEvent>(e =>
                {
                    if (e.SlotIndex < 0 || e.SlotIndex >= mSlots.Length) return;
                    mSlots[e.SlotIndex].SetDurability(e.Current, e.Max);
                })
                .UnRegisterWhenGameObjectDestroyed(gameObject);

            // 监听"武器报废"事件：槽位已腾空格，HUD 同步隐藏格子（不自动切枪，与系统行为一致）。
            this.RegisterEvent<WeaponBrokenEvent>(e =>
                {
                    if (e.SlotIndex < 0 || e.SlotIndex >= mSlots.Length) return;
                    mSlots[e.SlotIndex].gameObject.SetActive(false);
                })
                .UnRegisterWhenGameObjectDestroyed(gameObject);

            // 新增武器时补齐槽位展示并使弹量缓存失效。
            this.RegisterEvent<WeaponAddedEvent>(OnWeaponAdded)
                .UnRegisterWhenGameObjectDestroyed(gameObject);

            // 立即刷新一次高亮，面板打开时就显示正确的当前槽。
            OnWeaponSwitched(new WeaponSwitchedEvent { SlotIndex = mWeaponSystem.CurrentIndex });
        }

        // 作用：缓存波次系统并订阅安全拾取、掉落倍率与本局金币；返回：无返回值。
        private void InitWaveAndEconomy()
        {
            mEnemySpawnSystem = this.GetSystem<IEnemySpawnSystem>();
            // 只在进入安全拾取时替换波次文案，同时阻止无下一波的轮询分支覆盖提示。
            this.GetModel<IGameStateModel>().State.Register(state =>
                {
                    if (state != GameState.SafeLoot) return;
                    mShowingFinalWave = true;
                    WaveCountdownText.text = "安全拾取";
                    WavePreviewText.text = "拾取完成后点击结算";
                })
                .UnRegisterWhenGameObjectDestroyed(gameObject);

            // 掉落倍率：F 提前召唤提升一档、自然到点重置 1.0，订阅即刷新（含初始 1.0）。
            this.GetModel<IGameStateModel>().SummonMultiplier.RegisterWithInitValue(multiplier =>
                {
                    MultiplierText.text = $"掉落倍率 x{multiplier:0.##}";
                })
                .UnRegisterWhenGameObjectDestroyed(gameObject);

            // 本局金币：拾取金币入账即刷新，结算时按此值 + 通关奖励汇总。
            this.GetModel<IEconomyModel>().RunGold.RegisterWithInitValue(gold =>
                {
                    RunGoldText.text = $"金币：{gold}";
                })
                .UnRegisterWhenGameObjectDestroyed(gameObject);
        }

        // 作用：初始化库存文本并订阅库存变化和缺弹提示事件；返回：无返回值。
        private void InitAmmoInventory()
        {
            mBulletInventory = this.GetModel<IBulletInventoryModel>();

            // 任一口径或等级的库存变化都重建全部库存行，订阅在对象销毁时自动注销。
            this.RegisterEvent<BulletInventoryChangedEvent>(_ => RefreshAmmoInventory())
                .UnRegisterWhenGameObjectDestroyed(gameObject);

            // 打开面板时先刷一次：开局首次装填的预扣发生在 HUD 打开前，那次事件已经错过。
            RefreshAmmoInventory();
            AmmoShortageText.gameObject.SetActive(false);

            // 缺弹事件：显示提示文本（部分装填显示装入数/需求数），ShortageShowSeconds 后自动隐藏。
            this.RegisterEvent<AmmoShortageEvent>(e =>
                {
                    AmmoShortageText.text = e.Loaded > 0 ? $"子弹不足 {e.Loaded}/{e.Wanted}" : "子弹耗尽！";
                    AmmoShortageText.gameObject.SetActive(true);
                    mShortageHideTime = Time.time + ShortageShowSeconds;
                })
                .UnRegisterWhenGameObjectDestroyed(gameObject);
        }

        // 作用：每帧更新波次、武器展示及限时提示；返回：无返回值。
        private void Update()
        {
            // 先刷新系统状态对应的展示，再检查缺弹与破盾提示的截止时间。
            RefreshWaveCountdown();
            RefreshWeaponSlots();
            HideShortageIfDue();
            UpdateShieldBreakFlash();
        }

        // 作用：订阅护盾数据和破盾事件以驱动护盾条；返回：无返回值。
        private void InitShieldBar()
        {
            var model = this.GetModel<IPlayerModel>();
            // 各属性订阅立即用当前模型刷新，所有订阅均绑定对象销毁时注销。
            model.ShieldLevel.RegisterWithInitValue(_ => RefreshShieldBar()).UnRegisterWhenGameObjectDestroyed(gameObject);
            model.Shield.RegisterWithInitValue(_ => RefreshShieldBar()).UnRegisterWhenGameObjectDestroyed(gameObject);
            model.MaxShield.RegisterWithInitValue(_ => RefreshShieldBar()).UnRegisterWhenGameObjectDestroyed(gameObject);
            this.RegisterEvent<PlayerShieldBrokenEvent>(_ =>
            {
                // 破盾时重置提示期限并立即显示，期限结束由 Update 恢复常规文本。
                mShieldBreakUntil = Time.time + ShieldBreakFlashSeconds;
                RefreshShieldBar();
            }).UnRegisterWhenGameObjectDestroyed(gameObject);
        }

        // 作用：按护盾模型刷新显隐、填充比例及提示文本；返回：无返回值。
        private void RefreshShieldBar()
        {
            var model = this.GetModel<IPlayerModel>();
            var level = model.ShieldLevel.Value;
            var current = model.Shield.Value;
            var max = model.MaxShield.Value;
            // 有等级且容量上限有效才显示；当前护盾归零不单独隐藏护盾条。
            var show = level > 0 && max > 0f;
            if (ShieldBar.gameObject.activeSelf != show)
            {
                ShieldBar.gameObject.SetActive(show);
            }
            if (!show)
            {
                return;
            }
            ShieldFill.fillAmount = max > 0f ? Mathf.Clamp01(current / max) : 0f;
            // 提示期限内优先显示破盾警示，过期后恢复等级、容量与常规颜色。
            if (Time.time < mShieldBreakUntil)
            {
                ShieldText.text = "破盾！";
                ShieldText.color = new Color(1f, 0.35f, 0.3f);
            }
            else
            {
                ShieldText.text = $"S{level} {current:0}/{max:0}";
                ShieldText.color = Color.white;
            }
        }

        // 作用：检查破盾提示期限并在到期后恢复常规护盾文本；返回：无返回值。
        private void UpdateShieldBreakFlash()
        {
            // 仅处理已设置且到期的提示，清除期限后刷新一次，避免后续帧重复恢复。
            if (mShieldBreakUntil > 0f && Time.time >= mShieldBreakUntil)
            {
                mShieldBreakUntil = -1f;
                RefreshShieldBar();
            }
        }

        // 作用：绑定安全拾取结算按钮并使其显隐跟随游戏状态；返回：无返回值。
        private void InitSafeLootButton()
        {
            // 点击委托 GameRoot 处理结算，实际结算还由其检查 SafeLoot 状态。
            SafeLootButton.onClick.AddListener(GameRoot.CompleteSafeLoot);
            // 订阅时立即同步显隐，只有安全拾取阶段可见；对象销毁时自动注销订阅。
            this.GetModel<IGameStateModel>().State.RegisterWithInitValue(state =>
            {
                var show = state == GameState.SafeLoot;
                if (SafeLootButton.gameObject.activeSelf != show)
                {
                    SafeLootButton.gameObject.SetActive(show);
                }
            }).UnRegisterWhenGameObjectDestroyed(gameObject);
        }

        // 作用：按整秒和波次缓存刷新倒计时与敌人预告；返回：无返回值。
        private void RefreshWaveCountdown()
        {
            var countdown = mEnemySpawnSystem.NextWaveCountdown;

            if (countdown < 0f)
            {
                // 无下一波（已是最后一波）：只切换一次文案，避免每帧重复赋值。
                if (!mShowingFinalWave)
                {
                    mShowingFinalWave = true;
                    WaveCountdownText.text = "最终波";
                    WavePreviewText.text = "清空场上敌人即可通关";
                }
                return;
            }
            mShowingFinalWave = false;

            // 整秒变化才改倒计时文本，避免每帧字符串分配。
            var second = Mathf.CeilToInt(countdown);
            if (second != mLastCountdownSecond || mLastPreviewWave != mEnemySpawnSystem.NextWave)
            {
                mLastCountdownSecond = second;
                WaveCountdownText.text = $"下一波({mEnemySpawnSystem.NextWave}) {second}s";
            }

            // 预告内容只在波次切换时重建（生成组本身在一波倒计时内不变）。
            if (mLastPreviewWave != mEnemySpawnSystem.NextWave)
            {
                mLastPreviewWave = mEnemySpawnSystem.NextWave;
                WavePreviewText.text = BuildPreviewText(mEnemySpawnSystem.NextWavePreview);
            }
        }

        // 作用：比较枪械状态缓存并刷新变化槽位的弹量文本与换弹遮罩；返回：无返回值。
        private void RefreshWeaponSlots()
        {
            var weapons = mWeaponSystem.Weapons;
            // 仅遍历武器列表与界面格子共同覆盖的范围。
            var count = Mathf.Min(mSlots.Length, weapons.Count);

            for (var i = 0; i < count; i++)
            {
                // 空槽（已报废）与非枪械武器跳过；报废槽的格子已被事件隐藏。
                var gun = weapons[i] as GunWeapon;
                if (gun == null) continue;

                var state = mSlotAmmoCache[i];
                var resource = Mathf.CeilToInt(gun.Resource);

                // 弹量、已装等级、下次装填等级、换弹状态全都没变就跳过本格。
                if (resource == state.Resource && gun.LoadedLevel == state.LoadedLevel
                    && gun.NextLoadLevel == state.NextLevel && gun.IsReloading == state.Reloading) continue;

                state.Resource = resource;
                state.LoadedLevel = gun.LoadedLevel;
                state.NextLevel = gun.NextLoadLevel;
                state.Reloading = gun.IsReloading;
                mSlotAmmoCache[i] = state;

                mSlots[i].SetAmmo($"{resource}/{(int)gun.ResourceMax} L{gun.LoadedLevel}>{gun.NextLoadLevel}");
                mSlots[i].SetReloading(gun.IsReloading);
            }
        }

        // 作用：在缺弹提示到期且仍显示时隐藏提示；返回：无返回值。
        private void HideShortageIfDue()
        {
            // 同时检查期限与激活状态，隐藏后清除定时标记。
            if (mShortageHideTime > 0f && Time.time >= mShortageHideTime && AmmoShortageText.gameObject.activeSelf)
            {
                AmmoShortageText.gameObject.SetActive(false);
                mShortageHideTime = -1f;
            }
        }

        // 作用：重建各口径按等级排列的子弹库存文本；返回：无返回值。
        private void RefreshAmmoInventory()
        {
            // 按口径分行、等级用斜杠分隔，完整拼接后一次性赋给文本控件。
            var text = "";

            // 口径显示顺序固定 S/AR/L，与 Caliber 枚举声明顺序一致。
            var calibers = new[] { Caliber.S, Caliber.AR, Caliber.L };
            for (var c = 0; c < calibers.Length; c++)
            {
                if (c > 0) text += "\n";
                text += calibers[c] + "：";
                for (var level = 0; level < AmmoTypes.LevelCount; level++)
                {
                    if (level > 0) text += "/";
                    text += mBulletInventory.GetCount(calibers[c], level);
                }
            }

            AmmoInventoryText.text = text;
        }

        // 作用：按敌人名称和护盾等级汇总波次预告；返回：分行预告文本，无生成组时返回空字符串。
        private static string BuildPreviewText(IReadOnlyList<SpawnGroup> groups)
        {
            if (groups == null || groups.Count == 0) return "";
            // 名称列表保留首次出现顺序，内层有序字典按护盾等级累计同名敌人数量。
            var names = new List<string>();
            var counts = new Dictionary<string, SortedDictionary<int, int>>();
            foreach (var group in groups)
            {
                var name = EnemyConfigTable.Get(group.EnemyId).Name;
                if (!counts.TryGetValue(name, out var shields))
                {
                    names.Add(name);
                    shields = new SortedDictionary<int, int>();
                    counts.Add(name, shields);
                }
                shields.TryGetValue(group.ShieldLevel, out var count);
                shields[group.ShieldLevel] = count + group.Count;
            }
            // 每个名称输出一行，行内按护盾等级升序列出数量，零级使用无盾文案。
            var rows = new List<string>();
            foreach (var name in names)
            {
                var parts = new List<string>();
                foreach (var shield in counts[name])
                    parts.Add($"{(shield.Key == 0 ? "无盾" : shield.Key + "级盾")}×{shield.Value}");
                rows.Add(name + "：" + string.Join("，", parts));
            }
            return string.Join("\n", rows);
        }

        // 作用：根据武器切换事件更新所有格子的选中高亮；返回：无返回值。
        private void OnWeaponSwitched(WeaponSwitchedEvent e)
        {
            // 逐格与当前槽位比较，同时选中目标并取消其他格子的高亮。
            for (var i = 0; i < mSlots.Length; i++)
            {
                mSlots[i].SetSelected(i == e.SlotIndex);
            }
        }

        // 作用：根据武器资源变化事件刷新对应格子的资源条；返回：无返回值。
        private void OnResourceChanged(WeaponResourceChangedEvent e)
        {
            // 槽位下标越界时直接忽略（格子数量可能比武器少）。
            if (e.SlotIndex < 0 || e.SlotIndex >= mSlots.Length) return;

            mSlots[e.SlotIndex].SetResource(e.Current, e.Max);
        }

        // 作用：在新增武器时初始化并显示对应槽位；返回：无返回值。
        private void OnWeaponAdded(WeaponAddedEvent e)
        {
            // 按事件下标读取武器与界面格子，先同步基础显示和选中状态再激活。
            var weapon = mWeaponSystem.Weapons[e.SlotIndex];
            var slot = mSlots[e.SlotIndex];
            slot.Setup(e.SlotIndex, weapon.Name);
            slot.SetResource(weapon.Resource, weapon.ResourceMax);
            slot.SetDurability(weapon.Durability, weapon.DurabilityMax);
            slot.SetSelected(e.SlotIndex == mWeaponSystem.CurrentIndex);
            slot.gameObject.SetActive(true);
            // 使弹量缓存失效，立即刷新以免复用槽位时保留旧武器文本或换弹遮罩。
            mSlotAmmoCache[e.SlotIndex].Resource = -1;
            RefreshWeaponSlots();
        }

        // 作用：接收 HUD 打开回调；返回：无返回值。
        protected override void OnOpen(IUIData uiData = null)
        {
            // 展示初始化在 OnInit 完成，后续由订阅和 Update 更新，此处不重复初始化。
        }

        // 作用：接收 HUD 显示回调；返回：无返回值。
        protected override void OnShow()
        {
            // 已有订阅和轮询负责展示更新，显示时不重复注册。
        }

        // 作用：接收 HUD 隐藏回调；返回：无返回值。
        protected override void OnHide()
        {
            // 隐藏时保留订阅与缓存，不在此解除对象生命周期内的绑定。
        }

        // 作用：接收 HUD 关闭回调；返回：无返回值。
        protected override void OnClose()
        {
            // 订阅已绑定对象销毁时自动注销，关闭回调不手动重复注销。
        }

        // 作用：按缓存生命值刷新血条宽度与数值文本；返回：无返回值。
        private void RefreshHpBar()
        {
            if (HpFill == null) return;

            // 上限无效时按空血条处理，否则将比例限制在零到一之间。
            var ratio = mCurrentMaxHp > 0 ? Mathf.Clamp01((float)mCurrentHp / mCurrentMaxHp) : 0f;

            // 通过右侧锚点表现比例并清零边距：ratio=1 时铺满，ratio=0 时宽度为 0。
            var rect = HpFill.rectTransform;
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = new Vector2(ratio, 1f);
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;

            if (HpText != null)
            {
                HpText.text = $"{mCurrentHp:0.#}/{mCurrentMaxHp:0.#}";
            }
        }
    }
}
