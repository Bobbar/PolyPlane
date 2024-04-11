﻿using ENet;
using PolyPlane.GameObjects;
using System.Diagnostics;

namespace PolyPlane.Net
{
    public abstract class NetPlayHost : IDisposable
    {
        public const int MAX_CLIENTS = 30;
        public const int MAX_CHANNELS = 4;
        public const int CHANNEL_ID = 0;
        public const int TIMEOUT = 15;

        public RingBuffer<NetPacket> PacketSendQueue = new RingBuffer<NetPacket>(30);
        public RingBuffer<NetPacket> PacketReceiveQueue = new RingBuffer<NetPacket>(30);

        public Host Host;
        public ushort Port;
        public Address Address;
        public double CurrentTime;
        public TimeSpan NetTime => _netTime;

        private Thread _pollThread;
        private bool _runLoop = true;

        private TimeSpan _netTime = TimeSpan.Zero;
        private Stopwatch _netTimer = new Stopwatch();

        public event EventHandler<Peer> PeerTimeoutEvent;
        
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
                _netTimer.Restart();

                bool polled = false;

                while (!polled)
                {
                    if (Host.CheckEvents(out netEvent) <= 0)
                    {
                        if (Host.Service(TIMEOUT, out netEvent) <= 0)
                            break;

                        polled = true;
                    }

                    HandleEvent(netEvent);
                }

                ProcessQueue();
                CurrentTime = World.CurrentTime();

                _netTimer.Stop();
                _netTime = _netTimer.Elapsed;
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

                    PeerTimeoutEvent?.Invoke(this, netEvent.Peer);

                    break;

                case EventType.Receive:
                    HandleReceive(netEvent);
                    netEvent.Packet.Dispose();
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
            var packet = new SyncPacket(World.CurrentTime(), World.TimeOfDay, World.TimeOfDayDir);
            SendPacket(packet);
        }

        public virtual void SendPacket(NetPacket packet) { }
        public virtual void HandleConnect(Event netEvent) { }
        public virtual void HandleDisconnect(Event netEvent) { }
        public virtual void HandleTimeout(Event netEvent) { }
        public virtual void HandleReceive(Event netEvent) { }

        public abstract ulong PacketLoss();

        internal int GetChannel(NetPacket netpacket)
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

        internal Packet CreatePacket(NetPacket netPacket)
        {
            Packet packet = default(Packet);
            var data = IO.ObjectToByteArray(netPacket);
            packet.Create(data, PacketFlags.Reliable);
            //packet.Create(data, PacketFlags.Instant);

            return packet;
        }

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
