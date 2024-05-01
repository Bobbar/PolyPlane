using ENet;

namespace PolyPlane.Net
{
    public class ClientNetHost : NetPlayHost
    {
        public Peer Peer;

        public ClientNetHost(ushort port, string ip) : base(port, ip)
        {
        }

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

        public override void HandleReceive(Event netEvent)
        {
            base.HandleReceive(netEvent);

            ParsePacket(netEvent.Packet);
        }

        public override void SendPacket(NetPacket netPacket)
        {
            base.SendPacket(netPacket);

            var packet = CreatePacket(netPacket);
            var channel = GetChannel(netPacket);

            if (Peer.State != PeerState.Connected)
            {
                // Disconnect and stop processing packets.
                FireDisconnectEvent(Peer);
                this.DoStop();
                return;
            }

            Peer.Send((byte)channel, ref packet);
        }

        private void ParsePacket(Packet packet)
        {
            var buffer = new byte[packet.Length];
            packet.CopyTo(buffer);
            var packetObj = Serialization.ByteArrayToObject(buffer) as NetPacket;

            PacketReceiveQueue.Enqueue(packetObj);
        }

        private void RequestOtherPlanes()
        {
            var netPacket = new BasicPacket(PacketTypes.GetOtherPlanes, new GameObjects.GameID(-1, (int)Peer.ID));
            EnqueuePacket(netPacket);
        }

        public void SendPlayerDisconnectPacket(uint playerID)
        {
            var packet = new BasicPacket(PacketTypes.PlayerDisconnect, new GameObjects.GameID(playerID));
            EnqueuePacket(packet);
            Host.Flush();
        }

        public override uint GetPlayerRTT(int playerID)
        {
            return Peer.RoundTripTime;
        }

        public override ulong PacketLoss()
        {
            return Peer.PacketsLost;
        }

        public override void Disconnect(int playerID)
        {
            Peer.DisconnectNow(0);
            Host.Flush();
        }

        public override Peer? GetPeer(int playerID)
        {
            return Peer;
        }

        public override void Dispose()
        {
            Peer.DisconnectNow(0);
            
            Thread.Sleep(30);

            base.Dispose();
        }
    }
}
