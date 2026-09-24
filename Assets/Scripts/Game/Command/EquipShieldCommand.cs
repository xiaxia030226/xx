using QFramework;
using UnityEngine;

public class EquipShieldCommand : AbstractCommand
{
    private readonly int mLevel; // 待装备护盾的等级。
    private readonly float mCapacity; // 待装备护盾的剩余容量。
    public bool Succeeded { get; private set; } // true 表示已成功装备；false 表示尚未成功。
    public int ReplacedLevel { get; private set; } // 成功替换前的护盾等级。
    public float ReplacedCapacity { get; private set; } // 成功替换前的剩余盾量，供调用方处理旧盾。

    // 作用：记录待装备护盾的等级和余量；返回：无返回值（构造函数）。
    public EquipShieldCommand(int level, float capacity)
    {
        // 装备是否合法留到执行时结合游戏状态及当前护盾判断。
        mLevel = level;
        mCapacity = capacity;
    }

    // 作用：按阶段和升级规则装备护盾并记录旧盾；返回：无返回值，结果通过 Succeeded 等属性读取。
    protected override void OnExecute()
    {
        // 战斗及安全拾取阶段才允许换盾，等级须合法且新盾尚有容量。
        var state = this.GetModel<IGameStateModel>().State.Value;
        if (state != GameState.Playing && state != GameState.SafeLoot) return;
        if (mLevel < 1 || mLevel > 5 || mCapacity <= 0f) return;
        var model = this.GetModel<IPlayerModel>();
        // 旧盾未耗尽时只接受更高等级；已耗尽时不受旧等级限制。
        if (model.Shield.Value > 0f && mLevel <= model.ShieldLevel.Value) return;
        var config = ShieldConfigTable.Get(mLevel);
        ReplacedLevel = model.ShieldLevel.Value;
        ReplacedCapacity = model.Shield.Value;
        // 用配置确定最大容量，拾取物提供的余量不能超过该上限。
        model.ShieldLevel.Value = mLevel;
        model.MaxShield.Value = config.Capacity;
        model.Shield.Value = Mathf.Min(mCapacity, config.Capacity);
        Succeeded = true;
    }
}
