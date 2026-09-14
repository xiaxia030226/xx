using UnityEngine;

/// <summary>
/// GameRoot 的环境搭建部分：平行光、地面、围墙与功能挂点。
/// 用 partial 与主文件拆分——主文件专注游戏流程编排（Awake/Update/事件），
/// 本文件只负责把战场摆出来。
/// </summary>
public partial class GameRoot
{
    // ==================== 战场搭建 ====================

    /// <summary>
    /// 创建平行光，让场景中的 3D 几何体能正常渲染出明暗面。
    /// </summary>
    private void CreateDirectionalLight()
    {
        // go：灯光物体，挂在 GameRoot 下与战场处于同一生命周期。
        var go = new GameObject("Directional Light");
        go.transform.SetParent(transform, false);

        // 设置为斜上方 50 度的暖白色平行光，模仿日光效果。
        go.transform.rotation = Quaternion.Euler(50f, -30f, 0f);

        // light：平行光组件，开启软阴影让地面更有空间感。
        var light = go.AddComponent<Light>();
        light.type = LightType.Directional;
        light.color = new Color(1f, 0.96f, 0.9f);
        light.intensity = 1.1f;
        light.shadows = LightShadows.Soft;
    }

    /// <summary>
    /// 搭建战斗环境：100x100 地面、四面围墙、敌人/拾取物/子弹挂点。
    /// </summary>
    private void CreateBattleEnvironment()
    {
        // root：BattleRoot 作为所有动态战斗物体的容器，方便清理与遍历。
        var root = new GameObject("BattleRoot");
        root.transform.SetParent(transform, false);
        BattleRoot = root.transform;

        // ground：灰色平面，缩放后覆盖 100x100 范围（Plane 原始大小 10x10）。
        var ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
        ground.name = "Ground";
        ground.transform.SetParent(root.transform, false);
        ground.transform.localScale = new Vector3(10f, 1f, 10f);
        ground.GetComponent<Renderer>().material.color = new Color(0.22f, 0.25f, 0.28f);

        // 创建三个功能挂点：EnemyRoot 挂敌人、PickupRoot 挂水晶、BulletRoot 留待将来挂子弹。
        CreateChild(root.transform, "EnemyRoot");
        mPickupRoot = CreateChild(root.transform, "PickupRoot");
        CreateChild(root.transform, "BulletRoot");

        // 四面围墙：每面长 102 米，高 1 米，厚 1 米，灰色。
        CreateBoundaryWall(root.transform, "WallNorth", new Vector3(0f, 0.5f, 50f), new Vector3(102f, 1f, 1f));
        CreateBoundaryWall(root.transform, "WallSouth", new Vector3(0f, 0.5f, -50f), new Vector3(102f, 1f, 1f));
        CreateBoundaryWall(root.transform, "WallEast", new Vector3(50f, 0.5f, 0f), new Vector3(1f, 1f, 102f));
        CreateBoundaryWall(root.transform, "WallWest", new Vector3(-50f, 0.5f, 0f), new Vector3(1f, 1f, 102f));
    }

    /// <summary>
    /// 创建一个空子物体作为分类挂载点，返回其 Transform 供后续引用。
    /// </summary>
    private Transform CreateChild(Transform parent, string name)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        return go.transform;
    }

    /// <summary>
    /// 创建一面围墙：给定父节点、名称、位置和长宽高。
    /// </summary>
    private void CreateBoundaryWall(Transform parent, string name, Vector3 position, Vector3 scale)
    {
        // wall：普通 Cube，设为灰色作为边界。
        var wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
        wall.name = name;
        wall.transform.SetParent(parent, false);
        wall.transform.localPosition = position;
        wall.transform.localScale = scale;
        wall.GetComponent<Renderer>().material.color = new Color(0.35f, 0.37f, 0.4f);
    }
}
