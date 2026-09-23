using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 预制体提供场地和障碍物；这里只描述世界单位、轴对齐的 XZ 边界，不创建地图几何体。
/// 场地中心取此 Transform 的世界位置；旋转和缩放不会改变边界方向或 HalfSize。
/// </summary>
public class StageEnvironment : MonoBehaviour
{
    [SerializeField] private float mHalfSize = 45f;
    [SerializeField] private Transform[] mEntrances = Array.Empty<Transform>();
    [SerializeField] private EmergencySupplyCrate mEmergencySupplyCrate;

    private BattleNavigation mNavigation;

    public float HalfSize => mHalfSize;
    public IReadOnlyList<Transform> Entrances => mEntrances ?? Array.Empty<Transform>();
    public EmergencySupplyCrate EmergencySupplyCrate => mEmergencySupplyCrate;
    public BattleNavigation Navigation
    {
        get
        {
            if (mNavigation == null) mNavigation = GetComponent<BattleNavigation>();
            return mNavigation;
        }
    }

    public void Initialize()
    {
        if (Navigation != null) Navigation.Initialize(this);
        else Debug.LogError("StageEnvironment requires a BattleNavigation component on the same object.", this);
    }
}
