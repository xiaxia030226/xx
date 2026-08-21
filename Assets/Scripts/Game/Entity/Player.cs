using QFramework;
using UnityEngine;

public class Player : MonoBehaviour, IController
{
    private const float MapLimit = 49f;

    private Camera mMainCamera;

    public IArchitecture GetArchitecture() => GameArchitecture.Interface;

    private void Update()
    {
        var moveInput = GameInput.Move.ReadValue<Vector2>();
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
