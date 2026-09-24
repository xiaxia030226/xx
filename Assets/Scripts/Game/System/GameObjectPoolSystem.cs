using System;
using System.Collections.Generic;
using QFramework;
using UnityEngine;

/// <summary>
/// 对象池对外接口。调用者只依赖接口，不关心底层使用哪一种池实现。
/// </summary>
public interface IGameObjectPoolSystem : ISystem
{
    // 作用：用 factory 注册命名池并预热 initialCount 个实例，同名注册不替换已有池；返回：无返回值。
    void Register(string key, Func<GameObject> factory, int initialCount = 0);
    // 作用：从命名池租用对象并设置父节点、世界姿态及激活状态；返回：可供调用者初始化的场景对象。
    GameObject Spawn(string key, Vector3 position, Quaternion rotation, Transform parent = null);
    // 作用：将非空对象停用并归还指定池；返回：无返回值。
    void Recycle(string key, GameObject instance);
    // 作用：查询命名池内当前可复用对象数量；返回：缓存数，不含已租出的实例。
    int GetCachedCount(string key);

    // 作用：销毁全部池的缓存对象并移除注册，避免重进场景沿用旧引用；返回：无返回值。
    void ClearAll();
}

/// <summary>
/// 按字符串 key 管理多个 GameObject 对象池。
/// </summary>
public class GameObjectPoolSystem : AbstractSystem, IGameObjectPoolSystem
{
    private readonly Dictionary<string, SimpleObjectPool<GameObject>> mPools =
        new Dictionary<string, SimpleObjectPool<GameObject>>(); // 池标识到对象池的映射，仅池内缓存对象由池负责清理。

    // 作用：校验参数后注册并预热对象池，已有同名池时保持原注册；返回：无返回值。
    public void Register(string key, Func<GameObject> factory, int initialCount = 0)
    {
        if (string.IsNullOrEmpty(key)) throw new ArgumentException("对象池 key 不能为空", nameof(key));
        if (factory == null) throw new ArgumentNullException(nameof(factory));
        if (mPools.ContainsKey(key)) return;

        // 创建委托统一命名并停用新对象；回池委托仅停用，业务状态需调用者在租用或回收时处理。
        mPools.Add(key, new SimpleObjectPool<GameObject>(() =>
        {
            var instance = factory();
            instance.name = key;
            instance.SetActive(false);
            return instance;
        }, instance =>
        {
            if (instance != null) instance.SetActive(false);
        }, initialCount));

        Debug.Log($"[Pool] 注册 {key}，预热 {initialCount} 个");
    }

    // 作用：租用命名池对象并放到指定父节点及世界姿态；返回：已激活的 GameObject，未注册的池会抛错。
    public GameObject Spawn(string key, Vector3 position, Quaternion rotation, Transform parent = null)
    {
        var pool = GetPool(key);

        // 优先取缓存，池空才创建；先定位后激活，业务组件的初始化由调用者随后完成。
        var instance = pool.Allocate();
        instance.transform.SetParent(parent, false);
        instance.transform.SetPositionAndRotation(position, rotation);
        instance.SetActive(true);
        return instance;
    }

    // 作用：停用实例并归还指定池供后续复用，空实例直接忽略；返回：无返回值。
    public void Recycle(string key, GameObject instance)
    {
        if (instance == null) return;

        var pool = GetPool(key);
        instance.SetActive(false);

        // 只把引用交回对应 key 的缓存，不销毁；调用者须保证实例归还正确的池。
        pool.Recycle(instance);
        Debug.Log($"[Pool] 回收 {key}，缓存数量：{pool.CurCount}");
    }

    // 作用：读取指定池的空闲缓存数；返回：当前缓存数量，未注册的池会抛错。
    public int GetCachedCount(string key)
    {
        // 先确认池已注册，再读取空闲数量，不把已租出的对象算入缓存。
        return GetPool(key).CurCount;
    }

    // 作用：销毁缓存实例并清除所有池注册，供战斗场景重入时重建；返回：无返回值。
    public void ClearAll()
    {
        // 只清理已回池对象；租出的活跃对象仍由调用方或场景生命周期负责。
        foreach (var pool in mPools.Values)
        {
            pool.Clear(instance =>
            {
                if (instance != null) UnityEngine.Object.Destroy(instance);
            });
        }

        mPools.Clear();
    }

    // 作用：响应 QFramework 系统初始化，具体池等待场景按需注册；返回：无返回值。
    protected override void OnInit()
    {
        // 场景资源尚未绑定，这里不预先创建任何池。
    }

    // 作用：在架构反初始化时销毁池内缓存并移除注册；返回：无返回值。
    protected override void OnDeinit()
    {
        // 系统跨场景持有池引用，架构释放时也必须清理仍存在的缓存实例。
        foreach (var pool in mPools.Values)
        {
            pool.Clear(instance =>
            {
                if (instance != null) UnityEngine.Object.Destroy(instance);
            });
        }

        mPools.Clear();
    }

    // 作用：按标识取得已注册对象池，缺失时显式报错；返回：对应的 GameObject 对象池。
    private SimpleObjectPool<GameObject> GetPool(string key)
    {
        // 缺失注册属于装配错误，直接抛错而不是悄悄创建缺少工厂的池。
        if (!mPools.TryGetValue(key, out var pool))
        {
            throw new InvalidOperationException($"对象池尚未注册：{key}");
        }

        return pool;
    }
}
