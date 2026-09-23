#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEngine;

public static class StageFourAssetGenerator
{
    private const string Menu = "Game/阶段四/生成缺失冲锋枪配置";
    private const string Path = "Assets/Resources/Configs/Weapons/SMG.asset";

    [MenuItem(Menu)]
    private static void CreateSmg()
    {
        foreach (var guid in AssetDatabase.FindAssets("t:WeaponConfig"))
        {
            var existing = AssetDatabase.LoadAssetAtPath<WeaponConfig>(AssetDatabase.GUIDToAssetPath(guid));
            if (existing == null || existing.Id != WeaponConfigTable.SmgId) continue;
            Selection.activeObject = existing;
            Debug.Log("[阶段四] 已存在 smg 配置，未覆盖；请运行资源校验检查数值。", existing);
            return;
        }
        if (File.Exists(Path) || AssetDatabase.LoadMainAssetAtPath(Path) != null)
        {
            Debug.LogError("[阶段四] SMG.asset 路径已被其他资源占用，未覆盖。");
            return;
        }
        if (!AssetDatabase.IsValidFolder("Assets/Resources/Configs/Weapons"))
        {
            Debug.LogError("[阶段四] 请先创建 Assets/Resources/Configs/Weapons 文件夹。");
            return;
        }
        var config = ScriptableObject.CreateInstance<WeaponConfig>();
        try
        {
            var serialized = new SerializedObject(config);
            serialized.FindProperty("mId").stringValue = WeaponConfigTable.SmgId;
            serialized.FindProperty("mName").stringValue = "冲锋枪";
            serialized.FindProperty("mCaliber").intValue = (int)Caliber.S;
            serialized.FindProperty("mMagazine").intValue = 30;
            serialized.FindProperty("mReloadTime").floatValue = 1.8f;
            serialized.FindProperty("mDurabilityMax").floatValue = 160f;
            serialized.FindProperty("mDamage").floatValue = 8f;
            serialized.FindProperty("mRoundsPerMinute").floatValue = 720f;
            serialized.FindProperty("mSemiAutoInterval").floatValue = 0.25f;
            serialized.FindProperty("mIsAutomatic").boolValue = true;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            AssetDatabase.CreateAsset(config, Path);
            Selection.activeObject = config;
            Debug.Log("[阶段四] 已创建冲锋枪配置；未修改手枪、Stage1 或环境，请手工完成装配。", config);
        }
        finally
        {
            if (!AssetDatabase.Contains(config)) Object.DestroyImmediate(config);
        }
    }

    [MenuItem(Menu, true)]
    private static bool CanCreate() => !EditorApplication.isPlayingOrWillChangePlaymode && !EditorApplication.isCompiling;
}
#endif
