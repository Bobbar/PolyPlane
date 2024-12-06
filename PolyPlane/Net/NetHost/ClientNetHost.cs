using ENet;

namespace PolyPlane.Net.NetHost
{
    public class ClientNetHost : NetPlayHost
    {
        public Peer Peer;

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

        public override void SendPacket(ref Packet packet, byte channel)
        {
            if (Peer.State != PeerState.Connected)
            {
                // Disconnect and stop processing packets.
                FireDisconnectEvent(Peer);
                Stop();
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

        public override uint GetPlayerRTT(int playerID)
        {
            return Peer.RoundTripTime;
        }

        public override void Dispose()
        {
            Peer.DisconnectNow(0);

            base.Dispose();
        }
    }
}
