using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 预制体提供场地和障碍物；这里只描述世界单位、轴对齐的 XZ 边界，不创建地图几何体。
/// 场地中心取此 Transform 的世界位置；旋转和缩放不会改变边界方向或 HalfSize。
/// </summary>
public class StageEnvironment : MonoBehaviour
{
    [SerializeField] private float mHalfSize = 45f; // 以场地世界位置为中心的 XZ 正方形半边长。
    [SerializeField] private Transform[] mEntrances = Array.Empty<Transform>(); // 预制体配置的敌人入口节点。
    [SerializeField] private EmergencySupplyCrate mEmergencySupplyCrate; // 场景中装配的应急补给箱。

    private BattleNavigation mNavigation; // 同物体导航组件的惰性缓存。

    public float HalfSize => mHalfSize; // 世界单位半边长，不随 Transform 缩放变化。
    public IReadOnlyList<Transform> Entrances => mEntrances ?? Array.Empty<Transform>(); // 入口只读视图，未配置数组时返回空数组。
    public EmergencySupplyCrate EmergencySupplyCrate => mEmergencySupplyCrate; // 对外提供已装配的补给箱引用。
    public BattleNavigation Navigation // 此场地的导航组件；未装配时为 null。
    {
        // 作用：首次访问时查找同物体导航并缓存；返回：导航组件或 null。
        get
        {
            // 已缓存时直接复用，缺少组件时不自动创建替代装配。
            if (mNavigation == null) mNavigation = GetComponent<BattleNavigation>();
            return mNavigation;
        }
    }

    // 作用：将当前场地交给导航初始化，缺失组件时记录错误；返回：无返回值。
    public void Initialize()
    {
        // 只初始化预先装配的导航，缺失时直接报告，避免悄悄改变场景结构。
        if (Navigation != null) Navigation.Initialize(this);
        else Debug.LogError("StageEnvironment requires a BattleNavigation component on the same object.", this);
    }
}
