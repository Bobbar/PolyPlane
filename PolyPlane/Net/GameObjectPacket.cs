using PolyPlane.GameObjects;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using unvell.D2DLib;
using ENet;

namespace PolyPlane.Net
{
    [Serializable]
    public class NetPacket
    {
        public PacketTypes Type;
        public GameID ID;
        //public long ID;

        public NetPacket() { }

        public NetPacket(GameID id)
        {
            ID = id;
            //ID = iD;
        }

        public NetPacket(PacketTypes type, GameID id)
        {
            Type = type;
            ID = id;

            //ID = iD;
        }


        public NetPacket(long objectID)
        {
            ID = new GameID(-1, objectID);
            //ID = iD;
        }

        public NetPacket(int playerID, long objectID)
        {
            ID = new GameID(playerID, objectID);
            //ID = iD;
        }


        public NetPacket(PacketTypes type, long objectID)
        {
            Type = type;
            ID = new GameID(-1, objectID);

            //ID = iD;
        }

        public NetPacket(PacketTypes type, int playerID, long objectID)
        {
            Type = type;
            ID = new GameID(playerID, objectID);

            //ID = iD;
        }
    }

    [Serializable]
    public class PlaneListPacket : NetPacket
    {
        public List<PlanePacket> Planes = new List<PlanePacket>();

        public PlaneListPacket() 
        {
            Type = PacketTypes.PlaneUpdate;
        }

        public PlaneListPacket(List<PlanePacket> planes) 
        {
            Type = PacketTypes.PlaneUpdate;
            Planes = planes;
        }
    }


    [Serializable]
    public class MissileListPacket : NetPacket
    {
        public List<MissilePacket> Missiles = new List<MissilePacket>();

        public MissileListPacket() 
        {
            Type = PacketTypes.MissileUpdate;
        }

        public MissileListPacket(List<MissilePacket> missiles)
        {
            Type = PacketTypes.MissileUpdate;
            Missiles = missiles;
        }
    }


    [Serializable]
    public abstract class GameObjectPacket : NetPacket
    {
        public GameID OwnerID;

        //public long OwnerID;
        public PointF Position;
        public PointF Velocity;
        public float Rotation;
        public bool IsExpired;

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
            IsExpired = obj.IsExpired;
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
            IsExpired = obj.IsExpired;

        }

        public virtual void SyncObj(GameObject obj)
        {
            if (!this.ID.Equals(obj.ID))
                throw new InvalidOperationException($"Object ID [{obj}] does not match this packet ID [{this.ID}]");

            obj.Position = this.Position.ToD2DPoint();
            obj.Velocity = this.Velocity.ToD2DPoint();
            obj.Rotation = this.Rotation;
            obj.IsExpired = this.IsExpired;
        }
    }

    [Serializable]
    public class PlanePacket : GameObjectPacket
    {
        public float ThrustAmt;
        public float Deflection;
        public D2DColor PlaneColor;
        public bool IsDamaged;
        public bool HasCrashed;

        public PlanePacket() { }

        public PlanePacket(Plane obj) : base(obj)
        {
            ThrustAmt = obj.ThrustAmount;
            Deflection = obj.Deflection;
            PlaneColor = obj.PlaneColor;
            IsDamaged = obj.IsDamaged;
            HasCrashed = obj.HasCrashed;
        }

        public PlanePacket(Plane obj, PacketTypes type) : base(obj, type)
        {
            ThrustAmt = obj.ThrustAmount;
            Deflection = obj.Deflection;
            PlaneColor = obj.PlaneColor;
            IsDamaged = obj.IsDamaged;
            HasCrashed = obj.HasCrashed;
        }

        public virtual void SyncObj(Plane obj)
        {
            base.SyncObj(obj);
            //obj.PlaneColor = this.PlaneColor.
            obj.ThrustAmount = ThrustAmt;
            obj.Deflection = Deflection;
            obj.IsDamaged = IsDamaged;
            obj.HasCrashed = HasCrashed;

        }
    }

    [Serializable]
    public class BulletPacket : GameObjectPacket
    {

        public BulletPacket() { }

        public BulletPacket(GameObject obj) : base(obj)
        {
            //this.OwnerID = obj.Owner.ID;
        }

        public BulletPacket(GameObject obj, PacketTypes type) : base(obj, type)
        {
            //this.OwnerID = obj.Owner.ID;

        }

        public virtual void SyncObj(GameObject obj)
        {
            base.SyncObj(obj);
        }
    }

    [Serializable]
    public class MissilePacket : GameObjectPacket
    {
        public float Deflection;
        public float CurrentFuel;
        public GameID TargetID;

        public MissilePacket() 
        {
            Type = PacketTypes.NewMissile;
        }

        public MissilePacket(GuidedMissile obj) : base(obj)
        {
            Type = PacketTypes.NewMissile;

            this.OwnerID = obj.Owner.ID;
            this.Deflection = obj.Deflection;
            this.CurrentFuel = obj.CurrentFuel;
            this.TargetID = obj.Target.ID;


        }

        //public MissilePacket(GameObject obj, PacketTypes type) : base(obj, type)
        //{
        //    //this.OwnerID = obj.Owner.ID;

        //}

        public virtual void SyncObj(GuidedMissile obj)
        {
            base.SyncObj(obj);

            obj.Deflection = this.Deflection;
            obj.CurrentFuel = this.CurrentFuel;
        }
    }


    [Serializable]
    public class ImpactPacket : NetPacket
    {
        public GameID ImpactorID;
        public PointF ImpactPoint;

        public ImpactPacket()
        {
            Type = PacketTypes.Impact;
        }

        public ImpactPacket(GameID impactorId, GameID targetId, D2DPoint point)
        {
            ImpactorID = impactorId;
            ID = targetId;
            ImpactPoint = point.ToPoint();
            Type = PacketTypes.Impact;
        }
    }


}
