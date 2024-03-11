using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;
using ENet;
using static System.Runtime.InteropServices.JavaScript.JSType;

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
        private Thread _pollThread;
        private bool _runLoop = true;
        private const int MAX_CLIENTS = 3;
        private const int CHANNEL_ID = 0;
        private const int MAX_CHANNELS = 3;

        //public Client(ushort port)
        //{
        //    Address = new Address();
        //    Address.Port = port;
        //}

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
            _runLoop = false;
        }

        public void SendPacket(byte[] data)
        {
            Packet packet = default(Packet);

            packet.Create(data);
            Peer.Send(CHANNEL_ID, ref packet);

        }


        public void SendPacket(byte[] data, PacketTypes type)
        {
            Packet packet = default(Packet);

            packet.Create(data);
            //Peer.Send((byte)type, ref packet);
            Peer.Send(CHANNEL_ID, ref packet);

        }

        public void SendNewPlanePacket(GameObjects.Plane plane)
        {
            Packet reqPacket = default(Packet);

            var netPacket = new PlanePacket(plane, PacketTypes.NewPlayer);
            var data = IO.ObjectToByteArray(netPacket);

            reqPacket.Create(data);

            Peer.Send(CHANNEL_ID, ref reqPacket);
        }

        public void SendNewBulletPacket(GameObjects.Bullet bullet)
        {
            //Packet packet = default(Packet);
            var netPacket = new BulletPacket(bullet, PacketTypes.NewBullet);
            //var data = IO.ObjectToByteArray(netPacket);
            //packet.Create(data);

            EnqueuePacket(netPacket);
            //Peer.Send(CHANNEL_ID, ref packet);
        }


        public void EnqueuePacket(NetPacket packet)
        {
            PacketSendQueue.Enqueue(packet);
        }

        private void ProcessQueue()
        {
            while (PacketSendQueue.Count > 0)
            {
                if (PacketSendQueue.TryDequeue(out NetPacket packet))
                {
                    var data = IO.ObjectToByteArray(packet);
                    SendPacket(data, packet.Type);
                }
            }
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

                            break;

                        case EventType.Timeout:
                            Log("Client connection timeout");

                            break;

                        case EventType.Receive:
                            Log("Packet received from server - Channel ID: " + netEvent.ChannelID + ", Data length: " + netEvent.Packet.Length);


                            ParsePacket(netEvent.Packet);

                            netEvent.Packet.Dispose();
                            break;
                    }

                }

                ProcessQueue();

            }

        }


        private void RequestOtherPlanes()
        {
            Packet reqPacket = default(Packet);

            var netPacket = new NetPacket(PacketTypes.GetOtherPlanes, Peer.ID);
            var data = IO.ObjectToByteArray(netPacket);

            reqPacket.Create(data);

            Peer.Send(CHANNEL_ID, ref reqPacket);
        }

        private void ParsePacket(Packet packet)
        {
            var buffer = new byte[packet.Length];
            packet.CopyTo(buffer);

            var packetObj = IO.ByteArrayToObject(buffer) as NetPacket;

            PacketReceiveQueue.Enqueue(packetObj);

            //switch (packetObj.Type)
            //{
            //    case PacketTypes.PlaneUpdate:

            //        break;

            //    case PacketTypes.NewPlayer:
            //        break;

            //    case PacketTypes.SetID:
            //        PlaneID = packetObj.ID;
            //        break;

            //    case PacketTypes.ChatMessage:
            //        break;

            //}

        }

        private void Log(string message)
        {
            //Debug.WriteLine($"[CLIENT] {message}");
        }

        public void Dispose()
        {
            ClientHost.Flush();

            _runLoop = false;

            ClientHost?.Dispose();
        }
    }
}
