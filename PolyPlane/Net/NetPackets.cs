using NetStack.Serialization;
using PolyPlane.GameObjects;
using PolyPlane.GameObjects.Manager;
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
            data.AddGameID(ID);
            data.AddLong(FrameTime);
        }

        public virtual void Deserialize(BitBuffer data)
        {
            Type = (PacketTypes)data.ReadByte();
            ID = data.ReadGameID();
            FrameTime = data.ReadLong();
        }
    }


    public class ChatPacket : NetPacket
    {
        public string Message;
        public string PlayerName;

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

        public override void Serialize(BitBuffer data)
        {
            base.Serialize(data);

            data.AddString(Message);
            data.AddString(PlayerName);
        }

        public override void Deserialize(BitBuffer data)
        {
            base.Deserialize(data);

            Message = data.ReadString();
            PlayerName = data.ReadString();
        }
    }

    public class PlayerEventPacket : NetPacket
    {
        public string Message;

        public PlayerEventPacket(string message)
        {
            Message = message;
            Type = PacketTypes.PlayerEvent;
        }

        public PlayerEventPacket(BitBuffer data)
        {
            this.Deserialize(data);
        }

        public override void Serialize(BitBuffer data)
        {
            base.Serialize(data);

            data.AddString(Message);
        }

        public override void Deserialize(BitBuffer data)
        {
            base.Deserialize(data);

            Message = data.ReadString();
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

        public DiscoveryPacket(string ip, string name, int port) : base()
        {
            Type = PacketTypes.Discovery;
            IP = ip;
            Name = name;
            Port = port;
        }

        public override void Serialize(BitBuffer data)
        {
            base.Serialize(data);

            data.AddString(IP);
            data.AddString(Name);
            data.AddInt(Port);
        }

        public override void Deserialize(BitBuffer data)
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
        public bool GunsOnly;
        public float DeltaTime;

        public SyncPacket(BitBuffer data)
        {
            this.Deserialize(data);
        }

        public SyncPacket(long serverTime, float timeOfDay, float timeOfDayDir, bool gunsOnly, float deltaTime) : base()
        {
            Type = PacketTypes.ServerSync;
            ServerTime = serverTime;
            TimeOfDay = timeOfDay;
            TimeOfDayDir = timeOfDayDir;
            GunsOnly = gunsOnly;
            DeltaTime = deltaTime;
        }

        public override void Serialize(BitBuffer data)
        {
            base.Serialize(data);

            data.AddLong(ServerTime);
            data.AddFloat(TimeOfDay);
            data.AddFloat(TimeOfDayDir);
            data.AddBool(GunsOnly);
            data.AddFloat(DeltaTime);
        }

        public override void Deserialize(BitBuffer data)
        {
            base.Deserialize(data);

            ServerTime = data.ReadLong();
            TimeOfDay = data.ReadFloat();
            TimeOfDayDir = data.ReadFloat();
            GunsOnly = data.ReadBool();
            DeltaTime = data.ReadFloat();
        }
    }


    public class BasicPacket : NetPacket
    {
        public D2DPoint Position;

        public BasicPacket(PacketTypes type) : base(type) { }

        public BasicPacket() : base() { }

        public BasicPacket(BitBuffer data)
        {
            this.Deserialize(data);
        }

        public BasicPacket(PacketTypes type, GameID id) : base()
        {
            Type = type;
            ID = id;
        }

        public BasicPacket(PacketTypes type, GameID id, D2DPoint position) : base()
        {
            Type = type;
            ID = id;
            Position = position;
        }

        public override void Serialize(BitBuffer data)
        {
            base.Serialize(data);

            data.AddD2DPoint(Position);

        }

        public override void Deserialize(BitBuffer data)
        {
            base.Deserialize(data);

            Position = data.ReadD2DPoint();
        }
    }

    public class BasicListPacket : NetPacket
    {
        public List<BasicPacket> Packets = new List<BasicPacket>();

        public BasicListPacket(BitBuffer data)
        {
            this.Deserialize(data);
        }

        public BasicListPacket(PacketTypes type) : base(type) { }

        public override void Serialize(BitBuffer data)
        {
            base.Serialize(data);

            data.AddInt(Packets.Count);
            foreach (var packet in Packets)
                packet.Serialize(data);
        }

        public override void Deserialize(BitBuffer data)
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

        public PlaneListPacket() : base()
        {
            Type = PacketTypes.PlaneListUpdate;
        }

        public PlaneListPacket(BitBuffer data)
        {
            this.Deserialize(data);
        }

        public override void Serialize(BitBuffer data)
        {
            base.Serialize(data);

            data.AddInt(Planes.Count);
            foreach (var packet in Planes)
                packet.Serialize(data);
        }

        public override void Deserialize(BitBuffer data)
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

        public MissileListPacket() : base()
        {
            Type = PacketTypes.MissileUpdate;
        }

        public MissileListPacket(BitBuffer data)
        {
            this.Deserialize(data);
        }

        public override void Serialize(BitBuffer data)
        {
            base.Serialize(data);

            data.AddInt(Missiles.Count);
            foreach (var packet in Missiles)
                packet.Serialize(data);
        }

        public override void Deserialize(BitBuffer data)
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

        public NewPlayerPacket(FighterPlane plane) : base()
        {
            Type = PacketTypes.NewPlayer;
            Name = plane.PlayerName;
            PlaneColor = plane.PlaneColor;
            Position = plane.Position;
            ID = plane.ID;
        }

        public override void Serialize(BitBuffer data)
        {
            base.Serialize(data);

            data.AddString(Name);
            data.AddD2DColor(PlaneColor);
            data.AddD2DPoint(Position);
        }

        public override void Deserialize(BitBuffer data)
        {
            base.Deserialize(data);

            Name = data.ReadString();
            PlaneColor = data.ReadD2DColor();
            Position = data.ReadD2DPoint();
        }
    }

    public class PlayerListPacket : NetPacket
    {
        public List<NewPlayerPacket> Players = new List<NewPlayerPacket>();

        public PlayerListPacket(BitBuffer data)
        {
            this.Deserialize(data);
        }

        public PlayerListPacket(PacketTypes type, List<NewPlayerPacket> players) : base(type)
        {
            Players = players;
        }

        public override void Serialize(BitBuffer data)
        {
            base.Serialize(data);

            data.AddInt(Players.Count);
            foreach (var packet in Players)
                packet.Serialize(data);
        }

        public override void Deserialize(BitBuffer data)
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

        public GameObjectPacket() : base() { }

        public GameObjectPacket(BitBuffer data)
        {
            this.Deserialize(data);
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
                throw new InvalidOperationException($"Object ID [{obj.ID}] does not match this packet ID [{this.ID}]");

            //obj.Position = this.Position.ToD2DPoint();
            //obj.Velocity = this.Velocity.ToD2DPoint();
            //obj.Rotation = this.Rotation;
        }

        public override void Serialize(BitBuffer data)
        {
            base.Serialize(data);

            data.AddGameID(OwnerID);
            data.AddD2DPoint(Position);
            data.AddD2DPoint(Velocity, World.VeloBounds);
            data.AddFloat(Rotation);
        }

        public override void Deserialize(BitBuffer data)
        {
            base.Deserialize(data);

            OwnerID = data.ReadGameID();
            Position = data.ReadD2DPoint();
            Velocity = data.ReadD2DPoint(World.VeloBounds);
            Rotation = data.ReadFloat();
        }
    }

    public class GameObjectListPacket : BasicPacket
    {
        public List<GameObjectPacket> Packets = new List<GameObjectPacket>();

        public GameObjectListPacket(PacketTypes type) : base(type) { }

        public GameObjectListPacket() : base() { }

        public GameObjectListPacket(List<GameObjectPacket> packets)
        {
            Packets = packets;
        }

        public GameObjectListPacket(BitBuffer data)
        {
            this.Deserialize(data);
        }

        public override void Serialize(BitBuffer data)
        {
            base.Serialize(data);

            data.AddInt(Packets.Count);
            foreach (var packet in Packets)
                packet.Serialize(data);
        }

        public override void Deserialize(BitBuffer data)
        {
            base.Deserialize(data);

            var len = data.ReadInt();
            for (int i = 0; i < len; i++)
                Packets.Add(new GameObjectPacket(data));
        }
    }


    public class PlanePacket : GameObjectPacket
    {
        public float Deflection;
        public bool IsDisabled;
        public bool FiringBurst;
        public bool ThrustOn;
        public bool IsFlipped;
        public float Health; // TODO: Maybe send these stats periodically instead of on every frame.
        public int Score;
        public int Deaths;

        public PlanePacket(BitBuffer data)
        {
            this.Deserialize(data);
        }

        public PlanePacket(FighterPlane obj) : base(obj)
        {
            Deflection = obj.Deflection;
            IsDisabled = obj.IsDisabled;
            Health = obj.Health;
            FiringBurst = obj.FiringBurst;
            ThrustOn = obj.ThrustOn;
            IsFlipped = obj.Polygon.IsFlipped;
            Score = obj.Kills;
            Deaths = obj.Deaths;
        }

        public PlanePacket(FighterPlane obj, PacketTypes type) : base(obj, type)
        {
            Deflection = obj.Deflection;
            IsDisabled = obj.IsDisabled;
            Health = obj.Health;
            FiringBurst = obj.FiringBurst;
            ThrustOn = obj.ThrustOn;
            IsFlipped = obj.Polygon.IsFlipped;
            Score = obj.Kills;
            Deaths = obj.Deaths;
        }

        public virtual void SyncObj(FighterPlane obj)
        {
            base.SyncObj(obj);
            obj.Deflection = Deflection;
            obj.IsDisabled = IsDisabled;
            obj.FiringBurst = FiringBurst;
            obj.ThrustOn = ThrustOn;

            if (obj.Polygon.IsFlipped != IsFlipped)
                obj.FlipPoly(force: true);

            obj.Health = Health;
            obj.Kills = Score;
            obj.Deaths = Deaths;
        }

        public override void Serialize(BitBuffer data)
        {
            base.Serialize(data);

            data.AddFloat(Deflection);
            data.AddBool(IsDisabled);
            data.AddBool(FiringBurst);
            data.AddBool(ThrustOn);
            data.AddBool(IsFlipped);
            data.AddFloat(Health);
            data.AddInt(Score);
            data.AddInt(Deaths);
        }

        public override void Deserialize(BitBuffer data)
        {
            base.Deserialize(data);

            Deflection = data.ReadFloat();
            IsDisabled = data.ReadBool();
            FiringBurst = data.ReadBool();
            ThrustOn = data.ReadBool();
            IsFlipped = data.ReadBool();
            Health = data.ReadFloat();
            Score = data.ReadInt();
            Deaths = data.ReadInt();
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

        public MissilePacket(GuidedMissile obj, PacketTypes type) : base(obj, type)
        {
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

            data.AddFloat(Deflection);
            data.AddBool(FlameOn);
            data.AddGameID(TargetID);
        }

        public override void Deserialize(BitBuffer data)
        {
            base.Deserialize(data);

            Deflection = data.ReadFloat();
            FlameOn = data.ReadBool();
            TargetID = data.ReadGameID();
        }
    }

    public class ImpactPacket : GameObjectPacket
    {
        public GameID ImpactorID;
        public D2DPoint ImpactPoint;
        public ImpactType ImpactType;
        public float ImpactAngle;
        public float DamageAmount;
        public bool WasHeadshot;

        public ImpactPacket(BitBuffer data)
        {
            this.Deserialize(data);
        }

        public ImpactPacket(GameObject targetObj, GameID impactorID, D2DPoint point, float angle, float damageAmt, bool wasHeadshot, ImpactType impactType) : base(targetObj)
        {
            Type = PacketTypes.Impact;
            ImpactorID = impactorID;
            ImpactPoint = point;
            ImpactAngle = angle;
            WasHeadshot = wasHeadshot;
            DamageAmount = damageAmt;
            ImpactType = impactType;
        }

        public override void Serialize(BitBuffer data)
        {
            base.Serialize(data);

            data.AddGameID(ImpactorID);
            data.AddD2DPoint(ImpactPoint);
            data.AddByte((byte)ImpactType);
            data.AddFloat(ImpactAngle);
            data.AddFloat(DamageAmount);
            data.AddBool(WasHeadshot);
        }

        public override void Deserialize(BitBuffer data)
        {
            base.Deserialize(data);

            ImpactorID = data.ReadGameID();
            ImpactPoint = data.ReadD2DPoint();
            ImpactType = (ImpactType)data.ReadByte();
            ImpactAngle = data.ReadFloat();
            DamageAmount = data.ReadFloat();
            WasHeadshot = data.ReadBool();
        }
    }

    public class ImpactListPacket : GameObjectPacket
    {
        public List<ImpactPacket> Impacts = new List<ImpactPacket>();

        public ImpactListPacket() : base()
        {
            Type = PacketTypes.ImpactList;
        }

        public ImpactListPacket(BitBuffer data)
        {
            this.Deserialize(data);
        }

        public override void Serialize(BitBuffer data)
        {
            base.Serialize(data);

            data.AddInt(Impacts.Count);
            foreach (var packet in Impacts)
                packet.Serialize(data);
        }

        public override void Deserialize(BitBuffer data)
        {
            base.Deserialize(data);

            var len = data.ReadInt();
            for (int i = 0; i < len; i++)
                Impacts.Add(new ImpactPacket(data));
        }
    }
}