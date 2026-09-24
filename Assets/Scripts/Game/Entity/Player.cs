using System.Collections.Generic;
using QFramework;
using UnityEngine;

/// <summary>
/// 玩家在场景中的表现与控制组件。
/// 负责读取输入并移动、转向、攻击与切换武器；玩家属性从 Model 读取，不在表现层直接修改。
/// </summary>
public class Player : MonoBehaviour, IController
{
    private readonly HashSet<PoisonArea> mPoisonSources = new HashSet<PoisonArea>(); // 当前覆盖玩家的毒区集合，多来源不叠乘减速。
    private Collider mCollider; // 玩家碰撞体，用于取得世界空间水平移动半径。

    // 作用：按进出毒区更新减速来源集合；返回：无返回值。
    public void SetPoisonSource(PoisonArea source, bool inside)
    {
        // 集合去重；离开一处毒区不影响其他仍覆盖玩家的毒区。
        if (inside) mPoisonSources.Add(source);
        else mPoisonSources.Remove(source);
    }

    // 作用：立即清除全部毒区来源；返回：无返回值。
    public void ClearPoisonSources() => mPoisonSources.Clear(); // 直接清空来源集合，使后续移动不再因旧毒区减速。

    // 作用：缓存玩家自身碰撞体；返回：无返回值。
    private void Awake() => mCollider = GetComponent<Collider>(); // 从当前对象取得碰撞体并缓存，供移动时计算水平半径。

    // 作用：禁用时清理残留减速来源；返回：无返回值。
    private void OnDisable() => mPoisonSources.Clear(); // 禁用时清空来源集合，避免再次启用后沿用旧毒区登记。

    private const float ScrollThreshold = 0.1f; // 滚轮切换阈值，用于过滤微小滚动输入。

    private Camera mMainCamera; // 将鼠标屏幕位置投影到地面的主相机缓存。

    private IWeaponSystem mWeaponSystem; // 惰性获取的武器系统，避免重复查找。

    // 作用：接入游戏架构；返回：游戏架构实例。
    public IArchitecture GetArchitecture() => GameArchitecture.Interface; // 直接取游戏架构入口，供框架扩展方法访问模型和系统。

    // 作用：按游戏状态分派移动、瞄准及武器输入；返回：无返回值。
    private void Update()
    {
        // 安全拾取可走动和转向，但只有战斗阶段允许攻击、换弹和切枪。
        var state = this.GetModel<IGameStateModel>().State.Value;
        if (state != GameState.Playing && state != GameState.SafeLoot) return;
        HandleMove();
        FaceMouse();
        if (state != GameState.Playing) return;
        HandleAttack();
        HandleWeaponOps();
        HandleWeaponSwitch();
    }

    // 作用：将换弹和下次装填等级切换输入交给武器系统；返回：无返回值。
    private void HandleWeaponOps()
    {
        // 两个输入独立检测，同一帧都按下时先请求换弹再切换下次等级。
        if (GameInput.Reload.WasPressedThisFrame())
        {
            GetWeaponSystem().RequestReloadCurrent();
        }

        if (GameInput.CycleBulletLevel.WasPressedThisFrame())
        {
            GetWeaponSystem().CycleNextLoadLevelCurrent();
        }
    }

    // 作用：读取平面移动输入并经导航处理减速、避障和边界；返回：无返回值。
    private void HandleMove()
    {
        var moveInput = GameInput.Move.ReadValue<Vector2>();

        // 斜向同时按两个键时向量长度会大于 1，归一化可避免斜向移动更快。
        if (moveInput.sqrMagnitude > 1f)
        {
            moveInput.Normalize();
        }

        var speed = this.GetModel<IPlayerModel>().MoveSpeed.Value;

        // 有任一毒区来源就减速一次，再用碰撞体水平半径交给导航扫掠滑移。
        if (mPoisonSources.Count > 0) speed *= 0.8f;
        var displacement = new Vector3(moveInput.x, 0f, moveInput.y) * (speed * Time.deltaTime);
        var radius = Mathf.Max(mCollider.bounds.extents.x, mCollider.bounds.extents.z);
        transform.position = GameRoot.Environment.Navigation.Move(transform.position, displacement, radius, mCollider);
    }

    // 作用：将鼠标投影到地面并让玩家水平朝向该位置；返回：无返回值。
    private void FaceMouse()
    {
        // 第一次调用时查找主相机并缓存，缺少相机时跳过。
        if (mMainCamera == null)
        {
            mMainCamera = Camera.main;
            if (mMainCamera == null) return;
        }

        // 与 y=0 的数学平面求交，不依赖场景碰撞体；未命中地面时保持朝向。
        var ray = mMainCamera.ScreenPointToRay(GameInput.MousePosition.ReadValue<Vector2>());
        var plane = new Plane(Vector3.up, Vector3.zero);

        if (plane.Raycast(ray, out float enter))
        {
            var hit = ray.GetPoint(enter);

            // 忽略高度差，避免角色跟随俯视射线发生倾斜。
            var dir = hit - transform.position;
            dir.y = 0f;

            // 鼠标恰好悬停在玩家正上方时方向长度接近 0，跳过以防转向错误。
            if (dir.sqrMagnitude > 0.001f)
            {
                transform.rotation = Quaternion.LookRotation(dir);
            }
        }
    }

    // 作用：按当前武器的自动或单发模式请求攻击；返回：无返回值。
    private void HandleAttack()
    {
        var weapon = GetWeaponSystem().CurrentWeapon;
        if (weapon == null) return;

        // 自动武器长按逐帧尝试，单发武器只响应按下瞬间；射速等限制由武器系统判定。
        var triggered = weapon.IsAutomatic
            ? GameInput.Attack.IsPressed()
            : GameInput.Attack.WasPressedThisFrame();

        if (triggered)
        {
            GetWeaponSystem().TryAttackCurrent();
        }
    }

    // 作用：将槽位按键和滚轮输入转为切换武器请求；返回：无返回值。
    private void HandleWeaponSwitch()
    {
        // 遍历全部槽位按键，再处理滚轮；同帧多个输入会按此顺序依次提交。
        for (var i = 0; i < GameInput.SwitchSlots.Length; i++)
        {
            if (GameInput.SwitchSlots[i].WasPressedThisFrame())
            {
                GetWeaponSystem().SwitchTo(i);
            }
        }

        // scroll：滚轮滚动值，y 大于阈值表示上滚，小于负阈值表示下滚。
        var scroll = GameInput.ScrollWeapon.ReadValue<Vector2>().y;
        if (scroll > ScrollThreshold)
        {
            GetWeaponSystem().SwitchBy(1);
        }
        else if (scroll < -ScrollThreshold)
        {
            GetWeaponSystem().SwitchBy(-1);
        }
    }

    // 作用：首次查询并缓存武器系统，后续复用；返回：当前架构的武器系统。
    private IWeaponSystem GetWeaponSystem()
    {
        // 仅在缓存为空时从架构查询，供攻击、换弹和切枪等入口复用同一引用。
        if (mWeaponSystem == null)
        {
            mWeaponSystem = this.GetSystem<IWeaponSystem>();
        }

        return mWeaponSystem;
    }
}
