using QFramework;
using UnityEngine;

public class EquipShieldCommand : AbstractCommand
{
    private readonly int mLevel;
    private readonly float mCapacity;
    public bool Succeeded { get; private set; }
    public int ReplacedLevel { get; private set; }
    public float ReplacedCapacity { get; private set; }

    public EquipShieldCommand(int level, float capacity)
    {
        mLevel = level;
        mCapacity = capacity;
    }

    protected override void OnExecute()
    {
        var state = this.GetModel<IGameStateModel>().State.Value;
        if (state != GameState.Playing && state != GameState.SafeLoot) return;
        if (mLevel < 1 || mLevel > 5 || mCapacity <= 0f) return;
        var model = this.GetModel<IPlayerModel>();
        if (model.Shield.Value > 0f && mLevel <= model.ShieldLevel.Value) return;
        var config = ShieldConfigTable.Get(mLevel);
        ReplacedLevel = model.ShieldLevel.Value;
        ReplacedCapacity = model.Shield.Value;
        model.ShieldLevel.Value = mLevel;
        model.MaxShield.Value = config.Capacity;
        model.Shield.Value = Mathf.Min(mCapacity, config.Capacity);
        Succeeded = true;
    }
}
