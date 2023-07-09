using System.Collections.Concurrent;

namespace NetworkLibrary.Utils
{
    public class ConcurrentObjectPool<T> where T : new()
    {
        public delegate T ConstructorCallback();
        public ConstructorCallback HowToConstruct;

        public readonly ConcurrentBag<T> pool = new ConcurrentBag<T>();

        public T RentObject()
        {
            if (!pool.TryTake(out T obj))
            {
                return HowToConstruct == null ? new T() : HowToConstruct.Invoke();
            }
            return obj;
        }

        public void ReturnObject(T obj)
        {
            pool.Add(obj);
        }
    }
}
