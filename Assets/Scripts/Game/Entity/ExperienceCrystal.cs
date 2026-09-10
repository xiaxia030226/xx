using QFramework;
using UnityEngine;

/// <summary>
/// 经验水晶：敌人死亡后生成在死亡位置，玩家靠近后加速吸附，接触即拾取。
/// 拾取时发送 GainExpCommand，并把自身回收到对象池。
/// </summary>
public class ExperienceCrystal : MonoBehaviour, IController
{
    private const float PickupRadius = 3f;
    private const float MagnetSpeed = 12f;
    private const float CollectDistance = 0.6f;

    private int mExpValue;
    private Transform mPlayer;
    private bool mActive;

    public IArchitecture GetArchitecture() => GameArchitecture.Interface;

    /// <summary>从对象池取出时调用：重置经验值和玩家引用，激活更新逻辑。</summary>
    public void OnSpawn(int expValue, Transform player)
    {
        mExpValue = expValue;
        mPlayer = player;
        mActive = true;
    }

    private void Update()
    {
        if (!mActive || mPlayer == null) return;

        var toPlayer = mPlayer.position - transform.position;
        toPlayer.y = 0f;
        var sqrDistance = toPlayer.sqrMagnitude;

        // 玩家进入吸附半径后，水晶加速飞向玩家；越近飞得越快，营造"吸附感"。
        if (sqrDistance <= PickupRadius * PickupRadius)
        {
            var speed = MagnetSpeed + (PickupRadius - Mathf.Sqrt(sqrDistance)) * 4f;
            transform.position += toPlayer.normalized * (speed * Time.deltaTime);
        }

        // 距离足够近时判定拾取；用平方距离比较，避免开平方运算。
        if (sqrDistance <= CollectDistance * CollectDistance)
        {
            Collect();
        }
    }

    private void Collect()
    {
        mActive = false;

        this.SendCommand(new GainExpCommand(mExpValue));

        // 先发命令后回池：保证经验已入账，水晶才消失。
        this.GetSystem<IGameObjectPoolSystem>().Recycle("exp_crystal", gameObject);
    }
}
