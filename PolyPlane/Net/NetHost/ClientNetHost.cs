using ENet;
using PolyPlane.Helpers;

namespace PolyPlane.Net.NetHost
{
    public class ClientNetHost : NetPlayHost
    {
        public Peer Peer;

        private SmoothDouble _rttSmooth = new SmoothDouble(30);

        public ClientNetHost(ushort port, string ip) : base(port, ip)
        { }

        public override void DoStart()
        {
            base.DoStart();

            Host = new Host();
            Host.Create();

            Peer = Host.Connect(Address, MAX_CHANNELS);
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
            if (Peer.State != PeerState.Connected)
            {
                // Disconnect and stop processing packets.
                Disconnect(0);
                Stop();
                FireDisconnectEvent(Peer);
                return;
            }

            Peer.Send(channel, ref packet);
        }

        public override Peer? GetPeer(int playerID)
        {
            return Peer;
        }

        public override void Disconnect(int playerID)
        {
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
