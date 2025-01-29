using PolyPlane.GameObjects;
using PolyPlane.GameObjects.Interfaces;
using PolyPlane.GameObjects.Manager;
using PolyPlane.Helpers;
using PolyPlane.Net.NetHost;
using PolyPlane.Rendering;
using System.Diagnostics;

namespace PolyPlane.Net
{
    public partial class NetEventManager : IImpactEvent, IPlayerScoredEvent
    {
        public NetPlayHost Host;
        public ChatInterface ChatInterface;
        public bool IsServer = false;
        public FighterPlane PlayerPlane = null;
        public double PacketDelay = 0;
        public int NumDeferredPackets = 0;
        public int NumExpiredPackets = 0;

        private GameObjectManager _objs = World.ObjectManager;
        private SmoothDouble _packetDelayAvg = new SmoothDouble(100);
        private Dictionary<int, List<ImpactPacket>> _impacts = new Dictionary<int, List<ImpactPacket>>();
        private List<NetPacket> _deferredPackets = new List<NetPacket>();

        private bool _netIDIsSet = false;
        private bool _receivedFirstSync = false;
        private long _lastNetTime = 0;

        public event EventHandler<int> PlayerIDReceived;
        public event EventHandler<ImpactEvent> ImpactEvent;
        public event EventHandler<ChatPacket> NewChatMessage;
        public event EventHandler<int> PlayerKicked;
        public event EventHandler<int> PlayerDisconnected;
        public event EventHandler<int> PlayerJoined;
        public event EventHandler<FighterPlane> PlayerRespawned;
        public event EventHandler<string> PlayerEventMessage;
        public event EventHandler<PlayerScoredEventArgs> PlayerScoredEvent;

        private const long MAX_DEFER_AGE = 400; // Max age allowed for deferred packets.
        private const int LIST_PACKET_BATCH_COUNT = 30; // Max list packet count before batching into multiple packets.

        public NetEventManager(NetPlayHost host, FighterPlane playerPlane)
        {
            Host = host;
            PlayerPlane = playerPlane;
            IsServer = false;
            ChatInterface = new ChatInterface(this, playerPlane.PlayerName);
            AttachEvents();

            _lastNetTime = World.CurrentTimeTicks();
        }

        public NetEventManager(NetPlayHost host)
        {
            Host = host;
            PlayerPlane = null;
            IsServer = true;
            ChatInterface = new ChatInterface(this, "Server");
            AttachEvents();

            _lastNetTime = World.CurrentTimeTicks();
        }

        private void AttachEvents()
        {
            Host.PeerDisconnectedEvent += Host_PeerDisconnectedEvent;
        }

        private void Host_PeerDisconnectedEvent(object? sender, ENet.Peer e)
        {
            HandlePlayerDisconnected((int)e.ID);

            if (IsServer)
                ClearImpacts((int)e.ID);
        }

        public void HandleNetEvents(float dt)
        {
            var now = World.CurrentTimeTicks();
            var elap = TimeSpan.FromTicks(now - _lastNetTime).TotalMilliseconds;

            // Send updates at an interval approx twice the frame time.
            // (This will be approx 30 FPS when target FPS is set to 60)
            if (elap >= (World.TARGET_FRAME_TIME_NET))
            {
                SendPlaneUpdates();
                SendMissileUpdates();
                SendExpiredObjects();

                if (IsServer)
                    SendSyncPacket();

                _lastNetTime = World.CurrentTimeTicks();
            }

            long totalPacketTime = 0;
            int numPackets = 0;

            var netNow = World.CurrentNetTimeTicks();

            while (Host.PacketReceiveQueue.Count > 0)
            {
                if (Host.PacketReceiveQueue.TryDequeue(out NetPacket packet))
                {
                    var netPacket = packet;
                    totalPacketTime += netNow - netPacket.FrameTime;
                    numPackets++;

                    HandleNetPacket(netPacket, dt);
                }
            }

            // Enqueue deferred packets to be handled on the next frame.
            EnqueueDeferredPackets();

            if (totalPacketTime > 0f && numPackets > 0)
            {
                var avgDelay = TimeSpan.FromTicks((long)(totalPacketTime / (float)numPackets)).TotalMilliseconds;
                PacketDelay = _packetDelayAvg.Add(avgDelay);
            }
        }

        /// <summary>
        /// Adds deferred packets back into the receive queue in order to make another attempt at handling them.
        /// </summary>
        private void EnqueueDeferredPackets()
        {
            while (_deferredPackets.Count > 0)
            {
                Host.PacketReceiveQueue.Enqueue(_deferredPackets.First());
                _deferredPackets.RemoveAt(0);
            }
        }


        /// <summary>
        /// Remove deferred packets with IDs matching the specified expired ID.
        /// </summary>
        /// <param name="expiredID"></param>
        private void PruneExpiredDeferredPackets(GameID expiredID)
        {
            for (int i = _deferredPackets.Count - 1; i >= 0; i--)
            {
                var packet = _deferredPackets[i];

                if (packet.ID.Equals(expiredID))
                    _deferredPackets.RemoveAt(i);
            }
        }

        /// <summary>
        /// Queues the specified packet to be handled on the next frame.
        /// 
        /// For situations where we can't handle the packet due to other packets which haven't arrived.
        /// </summary>
        /// <param name="packet"></param>
        private void DeferPacket(NetPacket packet)
        {
            if (packet.Age < MAX_DEFER_AGE)
            {
                _deferredPackets.Add(packet);
                NumDeferredPackets++;
            }
            else
            {
                NumExpiredPackets++;
                //Debug.WriteLine($"Can't defer, too old!  ID: {packet.ID}  Type: {packet.Type}   Age: {packet.Age}");
            }
        }

        /// <summary>
        /// True if we are not the server and we have received the ID and the first sync packets from the server.
        /// </summary>
        /// <returns></returns>
        private bool ClientIsReady()
        {
            if (IsServer)
                return true;

            return !IsServer && _netIDIsSet && _receivedFirstSync;
        }

        private void SaveImpact(ImpactPacket impactPacket)
        {
            if (!this.IsServer)
                return;

            if (impactPacket.ImpactType == ImpactType.Splash)
                return;

            if (_impacts.TryGetValue(impactPacket.ID.PlayerID, out var impacts))
                impacts.Add(impactPacket);
            else
                _impacts.Add(impactPacket.ID.PlayerID, new List<ImpactPacket>() { impactPacket });
        }

        private void ClearImpacts(int playerID)
        {
            if (!this.IsServer)
                return;

            if (_impacts.ContainsKey(playerID))
                _impacts.Remove(playerID);
        }


        private void HandleNetPacket(NetPacket packet, float dt)
        {
            switch (packet.Type)
            {
                case PacketTypes.PlaneListUpdate:

                    var planeListPacket = packet as PlaneListPacket;
                    HandleNetPlaneUpdates(planeListPacket);

                    break;

                case PacketTypes.PlaneUpdate:

                    var planePacket = packet as PlanePacket;
                    HandleNetPlaneUpdate(planePacket);

                    break;

                case PacketTypes.MissileUpdateList:

                    var missileListPacket = packet as MissileListPacket;
                    HandleNetMissileUpdates(missileListPacket);

                    break;

                case PacketTypes.MissileUpdate:

                    var missilePacket = packet as MissilePacket;
                    HandleNetMissileUpdate(missilePacket);

                    break;

                case PacketTypes.Impact:

                    var impactPacket = packet as ImpactPacket;
                    HandleNetImpact(impactPacket, dt);

                    break;

                case PacketTypes.NewPlayer:

                    var playerPacket = packet as NewPlayerPacket;

                    if (IsServer)
                    {
                        if (playerPacket != null)
                        {
                            var newPlane = new FighterPlane(playerPacket.Position, playerPacket.PlaneColor, playerPacket.ID, isAI: false, isNetPlane: true);
                            newPlane.PlayerName = playerPacket.Name;
                            newPlane.IsNetObject = true;
                            newPlane.PlayerKilledCallback += HandlePlayerKilled;

                            _objs.AddPlane(newPlane);
                        }

                        // Begin streaming out the current game state to the new player.
                        ServerSendOtherPlanes();
                        ServerSendExistingBullets();
                        ServerSendExistingMissiles();
                        ServerSendExistingDecoys();
                        ServerSendExistingImpacts(playerPacket.ID);
                        SendSyncPacket();
                    }

                    PlayerJoined?.Invoke(this, playerPacket.ID.PlayerID);

                    break;

                case PacketTypes.NewBullet:

                    var bulletPacket = packet as GameObjectPacket;
                    HandleNewBullet(bulletPacket, dt);

                    break;

                case PacketTypes.NewMissile:

                    var newMissilePacket = packet as MissilePacket;
                    HandleNewMissile(newMissilePacket);

                    break;

                case PacketTypes.NewDecoy:

                    var newDecoyPacket = packet as GameObjectPacket;
                    HandleNewDecoy(newDecoyPacket);

                    break;

                case PacketTypes.SetID:

                    if (!IsServer)
                    {
                        var idPacket = packet as BasicPacket;

                        _netIDIsSet = true;

                        var newID = new GameID(idPacket.ID.PlayerID, PlayerPlane.ID.ObjectID);
                        _objs.ChangeObjID(PlayerPlane, newID);

                        PlayerPlane.SetPosition(idPacket.Position);

                        var newPlayerPacket = new NewPlayerPacket(PlayerPlane);
                        Host.EnqueuePacket(newPlayerPacket);

                        PlayerIDReceived?.Invoke(this, packet.ID.PlayerID);
                    }

                    break;

                case PacketTypes.ChatMessage:

                    var chatPacket = packet as ChatPacket;
                    NewChatMessage?.Invoke(this, chatPacket);

                    break;

                case PacketTypes.GetOtherPlanes:

                    if (!IsServer)
                    {
                        var listPacket = packet as PlayerListPacket;
                        HandleNewPlayers(listPacket);
                    }

                    break;

                case PacketTypes.ExpiredObjects:

                    var expiredPacket = packet as BasicListPacket;

                    HandleExpiredObjects(expiredPacket);

                    break;

                case PacketTypes.PlayerDisconnect:

                    var disconnectPack = packet as BasicPacket;
                    HandlePlayerDisconnected(disconnectPack.ID.PlayerID);
                    ClearImpacts(disconnectPack.ID.PlayerID);

                    break;

                case PacketTypes.PlayerReset:

                    var resetPack = packet as BasicPacket;

                    var resetPlane = _objs.GetObjectByID(resetPack.ID) as FighterPlane;

                    if (resetPlane != null)
                    {
                        ClearImpacts(resetPack.ID.PlayerID);
                        resetPlane.FixPlane();
                        PlayerRespawned?.Invoke(this, resetPlane);
                    }

                    break;

                case PacketTypes.ServerSync:

                    var syncPack = packet as SyncPacket;
                    if (syncPack != null)
                    {
                        if (!IsServer)
                        {
                            // Compute server time offset.
                            var now = World.CurrentTimeTicks();
                            var rtt = Host.GetPlayerRTT(PlayerPlane.PlayerID);
                            var rttTicks = TimeSpan.FromMilliseconds(rtt).Ticks;
                            var frameDelay = TimeSpan.FromMilliseconds(World.LAST_FRAME_TIME).Ticks;
                            var transitOffset = (rttTicks / 2d) + (frameDelay / 2d);
                            var serverOffset = (syncPack.ServerTime + transitOffset) - now;

                            World.ServerTimeOffset = serverOffset;

                            World.TimeOfDay = syncPack.TimeOfDay;
                            World.TimeOfDayDir = syncPack.TimeOfDayDir;
                            World.GunsOnly = syncPack.GunsOnly;
                            World.TargetDT = syncPack.DeltaTime;
                            World.IsPaused = syncPack.IsPaused;

                            _receivedFirstSync = true;
                        }
                    }
                    break;

                case PacketTypes.KickPlayer:

                    var kickPacket = packet as BasicPacket;

                    if (!IsServer)
                    {
                        if (kickPacket.ID.Equals(PlayerPlane.ID))
                        {
                            Host.SendPlayerDisconnectPacket((uint)kickPacket.ID.PlayerID);
                            Host.Disconnect(kickPacket.ID.PlayerID);
                        }
                    }

                    ClearImpacts(kickPacket.ID.PlayerID);
                    PlayerKicked?.Invoke(this, kickPacket.ID.PlayerID);

                    break;

                case PacketTypes.ImpactList:

                    if (!IsServer)
                    {
                        var impactsPacket = packet as ImpactListPacket;

                        if (impactsPacket != null)
                        {
                            HandleNewImpactList(impactsPacket, dt);
                        }
                    }
                    break;

                case PacketTypes.BulletList:

                    var bulletList = packet as GameObjectListPacket;

                    if (!IsServer)
                    {
                        bulletList.Packets.ForEach(b => HandleNewBullet(b, dt));
                    }

                    break;

                case PacketTypes.MissileList:

                    var missileList = packet as MissileListPacket;

                    if (!IsServer)
                    {
                        missileList.Missiles.ForEach(HandleNewMissile);
                    }

                    break;

                case PacketTypes.DecoyList:

                    var decoyList = packet as GameObjectListPacket;

                    if (!IsServer)
                    {
                        decoyList.Packets.ForEach(d =>
                        {
                            var owner = GetNetPlane(d.OwnerID);

                            if (owner != null)
                            {
                                var decoy = new Decoy(owner, d.Position, d.Velocity);
                                decoy.ID = d.ID;
                                _objs.EnqueueDecoy(decoy);
                            }
                        });
                    }

                    break;

                case PacketTypes.PlayerEvent:

                    var eventPacket = packet as PlayerEventPacket;

                    if (eventPacket != null)
                    {
                        PlayerEventMessage?.Invoke(this, eventPacket.Message);
                    }

                    break;

                case PacketTypes.ScoreEvent:

                    var scorePacket = packet as PlayerScoredPacket;
                    HandlePlayerScored(scorePacket);

                    break;

                case PacketTypes.PlaneStatusList:

                    var statusListPacket = packet as PlaneStatusListPacket;
                    HandlePlaneStatusListUpdate(statusListPacket);

                    break;

                case PacketTypes.PlaneStatus:

                    var statusPacket = packet as PlaneStatusPacket;
                    HandlePlaneStatusUpdate(statusPacket);

                    break;
            }
        }

        private FighterPlane GetNetPlane(GameID id, bool netOnly = true)
        {
            if (_objs.TryGetObjectByID(id, out GameObject obj))
            {
                if (obj != null && obj is FighterPlane plane)
                {
                    if (netOnly && !plane.IsNetObject)
                        return null;

                    return plane;
                }
            }

            return null;
        }
    }
}
