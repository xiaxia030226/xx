using UnityEngine;

[CreateAssetMenu(fileName = "NewShield", menuName = "Game/护盾配置", order = 0)]
public class ShieldConfig : ScriptableObject
{
    [SerializeField, Range(1, 5)] private int mLevel; // 护盾等级，用于配置查询及穿甲比较。
    [SerializeField, Min(0f)] private float mCapacity; // 该等级护盾的最大容量。

    public int Level => mLevel; // 护盾等级。
    public float Capacity => mCapacity; // 护盾容量上限。
}
