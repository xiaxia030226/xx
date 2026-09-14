using System.Collections.Generic;
using QFramework;
using UnityEngine;

/// <summary>
/// 武器系统对外接口，向 Player 隐藏具体武器列表和切换规则。
/// </summary>
public interface IWeaponSystem : ISystem
{
    int CurrentIndex { get; }
    IReadOnlyList<WeaponBase> Weapons { get; }
    void Setup(Transform owner);
    void Tick(float deltaTime);
    bool TryAttackCurrent();
    void SwitchTo(int index);
    void SwitchBy(int offset);
}

/// <summary>
/// 管理玩家最多九个武器槽、攻击、资源更新和切换。
/// </summary>
public class WeaponSystem : AbstractSystem, IWeaponSystem
{
    private readonly List<WeaponBase> mWeapons = new List<WeaponBase>(9);
    private Transform mOwner;

    // mLastSentResources：每个槽位上一次广播资源变化事件时的资源值。
    // 用于值变化才发事件，避免数值没变时 HUD 重复刷新相同显示。
    private float[] mLastSentResources;

    // ResourceChangeThreshold：资源变化的判定阈值，低于该值的变化不触发广播。
    private const float ResourceChangeThreshold = 0.01f;

    public int CurrentIndex { get; private set; }
    public IReadOnlyList<WeaponBase> Weapons => mWeapons;

    /// <summary>
    /// 绑定武器持有者并建立初始武器栏。目前第一格固定放入铁剑。
    /// 注意：这里不广播切枪事件——本方法执行时 HUD 尚未打开，
    /// 事件会因无人订阅而丢失；HUD 在 OnInit 中主动拉取当前状态完成初始显示。
    /// </summary>
    public void Setup(Transform owner)
    {
        mOwner = owner;
        if (mWeapons.Count == 0) mWeapons.Add(new SwordWeapon());
        CurrentIndex = 0;

        // 初始化资源缓存：初值 -1 与任何合法资源值（0 及以上）都不同，保证首帧必发一次事件。
        // 注意数组长度按当前武器数分配；将来若运行中新增武器，需要同步扩容此数组。
        mLastSentResources = new float[mWeapons.Count];
        for (var i = 0; i < mLastSentResources.Length; i++)
        {
            mLastSentResources[i] = -1f;
        }
    }

    public void Tick(float deltaTime)
    {
        for (var i = 0; i < mWeapons.Count; i++)
        {
            mWeapons[i].Tick(deltaTime);

            // 与上次广播值相比变化超过阈值才发事件。
            // 能量型武器恢复期间数值每帧都在变，仍会正常发；
            // 但弹药型武器静止时数值不变，此时发事件只会让 HUD 重复设置相同的锚点。
            if (Mathf.Abs(mWeapons[i].Resource - mLastSentResources[i]) > ResourceChangeThreshold)
            {
                mLastSentResources[i] = mWeapons[i].Resource;
                this.SendEvent(new WeaponResourceChangedEvent
                {
                    SlotIndex = i,
                    Current = mWeapons[i].Resource,
                    Max = mWeapons[i].ResourceMax
                });
            }
        }
    }

    /// <summary>
    /// 尝试使用当前武器；资源不足时寻找下一把可用武器。
    /// </summary>
    public bool TryAttackCurrent()
    {
        if (mOwner == null || mWeapons.Count == 0) return false;

        var weapon = mWeapons[CurrentIndex];
        if (weapon.TryAttack(mOwner)) return true;

        if (weapon.Resource < weapon.CostPerAttack) SwitchToNextReady();
        return false;
    }

    public void SwitchTo(int index)
    {
        if (index < 0 || index >= mWeapons.Count || index == CurrentIndex) return;
        CurrentIndex = index;
        SendCurrentWeaponEvents();
    }

    public void SwitchBy(int offset)
    {
        if (mWeapons.Count <= 1 || offset == 0) return;

        // 取模实现首尾循环；C# 负数取模仍为负，所以反向切换时要再加一次数量。
        var index = (CurrentIndex + offset) % mWeapons.Count;
        if (index < 0) index += mWeapons.Count;
        SwitchTo(index);
    }

    protected override void OnInit()
    {
    }

    private void SwitchToNextReady()
    {
        for (var offset = 1; offset < mWeapons.Count; offset++)
        {
            var index = (CurrentIndex + offset) % mWeapons.Count;
            if (!mWeapons[index].CanAttack) continue;
            SwitchTo(index);
            return;
        }
    }

    private void SendCurrentWeaponEvents()
    {
        this.SendEvent(new WeaponSwitchedEvent { SlotIndex = CurrentIndex });
        if (mWeapons.Count == 0) return;

        var weapon = mWeapons[CurrentIndex];

        // 同步更新缓存为当前值，避免切枪后下一帧 Tick 因差值超过阈值再重复发一次相同数值。
        mLastSentResources[CurrentIndex] = weapon.Resource;
        this.SendEvent(new WeaponResourceChangedEvent
        {
            SlotIndex = CurrentIndex,
            Current = weapon.Resource,
            Max = weapon.ResourceMax
        });
    }
}
