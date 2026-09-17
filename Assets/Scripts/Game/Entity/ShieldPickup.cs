using QFramework;
using TMPro;
using UnityEngine;

public class ShieldPickup : MonoBehaviour, IController
{
    public const string PoolKey = "shield_pickup";
    [SerializeField] private TMP_Text mLabel;
    private int mLevel;
    private float mCapacity;
    private Transform mTarget;

    public IArchitecture GetArchitecture() => GameArchitecture.Interface;

    public void OnSpawn(int level, float capacity, Transform target)
    {
        mLevel = level;
        mCapacity = capacity;
        mTarget = target;
        if (mLabel != null) mLabel.text = $"{level}级盾 {capacity:0.#}";
    }

    private void Update()
    {
        if (mTarget == null) return;
        var state = this.GetModel<IGameStateModel>().State.Value;
        if (state != GameState.Playing && state != GameState.SafeLoot) return;
        var offset = mTarget.position - transform.position;
        offset.y = 0f;
        if (offset.sqrMagnitude > 0.8f * 0.8f) return;
        var command = new EquipShieldCommand(mLevel, mCapacity);
        this.SendCommand(command);
        if (!command.Succeeded) return;
        var position = transform.position;
        var parent = transform.parent;
        var target = mTarget;
        var pool = this.GetSystem<IGameObjectPoolSystem>();
        pool.Recycle(PoolKey, gameObject);
        if (command.ReplacedCapacity <= 0f) return;
        var old = pool.Spawn(PoolKey, position, Quaternion.identity, parent);
        old.GetComponent<ShieldPickup>().OnSpawn(command.ReplacedLevel, command.ReplacedCapacity, target);
    }
}
