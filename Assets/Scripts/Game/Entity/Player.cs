using System.Collections.Generic;
using QFramework;
using UnityEngine;

/// <summary>
/// 玩家在场景中的表现与控制组件。
/// 负责读取输入并移动、转向、攻击与切换武器；玩家属性从 Model 读取，不在表现层直接修改。
/// </summary>
public class Player : MonoBehaviour, IController
{
    private readonly HashSet<PoisonArea> mPoisonSources = new HashSet<PoisonArea>();
    private Collider mCollider;

    public void SetPoisonSource(PoisonArea source, bool inside)
    {
        if (inside) mPoisonSources.Add(source);
        else mPoisonSources.Remove(source);
    }

    public void ClearPoisonSources() => mPoisonSources.Clear();

    private void Awake() => mCollider = GetComponent<Collider>();

    private void OnDisable() => mPoisonSources.Clear();

    // ScrollThreshold：滚轮切换武器的判定阈值，过滤滚动结束前的微小抖动。
    private const float ScrollThreshold = 0.1f;

    // mMainCamera：主相机缓存，用于把鼠标屏幕坐标转换成地面上的世界坐标。
    private Camera mMainCamera;

    // mWeaponSystem：武器系统缓存，避免每帧重复向架构查找。
    private IWeaponSystem mWeaponSystem;

    public IArchitecture GetArchitecture() => GameArchitecture.Interface;

    private void Update()
    {
        var state = this.GetModel<IGameStateModel>().State.Value;
        if (state != GameState.Playing && state != GameState.SafeLoot) return;
        HandleMove();
        FaceMouse();
        if (state != GameState.Playing) return;
        HandleAttack();
        HandleWeaponOps();
        HandleWeaponSwitch();
    }

    /// <summary>
    /// 处理换弹与子弹等级切换：R 主动换弹，B 循环切换下次装填的穿甲等级（各枪独立记忆）。
    /// </summary>
    private void HandleWeaponOps()
    {
        if (GameInput.Reload.WasPressedThisFrame())
        {
            GetWeaponSystem().RequestReloadCurrent();
        }

        if (GameInput.CycleBulletLevel.WasPressedThisFrame())
        {
            GetWeaponSystem().CycleNextLoadLevelCurrent();
        }
    }

    /// <summary>
    /// 读取 WASD 输入并移动玩家，移动速度从 PlayerModel 读取，位置限制在地图边界内。
    /// </summary>
    private void HandleMove()
    {
        // moveInput：WASD 合成的移动方向向量（x 为左右，y 为前后）。
        var moveInput = GameInput.Move.ReadValue<Vector2>();

        // 斜向同时按两个键时向量长度会大于 1，归一化可避免斜向移动更快。
        if (moveInput.sqrMagnitude > 1f)
        {
            moveInput.Normalize();
        }

        // speed：从 Model 读取移动速度，玩家属性统一由 Model 管理。
        var speed = this.GetModel<IPlayerModel>().MoveSpeed.Value;

        if (mPoisonSources.Count > 0) speed *= 0.8f;
        var displacement = new Vector3(moveInput.x, 0f, moveInput.y) * (speed * Time.deltaTime);
        var radius = Mathf.Max(mCollider.bounds.extents.x, mCollider.bounds.extents.z);
        transform.position = GameRoot.Environment.Navigation.Move(transform.position, displacement, radius, mCollider);
    }

    /// <summary>
    /// 让玩家始终面向鼠标指向的地面位置。
    /// </summary>
    private void FaceMouse()
    {
        // 第一次调用时查找主相机并缓存，之后不再重复查找。
        if (mMainCamera == null)
        {
            mMainCamera = Camera.main;
            if (mMainCamera == null) return;
        }

        // ray：从相机经过鼠标屏幕位置发出的射线。
        // 与 y=0 的数学平面求交，不依赖 Collider，适合只计算鼠标在地面上的指向。
        var ray = mMainCamera.ScreenPointToRay(GameInput.MousePosition.ReadValue<Vector2>());
        var plane = new Plane(Vector3.up, Vector3.zero);

        // enter：射线到达平面的距离；未命中平面时跳过转向。
        if (plane.Raycast(ray, out float enter))
        {
            // hit：鼠标指向的地面世界坐标。
            var hit = ray.GetPoint(enter);

            // dir：玩家指向鼠标的水平方向（忽略高度差）。
            var dir = hit - transform.position;
            dir.y = 0f;

            // 鼠标恰好悬停在玩家正上方时方向长度接近 0，跳过以防转向错误。
            if (dir.sqrMagnitude > 0.001f)
            {
                transform.rotation = Quaternion.LookRotation(dir);
            }
        }
    }

    /// <summary>
    /// 按当前武器的触发方式处理攻击输入：
    /// 非自动武器（手枪）用 WasPressedThisFrame——按下瞬间只发一次；
    /// 自动武器（机枪）用 IsPressed——长按期间每帧尝试，射速由武器攻击间隔限制。
    /// </summary>
    private void HandleAttack()
    {
        // weapon：当前手持武器；尚未初始化完成时本帧不处理攻击。
        var weapon = GetWeaponSystem().CurrentWeapon;
        if (weapon == null) return;

        // triggered：本帧是否触发了攻击输入，按武器类型选择检测方式。
        var triggered = weapon.IsAutomatic
            ? GameInput.Attack.IsPressed()
            : GameInput.Attack.WasPressedThisFrame();

        if (triggered)
        {
            GetWeaponSystem().TryAttackCurrent();
        }
    }

    /// <summary>
    /// 处理武器切换：数字键 1-9 直接切到对应槽位，滚轮上下滚动按顺序循环切换。
    /// </summary>
    private void HandleWeaponSwitch()
    {
        // 遍历 1-9 数字键，哪个键在这一帧被按下，就切到对应槽位（下标 = 数字 - 1）。
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

    /// <summary>
    /// 获取武器系统；首次访问时向架构查找并缓存，之后直接复用。
    /// </summary>
    private IWeaponSystem GetWeaponSystem()
    {
        if (mWeaponSystem == null)
        {
            mWeaponSystem = this.GetSystem<IWeaponSystem>();
        }

        return mWeaponSystem;
    }
}
