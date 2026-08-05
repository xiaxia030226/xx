
public interface IPool
{
    IPoolObj GetObject();
    void PutObject(IPoolObj obj);
}
