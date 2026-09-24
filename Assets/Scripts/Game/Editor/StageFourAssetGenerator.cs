#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEngine;

public static class StageFourAssetGenerator
{
    private const string Menu = "Game/阶段四/生成缺失冲锋枪配置"; // 生成冲锋枪配置的编辑器菜单路径。
    private const string Path = "Assets/Resources/Configs/Weapons/SMG.asset"; // 冲锋枪配置资产的目标路径。

    // 作用：检查已有 ID 和路径后创建并持久化缺失的冲锋枪配置；返回：无返回值。
    [MenuItem(Menu)]
    private static void CreateSmg()
    {
        // 全项目按 ID 查重，找到即选中并退出，不覆盖已有配置。
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
        // 路径未占用且目录存在才创建临时配置，通过序列化字段写入后由 CreateAsset 保存到磁盘。
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
            // 已持久化的配置保留；创建失败留下的内存对象立即销毁。
            if (!AssetDatabase.Contains(config)) Object.DestroyImmediate(config);
        }
    }

    // 作用：直接计算菜单是否可用；返回：true 表示未播放、未切换到播放且未编译，false 表示禁止生成。
    [MenuItem(Menu, true)]
    private static bool CanCreate() => !EditorApplication.isPlayingOrWillChangePlaymode && !EditorApplication.isCompiling; // 同时排除播放切换与编译阶段，避免生成时机冲突。
}
#endif
