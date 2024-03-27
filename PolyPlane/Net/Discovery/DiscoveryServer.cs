using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Net.Sockets;
using PolyPlane.Net;
using System.Net;
using ENet;
using static GrEmit.GroboIL;

namespace PolyPlane.Net.Discovery
{
    public class DiscoveryServer : IDisposable
    {
        private const int PORT = 4321;
        private UdpClient _udpListener = new UdpClient();
        private Thread _listenThread;
        private bool _running = false;
        private bool _isServer = false;
        private string _localIP;

        public event EventHandler<DiscoveryPacket> NewDiscoveryReceived;


        public DiscoveryServer()
        {
            _isServer = true;
            _udpListener.Client.Bind(new IPEndPoint(IPAddress.Any, PORT));

            _listenThread = new Thread(ListenLoop);
        }

        public DiscoveryServer(string localIP)
        {
            _localIP = localIP;
            _isServer = false;
            //_udpListener.Client.Bind(new IPEndPoint(IPAddress.Parse(localIP), PORT));
            _udpListener.Client.Bind(new IPEndPoint(IPAddress.Any, PORT));

            _listenThread = new Thread(ListenLoop);
        }

        public void Start()
        {
            _running = true;

            _listenThread.Start();
        }

        public void Stop()
        {
            _running = false;
            _listenThread.Join(150);
        }

        public void Dispose()
        {
            _running = false;

            Thread.Sleep(30);

            _listenThread.Join(150);

            _udpListener?.Close();
            _udpListener?.Dispose();
        }

        private void ListenLoop()
        {
            var from = new IPEndPoint(0, 0);

            while (_running)
            {
                var recBuff = _udpListener.Receive(ref from);
                var packet = IO.ByteArrayToObject(recBuff) as NetPacket;

                if (packet != null)
                {
                    HandlePacket(packet);
                }
            }
        }

        private void HandlePacket(NetPacket packet)
        {
            switch (packet.Type)
            {
                case PacketTypes.Discovery:

                    var discoverPacket = packet as DiscoveryPacket;

                    if (discoverPacket != null)
                    {
                        NewDiscoveryReceived?.Invoke(this, discoverPacket);
                    }


                    break;
            }
        }

        public void SendServerInfo(string clientIP, DiscoveryPacket packet)
        {
            var data = IO.ObjectToByteArray(packet);
            _udpListener.Send(data, data.Length, new IPEndPoint(IPAddress.Parse(clientIP), PORT));
        }

        public void BroadcastServerInfo(DiscoveryPacket packet)
        {
            var data = IO.ObjectToByteArray(packet);
            _udpListener.Send(data, data.Length, new IPEndPoint(IPAddress.Broadcast, PORT));
        }

        public void QueryForServers(string clientIP)
        {
            var queryPacket = new DiscoveryPacket(clientIP);
            var data = IO.ObjectToByteArray(queryPacket);

            _udpListener.Send(data, data.Length, new IPEndPoint(IPAddress.Broadcast, PORT));

            //_udpListener.Send(data, data.Length, new IPEndPoint(IPAddress.Parse(clientIP), PORT));
        }
    }
}
