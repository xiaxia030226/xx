using System.Collections.Generic;

public class SubObjectPool<T> : IPool where T : IPoolObj, new()
{
    public readonly Stack<IPoolObj> objects = new Stack<IPoolObj>();

    public IPoolObj GetObject()
    {

        if (objects.Count > 0)
        {
            IPoolObj obj = objects.Pop();
            obj.ResetData();
            return obj;
        }
        else
        {
            IPoolObj obj = new T();
            obj.FirstInit();
            obj.ResetData();
            return obj;
        }
    }

    public void PutObject(IPoolObj obj)
    {
        if (obj is not T) return;

        obj.ResetData();
        objects.Push(obj);
    }

}