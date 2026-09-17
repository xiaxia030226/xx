using UnityEngine;

/// <summary>
/// 显式标记会阻挡移动的碰撞体，包括 trigger。普通角色、地面和拾取物不挂此组件。
/// 静态墙岩放在 StageEnvironment 子层级；树精使用 mDynamic，不进入静态网格。
/// </summary>
public class BattleObstacle : MonoBehaviour
{
    [SerializeField] private bool mDynamic = false;

    private Collider mShape;

    public bool IsDynamic => mDynamic;
    public Collider Shape
    {
        get
        {
            if (mShape == null)
            {
                mShape = GetComponent<Collider>();
                if (mShape == null) mShape = GetComponentInChildren<Collider>(true);
            }
            return mShape;
        }
    }
}
