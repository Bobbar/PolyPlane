using NetStack.Serialization;
using PolyPlane.GameObjects;
using unvell.D2DLib;


namespace PolyPlane.Net
{
    public abstract class NetPacket
    {
        public PacketTypes Type;
        public GameID ID;
        public long FrameTime;

        public NetPacket()
        {
            FrameTime = World.CurrentTime();
        }

        public NetPacket(BitBuffer data)
        {
            this.Deserialize(data);
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

        public virtual void Serialize(BitBuffer data)
        {
            data.AddByte((byte)Type);
            ID.Serialize(data);
            data.AddLong(FrameTime);
        }

        public virtual void Deserialize(BitBuffer data)
        {
            Type = (PacketTypes)data.ReadByte();
            ID.Deserialize(data);
            FrameTime = data.ReadLong();
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

        public ChatPacket(BitBuffer data)
        {
            this.Deserialize(data);
        }

        public ChatPacket(string message, string playerName) : base()
        {
            Type = PacketTypes.ChatMessage;
            Message = message;
            PlayerName = playerName;
        }

        public virtual void Serialize(BitBuffer data)
        {
            base.Serialize(data);

            data.AddString(Message);
            data.AddString(PlayerName);
        }

        public virtual void Deserialize(BitBuffer data)
        {
            base.Deserialize(data);
            Message = data.ReadString();
            PlayerName = data.ReadString();
        }
    }

    public class DiscoveryPacket : NetPacket
    {
        public string IP;
        public string Name;
        public int Port;

        public DiscoveryPacket(BitBuffer data)
        {
            this.Deserialize(data);
        }

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
            this.ID = new GameID(123, 321);

        }

        public virtual void Serialize(BitBuffer data)
        {
            base.Serialize(data);
            data.AddString(IP);
            data.AddString(Name);
            data.AddInt(Port);
        }

        public virtual void Deserialize(BitBuffer data)
        {
            base.Deserialize(data);
            IP = data.ReadString();
            Name = data.ReadString();
            Port = data.ReadInt();
        }
    }


    public class SyncPacket : NetPacket
    {
        public long ServerTime;
        public float TimeOfDay;
        public float TimeOfDayDir;

        public SyncPacket(BitBuffer data)
        {
            this.Deserialize(data);
        }

        public SyncPacket() : base()
        {
            Type = PacketTypes.ServerSync;
        }

        public SyncPacket(long serverTime, float timeOfDay, float timeOfDayDir) : base()
        {
            Type = PacketTypes.ServerSync;
            ServerTime = serverTime;
            TimeOfDay = timeOfDay;
            TimeOfDayDir = timeOfDayDir;
        }

        public virtual void Serialize(BitBuffer data)
        {
            base.Serialize(data);

            data.AddLong(ServerTime);
            TimeOfDay.Serialize(data);
            TimeOfDayDir.Serialize(data);
        }

        public virtual void Deserialize(BitBuffer data)
        {
            base.Deserialize(data);
            ServerTime = data.ReadLong();
            TimeOfDay.Deserialize(data);
            TimeOfDayDir.Deserialize(data);
        }
    }


    public class BasicPacket : NetPacket
    {
        public BasicPacket(BitBuffer data)
        {
            this.Deserialize(data);
        }

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

        public BasicListPacket(BitBuffer data)
        {
            this.Deserialize(data);
        }

        public BasicListPacket() : base() { }

        public BasicListPacket(PacketTypes type) : base(type)
        {
        }

        public virtual void Serialize(BitBuffer data)
        {
            base.Serialize(data);
            data.AddInt(Packets.Count);
            foreach (var packet in Packets)
                packet.Serialize(data);
        }

        public virtual void Deserialize(BitBuffer data)
        {
            base.Deserialize(data);
            var len = data.ReadInt();
            for (int i = 0; i < len; i++)
                Packets.Add(new BasicPacket(data));
        }
    }


    public class PlaneListPacket : NetPacket
    {
        public List<PlanePacket> Planes = new List<PlanePacket>();

        public PlaneListPacket(BitBuffer data)
        {
            this.Deserialize(data);
        }

        public PlaneListPacket() : base()
        {
            Type = PacketTypes.PlaneUpdate;
        }

        public PlaneListPacket(List<PlanePacket> planes) : base()
        {
            Type = PacketTypes.PlaneUpdate;
            Planes = planes;
        }

        public virtual void Serialize(BitBuffer data)
        {
            base.Serialize(data);
            data.AddInt(Planes.Count);
            foreach (var packet in Planes)
                packet.Serialize(data);
        }

        public virtual void Deserialize(BitBuffer data)
        {
            base.Deserialize(data);
            var len = data.ReadInt();
            for (int i = 0; i < len; i++)
                Planes.Add(new PlanePacket(data));
        }
    }


    public class MissileListPacket : NetPacket
    {
        public List<MissilePacket> Missiles = new List<MissilePacket>();

        public MissileListPacket(BitBuffer data)
        {
            this.Deserialize(data);
        }

        public MissileListPacket() : base()
        {
            Type = PacketTypes.MissileUpdate;
        }

        public MissileListPacket(List<MissilePacket> missiles) : base()
        {
            Type = PacketTypes.MissileUpdate;
            Missiles = missiles;
        }

        public virtual void Serialize(BitBuffer data)
        {
            base.Serialize(data);
            data.AddInt(Missiles.Count);
            foreach (var packet in Missiles)
                packet.Serialize(data);
        }

        public virtual void Deserialize(BitBuffer data)
        {
            base.Deserialize(data);
            var len = data.ReadInt();
            for (int i = 0; i < len; i++)
                Missiles.Add(new MissilePacket(data));
        }
    }

    public class NewPlayerPacket : NetPacket
    {
        public string Name;
        public D2DColor PlaneColor;
        public D2DPoint Position;

        public NewPlayerPacket(BitBuffer data)
        {
            this.Deserialize(data);
        }

        public NewPlayerPacket() : base()
        {
            Type = PacketTypes.NewPlayer;
        }

        public NewPlayerPacket(FighterPlane plane) : base()
        {
            Type = PacketTypes.NewPlayer;
            Name = plane.PlayerName;
            PlaneColor = plane.PlaneColor;
            Position = plane.Position;
            ID = plane.ID;
        }

        public virtual void Serialize(BitBuffer data)
        {
            base.Serialize(data);
            data.AddString(Name);
            PlaneColor.Serialize(data);
            Position.Serialize(data);
        }

        public virtual void Deserialize(BitBuffer data)
        {
            base.Deserialize(data);
            Name = data.ReadString();
            PlaneColor.Deserialize(data);
            Position.Deserialize(data);
        }
    }

    public class PlayerListPacket : NetPacket
    {
        public List<NewPlayerPacket> Players = new List<NewPlayerPacket>();

        public PlayerListPacket(BitBuffer data)
        {
            this.Deserialize(data);
        }

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

        public virtual void Serialize(BitBuffer data)
        {
            base.Serialize(data);
            data.AddInt(Players.Count);
            foreach (var packet in Players)
                packet.Serialize(data);
        }

        public virtual void Deserialize(BitBuffer data)
        {
            base.Deserialize(data);
            var len = data.ReadInt();
            for (int i = 0; i < len; i++)
                Players.Add(new NewPlayerPacket(data));
        }

    }

    public class GameObjectPacket : NetPacket
    {
        public GameID OwnerID;
        public D2DPoint Position;
        public D2DPoint Velocity;
        public float Rotation;

        public GameObjectPacket(BitBuffer data)
        {
            this.Deserialize(data);
        }

        public GameObjectPacket() : base()
        {

        }

        public GameObjectPacket(GameObject obj) : base(obj.ID)
        {
            ID = obj.ID;

            if (obj.Owner != null)
                OwnerID = obj.Owner.ID;

            Position = obj.Position;
            Velocity = obj.Velocity;
            Rotation = obj.Rotation;
        }


        public GameObjectPacket(GameObject obj, PacketTypes type) : base(type, obj.ID)
        {
            Type = type;
            ID = obj.ID;

            if (obj.Owner != null)
                OwnerID = obj.Owner.ID;

            Position = obj.Position;
            Velocity = obj.Velocity;
            Rotation = obj.Rotation;
        }

        public virtual void SyncObj(GameObject obj)
        {
            if (!this.ID.Equals(obj.ID))
                throw new InvalidOperationException($"Object ID [{obj}] does not match this packet ID [{this.ID}]");

            //obj.Position = this.Position.ToD2DPoint();
            //obj.Velocity = this.Velocity.ToD2DPoint();
            //obj.Rotation = this.Rotation;
        }

        public override void Serialize(BitBuffer data)
        {
            base.Serialize(data);

            OwnerID.Serialize(data);
            Position.Serialize(data);
            Velocity.Serialize(data);
            Rotation.Serialize(data);
        }

        public override void Deserialize(BitBuffer data)
        {
            base.Deserialize(data);

            OwnerID.Deserialize(data);
            Position.Deserialize(data);
            Velocity.Deserialize(data);
            Rotation.Deserialize(data);
        }
    }


    public class PlanePacket : GameObjectPacket
    {
        public float Deflection;
        public bool IsDamaged;
        public bool FiringBurst;
        public int Hits;
        public int Kills;

        public PlanePacket(BitBuffer data)
        {
            this.Deserialize(data);
        }


        public PlanePacket() { }

        public PlanePacket(FighterPlane obj) : base(obj)
        {
            Deflection = obj.Deflection;
            IsDamaged = obj.IsDamaged;
            Hits = obj.Hits;
            FiringBurst = obj.FiringBurst;
            Kills = obj.Kills;
        }

        public virtual void SyncObj(FighterPlane obj)
        {
            base.SyncObj(obj);
            obj.Deflection = Deflection;
            obj.IsDamaged = IsDamaged;
            obj.FiringBurst = FiringBurst;
            obj.Hits = Hits;
            obj.Kills = Kills;
        }

        public override void Serialize(BitBuffer data)
        {
            base.Serialize(data);

            Deflection.Serialize(data);
            data.AddBool(IsDamaged);
            data.AddBool(FiringBurst);
            data.AddInt(Hits);
            data.AddInt(Kills);
        }

        public override void Deserialize(BitBuffer data)
        {
            base.Deserialize(data);

            Deflection.Deserialize(data);
            IsDamaged = data.ReadBool();
            FiringBurst = data.ReadBool();
            Hits = data.ReadInt();
            Kills = data.ReadInt();
        }
    }

    public class MissilePacket : GameObjectPacket
    {
        public float Deflection;
        public bool FlameOn;
        public GameID TargetID;

        public MissilePacket(BitBuffer data)
        {
            this.Deserialize(data);
        }

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
            this.TargetID = obj.Target.ID;
        }

        public virtual void SyncObj(GuidedMissile obj)
        {
            base.SyncObj(obj);

            obj.Deflection = this.Deflection;
            obj.FlameOn = this.FlameOn;
        }

        public override void Serialize(BitBuffer data)
        {
            base.Serialize(data);

            Deflection.Serialize(data);
            data.AddBool(FlameOn);
            TargetID.Serialize(data);
        }

        public override void Deserialize(BitBuffer data)
        {
            base.Deserialize(data);

            Deflection.Deserialize(data);
            FlameOn = data.ReadBool();
            TargetID.Deserialize(data);
        }
    }

    public class ImpactPacket : GameObjectPacket
    {
        public GameID ImpactorID;
        public D2DPoint ImpactPoint;
        public bool DoesDamage;
        public bool WasHeadshot;
        public bool WasMissile;

        public ImpactPacket(BitBuffer data)
        {
            this.Deserialize(data);
        }

        public ImpactPacket() : base()
        {
            Type = PacketTypes.Impact;
        }

        public ImpactPacket(GameObject targetObj, GameID impactorID, D2DPoint point, bool doesDamage, bool wasHeadshot, bool wasMissile) : base(targetObj)
        {
            ImpactorID = impactorID;
            //ID = targetId;
            ImpactPoint = point;
            Type = PacketTypes.Impact;
            DoesDamage = doesDamage;
            WasHeadshot = wasHeadshot;
            WasMissile = wasMissile;
        }

        public override void Serialize(BitBuffer data)
        {
            base.Serialize(data);
            ImpactorID.Serialize(data);
            ImpactPoint.Serialize(data);
            data.AddBool(DoesDamage);
            data.AddBool(WasHeadshot);
            data.AddBool(WasMissile);
        }

        public override void Deserialize(BitBuffer data)
        {
            base.Deserialize(data);
            ImpactorID.Deserialize(data);
            ImpactPoint.Deserialize(data);
            DoesDamage = data.ReadBool();
            WasHeadshot = data.ReadBool();
            WasMissile = data.ReadBool();
        }
    }

    public class ImpactListPacket : GameObjectPacket
    {
        public List<ImpactPacket> Impacts = new List<ImpactPacket>();

        public ImpactListPacket(BitBuffer data)
        {
            this.Deserialize(data);
        }


        public ImpactListPacket() : base()
        {
            Type = PacketTypes.ImpactList;
        }

        public ImpactListPacket(List<ImpactPacket> impacts) : base()
        {
            Type = PacketTypes.ImpactList;
            Impacts = impacts;
        }

        public virtual void Serialize(BitBuffer data)
        {
            base.Serialize(data);
            data.AddInt(Impacts.Count);
            foreach (var packet in Impacts)
                packet.Serialize(data);
        }

        public virtual void Deserialize(BitBuffer data)
        {
            base.Deserialize(data);
            var len = data.ReadInt();
            for (int i = 0; i < len; i++)
                Impacts.Add(new ImpactPacket(data));
        }
    }
}