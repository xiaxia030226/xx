using TMPro;
using UnityEngine;

public class EnemyTelegraph : MonoBehaviour
{
    [SerializeField] private LineRenderer[] mLines; // 预制体线条：0 技能预告，1～3 鼓手支援连线，4 护盾环。
    [SerializeField] private TMP_Text mShieldLabel; // 护盾等级、数值或破盾提示文本。

    private const int CircleSegments = 48; // 圆形预告的分段数，首尾重合需多一个顶点。
    private static readonly Color DangerColor = new Color(1f, 0.25f, 0.08f, 0.9f); // 通常攻击预告的颜色。
    private static readonly Color ShieldColor = new Color(0.15f, 0.8f, 1f, 0.9f); // 护盾环与支援连线的颜色。
    private float mBreakTime; // 破盾闪白效果的剩余秒数。

    // 作用：清空全部预告、支援线及护盾显示；返回：无返回值。
    public void ResetVisuals()
    {
        // 池复用或禁用时逐条清理，缺少可选线条不影响其他线条重置。
        mBreakTime = 0f;
        if (mLines != null)
            foreach (var line in mLines)
                if (line != null)
                {
                    line.positionCount = 0;
                    line.enabled = false;
                }
        if (mShieldLabel != null) mShieldLabel.text = string.Empty;
    }

    // 作用：隐藏技能预告而不影响护盾与支援线；返回：无返回值。
    public void ClearAttack() => HideLine(0); // 只将技能预告槽 0 转交隐藏，保留其余线条。

    // 作用：在地面绘制攻击方向线；返回：无返回值。
    public void ShowLine(Vector3 from, Vector3 to, float width = 0.12f)
    {
        // 复用技能预告槽的两个顶点，将起终点压到统一地面高度后连线。
        var line = PrepareLine(0, DangerColor, width, 2);
        if (line == null) return;
        line.SetPosition(0, Ground(from));
        line.SetPosition(1, Ground(to));
    }

    // 作用：绘制地面圆形攻击范围；返回：无返回值。
    public void ShowCircle(Vector3 center, float radius)
    {
        // 为技能槽预留分段数加一的顶点，再交给圆形绘制逻辑写入含闭合点的整圈。
        var line = PrepareLine(0, DangerColor, 0.1f, CircleSegments + 1);
        DrawCircle(line, center, radius, 0);
    }

    // 作用：在同一条线中绘制瞬移出发与到达圆环；返回：无返回值。
    public void ShowTeleport(Vector3 from, Vector3 to, float radius)
    {
        // 两组圆顶点连续写入同一 LineRenderer，顶点组之间也会连线。
        var line = PrepareLine(0, new Color(0.85f, 0.3f, 1f, 0.95f), 0.1f,
            (CircleSegments + 1) * 2);
        DrawCircle(line, from, radius, 0);
        DrawCircle(line, to, radius, CircleSegments + 1);
    }

    // 作用：更新指定支援槽到目标的连线；返回：无返回值。
    public void ShowSupport(int index, Vector3 from, Vector3 to)
    {
        // 支援槽 0～2 映射到线条 1～3，避免占用技能预告线。
        if (index < 0 || index > 2) return;
        var line = PrepareLine(index + 1, ShieldColor, 0.07f, 2);
        if (line == null) return;
        line.SetPosition(0, from + Vector3.up * 0.3f);
        line.SetPosition(1, to + Vector3.up * 0.3f);
    }

    // 作用：隐藏有效支援槽对应的连线；返回：无返回值。
    public void ClearSupport(int index)
    {
        // 只接受三个支援槽，并偏移一位映射到线条，避免误清技能预告槽。
        if (index >= 0 && index < 3) HideLine(index + 1);
    }

    // 作用：重新启动短暂的破盾闪白计时；返回：无返回值。
    public void FlashShieldBreak() => mBreakTime = 0.3f; // 将剩余闪白时间直接重置为 0.3 秒，不与旧计时累加。

    // 作用：推进破盾效果并更新护盾环、数值与朝向；返回：无返回值。
    public void RefreshShield(Vector3 center, float radius, int level, float current, float maximum,
        float deltaTime)
    {
        // 盾已耗尽时仍保留尚未结束的破盾效果，计时结束才隐藏。
        mBreakTime = Mathf.Max(0f, mBreakTime - deltaTime);
        if (level <= 0 || (current <= 0f && mBreakTime <= 0f))
        {
            HideLine(4);
            if (mShieldLabel != null) mShieldLabel.text = string.Empty;
            return;
        }

        var color = mBreakTime > 0f ? Color.white : ShieldColor;
        var line = PrepareLine(4, color, mBreakTime > 0f ? 0.2f : 0.08f, CircleSegments + 1);
        DrawCircle(line, center, radius + 0.18f + (mBreakTime > 0f ? 0.3f - mBreakTime : 0f), 0);
        if (mShieldLabel != null)
        {
            mShieldLabel.text = current > 0f ? $"S{level} {current:0}/{maximum:0}" : "BREAK";
            mShieldLabel.color = color;
            var camera = Camera.main;
            if (camera != null) mShieldLabel.transform.rotation = camera.transform.rotation;
        }
    }

    // 作用：复用指定预制体线条并配置显示参数；返回：可用线条，索引无效或未装配时为 null。
    private LineRenderer PrepareLine(int index, Color color, float width, int count)
    {
        // 缺少有效槽位时不创建替代对象；可用时覆盖世界坐标、颜色、宽度及顶点数后启用。
        if (mLines == null || index < 0 || index >= mLines.Length || mLines[index] == null) return null;
        var line = mLines[index];
        line.useWorldSpace = true;
        line.loop = false;
        line.startColor = line.endColor = color;
        line.startWidth = line.endWidth = width;
        line.positionCount = count;
        line.enabled = true;
        return line;
    }

    // 作用：从指定顶点下标写入闭合的地面圆形折线；返回：无返回值。
    private static void DrawCircle(LineRenderer line, Vector3 center, float radius, int start)
    {
        if (line == null) return;
        center = Ground(center);
        // 包含整圈末端的重复顶点，不依赖 LineRenderer.loop 闭合。
        for (var i = 0; i <= CircleSegments; i++)
        {
            var angle = i * Mathf.PI * 2f / CircleSegments;
            line.SetPosition(start + i, center + new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * radius);
        }
    }

    // 作用：清空并禁用指定线条，忽略缺失装配；返回：无返回值。
    private void HideLine(int index)
    {
        // 确认槽位有效后同时清空顶点并关闭渲染，避免复用时残留旧线形。
        if (mLines == null || index < 0 || index >= mLines.Length || mLines[index] == null) return;
        mLines[index].positionCount = 0;
        mLines[index].enabled = false;
    }

    // 作用：将位置投影到统一的预告显示高度；返回：保留 XZ、Y 为 0.06 的位置。
    private static Vector3 Ground(Vector3 position)
    {
        // 只覆盖位置副本的高度，使预告保持原有平面落点并略高于地面。
        position.y = 0.06f;
        return position;
    }

    // 作用：禁用时清理全部显示以便池复用；返回：无返回值。
    private void OnDisable() => ResetVisuals(); // 将禁用清理统一转交 ResetVisuals，重置线条、标签和破盾计时。
}
