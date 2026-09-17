using UnityEngine;

[CreateAssetMenu(fileName = "NewShield", menuName = "Game/护盾配置", order = 0)]
public class ShieldConfig : ScriptableObject
{
    [SerializeField, Range(1, 5)] private int mLevel;
    [SerializeField, Min(0f)] private float mCapacity;

    public int Level => mLevel;
    public float Capacity => mCapacity;
}
