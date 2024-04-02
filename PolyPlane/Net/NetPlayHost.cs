using ENet;
using PolyPlane.GameObjects;
using System.Collections.Concurrent;

namespace PolyPlane.Net
{
    public abstract class NetPlayHost : IDisposable
    {
        public const int MAX_CLIENTS = 30;
        public const int MAX_CHANNELS = 4;
        public const int CHANNEL_ID = 0;

        public ConcurrentQueue<NetPacket> PacketSendQueue = new ConcurrentQueue<NetPacket>();
        public ConcurrentQueue<NetPacket> PacketReceiveQueue = new ConcurrentQueue<NetPacket>();

        public Host Host;
        public ushort Port;
        public Address Address;
        public double CurrentTime;

        private Thread _pollThread;
        private bool _runLoop = true;

        public NetPlayHost(ushort port, string ip)
        {
            Port = port;

            Address = new Address();
            Address.Port = port;
            Address.SetIP(ip);
            //Address.SetHost(ip);
        }

        public void Start()
        {
            DoStart();

            _pollThread = new Thread(PollLoop);
            _pollThread.Start();
        }

        public void Stop()
        {
            _runLoop = false;

            DoStop();
        }

        public virtual void DoStop() { }

        public virtual void DoStart() { }

        private void PollLoop()
        {
            Event netEvent;

            while (_runLoop)
            {
                bool polled = false;

                while (!polled)
                {
                    if (Host.CheckEvents(out netEvent) <= 0)
                    {
                        if (Host.Service(15, out netEvent) <= 0)
                            break;

                        polled = true;
                    }

                    HandleEvent(netEvent);

                    netEvent.Packet.Dispose();
                }

                ProcessQueue();
                CurrentTime = World.CurrentTime();
            }
        }

        private void HandleEvent(Event netEvent)
        {
            switch (netEvent.Type)
            {
                case EventType.None:
                    break;

                case EventType.Connect:
                    HandleConnect(netEvent);
                    break;

                case EventType.Disconnect:
                    HandleDisconnect(netEvent);
                    break;

                case EventType.Timeout:
                    HandleTimeout(netEvent);
                    break;

                case EventType.Receive:
                    HandleReceive(netEvent);
                    break;
            }
        }

        private void ProcessQueue()
        {
            while (PacketSendQueue.Count > 0)
            {
                if (PacketSendQueue.TryDequeue(out NetPacket packet))
                {
                    SendPacket(packet);
                }
            }
        }

        public void EnqueuePacket(NetPacket packet)
        {
            PacketSendQueue.Enqueue(packet);
        }

        public void SendNewBulletPacket(Bullet bullet)
        {
            var netPacket = new BulletPacket(bullet);
            SendPacket(netPacket);
        }

        public void SendNewMissilePacket(GuidedMissile missile)
        {
            var netPacket = new MissilePacket(missile);
            EnqueuePacket(netPacket);
        }

        public void SendPlayerDisconnectPacket(uint playerID)
        {
            var packet = new BasicPacket(PacketTypes.PlayerDisconnect, new GameID(playerID));
            SendPacket(packet);
        }

        public void SendSyncPacket()
        {
            var packet = new SyncPacket(World.CurrentTime());
            SendPacket(packet);
        }

        public virtual void SendPacket(NetPacket packet) { }
        public virtual void HandleConnect(Event netEvent) { }
        public virtual void HandleDisconnect(Event netEvent) { }
        public virtual void HandleTimeout(Event netEvent) { }
        public virtual void HandleReceive(Event netEvent) { }

        public virtual uint GetPlayerRTT(int playerID)
        {
            return 0;
        }


        public virtual void Dispose()
        {
            Host.Flush();
            _runLoop = false;
            Thread.Sleep(30);
            Host?.Dispose();
        }
    }
}
