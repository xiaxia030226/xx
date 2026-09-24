using QFramework;
using UnityEngine;

/// <summary>将跨局金币与单局掉落收入分开管理的经济数据接口。</summary>
public interface IEconomyModel : IModel
{
    BindableProperty<int> Gold { get; } // 跨局金币余额，变更同步到 PlayerPrefs。

    BindableProperty<int> RunGold { get; } // 本局掉落金币统计，由结算流程并入 Gold，不单独存档。
}

/// <summary>从 PlayerPrefs 读取金币并订阅变更；SetInt 更新偏好值，此处未显式调用 Save。</summary>
public class EconomyModel : AbstractModel, IEconomyModel
{
    private const string GoldKey = "gold"; // 金币在 PlayerPrefs 中的键名。

    public BindableProperty<int> Gold { get; } = new BindableProperty<int>(0); // 可订阅的跨局金币余额。
    public BindableProperty<int> RunGold { get; } = new BindableProperty<int>(0); // 可订阅的本局掉落收入，初始为零。

    // 作用：读取跨局金币并注册后续写回回调；返回：无返回值。
    protected override void OnInit()
    {
        // 先读档再订阅，初始化读取不会触发本方法新建的写回回调。
        Gold.Value = PlayerPrefs.GetInt(GoldKey, 0);

        // 仅订阅 Gold；变化时更新偏好值，不保证异常退出前已刷盘。
        Gold.Register(newGold => PlayerPrefs.SetInt(GoldKey, newGold));
    }
}
