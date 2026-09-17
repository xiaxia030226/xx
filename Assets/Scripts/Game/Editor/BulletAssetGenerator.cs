using System.IO;
using UnityEditor;
using UnityEngine;

/// <summary>
/// 子弹配置资产生成器：一键生成 3 口径 × 6 穿甲等级共 18 个 BulletConfig 资产
/// 到 Resources/Configs/Bullets/ 下。已存在的资产跳过（幂等，可重复执行）。
/// 同口径 6 个等级共享同一子弹预制体，仅穿甲等级不同。
/// </summary>
public static class BulletAssetGenerator
{
    // OutputFolder：资产输出目录（Unity 路径，相对工程根）。
    private const string OutputFolder = "Assets/Resources/Configs/Bullets";

    // BulletPrefabPath：子弹预制体在 Resources 下的相对路径（全部口径共用）。
    private const string BulletPrefabPath = "Prefabs/Bullet";

    // BulletSpeed：子弹飞行速度（米/秒），与旧 NormalBullet 保持一致。
    private const float BulletSpeed = 30f;

    [MenuItem("Game/生成子弹配置资产（18个）")]
    public static void Generate()
    {
        if (!Directory.Exists(OutputFolder))
        {
            Directory.CreateDirectory(OutputFolder);
        }

        // created：本次新建的资产数；已存在的跳过，重复执行不会覆盖手改过的资产。
        var created = 0;

        foreach (Caliber caliber in System.Enum.GetValues(typeof(Caliber)))
        {
            for (var level = 0; level < AmmoTypes.LevelCount; level++)
            {
                var id = AmmoTypes.BulletId(caliber, level);
                var path = $"{OutputFolder}/{id}.asset";

                if (AssetDatabase.LoadAssetAtPath<BulletConfig>(path) != null) continue;

                var config = ScriptableObject.CreateInstance<BulletConfig>();

                // BulletConfig 字段均为私有序列化字段，通过 SerializedObject 按字段名写入。
                var serialized = new SerializedObject(config);
                serialized.FindProperty("mId").stringValue = id;
                serialized.FindProperty("mName").stringValue = $"{caliber} 弹 Lv.{level}";
                serialized.FindProperty("mCaliber").enumValueIndex = (int)caliber;
                serialized.FindProperty("mPenetrationLevel").intValue = level;
                serialized.FindProperty("mSpeed").floatValue = BulletSpeed;
                serialized.FindProperty("mPrefabPath").stringValue = BulletPrefabPath;
                serialized.ApplyModifiedPropertiesWithoutUndo();

                AssetDatabase.CreateAsset(config, path);
                created++;
            }
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log($"[BulletAssetGenerator] 完成：新建 {created} 个子弹配置（已存在则跳过），目录 {OutputFolder}");
    }
}
