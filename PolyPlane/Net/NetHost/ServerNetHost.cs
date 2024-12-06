using ENet;
using PolyPlane.Helpers;

namespace PolyPlane.Net.NetHost
{
    public class ServerNetHost : NetPlayHost
    {
        private Dictionary<uint, Peer> _peers = new Dictionary<uint, Peer>();

        public ServerNetHost(ushort port, string ip) : base(port, ip)
        { }

        public override void DoStart()
        {
            base.DoStart();

            Host = new Host();
            Host.Create(Address, MAX_CLIENTS, MAX_CHANNELS);
        }

        public override void DoStop()
        {
            base.DoStop();

            foreach (var peer in _peers.Values)
                peer.DisconnectNow(0);
        }

        public override void HandleConnect(ref Event netEvent)
        {
            base.HandleConnect(ref netEvent);

            var peer = netEvent.Peer;

            _peers.Add(peer.ID, peer);

            var spawnPosition = Utilities.FindSafeSpawnPoint();
            var idPacket = new BasicPacket(PacketTypes.SetID, new GameObjects.GameID((int)peer.ID, 0), spawnPosition);
            SendIDPacket(peer, idPacket);
        }

        public override void HandleDisconnect(ref Event netEvent)
        {
            base.HandleDisconnect(ref netEvent);

            var peer = netEvent.Peer;

            SendPlayerDisconnectPacket(peer.ID);

            _peers.Remove(peer.ID);
        }

        public override void HandleTimeout(ref Event netEvent)
        {
            base.HandleTimeout(ref netEvent);

            var peer = netEvent.Peer;

            SendPlayerDisconnectPacket(peer.ID);

            _peers.Remove(peer.ID);
        }

        public override void HandleReceive(NetPacket netPacket)
        {
            base.HandleReceive(netPacket);

            switch (netPacket.Type)
            {
                case PacketTypes.PlaneUpdate
                or PacketTypes.MissileUpdate
                or PacketTypes.ChatMessage
                or PacketTypes.NewBullet
                or PacketTypes.NewMissile
                or PacketTypes.Impact
                or PacketTypes.PlayerDisconnect
                or PacketTypes.PlayerReset
                or PacketTypes.PlayerEvent:

                    // Queue certain updates to re-broadcast ASAP.
                    PacketSendQueue.Enqueue(netPacket);
                    break;
            }
        }

        public override ulong PacketLoss()
        {
            return 0;
        }

        public override void SendPacket(ref Packet packet, byte channel)
        {
            Host.Broadcast(channel, ref packet);
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

        public override uint GetPlayerRTT(int playerID)
        {
            if (_peers.TryGetValue((uint)playerID, out var peer))
            {
                return peer.RoundTripTime;
            }

            return 0;
        }

        /// <summary>
        /// Send to all other peers except for the peer the packet originated from.
        /// </summary>
        /// <param name="netPacket"></param>
        private void BroadcastToOtherPeersOnly(NetPacket netPacket)
        {
            var packet = CreatePacket(netPacket);
            var channel = GetChannel(netPacket);

            foreach (var peer in _peers.Values)
            {
                if (peer.ID != netPacket.ID.PlayerID)
                    peer.Send(channel, ref packet);
            }
        }
        private void SendIDPacket(Peer peer, NetPacket packet)
        {
            var idPacket = CreatePacket(packet);
            peer.Send(CHANNEL_ID, ref idPacket);
        }
    }
}
