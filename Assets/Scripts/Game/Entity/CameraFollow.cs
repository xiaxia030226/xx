using UnityEngine;

public class CameraFollow : MonoBehaviour
{
    public Transform Target;
    public Vector3 Offset = new Vector3(0f, 12f, -9f);
    public float SmoothTime = 0.15f;

    private Vector3 mVelocity;

    private void LateUpdate()
    {
        if (Target == null) return;

        transform.position = Vector3.SmoothDamp(transform.position, Target.position + Offset, ref mVelocity,
            SmoothTime);
        transform.rotation = Quaternion.LookRotation(Target.position - transform.position);
    }
}
