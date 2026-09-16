using QFramework;
using UnityEngine;

/// <summary>
/// 经济数据接口。金币是跨局保留的局外资源，与单局运行数据分开管理。
/// </summary>
public interface IEconomyModel : IModel
{
    BindableProperty<int> Gold { get; }
}

/// <summary>
/// 管理金币：启动时从 PlayerPrefs 读档，每次变动立即落盘，强退不丢。
/// </summary>
public class EconomyModel : AbstractModel, IEconomyModel
{
    // GoldKey：金币在 PlayerPrefs 中的存档键。
    private const string GoldKey = "gold";

    public BindableProperty<int> Gold { get; } = new BindableProperty<int>(0);

    protected override void OnInit()
    {
        // 第一步：读取存档中的金币总数，没有存档时从 0 开始。
        Gold.Value = PlayerPrefs.GetInt(GoldKey, 0);

        // 第二步：订阅金币变化即时写入存档。
        // 订阅伴随架构生命周期常驻，无需注销。
        Gold.Register(newGold => PlayerPrefs.SetInt(GoldKey, newGold));
    }
}
