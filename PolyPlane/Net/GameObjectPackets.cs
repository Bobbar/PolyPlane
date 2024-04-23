using PolyPlane.GameObjects;
using unvell.D2DLib;


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

        public static NetPoint operator -(NetPoint a, NetPoint b)
        {
            return new NetPoint(a.X - b.X, a.Y - b.Y);
        }

        public static NetPoint operator *(NetPoint a, NetPoint b)
        {
            return new NetPoint(a.X * b.X, a.Y * b.Y);
        }

        public static NetPoint operator *(NetPoint a, float val)
        {
            return new NetPoint(a.X * val, a.Y * val);
        }

        public static NetPoint operator +(NetPoint a, NetPoint b)
        {
            return new NetPoint(a.X + b.X, a.Y + b.Y);
        }
        public static NetPoint operator +(NetPoint a, float val)
        {
            return new NetPoint(a.X + val, a.Y + val);
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

    public abstract class NetPacket
    {
        public PacketTypes Type;
        public GameID ID;
        public double FrameTime;

        public NetPacket()
        {
            FrameTime = World.CurrentTime();
        }

        public NetPacket(PacketTypes type) : this()
        {
            Type = type;
        }

        public NetPacket(GameID id) : this()
        {
            ID = id;
        }

        public NetPacket(PacketTypes type, GameID id) : this(type)
        {
            ID = id;
        }
    }

    public class ChatPacket : NetPacket
    {
        public string Message;
        public string PlayerName;

        public ChatPacket() : base()
        {
            Type = PacketTypes.ChatMessage;
        }

        public ChatPacket(string message, string playerName) : base()
        {
            Type = PacketTypes.ChatMessage;
            Message = message;
            PlayerName = playerName;
        }

    }


    public class DiscoveryPacket : NetPacket
    {
        public string IP;
        public string Name;
        public int Port;

        public DiscoveryPacket() : base()
        {
            Type = PacketTypes.Discovery;
        }

        public DiscoveryPacket(string ip) : base()
        {
            Type = PacketTypes.Discovery;
            IP = ip;
        }

        public DiscoveryPacket(string ip, string name) : base()
        {
            Type = PacketTypes.Discovery;
            IP = ip;
            Name = name;
        }

        public DiscoveryPacket(string ip, string name, int port) : base()
        {
            Type = PacketTypes.Discovery;
            IP = ip;
            Name = name;
            Port = port;
        }


    }


    public class SyncPacket : NetPacket
    {
        public double ServerTime;
        public float TimeOfDay;
        public float TimeOfDayDir;


        public SyncPacket() : base()
        {
            Type = PacketTypes.ServerSync;
        }

        public SyncPacket(double serverTime, float timeOfDay, float timeOfDayDir) : base()
        {
            Type = PacketTypes.ServerSync;
            ServerTime = serverTime;
            TimeOfDay = timeOfDay;
            TimeOfDayDir = timeOfDayDir;
        }
    }


    public class BasicPacket : NetPacket
    {
        public BasicPacket() : base() { }

        public BasicPacket(PacketTypes type, GameID id) : base()
        {
            Type = type;
            ID = id;
        }
    }

    public class BasicListPacket : NetPacket
    {
        public List<BasicPacket> Packets = new List<BasicPacket>();

        public BasicListPacket() : base() { }

        public BasicListPacket(PacketTypes type) : base(type)
        {
        }
    }


    public class PlaneListPacket : NetPacket
    {
        public List<PlanePacket> Planes = new List<PlanePacket>();

        public PlaneListPacket() : base()
        {
            Type = PacketTypes.PlaneUpdate;
        }

        public PlaneListPacket(List<PlanePacket> planes) : base()
        {
            Type = PacketTypes.PlaneUpdate;
            Planes = planes;
        }
    }


    public class MissileListPacket : NetPacket
    {
        public List<MissilePacket> Missiles = new List<MissilePacket>();

        public MissileListPacket() : base()
        {
            Type = PacketTypes.MissileUpdate;
        }

        public MissileListPacket(List<MissilePacket> missiles) : base()
        {
            Type = PacketTypes.MissileUpdate;
            Missiles = missiles;
        }
    }

    public class NewPlayerPacket : NetPacket
    {
        public string Name;
        public D2DColor PlaneColor;
        public NetPoint Position;

        public NewPlayerPacket() : base()
        {
            Type = PacketTypes.NewPlayer;
        }

        public NewPlayerPacket(FighterPlane plane) : base()
        {
            Type = PacketTypes.NewPlayer;
            Name = plane.PlayerName;
            PlaneColor = plane.PlaneColor;
            Position = plane.Position.ToNetPoint();
            ID = plane.ID;
        }
    }

    public class PlayerListPacket : NetPacket
    {
        public List<NewPlayerPacket> Players = new List<NewPlayerPacket>();

        public PlayerListPacket() : base()
        {
        }

        public PlayerListPacket(PacketTypes type) : base(type)
        {
        }

        public PlayerListPacket(PacketTypes type, List<NewPlayerPacket> players) : base(type)
        {
            Players = players;
        }
    }

    public class GameObjectPacket : NetPacket
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

            Position = obj.Position.ToNetPoint();
            Velocity = obj.Velocity.ToNetPoint();
            Rotation = obj.Rotation;
            IsExpired = obj.IsExpired;
        }


        public GameObjectPacket(GameObject obj, PacketTypes type) : base(type, obj.ID)
        {
            Type = type;
            ID = obj.ID;

            if (obj.Owner != null)
                OwnerID = obj.Owner.ID;

            Position = obj.Position.ToNetPoint();
            Velocity = obj.Velocity.ToNetPoint();
            Rotation = obj.Rotation;
            IsExpired = obj.IsExpired;

        }

        public virtual void SyncObj(GameObject obj)
        {
            if (!this.ID.Equals(obj.ID))
                throw new InvalidOperationException($"Object ID [{obj}] does not match this packet ID [{this.ID}]");

            //obj.Position = this.Position.ToD2DPoint();
            //obj.Velocity = this.Velocity.ToD2DPoint();
            //obj.Rotation = this.Rotation;

            if (!obj.IsExpired) // Prevent new packets from un-expiring objects.
                obj.IsExpired = this.IsExpired;
        }
    }


    public class PlanePacket : GameObjectPacket
    {
        public float ThrustAmt;
        public float Deflection;
        public bool IsDamaged;
        public bool HasCrashed;
        public bool WasHeadshot;
        public bool FiringBurst;
        public int Hits;

        public PlanePacket() { }

        public PlanePacket(FighterPlane obj) : base(obj)
        {
            ThrustAmt = obj.ThrustAmount;
            Deflection = obj.Deflection;
            IsDamaged = obj.IsDamaged;
            HasCrashed = obj.HasCrashed;
            WasHeadshot = obj.WasHeadshot;
            Hits = obj.Hits;
            FiringBurst = obj.FiringBurst;
        }

        public PlanePacket(FighterPlane obj, PacketTypes type) : base(obj, type)
        {
            ThrustAmt = obj.ThrustAmount;
            Deflection = obj.Deflection;
            IsDamaged = obj.IsDamaged;
            HasCrashed = obj.HasCrashed;
            WasHeadshot = obj.WasHeadshot;
            Hits = obj.Hits;
        }

        public virtual void SyncObj(FighterPlane obj)
        {
            base.SyncObj(obj);
            obj.ThrustAmount = ThrustAmt;
            obj.Deflection = Deflection;
            obj.IsDamaged = IsDamaged;
            obj.HasCrashed = HasCrashed;
            obj.WasHeadshot = WasHeadshot;
            obj.FiringBurst = FiringBurst;
            obj.Hits = Hits;
        }
    }


    public class BulletPacket : GameObjectPacket
    {
        public BulletPacket() : base()
        {
            Type = PacketTypes.NewBullet;
        }

        public BulletPacket(GameObject obj) : base(obj)
        {
            Type = PacketTypes.NewBullet;
        }

        public virtual void SyncObj(GameObject obj)
        {
            base.SyncObj(obj);
        }
    }


    public class MissilePacket : GameObjectPacket
    {
        public float Deflection;
        public float CurrentFuel;
        public bool FlameOn;
        public bool IsDistracted;
        public GameID TargetID;

        public MissilePacket() : base()
        {
            Type = PacketTypes.NewMissile;
        }

        public MissilePacket(GuidedMissile obj) : base(obj)
        {
            Type = PacketTypes.NewMissile;

            this.OwnerID = obj.Owner.ID;
            this.FlameOn = obj.FlameOn;
            this.Deflection = obj.Deflection;
            this.CurrentFuel = obj.CurrentFuel;
            this.TargetID = obj.Target.ID;
            this.IsDistracted = obj.IsDistracted;
        }

        public virtual void SyncObj(GuidedMissile obj)
        {
            base.SyncObj(obj);

            obj.Deflection = this.Deflection;
            obj.CurrentFuel = this.CurrentFuel;
            obj.FlameOn = this.FlameOn;
            obj.IsDistracted = this.IsDistracted;
        }
    }

    public class ImpactPacket : GameObjectPacket
    {
        public GameID ImpactorID;
        public NetPoint ImpactPoint;
        public bool DoesDamage;
        public bool WasHeadshot;
        public bool WasMissile;

        public ImpactPacket() : base()
        {
            Type = PacketTypes.Impact;
        }

        public ImpactPacket(GameObject targetObj, GameID impactorID, D2DPoint point, bool doesDamage, bool wasHeadshot, bool wasMissile) : base(targetObj)
        {
            ImpactorID = impactorID;
            //ID = targetId;
            ImpactPoint = point.ToNetPoint();
            Type = PacketTypes.Impact;
            DoesDamage = doesDamage;
            WasHeadshot = wasHeadshot;
            WasMissile = wasMissile;
        }
    }

    public class DecoyPacket : GameObjectPacket
    {
        public DecoyPacket()
        {
            Type = PacketTypes.NewDecoy;
        }

        public DecoyPacket(GameObject decoy) : base(decoy)
        {
            Type = PacketTypes.NewDecoy;
        }
    }
}