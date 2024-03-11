using PolyPlane.GameObjects;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using unvell.D2DLib;

namespace PolyPlane.Net
{
    [Serializable]
    public class NetPacket
    {
        public PacketTypes Type;
        public long ID;

        public NetPacket() { }

        public NetPacket(long iD)
        {
            ID = iD;
        }

        public NetPacket(PacketTypes type, long iD)
        {
            Type = type;
            ID = iD;
        }
    }

    [Serializable]
    public class PlaneListPacket : NetPacket
    {
        public List<PlanePacket> Planes;

        public PlaneListPacket() { }

        public PlaneListPacket(List<PlanePacket> planes) 
        {
            Planes = planes;
        }
    }


    [Serializable]
    public abstract class GameObjectPacket : NetPacket
    {
       
        public long OwnerID;
        public PointF Position;
        public PointF Velocity;
        public float Rotation;

        public GameObjectPacket() : base()
        {

        }

        public GameObjectPacket(GameObject obj) : base(obj.ID)
        {
            ID = obj.ID;

            if (obj.Owner != null)
                OwnerID = obj.Owner.ID;

            Position = obj.Position.ToPoint();
            Velocity = obj.Velocity.ToPoint();
            Rotation = obj.Rotation;
        }

        public GameObjectPacket(GameObject obj, PacketTypes type) : base(type, obj.ID)
        {
            Type = type;
            ID = obj.ID;

            if (obj.Owner != null)
                OwnerID = obj.Owner.ID;

            Position = obj.Position.ToPoint();
            Velocity = obj.Velocity.ToPoint();
            Rotation = obj.Rotation;
        }

        public virtual void SyncObj(GameObject obj)
        {
            if (this.ID != obj.ID)
                throw new InvalidOperationException($"Object ID [{obj}] does not match this packet ID [{this.ID}]");

            obj.Position = this.Position.ToD2DPoint();
            obj.Velocity = this.Velocity.ToD2DPoint();
            obj.Rotation = this.Rotation;
         
        }
    }

    [Serializable]
    public class PlanePacket : GameObjectPacket
    {
        public float ThrustAmt;
        public D2DColor PlaneColor;

        public PlanePacket() { }

        public PlanePacket(Plane obj) : base(obj)
        {
            ThrustAmt = obj.ThrustAmount;
            PlaneColor = obj.PlaneColor;
        }

        public PlanePacket(Plane obj, PacketTypes type) : base(obj, type)
        {
            ThrustAmt = obj.ThrustAmount;
        }

        public virtual void SyncObj(Plane obj)
        {
            base.SyncObj(obj);

            obj.ThrustAmount = ThrustAmt;
        }
    }
   }
