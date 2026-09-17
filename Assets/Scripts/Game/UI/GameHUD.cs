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
    /// 波次倒计时与预告走 Update 轮询（系统侧只读属性，不推事件）；其余均事件/订阅驱动。
    /// 控件引用在 GameHUD.Designer.cs 中声明，由 prefab 上的 Bind 组件生成代码赋值，勿在此重复声明。
    /// </summary>
    public partial class GameHUD : UIPanel, IController
    {
        // ShortageShowSeconds：缺弹提示的展示时长（秒），超时自动隐藏。
        private const float ShortageShowSeconds = 2f;

        // ShieldBreakFlashSeconds：破盾后护盾条闪烁"破盾！"提示的时长（秒）。
        private const float ShieldBreakFlashSeconds = 0.6f;

        /// <summary>
        /// 槽位弹量显示要素缓存：任一要素变化才重建文本，避免 Update 每帧字符串分配。
        /// </summary>
        private struct SlotAmmoState
        {
            public int Resource;
            public int LoadedLevel;
            public int NextLevel;
            public bool Reloading;
        }

        private float mCurrentHp;
        private float mCurrentMaxHp;

        // mSlots：全部武器格子数组，按槽位下标访问（预制体提供 Slot1~Slot9 共九格，与武器槽上限一致）。
        private WeaponBarSlot[] mSlots;

        // mEnemySpawnSystem / mWeaponSystem：系统缓存，Update 轮询波次与武器状态用，OnInit 取一次。
        private IEnemySpawnSystem mEnemySpawnSystem;
        private IWeaponSystem mWeaponSystem;

        // mBulletInventory：子弹库存 Model 缓存，刷新库存文本用。
        private IBulletInventoryModel mBulletInventory;

        // mSlotAmmoCache：各槽上次显示的弹量状态，与当前值比对决定是否刷新格子文本。
        private SlotAmmoState[] mSlotAmmoCache;

        // mShowingFinalWave：是否正显示"最终波"文案（无下一波时只切换一次，避免每帧重复赋值）。
        private bool mShowingFinalWave;

        // mLastCountdownSecond：上次显示的倒计时整秒，整秒变化才刷新文本。
        private int mLastCountdownSecond = -1;

        // mLastPreviewWave：上次生成预告文本的波次号，波次切换时才重建预告串。
        private int mLastPreviewWave = -1;

        // mShortageHideTime：缺弹提示的隐藏时刻（Time.time）；<=0 表示当前没有提示在显示。
        private float mShortageHideTime = -1f;

        // mShieldBreakUntil：破盾闪烁的截止时刻（Time.time）；<=0 表示当前没有破盾提示。
        private float mShieldBreakUntil = -1f;

        public IArchitecture GetArchitecture() => GameArchitecture.Interface;

        /// <summary>
        /// 面板实例创建时执行一次：订阅血量、护盾、武器、波次、经济数据并做初始刷新。
        /// </summary>
        protected override void OnInit(IUIData uiData = null)
        {
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
        /// 订阅切换/弹量/耐久/报废事件并用当前武器状态完成初始刷新。
        /// </summary>
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
                    // 当前只有手枪+机枪两把，多余的格子隐藏；将来获得新武器时需要额外逻辑重新显示。
                    mSlots[i].gameObject.SetActive(false);
                }
            }

            // 监听"切换武器"事件，用于更新格子高亮。
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

            this.RegisterEvent<WeaponAddedEvent>(OnWeaponAdded)
                .UnRegisterWhenGameObjectDestroyed(gameObject);

            // 立即刷新一次高亮，面板打开时就显示正确的当前槽。
            OnWeaponSwitched(new WeaponSwitchedEvent { SlotIndex = mWeaponSystem.CurrentIndex });
        }

        /// <summary>
        /// 订阅掉落倍率与本局金币；缓存刷怪系统供 Update 轮询波次倒计时与预告。
        /// </summary>
        private void InitWaveAndEconomy()
        {
            mEnemySpawnSystem = this.GetSystem<IEnemySpawnSystem>();
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

        /// <summary>
        /// 初始化子弹库存显示：订阅库存变化与缺弹事件，并立即全量刷新一次。
        /// </summary>
        private void InitAmmoInventory()
        {
            mBulletInventory = this.GetModel<IBulletInventoryModel>();

            // 任何一桶库存变化都全量刷新（三行文本重建成本极低，事件频率低）。
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

        /// <summary>
        /// 每帧轮询：波次倒计时与预告、各槽弹量/换弹状态、缺弹提示的自动隐藏。
        /// 这三类数据来自系统只读属性或计时器，不适合事件驱动，故统一轮询。
        /// </summary>
        private void Update()
        {
            RefreshWaveCountdown();
            RefreshWeaponSlots();
            HideShortageIfDue();
            UpdateShieldBreakFlash();
        }

        /// <summary>
        /// 订阅护盾等级/当前值/上限并刷新护盾条；监听破盾事件触发短暂"破盾！"提示。
        /// 护盾等级 0（未装备）时整条隐藏；破盾后等级保留、容量归零，条保持可见显示空盾。
        /// </summary>
        private void InitShieldBar()
        {
            var model = this.GetModel<IPlayerModel>();
            model.ShieldLevel.RegisterWithInitValue(_ => RefreshShieldBar()).UnRegisterWhenGameObjectDestroyed(gameObject);
            model.Shield.RegisterWithInitValue(_ => RefreshShieldBar()).UnRegisterWhenGameObjectDestroyed(gameObject);
            model.MaxShield.RegisterWithInitValue(_ => RefreshShieldBar()).UnRegisterWhenGameObjectDestroyed(gameObject);
            this.RegisterEvent<PlayerShieldBrokenEvent>(_ =>
            {
                mShieldBreakUntil = Time.time + ShieldBreakFlashSeconds;
                RefreshShieldBar();
            }).UnRegisterWhenGameObjectDestroyed(gameObject);
        }

        /// <summary>
        /// 按当前护盾模型刷新护盾条显隐、填充与文本；破盾闪烁期间文本显示"破盾！"。
        /// </summary>
        private void RefreshShieldBar()
        {
            var model = this.GetModel<IPlayerModel>();
            var level = model.ShieldLevel.Value;
            var current = model.Shield.Value;
            var max = model.MaxShield.Value;
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

        /// <summary>
        /// 破盾闪烁结束后恢复正常护盾文本；由 Update 每帧检查。
        /// </summary>
        private void UpdateShieldBreakFlash()
        {
            if (mShieldBreakUntil > 0f && Time.time >= mShieldBreakUntil)
            {
                mShieldBreakUntil = -1f;
                RefreshShieldBar();
            }
        }

        /// <summary>
        /// 结算按钮：点击进入结算（GameRoot.CompleteSafeLoot 内有 SafeLoot 状态门控）；
        /// 仅在 SafeLoot 状态显示，其余状态隐藏。
        /// </summary>
        private void InitSafeLootButton()
        {
            SafeLootButton.onClick.AddListener(GameRoot.CompleteSafeLoot);
            this.GetModel<IGameStateModel>().State.RegisterWithInitValue(state =>
            {
                var show = state == GameState.SafeLoot;
                if (SafeLootButton.gameObject.activeSelf != show)
                {
                    SafeLootButton.gameObject.SetActive(show);
                }
            }).UnRegisterWhenGameObjectDestroyed(gameObject);
        }

        /// <summary>
        /// 刷新波次倒计时与预告文本。无下一波时显示"最终波"；有下一波时按整秒刷新倒计时，
        /// 预告内容只在波次切换时重建。
        /// </summary>
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

        /// <summary>
        /// 刷新各武器格子的弹量文本与换弹遮罩：逐槽读取枪械状态，
        /// 与缓存比对，任一要素变化才重建文本并切换遮罩。
        /// </summary>
        private void RefreshWeaponSlots()
        {
            var weapons = mWeaponSystem.Weapons;
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

        /// <summary>
        /// 缺弹提示到时自动隐藏。
        /// </summary>
        private void HideShortageIfDue()
        {
            if (mShortageHideTime > 0f && Time.time >= mShortageHideTime && AmmoShortageText.gameObject.activeSelf)
            {
                AmmoShortageText.gameObject.SetActive(false);
                mShortageHideTime = -1f;
            }
        }

        /// <summary>
        /// 重建子弹库存文本：三口径各一行，行内为 0~5 级库存数量（/ 分隔）。
        /// 低频刷新（库存变化事件触发），字符串直接累加即可。
        /// </summary>
        private void RefreshAmmoInventory()
        {
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

        private static string BuildPreviewText(IReadOnlyList<SpawnGroup> groups)
        {
            if (groups == null || groups.Count == 0) return "";
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

        private void OnWeaponAdded(WeaponAddedEvent e)
        {
            var weapon = mWeaponSystem.Weapons[e.SlotIndex];
            var slot = mSlots[e.SlotIndex];
            slot.Setup(e.SlotIndex, weapon.Name);
            slot.SetResource(weapon.Resource, weapon.ResourceMax);
            slot.SetDurability(weapon.Durability, weapon.DurabilityMax);
            slot.SetSelected(e.SlotIndex == mWeaponSystem.CurrentIndex);
            slot.gameObject.SetActive(true);
            mSlotAmmoCache[e.SlotIndex].Resource = -1;
            RefreshWeaponSlots();
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
                HpText.text = $"{mCurrentHp:0.#}/{mCurrentMaxHp:0.#}";
            }
        }
    }
}
