using TMPro;
using UnityEngine;

public class EnemyTelegraph : MonoBehaviour
{
    // 0：技能预告；1～3：鼓手连线；4：护盾环。全部由 prefab 装配。
    [SerializeField] private LineRenderer[] mLines;
    [SerializeField] private TMP_Text mShieldLabel;

    private const int CircleSegments = 48;
    private static readonly Color DangerColor = new Color(1f, 0.25f, 0.08f, 0.9f);
    private static readonly Color ShieldColor = new Color(0.15f, 0.8f, 1f, 0.9f);
    private float mBreakTime;

    public void ResetVisuals()
    {
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

    public void ClearAttack() => HideLine(0);

    public void ShowLine(Vector3 from, Vector3 to, float width = 0.12f)
    {
        var line = PrepareLine(0, DangerColor, width, 2);
        if (line == null) return;
        line.SetPosition(0, Ground(from));
        line.SetPosition(1, Ground(to));
    }

    public void ShowCircle(Vector3 center, float radius)
    {
        var line = PrepareLine(0, DangerColor, 0.1f, CircleSegments + 1);
        DrawCircle(line, center, radius, 0);
    }

    public void ShowTeleport(Vector3 from, Vector3 to, float radius)
    {
        var line = PrepareLine(0, new Color(0.85f, 0.3f, 1f, 0.95f), 0.1f,
            (CircleSegments + 1) * 2);
        DrawCircle(line, from, radius, 0);
        DrawCircle(line, to, radius, CircleSegments + 1);
    }

    public void ShowSupport(int index, Vector3 from, Vector3 to)
    {
        if (index < 0 || index > 2) return;
        var line = PrepareLine(index + 1, ShieldColor, 0.07f, 2);
        if (line == null) return;
        line.SetPosition(0, from + Vector3.up * 0.3f);
        line.SetPosition(1, to + Vector3.up * 0.3f);
    }

    public void ClearSupport(int index)
    {
        if (index >= 0 && index < 3) HideLine(index + 1);
    }

    public void FlashShieldBreak() => mBreakTime = 0.3f;

    public void RefreshShield(Vector3 center, float radius, int level, float current, float maximum,
        float deltaTime)
    {
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

    private LineRenderer PrepareLine(int index, Color color, float width, int count)
    {
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

    private static void DrawCircle(LineRenderer line, Vector3 center, float radius, int start)
    {
        if (line == null) return;
        center = Ground(center);
        for (var i = 0; i <= CircleSegments; i++)
        {
            var angle = i * Mathf.PI * 2f / CircleSegments;
            line.SetPosition(start + i, center + new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * radius);
        }
    }

    private void HideLine(int index)
    {
        if (mLines == null || index < 0 || index >= mLines.Length || mLines[index] == null) return;
        mLines[index].positionCount = 0;
        mLines[index].enabled = false;
    }

    private static Vector3 Ground(Vector3 position)
    {
        position.y = 0.06f;
        return position;
    }

    private void OnDisable() => ResetVisuals();
}
