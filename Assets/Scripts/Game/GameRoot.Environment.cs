using UnityEngine;

public partial class GameRoot
{
    private void CreateDirectionalLight()
    {
        var go = new GameObject("Directional Light");
        go.transform.SetParent(transform, false);
        go.transform.rotation = Quaternion.Euler(50f, -30f, 0f);
        var light = go.AddComponent<Light>();
        light.type = LightType.Directional;
        light.color = new Color(1f, 0.96f, 0.9f);
        light.intensity = 1.1f;
        light.shadows = LightShadows.Soft;
    }

    private void CreateBattleEnvironment()
    {
        BattleRoot = CreateChild(transform, "BattleRoot");
        var prefab = Resources.Load<GameObject>(mStage.EnvironmentPath);
        if (prefab == null) throw new System.InvalidOperationException($"缺少地图：{mStage.EnvironmentPath}，请执行阶段三资源生成菜单。");
        Environment = Instantiate(prefab, BattleRoot).GetComponent<StageEnvironment>();
        Environment.Initialize();
        CreateChild(BattleRoot, "EnemyRoot");
        mPickupRoot = CreateChild(BattleRoot, "PickupRoot");
        mBulletRoot = CreateChild(BattleRoot, "BulletRoot");
    }

    private Transform CreateChild(Transform parent, string name)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        return go.transform;
    }
}
