using QFramework;
using TMPro;
using UnityEngine;

public class ShieldPickup : MonoBehaviour, IController
{
    public const string PoolKey = "shield_pickup"; // 护盾拾取物的对象池键。
    [SerializeField] private TMP_Text mLabel; // 展示护盾等级和容量的文本。
    private int mLevel; // 本次待装备护盾的等级。
    private float mCapacity; // 本次拾取可提供的护盾容量。
    private Transform mTarget; // 用于距离判定的玩家目标。

    // 作用：接入游戏架构；返回：游戏架构实例。
    public IArchitecture GetArchitecture() => GameArchitecture.Interface; // 直接取游戏架构入口，供框架扩展方法访问模型和系统。

    // 作用：覆盖池对象本次护盾数据并刷新标签；返回：无返回值。
    public void OnSpawn(int level, float capacity, Transform target)
    {
        // 覆盖本轮盾等级、容量和目标；标签存在时同步显示，避免复用后仍展示旧盾。
        mLevel = level;
        mCapacity = capacity;
        mTarget = target;
        if (mLabel != null) mLabel.text = $"{level}级盾 {capacity:0.#}";
    }

    // 作用：靠近时尝试装备护盾并把换下的盾放回场景；返回：无返回值。
    private void Update()
    {
        // 只在战斗或安全拾取阶段检查 XZ 距离，不受目标高度差影响。
        if (mTarget == null) return;
        var state = this.GetModel<IGameStateModel>().State.Value;
        if (state != GameState.Playing && state != GameState.SafeLoot) return;
        var offset = mTarget.position - transform.position;
        offset.y = 0f;
        if (offset.sqrMagnitude > 0.8f * 0.8f) return;
        var command = new EquipShieldCommand(mLevel, mCapacity);
        this.SendCommand(command);
        if (!command.Succeeded) return;
        // 回池前保存位置与目标；新生成的旧盾可能复用当前这个对象。
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
