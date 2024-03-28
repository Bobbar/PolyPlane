﻿using System.Net;
using System.Net.Sockets;

namespace PolyPlane.Net.Discovery
{
    public class DiscoveryServer : IDisposable
    {
        private const int PORT = 4321;
        private UdpClient _udpListener = new UdpClient();
        private Thread _listenThread;
        private bool _running = false;

        public event EventHandler<DiscoveryPacket> NewDiscoveryReceived;

        public DiscoveryServer()
        {
        }

        /// <summary>
        /// Begin listening for broadcast packets.
        /// </summary>
        public void StartListen()
        {
            _listenThread = new Thread(ListenLoop);

            _udpListener.Client.Bind(new IPEndPoint(IPAddress.Any, PORT));

            _running = true;

            _listenThread.Start();
        }

        public void StopListen()
        {
            _running = false;

            _udpListener?.Close();

            _listenThread?.Join(150);
        }

        public void Dispose()
        {
            _running = false;

            Thread.Sleep(30);

            _listenThread?.Join(150);

            _udpListener?.Close();
            _udpListener?.Dispose();
        }

        private void ListenLoop()
        {
            var from = new IPEndPoint(0, 0);
            try
            {
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
            catch
            {
                // Catch socket exceptions when the listener is closed.
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

        public void BroadcastServerInfo(DiscoveryPacket packet)
        {
            var data = IO.ObjectToByteArray(packet);
            _udpListener.Send(data, data.Length, new IPEndPoint(IPAddress.Broadcast, PORT));
        }
    }
}
