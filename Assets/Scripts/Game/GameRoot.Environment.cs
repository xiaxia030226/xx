using UnityEngine;

public partial class GameRoot
{
    // 作用：为本场战斗创建暖色、带软阴影的定向光；返回：无返回值。
    private void CreateDirectionalLight()
    {
        // 光源挂在场景入口下，随本场景一起销毁，不进入跨场景系统。
        var go = new GameObject("Directional Light");
        go.transform.SetParent(transform, false);
        go.transform.rotation = Quaternion.Euler(50f, -30f, 0f);
        var light = go.AddComponent<Light>();
        light.type = LightType.Directional;
        light.color = new Color(1f, 0.96f, 0.9f);
        light.intensity = 1.1f;
        light.shadows = LightShadows.Soft;
    }

    // 作用：按关卡配置实例化并初始化环境，建立战斗对象分类节点；返回：无返回值。
    private void CreateBattleEnvironment()
    {
        BattleRoot = CreateChild(transform, "BattleRoot");
        var prefab = Resources.Load<GameObject>(mStage.EnvironmentPath);
        // 缺失地图直接抛错，交给 Awake 的统一装配失败处理，不用占位地图掩盖问题。
        if (prefab == null) throw new System.InvalidOperationException($"缺少地图：{mStage.EnvironmentPath}，请执行阶段三资源生成菜单。");
        Environment = Instantiate(prefab, BattleRoot).GetComponent<StageEnvironment>();
        Environment.Initialize();
        // 系统稍后绑定这些节点；子弹、敌人和拾取物均由当前战斗场景持有。
        CreateChild(BattleRoot, "EnemyRoot");
        mPickupRoot = CreateChild(BattleRoot, "PickupRoot");
        mBulletRoot = CreateChild(BattleRoot, "BulletRoot");
    }

    // 作用：新建命名节点并以本地变换挂到指定父节点；返回：新节点的 Transform。
    private Transform CreateChild(Transform parent, string name)
    {
        // 不保持世界变换，使新空节点保留零位移、单位缩放的本地初始状态。
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        return go.transform;
    }
}
