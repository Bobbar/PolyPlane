using ENet;

namespace PolyPlane.Net
{
    public class ServerNetHost : NetPlayHost
    {
        private Dictionary<uint, Peer> _peers = new Dictionary<uint, Peer>();

        public ServerNetHost(ushort port, string ip) : base(port, ip)
        {
        }

        public override void DoStart()
        {
            base.DoStart();

            Host = new Host();
            Host.Create(Address, MAX_CLIENTS, MAX_CHANNELS);
        }

        public override void HandleConnect(Event netEvent)
        {
            base.HandleConnect(netEvent);

            _peers.Add(netEvent.Peer.ID, netEvent.Peer);

            var idPacket = new BasicPacket(PacketTypes.SetID, new GameObjects.GameID((int)netEvent.Peer.ID, 0));
            SendIDPacket(netEvent.Peer, idPacket);
        }

        public override void HandleDisconnect(Event netEvent)
        {
            base.HandleDisconnect(netEvent);

            SendPlayerDisconnectPacket(netEvent.Peer.ID);

            _peers.Remove(netEvent.Peer.ID);
        }

        public override void HandleTimeout(Event netEvent)
        {
            base.HandleTimeout(netEvent);

            SendPlayerDisconnectPacket(netEvent.Peer.ID);

            _peers.Remove(netEvent.Peer.ID);
        }

        public override void HandleReceive(Event netEvent)
        {
            base.HandleReceive(netEvent);

            ParsePacket(netEvent.Packet, netEvent.Peer);
        }


        public override uint GetPlayerRTT(int playerID)
        {
            if (_peers.TryGetValue((uint)playerID, out var peer))
            {
                return peer.RoundTripTime;
            }

            return 0;
        }

        private void ParsePacket(Packet packet, Peer peer)
        {
            var buffer = new byte[packet.Length];
            packet.CopyTo(buffer);

            var packetObj = Serialization.ByteArrayToObject(buffer) as NetPacket;

            switch (packetObj.Type)
            {

                case PacketTypes.PlaneUpdate or PacketTypes.MissileUpdate or PacketTypes.ChatMessage or PacketTypes.NewBullet or PacketTypes.NewMissile or PacketTypes.Impact or PacketTypes.PlayerDisconnect or PacketTypes.PlayerReset:

                    // Queue certain updates to re-broadcast ASAP.
                    PacketSendQueue.Enqueue(packetObj);
                    PacketReceiveQueue.Enqueue(packetObj);

                    break;

                default:

                    PacketReceiveQueue.Enqueue(packetObj);

                    break;
            }
        }

        private void BroadcastPacket(NetPacket netPacket)
        {
            var packet = CreatePacket(netPacket);
            var channel = GetChannel(netPacket);

            Host.Broadcast((byte)channel, ref packet);
        }

        public override void SendPacket(NetPacket packet)
        {
            base.SendPacket(packet);

            BroadcastPacket(packet);
        }

        private void SendIDPacket(Peer peer, NetPacket packet)
        {
            var idPacket = CreatePacket(packet);
            peer.Send(CHANNEL_ID, ref idPacket);
        }

        public override ulong PacketLoss()
        {
            return 0;
        }

        public override Peer? GetPeer(int playerID)
        {
            if (_peers.TryGetValue((uint)playerID, out var peer))
             return peer;

            return null;
        }

        public override void Disconnect(int playerID)
        {
            if (_peers.TryGetValue((uint)playerID, out Peer peer))
            {
                peer.DisconnectNow(0);
                _peers.Remove((uint)playerID);
            }
        }
    }
}
