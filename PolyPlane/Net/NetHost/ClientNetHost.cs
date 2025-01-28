using ENet;
using PolyPlane.Helpers;

namespace PolyPlane.Net.NetHost
{
    public class ClientNetHost : NetPlayHost
    {
        public Peer Peer;

        private SmoothDouble _rttSmooth = new SmoothDouble(30);
        private double _startTime = 0;

        private const double CONNECT_TIMEOUT = 1000;

        public ClientNetHost(ushort port, string ip) : base(port, ip)
        { }

        public override void DoStart()
        {
            base.DoStart();

            Host = new Host();
            Host.Create();

            Peer = Host.Connect(Address, MAX_CHANNELS);

            _startTime = World.CurrentTimeMs();
        }

        public override void HandleConnect(ref Event netEvent)
        {
            base.HandleConnect(ref netEvent);
        }

        public override void HandleDisconnect(ref Event netEvent)
        {
            base.HandleDisconnect(ref netEvent);

            SendPlayerDisconnectPacket(netEvent.Peer.ID);
        }

        public override void HandleTimeout(ref Event netEvent)
        {
            base.HandleTimeout(ref netEvent);

            SendPlayerDisconnectPacket(netEvent.Peer.ID);
        }

        public override ulong PacketLoss()
        {
            return Peer.PacketsLost;
        }

        protected override void SendPacket(ref Packet packet, uint peerID, byte channel)
        {
            // Wait for peer to connect.
            while (Peer.State == PeerState.Connecting)
            {
                Thread.Sleep(100);

                var elap = World.CurrentTimeMs() - _startTime;

                if (elap > CONNECT_TIMEOUT)
                {
                    // Unable to connect to server.
                    DisconnectClient();
                    return;
                }

                Host.Service(10, out Event e);
            }

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
