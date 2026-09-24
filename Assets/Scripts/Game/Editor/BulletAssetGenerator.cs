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
    private const string OutputFolder = "Assets/Resources/Configs/Bullets"; // 配置资产输出目录，相对 Unity 工程根目录。

    private const string BulletPrefabPath = "Prefabs/Bullet"; // 所有口径共用的子弹预制体 Resources 相对路径。

    private const float BulletSpeed = 30f; // 子弹速度（米/秒），与旧 NormalBullet 保持一致。

    // 作用：生成缺失的各口径、各穿甲等级子弹配置并保存资源；返回：无返回值。
    [MenuItem("Game/生成子弹配置资产（18个）")]
    public static void Generate()
    {
        // 先准备输出目录；已有配置按路径跳过，不覆盖手工调整。
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

        // CreateAsset 已创建磁盘资产；这里还会全局保存脏资产并刷新资源库，并非只读预检。
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log($"[BulletAssetGenerator] 完成：新建 {created} 个子弹配置（已存在则跳过），目录 {OutputFolder}");
    }
}
