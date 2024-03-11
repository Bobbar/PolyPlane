using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Linq;
using System.Security.Policy;
using System.Text;
using System.Threading.Tasks;
using ENet;
using static System.Runtime.InteropServices.JavaScript.JSType;

namespace PolyPlane.Net
{
    public class Server : IDisposable
    {
        public ConcurrentQueue<NetPacket> PacketQueue = new ConcurrentQueue<NetPacket>();

        public Host ServerHost;
        public int Port;
        public Address Address;
        private Thread _pollThread;
        private bool _runLoop = true;
        private const int MAX_CLIENTS = 3;
        private const int MAX_CHANNELS = 3;
        private const int CHANNEL_ID = 0;

        //private List<Peer> _peers = new List<Peer>();
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
            var ip = Address.GetIP();
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

                            var idPacket = new NetPacket(PacketTypes.SetID, (long)netEvent.Peer.ID);
                            SendIDPacket(netEvent.Peer, idPacket);



                            break;

                        case EventType.Disconnect:
                            Log("Client disconnected - ID: " + netEvent.Peer.ID + ", IP: " + netEvent.Peer.IP);

                            _peers.Remove(netEvent.Peer.ID);

                            break;

                        case EventType.Timeout:
                            Log("Client timeout - ID: " + netEvent.Peer.ID + ", IP: " + netEvent.Peer.IP);

                            _peers.Remove(netEvent.Peer.ID);

                            break;

                        case EventType.Receive:
                            Log("Packet received from - ID: " + netEvent.Peer.ID + ", IP: " + netEvent.Peer.IP + ", Channel ID: " + netEvent.ChannelID + ", Data length: " + netEvent.Packet.Length);

                            //ParseTestPacket(netEvent.Packet);

                            ParsePacket(netEvent.Packet, netEvent.Peer);

                            netEvent.Packet.Dispose();
                            break;
                    }

                }

            }

        }

        //public void SyncOtherPlanes(List<Net.PlanePacket> planes, ushort requestID)
        //{
        //    var data = IO.ObjectToByteArray(planes);
        //    Packet planesPacket = default(Packet);
        //    planesPacket.Create(data);

        //    var peer = _peers[requestID];
        //    ServerHost.Broadcast(CHANNEL_ID, ref planesPacket, peer);
        //}

        public void SendPlaneUpdate(PlanePacket plane)
        {
            var data = IO.ObjectToByteArray(plane);
            Packet packet = default(Packet);
            packet.Create(data);

            ServerHost.Broadcast(CHANNEL_ID, ref packet);
            //peer.Send(CHANNEL_ID, ref packet)
        }


        public void SyncOtherPlanes(PlaneListPacket planePacket, ushort requestID)
        {
            var data = IO.ObjectToByteArray(planePacket);
            Packet planesPacket = default(Packet);
            planesPacket.Create(data);

            //var peer = _peers[requestID];
            //ServerHost.Broadcast(CHANNEL_ID, ref planesPacket, peer);

            ServerHost.Broadcast(CHANNEL_ID, ref planesPacket);
        }

        private void SendIDPacket(Peer peer, NetPacket packet)
        {
           var data = IO.ObjectToByteArray(packet);
            Packet idPacket = default(Packet);

            idPacket.Create(data);
            peer.Send(CHANNEL_ID, ref idPacket);
        }

        private void SendPacketToPeer(Peer peer, NetPacket packet)
        {

        }

        //private void ParseTestPacket(Packet packet)
        //{
        //    var buffer = new byte[packet.Length];
        //    packet.CopyTo(buffer);


        //    var packetObj = IO.ByteArrayToObject(buffer) as NetPacket;

        //    PacketQueue.Enqueue(packetObj);
        //    //if (packetObj != null)
        //    //{
        //    //    Log(packetObj.ToString());
        //    //}
        //}

        private void ParsePacket(Packet packet, Peer peer)
        {
            var buffer = new byte[packet.Length];
            packet.CopyTo(buffer);


            var packetObj = IO.ByteArrayToObject(buffer) as NetPacket;

            if (packetObj.Type == PacketTypes.GetNextID)
            {
                var nextId = new NetPacket(PacketTypes.GetNextID, World.GetNextId());

                Packet idPacket = default(Packet);
                idPacket.Create(IO.ObjectToByteArray(nextId));
                peer.Send(CHANNEL_ID, ref idPacket);
            }
            else
            {
                PacketQueue.Enqueue(packetObj);
            }


            //if (packetObj != null)
            //{
            //    Log(packetObj.ToString());
            //}
        }


        private void Log(string message)
        {
            //Debug.WriteLine($"[SERVER] {message}");
        }


        public void Dispose()
        {
            ServerHost.Flush();

            _runLoop = false;

            ServerHost?.Dispose();
        }
    }
}
