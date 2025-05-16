using ENet;
using PolyPlane.Helpers;

namespace PolyPlane.Net.NetHost
{
    public class ClientNetHost : NetPlayHost
    {
        public Peer Peer;

        private uint _peerID = uint.MaxValue;
        private bool _clientReady = false;
        private SmoothDouble _rttSmooth = new SmoothDouble(30);

        private const double CONNECT_TIMEOUT = 1000;

        public ClientNetHost(ushort port, string ip) : base(port, ip)
        { }

        public override void DoStart()
        {
            base.DoStart();

            Host = new Host();
            Host.Create();

            Peer = Host.Connect(Address, MAX_CHANNELS);

            WaitForConnect();
        }

        public override void HandleReceive(NetPacket netPacket)
        {
            base.HandleReceive(netPacket);

            // Set the peer ID sent from the server.
            // This should be the first packet we receive.
            if (netPacket.Type == PacketTypes.SetID)
            {
                _peerID = (uint)netPacket.ID.PlayerID;
                _clientReady = true;
            }
        }

        public override ulong PacketLoss()
        {
            return Peer.PacketsLost;
        }

        protected override void SendPacket(NetPacket netPacket)
        {
            // Discard packets if we haven't received our ID yet.
            if (!_clientReady)
                return;

            // Always add our peer ID.
            netPacket.PeerID = _peerID;

            var packet = CreatePacket(netPacket);
            var channel = GetChannel(netPacket);

            // Disconnect peer in an invalid state.
            if (Peer.State != PeerState.Connected && Peer.State != PeerState.Connecting)
            {
                DisconnectClient();
                return;
            }

            Peer.Send(channel, ref packet);
        }

        private void DisconnectClient()
        {
            Stop();
            Disconnect(0);
            FireDisconnectEvent(Peer);
        }

        private void WaitForConnect()
        {
            // Wait for peer to connect.
            var startTime = World.CurrentTimeMs();

            while (Peer.State == PeerState.Connecting)
            {
                var elap = World.CurrentTimeMs() - startTime;

                if (elap > CONNECT_TIMEOUT)
                {
                    // Unable to connect to server.
                    // Reset peer to trigger disconnect.
                    Peer.Reset();
                    return;
                }

                Host.Service(100, out Event e);
            }
        }

        public override Peer? GetPeer(int playerID)
        {
            return Peer;
        }

        public override void Disconnect(int playerID)
        {
            if (Peer.State == PeerState.Connected)
                Peer.DisconnectNow(0);

            Host.Flush();
        }

        public override double GetPlayerRTT(int playerID)
        {
            return _rttSmooth.Add(Peer.RoundTripTime);
        }

        public override void Dispose()
        {
            Peer.DisconnectNow(0);

            base.Dispose();
        }
    }
}
