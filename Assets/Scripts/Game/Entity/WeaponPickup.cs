using QFramework;
using TMPro;
using UnityEngine;

public class WeaponPickup : MonoBehaviour, IController
{
    public const string PoolKey = "weapon_pickup"; // 武器拾取物的对象池键。
    [SerializeField] private TMP_Text mLabel; // 展示本次武器名称的文本。
    private string mWeaponId; // 本次拾取对应的武器配置标识。
    private ItemOrigin mOrigin; // 本次武器来源，由生成方指定。
    private Transform mTarget; // 用于距离判定的玩家目标。

    // 作用：接入游戏架构；返回：游戏架构实例。
    public IArchitecture GetArchitecture() => GameArchitecture.Interface; // 直接取游戏架构入口，供框架扩展方法访问模型和系统。

    // 作用：重设池对象的武器、来源、目标与名称；返回：无返回值。
    public void OnSpawn(string weaponId, ItemOrigin origin, Transform target)
    {
        // 每次生成覆盖来源，补给与敌人掉落复用同一个池也不继承旧来源。
        mWeaponId = weaponId;
        mOrigin = origin;
        mTarget = target;
        if (mLabel != null) mLabel.text = WeaponConfigTable.Get(weaponId).Name;
    }

    // 作用：靠近时请求拾取武器，成功后回池；返回：无返回值。
    private void Update()
    {
        // 战斗和安全拾取均允许触发；仅按 XZ 距离判定，不主动磁吸。
        if (mTarget == null) return;
        var state = this.GetModel<IGameStateModel>().State.Value;
        if (state != GameState.Playing && state != GameState.SafeLoot) return;
        var offset = mTarget.position - transform.position;
        offset.y = 0f;
        if (offset.sqrMagnitude > 0.8f * 0.8f) return;
        // 命令拒绝时保留场景物体，后续帧仍可尝试拾取。
        var command = new PickupWeaponCommand(mWeaponId, mOrigin);
        this.SendCommand(command);
        if (command.Succeeded) this.GetSystem<IGameObjectPoolSystem>().Recycle(PoolKey, gameObject);
    }
}
