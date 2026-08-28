using UnityEngine;

/// <summary>
/// 斜俯视相机跟随组件，在 LateUpdate 中平滑追踪玩家。
/// </summary>
public class CameraFollow : MonoBehaviour
{
    public Transform Target;
    public Vector3 Offset = new Vector3(0f, 12f, -9f);
    public float SmoothTime = 0.15f;

    private Vector3 mVelocity;

    private void LateUpdate()
    {
        if (Target == null) return;

        // 玩家在 Update 移动完成后再跟随，可减少相机抖动。
        // mVelocity 由 SmoothDamp 持续更新，用于计算平滑过渡速度。
        transform.position = Vector3.SmoothDamp(transform.position, Target.position + Offset, ref mVelocity,
            SmoothTime);
        transform.rotation = Quaternion.LookRotation(Target.position - transform.position);
    }
}
