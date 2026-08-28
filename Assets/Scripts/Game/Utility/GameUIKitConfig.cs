using QFramework;

/// <summary>
/// UIKit 配置：面板 prefab 统一放 Resources/UI/ 下，按面板类名加载。
/// 调用侧保持 UIKit.OpenPanel<T>() 即可，无需每次传 prefab 路径。
/// </summary>
public class GameUIKitConfig : UIKitConfig
{
    // Resources.Load 使用相对 Resources 文件夹且不带扩展名的路径。
    private const string PanelPathPrefix = "UI/";

    public override IPanel LoadPanel(PanelSearchKeys panelSearchKeys)
    {
        FillPrefabPath(panelSearchKeys);
        return base.LoadPanel(panelSearchKeys);
    }

    public override void LoadPanelAsync(PanelSearchKeys panelSearchKeys, System.Action<IPanel> onPanelLoad)
    {
        FillPrefabPath(panelSearchKeys);
        base.LoadPanelAsync(panelSearchKeys, onPanelLoad);
    }

    /// <summary>
    /// 未手动传 prefabName 时，使用面板类型名自动补出 Resources 加载路径。
    /// 如果调用方已经指定 GameObjName，则尊重调用方的自定义路径。
    /// </summary>
    private static void FillPrefabPath(PanelSearchKeys panelSearchKeys)
    {
        if (panelSearchKeys == null || panelSearchKeys.PanelType == null) return;
        if (!string.IsNullOrEmpty(panelSearchKeys.GameObjName)) return;

        panelSearchKeys.GameObjName = PanelPathPrefix + panelSearchKeys.PanelType.Name;
    }
}
