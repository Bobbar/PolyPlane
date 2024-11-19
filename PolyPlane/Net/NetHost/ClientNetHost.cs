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

        public override void HandleConnect(Event netEvent)
        {
            base.HandleConnect(netEvent);

            RequestOtherPlanes();
        }

        public override void HandleDisconnect(Event netEvent)
        {
            base.HandleDisconnect(netEvent);

            SendPlayerDisconnectPacket(netEvent.Peer.ID);
        }

        public override void HandleTimeout(Event netEvent)
        {
            base.HandleTimeout(netEvent);

            SendPlayerDisconnectPacket(netEvent.Peer.ID);
        }

        public override ulong PacketLoss()
        {
            return Peer.PacketsLost;
        }

        public override void SendPacket(Packet packet, byte channel)
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

        private void RequestOtherPlanes()
        {
            var netPacket = new BasicPacket(PacketTypes.GetOtherPlanes, new GameObjects.GameID(-1, Peer.ID));
            EnqueuePacket(netPacket);
        }

        private void SendPlayerDisconnectPacket(uint playerID)
        {
            var packet = new BasicPacket(PacketTypes.PlayerDisconnect, new GameObjects.GameID(playerID));
            EnqueuePacket(packet);
            Host.Flush();
        }
    }
}
