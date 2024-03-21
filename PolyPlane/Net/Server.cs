using ENet;
using System.Collections.Concurrent;
using System.Diagnostics;

namespace PolyPlane.Net
{
    public class Server : IDisposable
    {
        public ConcurrentQueue<NetPacket> PacketReceiveQueue = new ConcurrentQueue<NetPacket>();
        public ConcurrentQueue<NetPacket> PacketSendQueue = new ConcurrentQueue<NetPacket>();

        public Host ServerHost;
        public int Port;
        public Address Address;
        public double CurrentTime;

        public float BytesSentPerSecond;
        public float BytesReceivedPerSecond;

        private Thread _pollThread;
        private bool _runLoop = true;
        private const int MAX_CLIENTS = 3;
        private const int MAX_CHANNELS = 4;
        private const int CHANNEL_ID = 0;

        private SmoothFloat _bytesRecSmooth = new SmoothFloat(100);
        private SmoothFloat _bytesSentSmooth = new SmoothFloat(100);


        private uint _prevBytesRec = 0;
        private uint _prevBytesSent = 0;
        private double _lastFrameTime = 0;

        private Dictionary<uint, Peer> _peers = new Dictionary<uint, Peer>();

        public Server(ushort port)
        {
            Address = new Address();
            Address.Port = port;
        }

        public Server(ushort port, string ip)
        {
            Address = new Address();
            Address.Port = port;
            Address.SetIP(ip);
        }

        public void Start()
        {
            ServerHost = new Host();
            ServerHost.Create(Address, MAX_CLIENTS, MAX_CHANNELS);

            _pollThread = new Thread(PollLoop);
            _pollThread.Start();
        }

        public void Stop()
        {
            _runLoop = false;
        }

        private void PollLoop()
        {
            Event netEvent;


            while (_runLoop)
            {
                bool polled = false;

                while (!polled)
                {
                    if (ServerHost.CheckEvents(out netEvent) <= 0)
                    {
                        if (ServerHost.Service(15, out netEvent) <= 0)
                            break;

                        polled = true;
                    }

                    switch (netEvent.Type)
                    {
                        case EventType.None:
                            break;

                        case EventType.Connect:
                            Log("Client connected - ID: " + netEvent.Peer.ID + ", IP: " + netEvent.Peer.IP);

                            _peers.Add(netEvent.Peer.ID, netEvent.Peer);

                            var idPacket = new BasicPacket(PacketTypes.SetID, new GameObjects.GameID((int)netEvent.Peer.ID, 0));
                            SendIDPacket(netEvent.Peer, idPacket);

                            break;

                        case EventType.Disconnect:
                            Log("Client disconnected - ID: " + netEvent.Peer.ID + ", IP: " + netEvent.Peer.IP);

                            SendPlayerDisconnectPacket(netEvent.Peer.ID);

                            _peers.Remove(netEvent.Peer.ID);

                            break;

                        case EventType.Timeout:
                            Log("Client timeout - ID: " + netEvent.Peer.ID + ", IP: " + netEvent.Peer.IP);


                            SendPlayerDisconnectPacket(netEvent.Peer.ID);

                            _peers.Remove(netEvent.Peer.ID);

                            break;

                        case EventType.Receive:
                            //Log("Packet received from - ID: " + netEvent.Peer.ID + ", IP: " + netEvent.Peer.IP + ", Channel ID: " + netEvent.ChannelID + ", Data length: " + netEvent.Packet.Length);

                            ParsePacket(netEvent.Packet, netEvent.Peer);

                            netEvent.Packet.Dispose();
                            break;
                    }

                }

                ProcessQueue();

                CurrentTime = World.CurrentTime();

                var elap = CurrentTime - _lastFrameTime;
                _lastFrameTime = CurrentTime;

                var bytesRec = ServerHost.BytesReceived - _prevBytesRec;
                _prevBytesRec = ServerHost.BytesReceived;

                var bytesSent = ServerHost.BytesSent - _prevBytesSent;
                _prevBytesSent = ServerHost.BytesSent;

                //var bytesRecPerSec = bytesRec / (float)(elap);
                //var bytesSentPerSec = bytesSent / (float)(elap);

                // TODO: Not so sure about this math...
                var bytesRecPerSec = (bytesRec / (float)(elap)) * 1000f;
                var bytesSentPerSec = (bytesSent / (float)(elap)) * 1000f;

                BytesReceivedPerSecond = _bytesRecSmooth.Add(bytesRecPerSec / 1000f);
                BytesSentPerSecond = _bytesSentSmooth.Add(bytesSentPerSec / 1000f);
            }

        }

        private void ProcessQueue()
        {
            while (PacketSendQueue.Count > 0)
            {
                if (PacketSendQueue.TryDequeue(out NetPacket packet))
                {
                    //Log(packet.Type.ToString());
                    BroadcastPacket(packet);
                }
            }
        }

        public void SendPlayerDisconnectPacket(uint playerID)
        {
            var packet = new BasicPacket(PacketTypes.PlayerDisconnect, new GameObjects.GameID(playerID));

            EnqueuePacket(packet);
        }


        public void SendNewBulletPacket(GameObjects.Bullet bullet)
        {
            var netPacket = new BulletPacket(bullet, PacketTypes.NewBullet);

            EnqueuePacket(netPacket);
        }

        public void SendNewMissilePacket(GameObjects.GuidedMissile missile)
        {
            var netPacket = new MissilePacket(missile);

            EnqueuePacket(netPacket);
        }

        public void SendSyncPacket()
        {
            var now = World.CurrentTime();
            var syncPacket = new SyncPacket(now);
            BroadcastPacket(syncPacket);
        }

        public void EnqueuePacket(NetPacket packet)
        {
            PacketSendQueue.Enqueue(packet);
        }

        public void BroadcastPacket(NetPacket netPacket)
        {
            var packet = CreatePacket(netPacket);
            var channel = GetChannel(netPacket);

            ServerHost.Broadcast((byte)channel, ref packet);
        }

        public int GetChannel(NetPacket netpacket)
        {
            switch (netpacket.Type)
            {
                case PacketTypes.PlaneUpdate:
                    return 0;

                case PacketTypes.MissileUpdate:
                    return 1;

                case PacketTypes.NewBullet:
                    return 2;

                default:
                    return 3;
            }
        }

        public uint GetPlayerRTT(int playerID)
        {
            if (_peers.TryGetValue((uint)playerID, out var peer))
            {
                return peer.RoundTripTime;
            }

            return 0;
        }

        private Packet CreatePacket(NetPacket netPacket)
        {
            Packet packet = default(Packet);
            var data = IO.ObjectToByteArray(netPacket);
            packet.Create(data, PacketFlags.Reliable);
            //packet.Create(data, PacketFlags.Instant);

            return packet;
        }

        private void SendIDPacket(Peer peer, NetPacket packet)
        {
            var data = IO.ObjectToByteArray(packet);
            Packet idPacket = default(Packet);

            idPacket.Create(data, PacketFlags.Reliable);
            peer.Send(CHANNEL_ID, ref idPacket);
        }

        private void ParsePacket(Packet packet, Peer peer)
        {
            var buffer = new byte[packet.Length];
            packet.CopyTo(buffer);

            var packetObj = IO.ByteArrayToObject(buffer) as NetPacket;

            switch (packetObj.Type)
            {
                case PacketTypes.GetNextID:

                    var nextId = new BasicPacket(PacketTypes.GetNextID, new GameObjects.GameID(-1, World.GetNextObjectId()));

                    Packet idPacket = default(Packet);
                    idPacket.Create(IO.ObjectToByteArray(nextId));
                    peer.Send(CHANNEL_ID, ref idPacket);

                    break;

                case PacketTypes.PlaneUpdate or PacketTypes.MissileUpdate or PacketTypes.NewBullet or PacketTypes.NewMissile or PacketTypes.Impact or PacketTypes.PlayerDisconnect or PacketTypes.PlayerReset:
                    
                    // Immediately re-broadcast certain updates.
                    BroadcastPacket(packetObj);
                    PacketReceiveQueue.Enqueue(packetObj);

                    break;

                default:

                    PacketReceiveQueue.Enqueue(packetObj);

                    break;
            }
        }

        private void Log(string message)
        {
            Debug.WriteLine($"[SERVER] {message}");
        }


        public void Dispose()
        {
            ServerHost.Flush();

            _runLoop = false;

            Thread.Sleep(30);

            ServerHost?.Dispose();
        }
    }
}
