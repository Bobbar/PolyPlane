using System.Net;
using System.Net.Sockets;

namespace PolyPlane.Net.Discovery
{
    public class DiscoveryServer : IDisposable
    {
        private const int PORT = 4321;
        private UdpClient _udpListener = new UdpClient();
        private Thread _listenThread;
        private bool _running = false;
        private CancellationTokenSource _tokenSource = new CancellationTokenSource();
        private CancellationToken _stopListenToken;

        public event EventHandler<DiscoveryPacket> NewDiscoveryReceived;

        public DiscoveryServer()
        {
        }

        /// <summary>
        /// Begin listening for broadcast packets.
        /// </summary>
        public void StartListen()
        {
            _stopListenToken = _tokenSource.Token;
            _listenThread = new Thread(ListenLoop);

            _udpListener.Client.Bind(new IPEndPoint(IPAddress.Any, PORT));

            _running = true;

            _listenThread.Start();
        }

        public void StopListen()
        {
            _tokenSource?.Cancel();
            _running = false;
        }

        public void Dispose()
        {
            _tokenSource?.Cancel();
            _running = false;
        }

        private async void ListenLoop()
        {
            try
            {
                while (_running)
                {
                    var res = await _udpListener.ReceiveAsync(_stopListenToken);
                    var recBuff = res.Buffer;

                    var packet = Serialization.ByteArrayToObject(recBuff) as NetPacket;

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
            finally
            {
                _udpListener?.Close();
                _udpListener?.Dispose();
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
            var data = Serialization.ObjectToByteArray(packet);

            var addys = Dns.GetHostAddresses(Dns.GetHostName());

            // Broadcast to all local interfaces.
            foreach (var addy in addys)
            {
                if (addy.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork && addy.IsIPv6LinkLocal == false)
                {
                    // Set last octet to 255 to build a broadcast address for this subnet.
                    // Ex: 10.10.80.123 => 10.10.80.255
                    var bytes = addy.GetAddressBytes();
                    bytes[3] = 255;

                    var broadcastIP = new IPAddress(bytes);

                    _udpListener.Send(data, data.Length, new IPEndPoint(broadcastIP, PORT));
                }
            }
        }
    }
}
