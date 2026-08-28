using QFramework;
using UnityEngine;

/// <summary>
/// 玩家在场景中的表现与控制组件。
/// 负责读取输入并移动、转向；玩家属性从 Model 读取，不在表现层直接修改。
/// </summary>
public class Player : MonoBehaviour, IController
{
    // 地图边界为 ±50，预留 1 单位避免角色和墙体重叠。
    private const float MapLimit = 49f;

    private Camera mMainCamera;

    public IArchitecture GetArchitecture() => GameArchitecture.Interface;

    private void Update()
    {
        var moveInput = GameInput.Move.ReadValue<Vector2>();

        // 斜向同时按两个键时长度会大于 1，归一化可避免斜向移动更快。
        if (moveInput.sqrMagnitude > 1f)
        {
            moveInput.Normalize();
        }

        var speed = this.GetModel<IPlayerModel>().MoveSpeed.Value;
        var position = transform.position;
        position += new Vector3(moveInput.x, 0f, moveInput.y) * (speed * Time.deltaTime);
        position.x = Mathf.Clamp(position.x, -MapLimit, MapLimit);
        position.z = Mathf.Clamp(position.z, -MapLimit, MapLimit);
        transform.position = position;

        FaceMouse();
    }

    private void FaceMouse()
    {
        if (mMainCamera == null)
        {
            mMainCamera = Camera.main;
            if (mMainCamera == null) return;
        }

        // 从相机经过鼠标位置发出射线，再与 y=0 的数学平面求交。
        // 这里不依赖 Collider，比 Physics.Raycast 更适合只计算鼠标在地面上的指向。
        var ray = mMainCamera.ScreenPointToRay(GameInput.MousePosition.ReadValue<Vector2>());
        var plane = new Plane(Vector3.up, Vector3.zero);
        if (plane.Raycast(ray, out float enter))
        {
            var hit = ray.GetPoint(enter);
            var dir = hit - transform.position;
            dir.y = 0f;
            if (dir.sqrMagnitude > 0.001f)
            {
                transform.rotation = Quaternion.LookRotation(dir);
            }
        }
    }
}
