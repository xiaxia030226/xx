using System;
using System.Collections.Generic;
using QFramework;
using UnityEngine;

/// <summary>
/// 对象池对外接口。调用者只依赖接口，不关心底层使用哪一种池实现。
/// </summary>
public interface IGameObjectPoolSystem : ISystem
{
    // factory 定义如何创建新对象，initialCount 表示注册时预先创建多少个缓存对象。
    void Register(string key, Func<GameObject> factory, int initialCount = 0);
    GameObject Spawn(string key, Vector3 position, Quaternion rotation, Transform parent = null);
    void Recycle(string key, GameObject instance);
    int GetCachedCount(string key);

    // ClearAll：清空全部池并销毁缓存对象，切换场景重入时必须调用，
    // 避免池里残留已随旧场景卸载的无效引用。
    void ClearAll();
}

/// <summary>
/// 按字符串 key 管理多个 GameObject 对象池。
/// </summary>
public class GameObjectPoolSystem : AbstractSystem, IGameObjectPoolSystem
{
    private readonly Dictionary<string, SimpleObjectPool<GameObject>> mPools =
        new Dictionary<string, SimpleObjectPool<GameObject>>();

    public void Register(string key, Func<GameObject> factory, int initialCount = 0)
    {
        if (string.IsNullOrEmpty(key)) throw new ArgumentException("对象池 key 不能为空", nameof(key));
        if (factory == null) throw new ArgumentNullException(nameof(factory));
        if (mPools.ContainsKey(key)) return;

        // SimpleObjectPool 的第一个委托负责创建对象，第二个委托负责对象回池时的重置。
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

    public GameObject Spawn(string key, Vector3 position, Quaternion rotation, Transform parent = null)
    {
        var pool = GetPool(key);

        // Allocate 优先取缓存对象；池为空时才调用注册时提供的 factory 创建新对象。
        var instance = pool.Allocate();
        instance.transform.SetParent(parent, false);
        instance.transform.SetPositionAndRotation(position, rotation);
        instance.SetActive(true);
        return instance;
    }

    public void Recycle(string key, GameObject instance)
    {
        if (instance == null) return;

        var pool = GetPool(key);
        instance.SetActive(false);

        // Recycle 不销毁对象，只把引用放回对应 key 的缓存栈供下次复用。
        pool.Recycle(instance);
        Debug.Log($"[Pool] 回收 {key}，缓存数量：{pool.CurCount}");
    }

    public int GetCachedCount(string key)
    {
        return GetPool(key).CurCount;
    }

    /// <summary>
    /// 清空全部对象池并销毁缓存对象。
    /// 池里缓存的是场景物体，场景卸载后引用即失效；重进战斗场景前必须调用本方法。
    /// </summary>
    public void ClearAll()
    {
        foreach (var pool in mPools.Values)
        {
            pool.Clear(instance =>
            {
                if (instance != null) UnityEngine.Object.Destroy(instance);
            });
        }

        mPools.Clear();
    }

    protected override void OnInit()
    {
    }

    protected override void OnDeinit()
    {
        foreach (var pool in mPools.Values)
        {
            pool.Clear(instance =>
            {
                if (instance != null) UnityEngine.Object.Destroy(instance);
            });
        }

        mPools.Clear();
    }

    private SimpleObjectPool<GameObject> GetPool(string key)
    {
        if (!mPools.TryGetValue(key, out var pool))
        {
            throw new InvalidOperationException($"对象池尚未注册：{key}");
        }

        return pool;
    }
}
