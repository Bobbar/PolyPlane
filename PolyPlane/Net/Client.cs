using ENet;
using System.Collections.Concurrent;
using System.Diagnostics;

namespace PolyPlane.Net
{
    public class Client : IDisposable
    {
        public ConcurrentQueue<NetPacket> PacketSendQueue = new ConcurrentQueue<NetPacket>();
        public ConcurrentQueue<NetPacket> PacketReceiveQueue = new ConcurrentQueue<NetPacket>();

        public long PlaneID = -1;
        public Host ClientHost;
        public Peer Peer;
        public int Port;
        public Address Address;
        public double CurrentTime;

        private Thread _pollThread;
        private bool _runLoop = true;
        private const int MAX_CLIENTS = 3;
        private const int CHANNEL_ID = 0;
        private const int MAX_CHANNELS = 4;

        public Client(ushort port, string ip)
        {
            Address = new Address();
            Address.Port = port;
            Address.SetHost(ip);
        }

        public void Start()
        {
            ClientHost = new Host();
            ClientHost.Create();

            Peer = ClientHost.Connect(Address, MAX_CHANNELS);

            _pollThread = new Thread(PollLoop);
            _pollThread.Start();
        }

        public void Stop()
        {
            SendPlayerDisconnectPacket((uint)PlaneID);

            ProcessQueue();

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
                    if (ClientHost.CheckEvents(out netEvent) <= 0)
                    {
                        if (ClientHost.Service(15, out netEvent) <= 0)
                            break;

                        polled = true;
                    }

                    switch (netEvent.Type)
                    {
                        case EventType.None:
                            break;

                        case EventType.Connect:
                            Log("Client connected to server");

                            RequestOtherPlanes();
                            break;

                        case EventType.Disconnect:
                            Log("Client disconnected from server");
                            SendPlayerDisconnectPacket(this.Peer.ID);
                            break;

                        case EventType.Timeout:
                            Log("Client connection timeout");
                            SendPlayerDisconnectPacket(this.Peer.ID);

                            break;

                        case EventType.Receive:
                            //Log("Packet received from server - Channel ID: " + netEvent.ChannelID + ", Data length: " + netEvent.Packet.Length);


                            ParsePacket(netEvent.Packet);

                            netEvent.Packet.Dispose();
                            break;
                    }

                }

                ProcessQueue();

                CurrentTime = World.CurrentTime();
            }

        }

        private void ProcessQueue()
        {
            while (PacketSendQueue.Count > 0)
            {
                if (PacketSendQueue.TryDequeue(out NetPacket packet))
                {
                    var data = IO.ObjectToByteArray(packet);
                    SendPacket(data);
                }
            }
        }
        public void SendPlayerDisconnectPacket(uint playerID)
        {
            var packet = new BasicPacket(PacketTypes.PlayerDisconnect, new GameObjects.GameID(playerID));

            var data = IO.ObjectToByteArray(packet);
            SendPacket(data);

            ClientHost.Flush();
        }

        public void SendPacket(byte[] data)
        {
            Packet packet = default(Packet);
            //packet.Create(data);
            packet.Create(data, PacketFlags.Reliable);
            //packet.Create(data, PacketFlags.Instant);

            Peer.Send(CHANNEL_ID, ref packet);
        }

        public void SendNewPlanePacket(GameObjects.Plane plane)
        {
            var netPacket = new PlanePacket(plane, PacketTypes.NewPlayer);
            EnqueuePacket(netPacket);
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

        public void EnqueuePacket(NetPacket packet)
        {
            PacketSendQueue.Enqueue(packet);
        }


        private void RequestOtherPlanes()
        {
            Packet reqPacket = default(Packet);

            var netPacket = new BasicPacket(PacketTypes.GetOtherPlanes, new GameObjects.GameID(-1, (int)Peer.ID));
            var data = IO.ObjectToByteArray(netPacket);

            reqPacket.Create(data, PacketFlags.Reliable);

            Peer.Send(CHANNEL_ID, ref reqPacket);
        }

        private void ParsePacket(Packet packet)
        {
            var buffer = new byte[packet.Length];
            packet.CopyTo(buffer);
            var packetObj = IO.ByteArrayToObject(buffer) as NetPacket;

            PacketReceiveQueue.Enqueue(packetObj);
        }

        private void Log(string message)
        {
            Debug.WriteLine($"[CLIENT] {message}");
        }

        public void Dispose()
        {
            SendPlayerDisconnectPacket(this.Peer.ID);
            ProcessQueue();


            ClientHost.Flush();

            _runLoop = false;

            ClientHost?.Dispose();
        }
    }
}
