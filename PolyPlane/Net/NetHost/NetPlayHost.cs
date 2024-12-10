using ENet;
using NetStack.Threading;
using PolyPlane.GameObjects;

namespace PolyPlane.Net.NetHost
{
    public abstract class NetPlayHost : IDisposable
    {
        public event EventHandler<Peer> PeerTimeoutEvent;
        public event EventHandler<Peer> PeerDisconnectedEvent;

        public ConcurrentBuffer PacketSendQueue = new ConcurrentBuffer(1024);
        public ConcurrentBuffer PacketReceiveQueue = new ConcurrentBuffer(1024);

        public Host Host;
        public readonly ushort Port;
        public readonly Address Address;

        protected const int MAX_CLIENTS = 30;
        protected const int MAX_CHANNELS = 7;
        protected const int CHANNEL_ID = 0;
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

        public void EnqueuePacket(NetPacket packet)
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
        protected abstract void SendPacket(ref Packet packet, byte channel);
        public abstract Peer? GetPeer(int playerID);
        public abstract void Disconnect(int playerID);
        public abstract uint GetPlayerRTT(int playerID);

        internal void PollLoop()
        {
            Event netEvent;
            bool polled = false;

            while (_runLoop)
            {
                ProcessSendQueue();

                polled = false;

                while (!polled && _runLoop)
                {
                    if (Host.CheckEvents(out netEvent) <= 0)
                    {
                        if (Host.Service(TIMEOUT, out netEvent) <= 0)
                            break;

                        polled = true;
                    }

                    HandleEvent(ref netEvent);
                }

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
                    var netPacket = ParsePacket(ref packet);

                    HandleReceive(netPacket);

                    packet.Dispose();

                    break;
            }
        }

        private void ProcessSendQueue()
        {
            while (PacketSendQueue.Count > 0)
            {
                if (PacketSendQueue.TryDequeue(out object packet))
                {
                    SendPacket((NetPacket)packet);
                }
            }
        }

        private void SendPacket(NetPacket netPacket)
        {
            var packet = CreatePacket(netPacket);
            var channel = GetChannel(netPacket);

            SendPacket(ref packet, channel);
        }

        private NetPacket ParsePacket(ref Packet packet)
        {
            var buffer = new byte[packet.Length];

            packet.CopyTo(buffer);

            var packetObj = Serialization.ByteArrayToObject(buffer) as NetPacket;

            return packetObj;
        }

        protected byte GetChannel(NetPacket netpacket)
        {
            switch (netpacket.Type)
            {
                case PacketTypes.PlaneUpdate:
                    return 0;

                case PacketTypes.MissileUpdate:
                    return 1;

                case PacketTypes.NewBullet:
                    return 2;

                case PacketTypes.NewMissile:
                    return 3;

                case PacketTypes.NewDecoy:
                    return 4;

                case PacketTypes.ExpiredObjects:
                    return 5;

                default:
                    return 6;
            }
        }

        private PacketFlags GetPacketFlags(NetPacket netpacket)
        {
            switch (netpacket.Type)
            {
                case PacketTypes.NewBullet or PacketTypes.NewMissile or PacketTypes.NewDecoy or PacketTypes.Impact:
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

            Host?.Flush();
            Host?.Dispose();
           
            _pollLimiter?.Dispose();
            
            Library.Deinitialize();
        }
    }
}
