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

    public int CurrentIndex { get; private set; }
    public IReadOnlyList<WeaponBase> Weapons => mWeapons;

    /// <summary>
    /// 绑定武器持有者并建立初始武器栏。目前第一格固定放入铁剑。
    /// </summary>
    public void Setup(Transform owner)
    {
        mOwner = owner;
        if (mWeapons.Count == 0) mWeapons.Add(new SwordWeapon());
        CurrentIndex = 0;
        SendCurrentWeaponEvents();
    }

    public void Tick(float deltaTime)
    {
        for (var i = 0; i < mWeapons.Count; i++)
        {
            mWeapons[i].Tick(deltaTime);
            this.SendEvent(new WeaponResourceChangedEvent
            {
                SlotIndex = i,
                Current = mWeapons[i].Resource,
                Max = mWeapons[i].ResourceMax
            });
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
        this.SendEvent(new WeaponResourceChangedEvent
        {
            SlotIndex = CurrentIndex,
            Current = weapon.Resource,
            Max = weapon.ResourceMax
        });
    }
}
