using PolyPlane.GameObjects;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using unvell.D2DLib;
using ENet;
using GroBuf;


namespace PolyPlane.Net
{

    public struct NetPoint
    {
        public float X;
        public float Y;

        public NetPoint() { }
        public NetPoint(float x, float y)
        {
            X = x;
            Y = y;
        }

        public NetPoint(D2DPoint point)
        {
            X = point.X;
            Y = point.Y;
        }

        public D2DPoint ToD2DPoint()
        {
            return new D2DPoint(this.X, this.Y);
        }

        public override string ToString()
        {
            return $"({this.X}, {this.Y})";
        }
    }


    public struct PayloadPacket
    {
        public PacketTypes Type;
        public byte[] Payload;

        public PayloadPacket() { }

        public PayloadPacket(PacketTypes type, byte[] payload)
        {
            this.Type = type;
            this.Payload = payload;
        }
    }

    public abstract partial class NetPacket
    {
        public PacketTypes Type;
        public GameID ID;
        public long FrameTime;

        public NetPacket()
        {
            FrameTime = DateTime.UtcNow.Ticks;
        }

        public NetPacket(GameID id) : this()
        {
            ID = id;
            //ID = iD;
        }

        public NetPacket(PacketTypes type, GameID id) : this()
        {
            Type = type;
            ID = id;

            //ID = iD;
        }


        //public NetPacket(long objectID) : this()
        //{
        //    ID = new GameID(-1, objectID);
        //    //ID = iD;
        //}

        //public NetPacket(int playerID, long objectID) : this()
        //{
        //    ID = new GameID(playerID, objectID);
        //    //ID = iD;
        //}


        public NetPacket(PacketTypes type, long objectID) : this()
        {

            Type = type;
            ID = new GameID(-1, objectID);

            //ID = iD;
        }

        public NetPacket(PacketTypes type, int playerID, long objectID) : this()
        {
            Type = type;
            ID = new GameID(playerID, objectID);

            //ID = iD;
        }


        public NetPacket(PacketTypes type, GameID id, long frameTime) : this()
        {
            Type = type;
            ID = id;
            FrameTime = frameTime;

            //ID = iD;
        }
    }



    public partial class BasicPacket : NetPacket
    {
        public BasicPacket() { }

        public BasicPacket(PacketTypes type, GameID id)
        {
            Type = type;
            ID = id;
        }
    }


    public partial class PlaneListPacket : NetPacket
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

        public PlaneListPacket(List<PlanePacket> planes, PacketTypes type, GameID id, long frameTime)
        {
            Type = PacketTypes.PlaneUpdate;
            Planes = planes;
            ID = id;
            FrameTime = frameTime;
        }
    }


    public partial class MissileListPacket : NetPacket
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

    public abstract partial class GameObjectPacket : NetPacket
    {
        public GameID OwnerID;
        public NetPoint Position;
        public NetPoint Velocity;
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


        public GameObjectPacket(GameID ownerID, NetPoint position, NetPoint velocity, float rotation, bool isExpired)
        {
            OwnerID = ownerID;
            Position = position;
            Velocity = velocity;
            Rotation = rotation;
            IsExpired = isExpired;

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



    public partial class PlanePacket : GameObjectPacket
    {
        public float ThrustAmt;
        public float Deflection;
        public D2DColor PlaneColor;
        public bool IsDamaged;
        public bool HasCrashed;
        public bool WasHeadshot;

        public PlanePacket() { }

        public PlanePacket(Plane obj) : base(obj)
        {
            ThrustAmt = obj.ThrustAmount;
            Deflection = obj.Deflection;
            PlaneColor = obj.PlaneColor;
            IsDamaged = obj.IsDamaged;
            HasCrashed = obj.HasCrashed;
            WasHeadshot = obj.WasHeadshot;
        }

        public PlanePacket(Plane obj, PacketTypes type) : base(obj, type)
        {
            ThrustAmt = obj.ThrustAmount;
            Deflection = obj.Deflection;
            PlaneColor = obj.PlaneColor;
            IsDamaged = obj.IsDamaged;
            HasCrashed = obj.HasCrashed;
            WasHeadshot = obj.WasHeadshot;

        }

        public virtual void SyncObj(Plane obj)
        {
            base.SyncObj(obj);
            //obj.PlaneColor = this.PlaneColor.
            obj.ThrustAmount = ThrustAmt;
            obj.Deflection = Deflection;
            obj.IsDamaged = IsDamaged;
            obj.HasCrashed = HasCrashed;
            obj.WasHeadshot = WasHeadshot;
        }
    }


    public partial class BulletPacket : GameObjectPacket
    {

        public BulletPacket() { }

        public BulletPacket(GameObject obj) : base(obj)
        {
        }

        public BulletPacket(GameObject obj, PacketTypes type) : base(obj, type)
        {
        }

        public BulletPacket(GameID ownerID, NetPoint position, NetPoint velocity, float rotation, bool isExpired)
        {
            OwnerID = ownerID;
            Position = position;
            Velocity = velocity;
            Rotation = rotation;
            IsExpired = isExpired;
        }

        public virtual void SyncObj(GameObject obj)
        {
            base.SyncObj(obj);
        }
    }


    public partial class MissilePacket : GameObjectPacket
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

        public MissilePacket(GameID ownerID, NetPoint position, NetPoint velocity, float rotation, bool isExpired, float deflection, float currentFuel, GameID targetID)
        {
            OwnerID = ownerID;
            Position = position;
            Velocity = velocity;
            Rotation = rotation;
            IsExpired = isExpired;

            Deflection = deflection;
            CurrentFuel = currentFuel;
            TargetID = targetID;
        }

        public virtual void SyncObj(GuidedMissile obj)
        {
            base.SyncObj(obj);

            obj.Deflection = this.Deflection;
            obj.CurrentFuel = this.CurrentFuel;
        }
    }

    public partial class ImpactPacket : GameObjectPacket
    {
        public GameID ImpactorID;
        public NetPoint ImpactPoint;

        public ImpactPacket()
        {
            Type = PacketTypes.Impact;
        }

        public ImpactPacket(GameObject targetObj, GameID impactorID, D2DPoint point) : base(targetObj)
        {
            ImpactorID = impactorID;
            //ID = targetId;
            ImpactPoint = point.ToPoint();
            Type = PacketTypes.Impact;
        }

        //public ImpactPacket(GameID ownerID, NetPoint position, NetPoint velocity, float rotation, bool isExpired, GameID impactorID, NetPoint impactPoint)
        //{
        //    OwnerID = ownerID;
        //    Position = position;
        //    Velocity = velocity;
        //    Rotation = rotation;
        //    IsExpired = isExpired;

        //    ImpactorID = impactorID;
        //    ImpactPoint = impactPoint;
        //}
    }


    public partial class DecoyPacket : GameObjectPacket
    {
        public DecoyPacket()
        {
            Type = PacketTypes.NewDecoy;
        }

        public DecoyPacket(GameObject decoy) : base(decoy)
        {
            Type = PacketTypes.NewDecoy;
        }

        public DecoyPacket(GameID ownerID, NetPoint position, NetPoint velocity, float rotation, bool isExpired)
        {
            Type = PacketTypes.NewDecoy;
            OwnerID = ownerID;
            Position = position;
            Velocity = velocity;
            Rotation = rotation;
            IsExpired = isExpired;

        }

    }
}