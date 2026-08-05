using System;
using System.Collections.Generic;

public class ObjectPool : Singleton<ObjectPool>
{
    public readonly Dictionary<Type, IPool> pools = new Dictionary<Type, IPool>();

    public IPoolObj GetObject<T>() where T : IPoolObj, new()
    {
        Type type = typeof(T);

        if (pools.TryGetValue(type, out IPool pool))
        {
            return pool.GetObject();
        }
        else
        {
            IPool newPool = new SubObjectPool<T>();
            pools[type] = newPool;
            return newPool.GetObject();
        }
    }

    public void PutObject(IPoolObj obj)
    {
        Type type = obj.GetType();
        if (pools.TryGetValue(type, out IPool pool))
        {
            pool.PutObject(obj);
        }
    }

}