using System.Collections.Concurrent;

namespace PolyPlane.Helpers
{
    /// <summary>
    /// Provides basic object pooling.
    /// </summary>
    /// <typeparam name="T"></typeparam>
    public sealed class GameObjectPool<T>
    {
        private ConcurrentQueue<T> _pool = new ConcurrentQueue<T>();
        private readonly Func<T> _factory;

        public GameObjectPool(Func<T> factory)
        {
            _factory = factory;
        }

        public T RentObject()
        {
            if (_pool.TryDequeue(out T obj))
            {
                return obj;
            }
            else
            {
                return _factory();
            }
        }

        public void ReturnObject(T obj)
        {
            _pool.Enqueue(obj);
        }

        public void Clear()
        {
            _pool.Clear();
        }
    }
}
