using QFramework;
using UnityEngine;

/// <summary>
/// UIKit 配置：面板 prefab 统一放 Resources/UI/ 下，按面板类名加载；
/// Resources 中找不到 prefab 时退化为纯代码创建面板，方便面板完全用代码搭建。
/// 调用侧保持 UIKit.OpenPanel<T>() 即可，无需每次传 prefab 路径。
/// </summary>
public class GameUIKitConfig : UIKitConfig
{
    private const string PanelPathPrefix = "UI/"; // 默认面板路径前缀，相对于 Resources 文件夹且不含扩展名。

    // 作用：按补全后的路径加载面板，缺少预制体时创建面板组件；返回：加载或创建的面板接口。
    public override IPanel LoadPanel(PanelSearchKeys panelSearchKeys)
    {
        FillPrefabPath(panelSearchKeys);

        // 先检查预制体是否存在；存在时交给框架加载，否则创建带 RectTransform 的对象并挂载面板。
        var prefab = Resources.Load<GameObject>(panelSearchKeys.GameObjName);
        if (prefab != null) return base.LoadPanel(panelSearchKeys);

        var go = new GameObject(panelSearchKeys.PanelType.Name, typeof(RectTransform));
        return (IPanel)go.AddComponent(panelSearchKeys.PanelType);
    }

    // 作用：通过框架异步加载或同步兜底创建面板并通知回调；返回：无返回值。
    public override void LoadPanelAsync(PanelSearchKeys panelSearchKeys, System.Action<IPanel> onPanelLoad)
    {
        FillPrefabPath(panelSearchKeys);

        // 这里先同步探测预制体，存在时才委托框架的异步加载流程。
        var prefab = Resources.Load<GameObject>(panelSearchKeys.GameObjName);
        if (prefab != null)
        {
            base.LoadPanelAsync(panelSearchKeys, onPanelLoad);
            return;
        }

        // 缺少预制体时立即创建对象；仅在回调非空时添加面板组件并同步交付。
        var go = new GameObject(panelSearchKeys.PanelType.Name, typeof(RectTransform));
        onPanelLoad?.Invoke((IPanel)go.AddComponent(panelSearchKeys.PanelType));
    }

    // 作用：未指定对象名时按面板类型补全默认资源路径；返回：无返回值。
    private static void FillPrefabPath(PanelSearchKeys panelSearchKeys)
    {
        // 缺少搜索条件或类型则不补全，已有自定义路径时也保持原值。
        if (panelSearchKeys == null || panelSearchKeys.PanelType == null) return;
        if (!string.IsNullOrEmpty(panelSearchKeys.GameObjName)) return;

        panelSearchKeys.GameObjName = PanelPathPrefix + panelSearchKeys.PanelType.Name;
    }
}
