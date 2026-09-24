using UnityEngine;

/// <summary>
/// 显式标记会阻挡移动的碰撞体，包括 trigger。普通角色、地面和拾取物不挂此组件。
/// 静态墙岩放在 StageEnvironment 子层级；树精使用 mDynamic，不进入静态网格。
/// </summary>
public class BattleObstacle : MonoBehaviour
{
    [SerializeField] private bool mDynamic = false; // 是否为动态障碍；动态障碍不进入静态寻路缓存和视线阻挡判定。

    private Collider mShape; // 根节点或子节点上首次找到的碰撞体缓存。

    public bool IsDynamic => mDynamic; // 为真表示只参与实时障碍查询，不作为静态墙岩。
    public Collider Shape // 标记对象的代表碰撞体，未找到时为 null。
    {
        // 作用：惰性查找根节点或含未激活子节点的碰撞体；返回：找到的碰撞体或 null。
        get
        {
            // 优先根节点，缓存为空时才继续搜索子层级。
            if (mShape == null)
            {
                mShape = GetComponent<Collider>();
                if (mShape == null) mShape = GetComponentInChildren<Collider>(true);
            }
            return mShape;
        }
    }
}
