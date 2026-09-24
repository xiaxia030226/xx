using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using QFramework;

/// <summary>
/// 阶段三 HUD 装配：向 GameHUD.prefab 添加 ShieldBar（ShieldFill/ShieldText）与 SafeLootButton，
/// 加高 WavePreviewText 预告区域以容纳 W5 的四行分组，然后调用 UIKit 代码生成重新产出
/// GameHUD.Designer.cs；编译后 UISerializer 自动把新字段接线到 prefab。
/// 已存在 ShieldBar 时跳过节点装配，但仍请求生成 Designer；装配会保存 HUD prefab，非只读操作。
/// </summary>
public static class StageThreeHudAssembler
{
    private const string HudPath = "Assets/Resources/UI/GameHUD.prefab"; // 要装配并保存的 HUD 预制体资产路径。

    // 作用：装配缺失的 HUD 节点、保存预制体并请求生成 Designer；返回：无返回值。
    [MenuItem("Game/阶段三/装配HUD并生成Designer")]
    public static void Assemble()
    {
        // 仅在停止播放且未编译时处理已存在的 HUD 资产。
        if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling)
        {
            Debug.LogWarning("[阶段三] 请在停止播放且编译结束后装配。");
            return;
        }
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(HudPath);
        if (prefab == null)
        {
            Debug.LogError("[阶段三] 缺少 " + HudPath);
            return;
        }
        // 在预制体编辑内容中检查幂等标记，仅缺失时装配并保存；finally 始终卸载临时内容。
        var root = PrefabUtility.LoadPrefabContents(HudPath);
        try
        {
            if (FindDeep(root.transform, "ShieldBar") != null)
            {
                Debug.Log("[阶段三] HUD 已存在 ShieldBar，跳过重复装配。");
            }
            else
            {
                AssembleInternal(root.transform);
                PrefabUtility.SaveAsPrefabAsset(root, HudPath);
                Debug.Log("[阶段三] HUD 装配完成：ShieldBar/ShieldFill/ShieldText/SafeLootButton 已加入，WavePreviewText 已加高。");
            }
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }

        // 重新生成 Designer（GameHUD.cs 已存在不会被覆盖）；生成后 Refresh 触发编译，
        // 编译完成的 DidReloadScripts 里 UISerializer 自动把新字段引用写回 prefab。
        UICodeGenerator.DoCreateCode(new Object[] { AssetDatabase.LoadAssetAtPath<GameObject>(HudPath) });
        AssetDatabase.Refresh();
        Debug.Log("[阶段三] Designer 已重新生成，等待编译完成后自动序列化引用。");
    }

    // 作用：参照已有血条和字体创建护盾条、结算按钮并调整波次预告布局；返回：无返回值。
    private static void AssembleInternal(Transform root)
    {
        var hpBar = FindDeep(root, "HPBar");
        var hpText = FindDeep(root, "HpText");
        if (hpBar == null || hpText == null) throw new System.InvalidOperationException("GameHUD 缺少 HPBar/HpText，无法定位装配。");
        var font = hpText.GetComponent<TextMeshProUGUI>()?.font;
        if (font == null) throw new System.InvalidOperationException("HpText 未绑定 TMP 字体。");

        // 护盾条：HPBar 正下方，与其同父级、同锚点与 pivot。
        var shieldBar = new GameObject("ShieldBar", typeof(RectTransform));
        shieldBar.transform.SetParent(hpBar.parent, false);
        shieldBar.transform.SetSiblingIndex(hpBar.GetSiblingIndex() + 1);
        var barRect = (RectTransform)shieldBar.transform;
        var hpRect = (RectTransform)hpBar;
        barRect.anchorMin = hpRect.anchorMin;
        barRect.anchorMax = hpRect.anchorMax;
        barRect.pivot = hpRect.pivot;
        barRect.anchoredPosition = new Vector2(20f, -48f);
        barRect.sizeDelta = new Vector2(300f, 14f);
        var barBg = shieldBar.AddComponent<Image>();
        barBg.color = new Color(0.1f, 0.12f, 0.16f, 0.85f);
        barBg.raycastTarget = false;
        // Bind：Designer 生成 Image 字段，代码按护盾有无整体显隐。
        shieldBar.AddComponent<Bind>();

        var fill = NewStretchChild(barRect, "ShieldFill");
        var fillImage = fill.gameObject.AddComponent<Image>();
        fillImage.type = Image.Type.Filled;
        fillImage.fillMethod = Image.FillMethod.Horizontal;
        fillImage.fillOrigin = (int)Image.OriginHorizontal.Left;
        fillImage.fillAmount = 1f;
        fillImage.color = new Color(0.15f, 0.8f, 1f, 0.9f);
        fillImage.raycastTarget = false;
        fill.gameObject.AddComponent<Bind>();

        var text = NewStretchChild(barRect, "ShieldText");
        var shieldText = text.gameObject.AddComponent<TextMeshProUGUI>();
        shieldText.font = font;
        shieldText.fontSize = 10f;
        shieldText.alignment = TextAlignmentOptions.Center;
        shieldText.color = Color.white;
        shieldText.raycastTarget = false;
        shieldText.text = string.Empty;
        text.gameObject.AddComponent<Bind>();

        // 结算按钮：底部居中、武器栏（高 100）上方；默认隐藏，进入 SafeLoot 由代码显示。
        var buttonGo = new GameObject("SafeLootButton", typeof(RectTransform));
        buttonGo.transform.SetParent(root, false);
        var buttonRect = (RectTransform)buttonGo.transform;
        buttonRect.anchorMin = new Vector2(0.5f, 0f);
        buttonRect.anchorMax = new Vector2(0.5f, 0f);
        buttonRect.pivot = new Vector2(0.5f, 0.5f);
        buttonRect.anchoredPosition = new Vector2(0f, 130f);
        buttonRect.sizeDelta = new Vector2(160f, 44f);
        var buttonImage = buttonGo.AddComponent<Image>();
        buttonImage.color = new Color(0.2f, 0.55f, 0.3f, 0.95f);
        var button = buttonGo.AddComponent<Button>();
        button.targetGraphic = buttonImage;
        button.transition = Selectable.Transition.ColorTint;
        buttonGo.AddComponent<Bind>();
        var labelRect = NewStretchChild(buttonRect, "Text");
        var label = labelRect.gameObject.AddComponent<TextMeshProUGUI>();
        label.font = font;
        label.fontSize = 22f;
        label.alignment = TextAlignmentOptions.Center;
        label.color = Color.white;
        label.text = "结算";
        buttonGo.SetActive(false);

        // 预告区域：pivot 改为顶部，向下加高到 100（W5 四行），倍率文本相应下移。
        var preview = FindDeep(root, "WavePreviewText");
        var multiplier = FindDeep(root, "MultiplierText");
        if (preview != null)
        {
            var previewRect = (RectTransform)preview;
            previewRect.pivot = new Vector2(0.5f, 1f);
            previewRect.anchoredPosition = new Vector2(0f, -48f);
            previewRect.sizeDelta = new Vector2(480f, 100f);
            var previewText = preview.GetComponent<TextMeshProUGUI>();
            if (previewText != null)
            {
                previewText.enableWordWrapping = true;
                previewText.verticalAlignment = VerticalAlignmentOptions.Top;
                previewText.overflowMode = TextOverflowModes.Overflow;
            }
        }
        if (multiplier != null)
        {
            ((RectTransform)multiplier).anchoredPosition = new Vector2(0f, -152f);
        }
    }

    // 作用：创建四边贴合父节点的 UI 子节点；返回：新节点的 RectTransform。
    private static RectTransform NewStretchChild(RectTransform parent, string name)
    {
        // 拉伸锚点并清零偏移，让填充条和文字共用父节点区域。
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        var rect = (RectTransform)go.transform;
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
        return rect;
    }

    // 作用：在根节点及全部子节点中按名称查找，包含未激活节点；返回：首个匹配的 Transform，未找到为 null。
    private static Transform FindDeep(Transform root, string name)
    {
        // 遍历包含隐藏 HUD 节点的完整层级，命中名称即停止查找。
        foreach (var t in root.GetComponentsInChildren<Transform>(true))
            if (t.name == name) return t;
        return null;
    }
}
