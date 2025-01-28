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
        protected abstract void SendPacket(ref Packet packet, uint peerId, byte channel);
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
                        // Set the peer ID only if the packet does not need bounced back.
                        if (ShouldBounceBack(netPacket) == false)
                            netPacket.PeerID = netEvent.Peer.ID;

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

        private void SendPacket(NetPacket netPacket)
        {
            var packet = CreatePacket(netPacket);
            var channel = GetChannel(netPacket);

            SendPacket(ref packet, netPacket.PeerID, channel);
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
        /// True if the specified packet should be bounced back to the client/peer from which it originated.
        /// </summary>
        /// <param name="packet"></param>
        /// <returns></returns>
        private bool ShouldBounceBack(NetPacket packet)
        {
            switch (packet.Type)
            {
                // We want these packet types to bounce back to clients/peers.
                // For example, with chat messages, we want to know that the message made it to 
                // the server before displaying it on the originating client.
                case PacketTypes.ChatMessage or PacketTypes.PlayerEvent or PacketTypes.PlayerReset:
                    return true;

                default:
                    return false;
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
                case PacketTypes.ServerSync:
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
