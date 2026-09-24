using UnityEngine;

/// <summary>
/// 斜俯视相机跟随组件，在 LateUpdate 中平滑追踪玩家。
/// </summary>
public class CameraFollow : MonoBehaviour
{
    public Transform Target; // 相机追踪并朝向的目标。
    public Vector3 Offset = new Vector3(0f, 12f, -9f); // 相对目标的世界空间位置偏移。
    public float SmoothTime = 0.15f; // 位置平滑趋近目标所用的时间参数。

    private Vector3 mVelocity; // SmoothDamp 跨帧维护的平滑移动速度。

    // 作用：在目标移动后平滑跟随并朝向目标；返回：无返回值。
    private void LateUpdate()
    {
        if (Target == null) return;

        // 在 Update 移动结束后跟随，位置平滑但朝向直接对准目标。
        transform.position = Vector3.SmoothDamp(transform.position, Target.position + Offset, ref mVelocity,
            SmoothTime);
        transform.rotation = Quaternion.LookRotation(Target.position - transform.position);
    }
}
