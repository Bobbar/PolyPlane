using PolyPlane.Helpers;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PolyPlane.Net
{
    public sealed class PacketPool
    {
        private Dictionary<Type, GameObjectPool<object>> _pool = new();



        public NetPacket RentPacket<T>(Func<T> factory) where T : NetPacket
        {
            var type = typeof(T);
            if (_pool.TryGetValue(type, out var pool))
            {
                return pool.RentObject() as NetPacket;
            }
            else
            {
                Add(factory);
                return RentPacket(factory);
            }
        }


        public void Add<T>(Func<T> factory) where T : NetPacket
        {
            var type = typeof(T);
            _pool.TryAdd(type, new GameObjectPool<object>(factory));
        }

        //public void ReturnPacket<T>(T packet) where T : NetPacket
        //{
        //    var type = typeof(T);
        //    if (_pool.TryGetValue(type, out var pool))
        //    {
        //        pool.ReturnObject(packet);
        //    }
        //}

        public void ReturnPacket(object packet)
        {
            //var type = typeof(packet);
            var type = packet.GetType();

            if (_pool.TryGetValue(type, out var pool))
            {
                pool.ReturnObject(packet);
            }
        }
    }
}
