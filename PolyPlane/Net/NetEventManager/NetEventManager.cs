﻿using PolyPlane.GameObjects;
using PolyPlane.GameObjects.Interfaces;
using PolyPlane.GameObjects.Managers;
using PolyPlane.Helpers;
using PolyPlane.Net.NetHost;
using PolyPlane.Rendering;

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
        public int NumHandledPackets = 0;

        private GameObjectManager _objs = World.ObjectManager;
        private SmoothDouble _packetDelayAvg = new SmoothDouble(100);
        private SmoothDouble _offsetDeltaSmooth = new SmoothDouble(10);
        private SmoothDouble _serverTimeOffsetSmooth = new SmoothDouble(10);
        private SmoothDouble _thetaSmooth = new SmoothDouble(10);

        private Dictionary<int, List<ImpactPacket>> _impacts = new Dictionary<int, List<ImpactPacket>>();
        private List<NetPacket> _deferredPackets = new List<NetPacket>();
        private HashSet<uint> _peersNeedingSync = new HashSet<uint>();

        private bool _netIDIsSet = false;
        private bool _receivedFirstSync = false;
        private bool _initialOffsetSet = false;
        private bool _syncingServerTime = true;
        private int _syncCount = 0;
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
        private const int SYNC_MAX_ATTEMPTS = 40; // Max number of attempts to find true server time.
        private const float SYNC_REFRESH_INTERVAL = 15000f; // Interval in milliseconds to start fresh sync cycle.
        private const float SYNC_RESPONSE_INTERVAL = 100f; // Interval in milliseconds to send sync packets while in a sync cycle.
        private const float SYNC_GAMESTATE_INTERVAL = 100f; // Interval in milliseconds for server to send game state packets.
        private const float SYNC_BAD_PEERS_INTERVAL = 300f; // Interval in milliseconds for server initiate sync cycles for out-of-sync peers.
        private const float SYNC_MAX_DELTA = 0.5f; // Server time offset delta must be less than this to accept the current offset.

        private ActionScheduler _netScheduler = new ActionScheduler();

        public NetEventManager(NetPlayHost host, FighterPlane playerPlane)
        {
            Host = host;
            PlayerPlane = playerPlane;
            IsServer = false;
            ChatInterface = new ChatInterface(this, playerPlane.PlayerName);
            AttachEvents();

            ScheduleUpdates();
            ScheduleClientEvents();

            _lastNetTime = World.CurrentTimeTicks();
        }

        public NetEventManager(NetPlayHost host)
        {
            Host = host;
            PlayerPlane = null;
            IsServer = true;
            ChatInterface = new ChatInterface(this, "Server");
            AttachEvents();

            ScheduleUpdates();
            ScheduleServerEvents();

            _lastNetTime = World.CurrentTimeTicks();
        }

        private void ScheduleUpdates()
        {
            // Schedule regular updates for clients and server.
            _netScheduler.AddAction(World.TARGET_FRAME_TIME_NET, () =>
            {
                SendPlaneUpdates();
                SendMissileUpdates();
                SendExpiredObjects();
            });
        }

        private void ScheduleClientEvents()
        {
            // Start new sync cycle.
            _netScheduler.AddAction(SYNC_REFRESH_INTERVAL, () => _syncingServerTime = true);

            // Send sync requests while in a cycle.
            _netScheduler.AddAction(SYNC_RESPONSE_INTERVAL, () =>
            {
                if (_syncingServerTime)
                    ClientSendSyncRequest();
            });
        }

        private void ScheduleServerEvents()
        {
            // Send current game state to peers.
            _netScheduler.AddAction(SYNC_GAMESTATE_INTERVAL, ServerSendGameState);

            // Send sync requests to out-of-sync peers.
            _netScheduler.AddAction(SYNC_BAD_PEERS_INTERVAL, ServerSendPeerSyncRequests);
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
            // Do scheduled net actions.
            _netScheduler.DoActions();

            long totalPacketTime = 0;
            int numPackets = 0;

            var netNow = World.CurrentNetTimeTicks();

            while (Host.PacketReceiveQueue.Count > 0)
            {
                if (Host.PacketReceiveQueue.TryDequeue(out NetPacket netPacket))
                {
                    var delay = netNow - netPacket.FrameTime;
                    totalPacketTime += delay;
                    numPackets++;

                    // Record IDs for peers who are sending packets from the future.
                    // Sync requests will be sent later.
                    if (IsServer)
                    {
                        if (delay < 0)
                            _peersNeedingSync.Add(netPacket.PeerID);
                    }

                    HandleNetPacket(netPacket, dt);
                }
            }

            // Try to handle deferred packets.
            HandleDeferredPackets(dt);

            if (totalPacketTime > 0f && numPackets > 0)
            {
                var avgDelay = TimeSpan.FromTicks((long)(totalPacketTime / (float)numPackets)).TotalMilliseconds;
                PacketDelay = _packetDelayAvg.Add(avgDelay);
            }
        }

        /// <summary>
        /// Attempt to handle deferred packets.
        /// </summary>
        /// <param name="dt"></param>
        private void HandleDeferredPackets(float dt)
        {
            if (_deferredPackets.Count == 0)
                return;

            var deferredCopy = new List<NetPacket>(_deferredPackets);
            _deferredPackets.Clear();

            while (deferredCopy.Count > 0)
            {
                HandleNetPacket(deferredCopy.First(), dt);
                deferredCopy.RemoveAt(0);
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
        /// Queues the specified packet to make an attempt at handling it later.
        /// 
        /// For situations where we can't handle the packet due to other requisite packets which haven't yet arrived.
        /// </summary>
        /// <param name="packet"></param>
        private void DeferPacket(NetPacket packet)
        {
            if (packet.Age < MAX_DEFER_AGE)
            {
                // Increment deferred count for unseen packets.
                if (packet.NumTimesDeferred == 0)
                    NumDeferredPackets++;

                packet.NumTimesDeferred++;
                _deferredPackets.Add(packet);
            }
            else
            {
                NumExpiredPackets++;
                //Debug.WriteLine($"Can't defer, too old!  ID: {packet.ID}  Type: {packet.Type}   Age: {packet.Age}");
            }
        }

        /// <summary>
        /// Increments the counter for successfully deferred packets as needed.
        /// </summary>
        /// <param name="packet"></param>
        private void RecordDeferSuccess(NetPacket packet)
        {
            if (packet.NumTimesDeferred > 0)
                NumHandledPackets++;
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

        public void ClearImpacts(int playerID)
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
                            var newPlane = new FighterPlane(playerPacket.Position, playerPacket.PlaneColor, playerPacket.ID);
                            newPlane.PlayerName = playerPacket.Name;
                            newPlane.IsNetObject = true;
                            newPlane.PlayerKilledCallback += HandlePlayerKilled;

                            _objs.AddPlane(newPlane);
                        }

                        // Begin streaming out the current game state to the new player.
                        ServerSendGameState();
                        ServerSendOtherPlanes();
                        ServerSendExistingBullets(playerPacket.ID);
                        ServerSendExistingMissiles(playerPacket.ID);
                        ServerSendExistingDecoys(playerPacket.ID);
                        ServerSendExistingImpacts(playerPacket.ID);
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
                    HandleNewDecoy(newDecoyPacket, dt);

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

                        ClientSendSyncRequest();
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
                        if (IsServer)
                        {
                            // Get a spawn point and send it back to the clients.
                            var spawnPos = Utilities.FindSafeSpawnPoint();
                            resetPack.Position = spawnPos;
                            Host.EnqueuePacket(resetPack);

                            // Respawn the plane locally to make sure it is occupying the spawn point.
                            // Prevents the spawn point from being given to another player before the client moves its plane there.
                            resetPlane.RespawnPlane(spawnPos);
                        }
                        else
                        {
                            resetPlane.RespawnPlane(resetPack.Position);
                        }

                        ClearImpacts(resetPack.ID.PlayerID);
                        PlayerRespawned?.Invoke(this, resetPlane);
                    }

                    break;

                case PacketTypes.SyncRequest:
                    var syncRequestPacket = packet as SyncPacket;

                    if (IsServer)
                    {
                        // Respond back with the original client time attached.
                        ServerSendSyncResponse(syncRequestPacket);
                    }
                    else
                    {
                        // Server has requested that we run a sync.
                        _syncingServerTime = true;
                    }

                    break;

                case PacketTypes.SyncResponse:

                    var syncPack = packet as SyncPacket;
                    if (syncPack != null)
                    {
                        if (!IsServer)
                        {
                            ClientHandleSyncResponse(syncPack);
                        }
                    }
                    break;

                case PacketTypes.GameStateUpdate:
                    var gameStatePacket = packet as GameStatePacket;

                    if (!IsServer)
                    {
                        if (gameStatePacket != null)
                        {
                            World.TimeOfDay = gameStatePacket.TimeOfDay;
                            World.TimeOfDayDir = gameStatePacket.TimeOfDayDir;
                            World.GunsOnly = gameStatePacket.GunsOnly;
                            World.GameSpeed = gameStatePacket.GameSpeed;
                            World.IsPaused = gameStatePacket.IsPaused;
                        }
                    }

                    break;

                case PacketTypes.KickPlayer:

                    var kickPacket = packet as BasicPacket;

                    if (!IsServer)
                    {
                        if (kickPacket.ID.Equals(PlayerPlane.ID))
                        {
                            SendPlayerDisconnectPacket((uint)kickPacket.ID.PlayerID);
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
                                decoy.IsNetObject = true;
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
