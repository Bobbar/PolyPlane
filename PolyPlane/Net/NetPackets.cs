using NetStack.Serialization;
using PolyPlane.GameObjects;
using PolyPlane.GameObjects.Managers;
using unvell.D2DLib;

namespace PolyPlane.Net
{
    public abstract class NetPacket
    {
        public PacketTypes Type;
        public SendType SendType;
        public GameID ID;
        public long FrameTime;
        public uint PeerID = uint.MaxValue;
        public int NumTimesDeferred = 0;

        /// <summary>
        /// Milliseconds elapsed between now and when this packet was created.
        /// </summary>
        public double Age
        {
            get
            {
                var now = World.CurrentNetTimeTicks();
                var age = TimeSpan.FromTicks(now - FrameTime).TotalMilliseconds;
                return age;
            }
        }

        /// <summary>
        /// Number of frames elapsed since this packet was created.
        /// </summary>
        public float LagFrames
        {
            get
            {
                var frames = (float)this.Age / (float)World.LastFrameTimeMs;
                return frames;
            }
        }

        // Get number of bits needed to store common enum types.
        public static readonly int NumBitsPacketType = Serialization.NumBitsEnum<PacketTypes>();
        public static readonly int NumBitsSendType = Serialization.NumBitsEnum<SendType>();
        public static readonly int NumBitsImpactType = Serialization.NumBitsEnum<ImpactType>();

        public NetPacket()
        {
            FrameTime = World.CurrentNetTimeTicks();
            SendType = SendType.ToAll;
        }

        public NetPacket(PacketTypes type) : this()
        {
            Type = type;
        }

        public NetPacket(PacketTypes type, SendType sendType) : this()
        {
            Type = type;
            SendType = sendType;
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
            data.Add(NumBitsPacketType, (uint)Type);
            data.Add(NumBitsSendType, (uint)SendType);
            data.AddGameID(ID);
            data.AddLong(FrameTime);
            data.AddUInt(PeerID);
        }

        public virtual void Deserialize(BitBuffer data)
        {
            Type = (PacketTypes)data.Read(NumBitsPacketType);
            SendType = (SendType)data.Read(NumBitsSendType);
            ID = data.ReadGameID();
            FrameTime = data.ReadLong();
            PeerID = data.ReadUInt();
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

        public ChatPacket(string message, string playerName) : base(PacketTypes.ChatMessage, SendType.ToAll)
        {
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

        public PlayerEventPacket(string message) : base(PacketTypes.PlayerEvent, SendType.ToAll)
        {
            Message = message;
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

        public DiscoveryPacket(string ip, string name, int port) : base(PacketTypes.Discovery, SendType.ToAll)
        {
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
        public long ClientTime;
        public long ServerTime;

        public SyncPacket(BitBuffer data)
        {
            this.Deserialize(data);
        }

        public SyncPacket(long clientTime, bool isResponse) : base()
        {
            ClientTime = clientTime;

            if (isResponse)
                Type = PacketTypes.SyncResponse;
            else
                Type = PacketTypes.SyncRequest;

            SendType = SendType.ToOnly;
        }


        public override void Serialize(BitBuffer data)
        {
            base.Serialize(data);

            data.AddLong(ClientTime);
            data.AddLong(ServerTime);
        }

        public override void Deserialize(BitBuffer data)
        {
            base.Deserialize(data);

            ClientTime = data.ReadLong();
            ServerTime = data.ReadLong();
        }
    }


    public class GameStatePacket : NetPacket
    {
        public float TimeOfDay;
        public float TimeOfDayDir;
        public bool GunsOnly;
        public bool IsPaused;
        public float GameSpeed;

        public GameStatePacket(BitBuffer data)
        {
            this.Deserialize(data);
        }

        public GameStatePacket(float timeOfDay, float timeOfDayDir, bool gunsOnly, bool isPaused, float gameSpeed) : base(PacketTypes.GameStateUpdate, SendType.ToAll)
        {
            TimeOfDay = timeOfDay;
            TimeOfDayDir = timeOfDayDir;
            GunsOnly = gunsOnly;
            IsPaused = isPaused;
            GameSpeed = gameSpeed;
        }

        public override void Serialize(BitBuffer data)
        {
            base.Serialize(data);

            data.AddFloat(TimeOfDay);
            data.AddFloat(TimeOfDayDir);
            data.AddBool(GunsOnly);
            data.AddBool(IsPaused);
            data.AddFloat(GameSpeed, Ranges.GameSpeedBounds);
        }

        public override void Deserialize(BitBuffer data)
        {
            base.Deserialize(data);

            TimeOfDay = data.ReadFloat();
            TimeOfDayDir = data.ReadFloat();
            GunsOnly = data.ReadBool();
            IsPaused = data.ReadBool();
            GameSpeed = data.ReadFloat(Ranges.GameSpeedBounds);
        }
    }


    public class BasicPacket : NetPacket
    {
        public D2DPoint Position;


        public BasicPacket(BitBuffer data)
        {
            this.Deserialize(data);
        }

        public BasicPacket(PacketTypes type, GameID id) : base(type, id) { }

        public BasicPacket(PacketTypes type, GameID id, D2DPoint position) : base(type, id)
        {
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

        public PlaneListPacket() : base(PacketTypes.PlaneListUpdate) { }

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

        public MissileListPacket(PacketTypes type) : base(type) { }

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

        public NewPlayerPacket(FighterPlane plane) : base(PacketTypes.NewPlayer)
        {
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

        public PlayerListPacket(PacketTypes type) : base(type) { }

        public PlayerListPacket(BitBuffer data)
        {
            this.Deserialize(data);
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

    public class PlayerScoredPacket : NetPacket
    {
        public GameID VictimID;
        public ImpactType ImpactType;
        public int Score;
        public int Deaths;
        public bool WasHeadshot => (ImpactType & ImpactType.Headshot) == ImpactType.Headshot;

        public PlayerScoredPacket(GameID scoringPlaneID, GameID victimPlaneID, int score, int deaths, ImpactType impactType) : base(PacketTypes.ScoreEvent, scoringPlaneID)
        {
            VictimID = victimPlaneID;
            ImpactType = impactType;
            Score = score;
            Deaths = deaths;
        }

        public PlayerScoredPacket(BitBuffer data)
        {
            this.Deserialize(data);
        }

        public override void Serialize(BitBuffer data)
        {
            base.Serialize(data);

            data.AddGameID(VictimID);
            data.Add(NumBitsImpactType, (uint)ImpactType);
            data.AddInt(Score);
            data.AddInt(Deaths);
        }

        public override void Deserialize(BitBuffer data)
        {
            base.Deserialize(data);

            VictimID = data.ReadGameID();
            ImpactType = (ImpactType)data.Read(NumBitsImpactType);
            Score = data.ReadInt();
            Deaths = data.ReadInt();
        }
    }

    public class PlaneStatusPacket : NetPacket
    {
        public bool IsDisabled;
        public float Health;
        public int Score;
        public int Deaths;

        public PlaneStatusPacket(FighterPlane plane) : base(PacketTypes.PlaneStatus, plane.ID)
        {
            IsDisabled = plane.IsDisabled;
            Health = plane.Health;
            Score = plane.Kills;
            Deaths = plane.Deaths;
        }

        public PlaneStatusPacket(BitBuffer data)
        {
            this.Deserialize(data);
        }

        public override void Serialize(BitBuffer data)
        {
            base.Serialize(data);

            data.AddBool(IsDisabled);
            data.AddFloat(Health, Ranges.HealthBounds);
            data.AddInt(Score);
            data.AddInt(Deaths);
        }

        public override void Deserialize(BitBuffer data)
        {
            base.Deserialize(data);

            IsDisabled = data.ReadBool();
            Health = data.ReadFloat(Ranges.HealthBounds);
            Score = data.ReadInt();
            Deaths = data.ReadInt();
        }
    }

    public class PlaneStatusListPacket : NetPacket
    {
        public List<PlaneStatusPacket> Planes = new List<PlaneStatusPacket>();

        public PlaneStatusListPacket() : base(PacketTypes.PlaneStatusList) { }

        public PlaneStatusListPacket(BitBuffer data)
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
                Planes.Add(new PlaneStatusPacket(data));
        }
    }

    public class GameObjectPacket : NetPacket
    {
        public GameID OwnerID;
        public D2DPoint Position;
        public D2DPoint Velocity;
        public float Rotation;
        public float RotationSpeed;

        public GameObjectPacket(PacketTypes type) : base(type) { }

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
            RotationSpeed = obj.RotationSpeed;
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
            RotationSpeed = obj.RotationSpeed;
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
            data.AddD2DPoint(Position, Ranges.WorldBounds);
            data.AddD2DPoint(Velocity, Ranges.VeloBounds);
            data.AddFloat(Rotation, Ranges.AngleBounds);
            data.AddFloat(RotationSpeed, Ranges.RotationSpeedBounds);
        }

        public override void Deserialize(BitBuffer data)
        {
            base.Deserialize(data);

            OwnerID = data.ReadGameID();
            Position = data.ReadD2DPoint(Ranges.WorldBounds);
            Velocity = data.ReadD2DPoint(Ranges.VeloBounds);
            Rotation = data.ReadFloat(Ranges.AngleBounds);
            RotationSpeed = data.ReadFloat(Ranges.RotationSpeedBounds);
        }
    }

    public class GameObjectListPacket : NetPacket
    {
        public List<GameObjectPacket> Packets = new List<GameObjectPacket>();

        public GameObjectListPacket(PacketTypes type) : base(type) { }

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

    public class ImpactListPacket : NetPacket
    {
        public List<ImpactPacket> Impacts = new List<ImpactPacket>();

        public ImpactListPacket(GameID destID) : base(PacketTypes.ImpactList, destID) { }

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

    public class PlanePacket : GameObjectPacket
    {
        public float Deflection;
        public bool FiringBurst;
        public float ThrustAmt;
        public bool IsFlipped;
        public int NumMissiles;
        public int NumBullets;
        public int NumDecoys;

        // Get number of bits needed to store loadout stats.
        private static readonly int NumBitsMissiles = Serialization.NumBits(FighterPlane.MAX_MISSILES);
        private static readonly int NumBitsBullets = Serialization.NumBits(FighterPlane.MAX_BULLETS);
        private static readonly int NumBitsDecoys = Serialization.NumBits(FighterPlane.MAX_DECOYS);

        public PlanePacket(BitBuffer data)
        {
            this.Deserialize(data);
        }

        public PlanePacket(FighterPlane plane) : base(plane)
        {
            Deflection = plane.Deflection;
            FiringBurst = plane.FiringBurst;
            ThrustAmt = plane.ThrustAmount;
            IsFlipped = plane.Polygon.IsFlipped;
            NumMissiles = plane.NumMissiles;
            NumBullets = plane.NumBullets;
            NumDecoys = plane.NumDecoys;
        }

        public PlanePacket(FighterPlane plane, PacketTypes type) : base(plane, type)
        {
            Deflection = plane.Deflection;
            FiringBurst = plane.FiringBurst;
            ThrustAmt = plane.ThrustAmount;
            IsFlipped = plane.Polygon.IsFlipped;
            NumMissiles = plane.NumMissiles;
            NumBullets = plane.NumBullets;
            NumDecoys = plane.NumDecoys;
        }

        public virtual void SyncObj(FighterPlane plane)
        {
            base.SyncObj(plane);
            plane.Deflection = Deflection;
            plane.FiringBurst = FiringBurst;
            plane.ThrustAmount = ThrustAmt;
            plane.NumMissiles = NumMissiles;
            plane.NumBullets = NumBullets;
            plane.NumDecoys = NumDecoys;

            if (plane.Polygon.IsFlipped != IsFlipped)
                plane.FlipPoly(force: true);
        }

        public override void Serialize(BitBuffer data)
        {
            base.Serialize(data);

            data.AddFloat(Deflection, Ranges.DeflectionBounds);
            data.AddBool(FiringBurst);
            data.AddFloat(ThrustAmt, Ranges.ZeroToOneBounds);
            data.AddBool(IsFlipped);
            data.Add(NumBitsMissiles, (uint)NumMissiles);
            data.Add(NumBitsBullets, (uint)NumBullets);
            data.Add(NumBitsDecoys, (uint)NumDecoys);
        }

        public override void Deserialize(BitBuffer data)
        {
            base.Deserialize(data);

            Deflection = data.ReadFloat(Ranges.DeflectionBounds);
            FiringBurst = data.ReadBool();
            ThrustAmt = data.ReadFloat(Ranges.ZeroToOneBounds);
            IsFlipped = data.ReadBool();
            NumMissiles = (int)data.Read(NumBitsMissiles);
            NumBullets = (int)data.Read(NumBitsBullets);
            NumDecoys = (int)data.Read(NumBitsDecoys);
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

            data.AddFloat(Deflection, Ranges.DeflectionBounds);
            data.AddBool(FlameOn);
            data.AddGameID(TargetID);
        }

        public override void Deserialize(BitBuffer data)
        {
            base.Deserialize(data);

            Deflection = data.ReadFloat(Ranges.DeflectionBounds);
            FlameOn = data.ReadBool();
            TargetID = data.ReadGameID();
        }
    }

    public class ImpactPacket : GameObjectPacket
    {
        public GameID ImpactorID;
        public D2DPoint ImpactPoint;
        /// <summary>
        /// Scaled impact point. For bullet hole location.
        /// </summary>
        public D2DPoint ImpactPointOrigin;
        public ImpactType ImpactType;
        public float ImpactAngle;
        public float DamageAmount;
        public float NewHealth;
        public bool WasHeadshot => (ImpactType & ImpactType.Headshot) == ImpactType.Headshot;
        public bool WasFlipped;

        public ImpactPacket(BitBuffer data)
        {
            this.Deserialize(data);
        }

        public ImpactPacket(GameObject targetObj, GameID impactorID, PlaneImpactResult result) : base(targetObj)
        {
            Type = PacketTypes.Impact;
            ImpactorID = impactorID;
            ImpactPoint = result.ImpactPoint;
            ImpactPointOrigin = result.ImpactPointOrigin;
            ImpactAngle = result.ImpactAngle;
            WasFlipped = result.WasFlipped;
            DamageAmount = result.DamageAmount;
            NewHealth = result.NewHealth;
            ImpactType = result.ImpactType;
        }

        public override void Serialize(BitBuffer data)
        {
            base.Serialize(data);

            data.AddGameID(ImpactorID);
            data.AddD2DPoint(ImpactPoint);
            data.AddD2DPoint(ImpactPointOrigin, Ranges.OriginBounds);
            data.Add(NumBitsImpactType, (uint)ImpactType);
            data.AddFloat(ImpactAngle, Ranges.AngleBounds);
            data.AddFloat(DamageAmount);
            data.AddFloat(NewHealth, Ranges.HealthBounds);
            data.AddBool(WasFlipped);
        }

        public override void Deserialize(BitBuffer data)
        {
            base.Deserialize(data);

            ImpactorID = data.ReadGameID();
            ImpactPoint = data.ReadD2DPoint();
            ImpactPointOrigin = data.ReadD2DPoint(Ranges.OriginBounds);
            ImpactType = (ImpactType)data.Read(NumBitsImpactType);
            ImpactAngle = data.ReadFloat(Ranges.AngleBounds);
            DamageAmount = data.ReadFloat();
            NewHealth = data.ReadFloat(Ranges.HealthBounds);
            WasFlipped = data.ReadBool();
        }
    }
}