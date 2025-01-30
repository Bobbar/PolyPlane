using ENet;
using PolyPlane.GameObjects;

namespace PolyPlane.Net.NetHost
{
    public abstract class NetPlayHost : IDisposable
    {
        public event EventHandler<Peer> PeerTimeoutEvent;
        public event EventHandler<Peer> PeerDisconnectedEvent;

        public RingBuffer<NetPacket> PacketSendQueue = new RingBuffer<NetPacket>(512);
        public RingBuffer<NetPacket> PacketReceiveQueue = new RingBuffer<NetPacket>(512);

        public Host Host;
        public readonly ushort Port;
        public readonly Address Address;

        protected const int MAX_CLIENTS = 30;
        protected const int MAX_CHANNELS = 8;
        protected const int TIMEOUT = 0;
        protected const int POLL_FPS = 1000;

        private Thread _pollThread;
        private FPSLimiter _pollLimiter = new FPSLimiter();
        private bool _runLoop = true;

        public NetPlayHost(ushort port, string ip)
        {
            Port = port;

            Address = new Address();
            Address.Port = port;
            Address.SetIP(ip);
        }

        public void Start()
        {
            Library.Initialize();

            DoStart();

            _pollThread = new Thread(PollLoop);
            _pollThread.IsBackground = true;
            _pollThread.Start();
        }

        public void Stop()
        {
            _runLoop = false;

            DoStop();
        }

        public virtual void DoStop()
        { }

        public virtual void DoStart()
        { }


        public virtual void HandleConnect(ref Event netEvent)
        { }

        public virtual void HandleDisconnect(ref Event netEvent)
        {
            PeerDisconnectedEvent?.Invoke(this, netEvent.Peer);
        }

        public virtual void HandleTimeout(ref Event netEvent)
        { }

        public virtual void HandleReceive(NetPacket netPacket)
        {
            PacketReceiveQueue.Enqueue(netPacket);
        }

        public virtual void EnqueuePacket(NetPacket packet)
        {
            PacketSendQueue.Enqueue(packet);
        }

        public void SendPlayerDisconnectPacket(uint playerID)
        {
            var packet = new BasicPacket(PacketTypes.PlayerDisconnect, new GameID(playerID));
            EnqueuePacket(packet);
            Host.Flush();
        }

        public abstract ulong PacketLoss();
        protected abstract void SendPacket(NetPacket netPacket);
        public abstract Peer? GetPeer(int playerID);
        public abstract void Disconnect(int playerID);
        public abstract double GetPlayerRTT(int playerID);

        internal void PollLoop()
        {
            Event netEvent;

            while (_runLoop)
            {
                ProcessSendQueue();

                while (_runLoop && Host.Service(TIMEOUT, out netEvent) > 0)
                    HandleEvent(ref netEvent);

                _pollLimiter.Wait(POLL_FPS);
            }
        }

        internal void HandleEvent(ref Event netEvent)
        {
            switch (netEvent.Type)
            {
                case EventType.None:
                    break;

                case EventType.Connect:

                    HandleConnect(ref netEvent);

                    break;

                case EventType.Disconnect:

                    HandleDisconnect(ref netEvent);

                    break;

                case EventType.Timeout:

                    HandleTimeout(ref netEvent);
                    PeerTimeoutEvent?.Invoke(this, netEvent.Peer);

                    break;

                case EventType.Receive:

                    var packet = netEvent.Packet;

                    if (TryParsePacket(ref packet, out NetPacket netPacket))
                    {
                        HandleReceive(netPacket);
                    }

                    packet.Dispose();

                    break;
            }
        }

        private void ProcessSendQueue()
        {
            while (PacketSendQueue.Count > 0)
            {
                if (PacketSendQueue.TryDequeue(out NetPacket packet))
                {
                    SendPacket(packet);
                }
            }
        }

        private bool TryParsePacket(ref Packet packet, out NetPacket netPacket)
        {
            try
            {
                var buffer = new byte[packet.Length];

                packet.CopyTo(buffer);

                var packetObj = Serialization.ByteArrayToObject(buffer) as NetPacket;

                netPacket = packetObj;
                return true;
            }
            catch
            {
                netPacket = null;
                return false;
            }
        }

        /// <summary>
        /// Get the send strategy for this packet type.
        /// </summary>
        /// <param name="packet"></param>
        /// <returns></returns>
        protected SendType GetSendType(NetPacket packet)
        {
            switch (packet.Type)
            {
                case PacketTypes.PlaneUpdate
                or PacketTypes.MissileUpdateList
                or PacketTypes.MissileUpdate
                or PacketTypes.NewBullet
                or PacketTypes.NewDecoy
                or PacketTypes.NewMissile:

                    return SendType.ToAllExcept;

                case PacketTypes.SyncRequest
                or PacketTypes.SyncResponse
                or PacketTypes.ImpactList:

                    return SendType.ToOnly;

                default:

                    return SendType.ToAll;
            }
        }

        protected byte GetChannel(NetPacket netpacket)
        {
            switch (netpacket.Type)
            {
                case PacketTypes.PlaneUpdate or PacketTypes.PlaneListUpdate or PacketTypes.PlaneStatus or PacketTypes.PlaneStatusList:
                    return 0;

                case PacketTypes.NewMissile or PacketTypes.MissileUpdateList or PacketTypes.MissileUpdate:
                    return 1;

                case PacketTypes.NewBullet:
                    return 2;

                case PacketTypes.NewDecoy:
                    return 3;

                case PacketTypes.ExpiredObjects:
                    return 4;

                case PacketTypes.Impact or PacketTypes.ImpactList:
                    return 5;

                default:
                    return 6;
            }
        }

        private PacketFlags GetPacketFlags(NetPacket netpacket)
        {
            switch (netpacket.Type)
            {
                case PacketTypes.SyncResponse or PacketTypes.SyncRequest:
                    return PacketFlags.Instant;

                default:
                    return PacketFlags.Reliable;
            }
        }

        protected Packet CreatePacket(NetPacket netPacket)
        {
            Packet packet = default;
            var data = Serialization.ObjectToByteArray(netPacket);
            var flags = GetPacketFlags(netPacket);
            packet.Create(data, flags);

            return packet;
        }

        protected void FireDisconnectEvent(Peer peer)
        {
            PeerDisconnectedEvent?.Invoke(this, peer);
        }

        public virtual void Dispose()
        {
            _runLoop = false;

            _pollThread?.Join(100);

            Host?.Flush();
            Host?.Dispose();

            Library.Deinitialize();
        }
    }
}
